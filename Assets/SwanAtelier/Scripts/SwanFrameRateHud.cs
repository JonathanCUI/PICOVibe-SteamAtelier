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

        void OnEnable() { counter.Reset(); if (meshText) meshText.gameObject.SetActive(!paused); nextDiagnostic = Time.realtimeSinceStartupAsDouble + 5; WriteLabel(); }
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
            Vector3 position=Vector3.zero;Quaternion rotation=Quaternion.identity;
            bool positionValid=headDevice.isValid&&(headDevice.TryGetFeatureValue(CommonUsages.centerEyePosition,out position)||headDevice.TryGetFeatureValue(CommonUsages.devicePosition,out position));
            bool rotationValid=headDevice.isValid&&(headDevice.TryGetFeatureValue(CommonUsages.centerEyeRotation,out rotation)||headDevice.TryGetFeatureValue(CommonUsages.deviceRotation,out rotation));
            bool tracked=!headDevice.TryGetFeatureValue(CommonUsages.isTracked,out bool isTracked)||isTracked;
            PoseResult=positionValid&&rotationValid&&tracked&&IsFinite(position)&&IsValid(rotation)?0:1;
            if(PoseResult!=0) {
                if(HasTrackedPose)Debug.LogWarning("SWAN_ATELIER_HUD_TRACKING_LOST");
                HasTrackedPose=false;meshText.gameObject.SetActive(false);return;
            }
            var pose=new Pose(position,rotation);
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
            HasTrackedPose=true;meshText.gameObject.SetActive(true);
#else
            if(editorPreviewCamera)ApplyHeadPose(new Pose(editorPreviewCamera.transform.position,editorPreviewCamera.transform.rotation),Matrix4x4.identity);
#endif
        }
        public void ApplyHeadPose(Pose pose, Matrix4x4 nativeToUnity)
        {
            var visual = VisualTransform;
            visual.SetPositionAndRotation(nativeToUnity.MultiplyPoint3x4(pose.position + pose.rotation * viewOffset), nativeToUnity.rotation * pose.rotation);
            visual.localScale = nativeToUnity.lossyScale;
        }
        static string TrackerState(Transform target) {
            var tracker = target.GetComponent<SpatialAdapterGameObjectTracker>();
            return tracker ? tracker.uid + ":" + tracker.isValid : "none";
        }
        static bool IsFinite(Vector3 v) => !float.IsNaN(v.x) && !float.IsNaN(v.y) && !float.IsNaN(v.z) && !float.IsInfinity(v.x) && !float.IsInfinity(v.y) && !float.IsInfinity(v.z);
        static bool IsValid(Quaternion q) { float n = Quaternion.Dot(q, q); return n > .5f && n < 1.5f; }
        void WriteLabel()
        {
            if (!meshText) return;
            DisplayText = counter.HasSample ? "APP " + counter.Fps.ToString("F0") + " FPS" : "APP -- FPS";
            meshText.Show(counter.HasSample ? (double?)counter.Fps : null);

        }
        void OnApplicationPause(bool value)
        {
            paused = value; counter.Reset(); HasTrackedPose = false;
            if (meshText) meshText.gameObject.SetActive(!value);
            if (!value) { nextDiagnostic = Time.realtimeSinceStartupAsDouble + 5; WriteLabel(); }
        }
        void OnDisable() { counter.Reset(); HasTrackedPose = false; if (meshText) meshText.gameObject.SetActive(false); }
    }
}


