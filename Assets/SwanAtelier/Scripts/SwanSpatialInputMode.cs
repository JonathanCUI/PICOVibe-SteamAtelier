using UnityEngine;

namespace SwanAtelier
{
    // The SDK synchronizer calls Android JNI in Editor Play Mode as well.
    // Keep the same public synchronization call, scoped to its supported player.
    [DisallowMultipleComponent]
    public sealed class SwanSpatialInputMode : MonoBehaviour
    {
        void Start()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            ByteDance.PICO.XR.PXR_SpatialAdapterSpatialMode.SyncSpatialAdapterSpatialMode();
            Debug.Log("SWAN_ATELIER_XR_SPATIAL_MODE_SYNCED");
#endif
        }
    }
}
