using System;
using System.Collections;
using System.Collections.Generic;
using ByteDance.PICO.XR;
using UnityEngine;
using UnityEngine.XR;

namespace SwanAtelier
{
    [DisallowMultipleComponent]
    public sealed class SwanTabletopPlacement : MonoBehaviour
    {
        public const float SnapDistance=.065f, LiftDistance=.055f, Clearance=.004f;
        public Vector3[] supportPoints;
        public Vector3 contactCentre;
        public float baseHeight;
        public int supportSamplesPerFootprint;
        public readonly SwanTableSurface Surface=new SwanTableSurface();
        public bool Attached {get;private set;}
        public bool Manipulating {get;private set;}
        public bool TooLarge {get;private set;}
        public string SensorStatus {get;private set;}="请看向茶几，扫描桌面";
        public string Hint => Attached&&Manipulating&&lift>.008f ? "桌面吸附 · 继续上提脱离 "+Mathf.RoundToInt(lift/LiftDistance*100)+"%" : TooLarge ? "桌面支撑范围不足 · 请缩小展台，或提起后移向中央" : Attached ? "已吸附桌面 · 向上提起约 5 厘米可拿起" : SensorStatus;
        public Action<string> Feedback;
        readonly object gate=new object();
        readonly Dictionary<Guid,PxrSpatialMeshInfo> pending=new Dictionary<Guid,PxrSpatialMeshInfo>();
        readonly List<PxrSpatialMeshInfo> updates=new List<PxrSpatialMeshInfo>();
        readonly List<MeshInfo> meshInfos=new List<MeshInfo>();
        XRMeshSubsystem meshSubsystem;
        bool ownsSubsystem,startedSubsystem,subscribed,paused,acceptEvents,needsClearance,permissionRequested;
        Coroutine startup;
        float surfaceHeight,lift,releaseTime,nextIdle,nextReport;
        float nextMeshPoll,nextPollReport;
        Vector3 lastPosition;
        Quaternion lastRotation;
        Vector3 lastScale;
        int callbacks;

