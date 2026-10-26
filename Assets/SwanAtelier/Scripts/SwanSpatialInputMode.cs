using System.Collections;
using UnityEngine;

namespace SwanAtelier
{
    // The SDK synchronizer calls Android JNI in Editor Play Mode as well.
    // Keep the same public synchronization call, scoped to its supported player.
    [DisallowMultipleComponent]
    public sealed class SwanSpatialInputMode : MonoBehaviour
    {
        Coroutine resumeSync;
        public static bool Ready {get;private set;}
#if UNITY_ANDROID && !UNITY_EDITOR
        UnityEngine.XR.Management.XRManagerSettings manager;
        bool ownsLoader;
#endif
        IEnumerator Start()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            Ready=false;
            // XR Management's public manual-init contract requires Start to finish.
            // Auto-init ran before the Spatial scene, sometimes while Swan slept;
            // input then worked but the mesh event callback never delivered data.
            yield return null;
            while(!Application.isFocused || ByteDance.PICO.SpatialAdapter.SpatialCamera.Current==null) yield return null;
            manager=UnityEngine.XR.Management.XRGeneralSettings.Instance.Manager;
            if(manager.activeLoader==null) {manager.InitializeLoaderSync();ownsLoader=manager.activeLoader!=null;}
            if(manager.activeLoader==null) {Debug.LogError("SWAN_ATELIER_XR_STAGE_INIT_FAILED");yield break;}
            manager.StartSubsystems();Sync("stage-ready-start");Ready=true;
            Debug.Log("SWAN_ATELIER_XR_STAGE_READY owned="+ownsLoader);
#else
            yield break;
#endif
        }
        void Sync(string reason)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            ByteDance.PICO.XR.PXR_SpatialAdapterSpatialMode.SyncSpatialAdapterSpatialMode();
            Debug.Log("SWAN_ATELIER_XR_SPATIAL_MODE_SYNCED reason="+reason);
#endif
        }
        void OnApplicationPause(bool paused)
        {
            if(resumeSync!=null){StopCoroutine(resumeSync);resumeSync=null;}
            if(!paused&&isActiveAndEnabled)resumeSync=StartCoroutine(ResumeMode());
        }
        IEnumerator ResumeMode()
        {
            // The permission controller temporarily owns a different Stage.
            // Reapply the public mode binding once our Stage has resumed.
            yield return null;
#if UNITY_ANDROID && !UNITY_EDITOR
            while(!Ready)yield return null;
#endif
            Sync("resume");
            yield return new WaitForSecondsRealtime(.5f);Sync("resume-settled");
            resumeSync=null;
        }
        void OnDisable(){if(resumeSync!=null){StopCoroutine(resumeSync);resumeSync=null;}}
        void OnDestroy()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            Ready=false;
            if(ownsLoader&&manager!=null){manager.StopSubsystems();manager.DeinitializeLoader();}
#endif
        }
    }
}
