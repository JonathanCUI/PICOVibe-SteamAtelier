#if UNITY_EDITOR
using UnityEngine;

namespace SwanJourney
{
    // Build-time dependency only. The app's scene processor removes this
    // component before Player serialization; it is absent from Player code.
    public sealed class SwanJourneyBuildDependencies : MonoBehaviour
    {
        public GameObject canyonPrefab;
    }
}
#endif