        void OnEnable() { StartSensing(); }
        void StartSensing() {
#if UNITY_ANDROID && !UNITY_EDITOR
            if(!paused && startup==null) startup=StartCoroutine(StartSensor());
#endif
        }
        IEnumerator StartSensor()
        {
            // Wait for SpatialAdapter's Stage and PICO input loader to initialize.
#if UNITY_ANDROID && !UNITY_EDITOR
            while(!SwanSpatialInputMode.Ready)yield return null;
#endif
            yield return new WaitForSecondsRealtime(2);
#if UNITY_ANDROID && !UNITY_EDITOR
            const string permission="com.picovr.permission.SPATIAL_DATA";
            if(!UnityEngine.Android.Permission.HasUserAuthorizedPermission(permission)) {
                // A permission dialog pauses this Activity. If it was declined,
                // resuming must not open the same dialog over and over again.
                if(permissionRequested) {
                    SensorStatus="空间数据未授权 · 可继续手动移动展台";
                    Debug.LogWarning("SWAN_TABLE_SENSOR permission not granted; no repeat prompt");startup=null;yield break;
                }
                permissionRequested=true;
                bool answered=false;
                var cb=new UnityEngine.Android.PermissionCallbacks();
                cb.PermissionGranted+=_=>answered=true; cb.PermissionDenied+=_=>answered=true;
                cb.PermissionDeniedAndDontAskAgain+=_=>answered=true;
                UnityEngine.Android.Permission.RequestUserPermission(permission,cb);
                float deadline=Time.realtimeSinceStartup+45;
                while(!answered && Time.realtimeSinceStartup<deadline) yield return null;
                if(!UnityEngine.Android.Permission.HasUserAuthorizedPermission(permission)) {
                    SensorStatus="请在系统权限中允许空间数据，随后重新打开应用";
                    Debug.LogWarning("SWAN_TABLE_SENSOR permission not granted");startup=null;yield break;
                }
            }
#endif
            for(int attempt=0;attempt<3;attempt++) {
                try { InitializeSensor(); }
                catch(Exception error) {
                    SensorStatus="桌面感知暂不可用 · 可继续手动移动展台";
                    Debug.LogWarning("SWAN_TABLE_SENSOR_ERROR "+error);
                    break;
                }
                if(meshSubsystem!=null)break;
                if(attempt<2)yield return new WaitForSecondsRealtime(1);
            }
            startup=null;
        }
        void InitializeSensor()
        {
            var existing=new List<XRMeshSubsystem>(); SubsystemManager.GetInstances(existing);
            foreach(var candidate in existing) if(candidate.SubsystemDescriptor.id=="PICO Mesh") {meshSubsystem=candidate;break;}
            if(meshSubsystem==null) {
                // The Spatial loader only auto-creates this subsystem with ARF 6.
                // Unity's public descriptor API lets this Unity 2022/ARF 5 app use
                // the same registered provider without changing ARF or rendering.
                var descriptors=new List<XRMeshSubsystemDescriptor>();SubsystemManager.GetSubsystemDescriptors(descriptors);
                // XR and Spatial native plugins can both register this name. A
                // descriptor from the inactive mode may return null; continue to
                // the other provider instead of treating that as no capability.
                int provider=0;
                foreach(var descriptor in descriptors) if(descriptor.id=="PICO Mesh") {
                    meshSubsystem=descriptor.Create();
                    Debug.Log("SWAN_TABLE_PROVIDER index="+(provider++)+" created="+(meshSubsystem!=null));
                    if(meshSubsystem!=null) {ownsSubsystem=true;break;}
                }
                Debug.Log("SWAN_TABLE_DESCRIPTORS "+string.Join(",",descriptors.ConvertAll(d=>d.id)));
            }
            if(meshSubsystem==null) { SensorStatus="桌面感知接口暂未就绪 · 可继续手动摆放";Debug.LogWarning("SWAN_TABLE_SENSOR providers returned null");return; }
            lock(gate) acceptEvents=true;
            PXR_Manager.SpatialMeshDataUpdated+=ReceiveMeshes;subscribed=true;
            if(!meshSubsystem.running) {meshSubsystem.Start();startedSubsystem=true;}
            SensorStatus=meshSubsystem.running ? "请缓慢扫视茶几表面与边缘" : "桌面感知未启动 · 可继续手动移动展台";
            Debug.Log("SWAN_TABLE_SENSOR running="+meshSubsystem.running+" owned="+ownsSubsystem);
        }
        void ReceiveMeshes(List<PxrSpatialMeshInfo> meshes)
        {
            // Native callbacks may be off the Unity thread. Coalesce by UUID;
            // never touch Transforms or create/export geometry in this callback.
            lock(gate) {
                if(!acceptEvents||meshes==null) return;
                callbacks++;
                foreach(var mesh in meshes) {
                    if(mesh.state==MeshChangeState.Unchanged) continue;
                    if(mesh.state==MeshChangeState.Removed || pending.Count<256 || pending.ContainsKey(mesh.uuid)) pending[mesh.uuid]=mesh;
                }
            }
        }
        void Update()
        {
            // XRMeshSubsystem is a polled provider. Follow ARMeshManager's public
            // update flow even though semantic geometry arrives via PICO's event;
            // no MeshFilter, rendered room mesh or environment collider is needed.
            if(meshSubsystem!=null&&meshSubsystem.running&&Time.unscaledTime>=nextMeshPoll) {
                nextMeshPoll=Time.unscaledTime+.2f;meshInfos.Clear();
                bool polled=meshSubsystem.TryGetMeshInfos(meshInfos);
                if(Time.unscaledTime>=nextPollReport){nextPollReport=Time.unscaledTime+5;
                    Debug.Log("SWAN_TABLE_POLL success="+polled+" infos="+meshInfos.Count+" callbacks="+callbacks);}
            }
            updates.Clear();
            lock(gate) { foreach(var m in pending.Values) updates.Add(m);pending.Clear(); }
            foreach(var mesh in updates) Surface.Update(mesh);
            if(updates.Count>0) {
                SensorStatus=Surface.TriangleCount>0 ? "已发现桌面 · 将展台底部移近桌面即可吸附" : "正在扫描 · 请看向茶几表面与边缘";
                if(Time.unscaledTime>=nextReport) {
                    nextReport=Time.unscaledTime+5;
                    Debug.Log("SWAN_TABLE_SCAN callbacks="+callbacks+" tableTriangles="+Surface.TriangleCount+" chunks="+updates.Count+" attached="+Attached);
                    if(Surface.TriangleCount>0) Debug.Log("SWAN_TABLE_ALIGNMENT "+Surface.Describe(supportPoints,transform.position,transform.rotation,transform.localScale,contactCentre));
                }
            }
            // One inexpensive placement check at 5 Hz when untouched. Once placed,
            // hold the pose; tracking loss/jitter must not move the user's workshop.
            if(!Manipulating && !Attached && Time.unscaledTime>=nextIdle && Time.unscaledTime>=releaseTime) {
                nextIdle=Time.unscaledTime+.2f;
                ApplyPose(transform.position,transform.rotation,transform.localScale,Vector3.zero,Time.unscaledTime);
            }
        }
        public void BeginManipulation() { Manipulating=true;lift=0;Debug.Log("SWAN_TABLE_GRAB attached="+Attached); }
        public void EndManipulation() { if(Manipulating)Debug.Log("SWAN_TABLE_RELEASE attached="+Attached+" lift="+lift.ToString("F3"));Manipulating=false;lift=0;needsClearance=false; }
        public Vector3 Constrain(Vector3 proposed, Quaternion rotation, Vector3 scale, Vector3 handDelta, float now)
        {
            TooLarge=false;
            if(Attached) {
                float previousLift=lift;
                lift=Mathf.Max(0,lift+handDelta.y);
                if(previousLift<.02f&&lift>=.02f)Debug.Log("SWAN_TABLE_RESIST lift="+lift.ToString("F3")+" threshold="+LiftDistance.ToString("F3"));
                if(Manipulating && lift>=LiftDistance) {
                    Attached=false;releaseTime=now+.6f;needsClearance=true;
                    // Start detached at the current visible height, avoiding a jump
                    // equal to the resistance distance. Following hand motion is 1:1.
                    proposed.y=transform.position.y+Mathf.Max(0,lift-LiftDistance);
                    lift=0;Emit("已拿起展台 · 可移动、旋转和缩放");return proposed;
                }
                proposed.y=surfaceHeight+Clearance-baseHeight*scale.y;
                if(Manipulating && !Surface.Fits(supportPoints,proposed,rotation,scale,surfaceHeight,supportSamplesPerFootprint)) {
                    // Keep the last supported horizontal pose/scale until lifted.
                    // This avoids sliding the locomotive through the real table edge.
                    TooLarge=true;return lastPosition;
                }
                return proposed;
            }
            if(now<releaseTime || supportPoints==null||supportPoints.Length==0) return proposed;
            var centre=proposed+rotation*Vector3.Scale(contactCentre,scale);
            centre.y=proposed.y+baseHeight*scale.y;
            if(needsClearance) {
                if(Surface.TryHeight(centre,.11f,out _)) return proposed;
                needsClearance=false;
            }
            if(!Surface.TryHeight(centre,SnapDistance,out float height,.035f)) return proposed;
            if(!Surface.Fits(supportPoints,proposed,rotation,scale,height,supportSamplesPerFootprint)) {TooLarge=true;return proposed;}
            surfaceHeight=height;Attached=true;lift=0;
            proposed.y=height+Clearance-baseHeight*scale.y;
            Emit("已吸附桌面 · 继续向上提起可脱离");return proposed;
        }
        public void ApplyPose(Vector3 position,Quaternion rotation,Vector3 scale,Vector3 handDelta,float now)
        {
            var constrained=Constrain(position,rotation,scale,handDelta,now);
            if(Attached&&TooLarge) {rotation=lastRotation;scale=lastScale;}
            transform.SetPositionAndRotation(constrained,rotation);transform.localScale=scale;
            lastPosition=transform.position;lastRotation=transform.rotation;lastScale=transform.localScale;
        }
        void Emit(string message) {Feedback?.Invoke(message);Debug.Log("SWAN_TABLE_"+(Attached?"ATTACH":"DETACH")+" position="+transform.position.ToString("F3")+" height="+surfaceHeight.ToString("F3"));}
        void OnApplicationPause(bool value) {paused=value;if(value) StopSensing();else if(isActiveAndEnabled) StartSensing();}
        void OnDisable() {StopSensing();EndManipulation();}
        void StopSensing()
        {
            if(startup!=null) {StopCoroutine(startup);startup=null;}
            lock(gate) {acceptEvents=false;pending.Clear();}
            if(subscribed) {PXR_Manager.SpatialMeshDataUpdated-=ReceiveMeshes;subscribed=false;}
            if(meshSubsystem!=null) {
                if(startedSubsystem && meshSubsystem.running) meshSubsystem.Stop();
                if(ownsSubsystem) meshSubsystem.Destroy();
            }
            meshSubsystem=null;startedSubsystem=ownsSubsystem=false;Surface.Clear();
        }
    }
}
