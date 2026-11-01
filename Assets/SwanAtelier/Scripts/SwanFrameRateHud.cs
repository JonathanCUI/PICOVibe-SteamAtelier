using System;
using ByteDance.PICO.SpatialAdapter;

using UnityEngine;
using UnityEngine.XR;
using System.Collections.Generic;

namespace SwanAtelier
{
    [DefaultExecutionOrder(1401)]
    public sealed class SwanFrameRateHud : MonoBehaviour
    {
        public SwanFpsMeshText meshText;
        public Camera editorPreviewCamera;
        public Vector3 viewOffset = new Vector3(-.32f, .25f, 1.1f);
        // Opt-in layout scale; older Swan scenes retain their original glyph size.
        public float visualScale = 1f;
        public bool HasTrackedPose { get; private set; }
        public int PoseResult { get; private set; } = -1;
        public string DisplayText { get; private set; }
        public double Fps => counter.Fps;
        public Transform VisualTransform => meshText ? meshText.transform : transform;
        readonly SwanFrameRateCounter counter = new SwanFrameRateCounter();
        bool paused;
        InputDevice headDevice;
        readonly List<XRInputSubsystem> inputSubsystems=new List<XRInputSubsystem>();
        double nextInputRetry;
        double nextDiagnostic;
        Pose latestPose, firstPose;
        float maxPositionChange, maxRotationChange;

        void OnEnable() { counter.Reset(); SetVisualActive(!paused && (Application.isEditor || HasTrackedPose)); nextDiagnostic = Time.realtimeSinceStartupAsDouble + 5; WriteLabel(); }
        void Update()
        {
            if (paused || !meshText) return;
            TrackView();
            double now = Time.realtimeSinceStartupAsDouble;
            if (counter.Sample(now)) WriteLabel();
            if (now >= nextDiagnostic) {
                nextDiagnostic = now + 10;
                Debug.Log("SWAN_ATELIER_FPS app=" + counter.Fps.ToString("F1") + " headPose=" + HasTrackedPose + " poseResult=" + PoseResult
                    + " posePosition=" + latestPose.position.ToString("F3") + " poseRotation=" + latestPose.rotation.ToString("F3")
                    + " positionDelta=" + maxPositionChange.ToString("F3") + " angleDelta=" + maxRotationChange.ToString("F1")
                    + " hudPosition=" + VisualTransform.position.ToString("F3") + " hudRotation=" + VisualTransform.rotation.ToString("F3")
                    + " hudScale=" + VisualTransform.lossyScale.ToString("F3") + " visualActive=" + meshText.gameObject.activeInHierarchy
                    + " visualNode=" + TrackerState(VisualTransform) + " wrapperNode=" + TrackerState(transform));
            }
        }
        void TrackView()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            double now=Time.realtimeSinceStartupAsDouble;
            if(now>=nextInputRetry) {
                nextInputRetry=now+2;
                SubsystemManager.GetInstances(inputSubsystems);
                foreach(var subsystem in inputSubsystems) {
                    if(!subsystem.running)continue;
                    var supported=subsystem.GetSupportedTrackingOriginModes();
                    if((supported&TrackingOriginModeFlags.Floor)!=0&&subsystem.GetTrackingOriginMode()!=TrackingOriginModeFlags.Floor)
                        subsystem.TrySetTrackingOriginMode(TrackingOriginModeFlags.Floor);
                    if(!HasTrackedPose)Debug.Log("SWAN_ATELIER_XR_INPUT origin="+subsystem.GetTrackingOriginMode()+" supported="+supported);
                }
                headDevice=InputDevices.GetDeviceAtXRNode(XRNode.Head);
            }
            PoseResult=SwanHeadPose.TryRead(out Pose pose)?0:1;
            if(PoseResult!=0) {
                if(HasTrackedPose)Debug.LogWarning("SWAN_ATELIER_HUD_TRACKING_LOST");
                HasTrackedPose=false;SetVisualActive(false);return;
            }
            Vector3 position=pose.position;Quaternion rotation=pose.rotation;
            latestPose=pose;
            if(!HasTrackedPose) {
                firstPose=pose;
                Debug.Log("SWAN_ATELIER_HUD_TRACKING_READY source=UnityXR device="+headDevice.name+" position="+position+" rotation="+rotation);
            }
            maxPositionChange=Mathf.Max(maxPositionChange,Vector3.Distance(firstPose.position,position));
            maxRotationChange=Mathf.Max(maxRotationChange,Quaternion.Angle(firstPose.rotation,rotation));
            // The Spatial input provider exposes the Unity tracking origin. Raw
            // compositor/world HMD poses are not interchangeable with this space.
            ApplyHeadPose(pose,Matrix4x4.identity);
            HasTrackedPose=true;SetVisualActive(true);
#else
            if(editorPreviewCamera)ApplyHeadPose(new Pose(editorPreviewCamera.transform.position,editorPreviewCamera.transform.rotation),Matrix4x4.identity);
#endif
        }
        public void ApplyHeadPose(Pose pose, Matrix4x4 nativeToUnity)
        {
            var visual = VisualTransform;
            visual.SetPositionAndRotation(nativeToUnity.MultiplyPoint3x4(pose.position + pose.rotation * viewOffset), nativeToUnity.rotation * pose.rotation);
            visual.localScale = nativeToUnity.lossyScale * Mathf.Max(.01f, visualScale);
        }
        void SetVisualActive(bool value)
        {
            if (meshText && meshText.gameObject.activeSelf != value) meshText.gameObject.SetActive(value);
        }
        static string TrackerState(Transform target) {
            var tracker = target.GetComponent<SpatialAdapterGameObjectTracker>();
            return tracker ? tracker.uid + ":" + tracker.isValid : "none";
        }
        void WriteLabel()
        {
            if (!meshText) return;
            DisplayText = counter.HasSample ? "APP " + counter.Fps.ToString("F0") + " FPS" : "APP -- FPS";
            meshText.Show(counter.HasSample ? (double?)counter.Fps : null);

        }
        void OnApplicationPause(bool value)
        {
            paused = value; counter.Reset(); HasTrackedPose = false;
            SetVisualActive(!value && Application.isEditor);
            if (!value) { nextDiagnostic = Time.realtimeSinceStartupAsDouble + 5; WriteLabel(); }
        }
        void OnDisable() { counter.Reset(); HasTrackedPose = false; SetVisualActive(false); }
    }
}


