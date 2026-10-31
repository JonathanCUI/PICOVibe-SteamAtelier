using UnityEngine;
using UnityEngine.SceneManagement;

namespace SwanJourney
{
    // The Swan adapter owns hit testing; the authored mechanisms move Transforms
    // directly. This scene contains no Unity rigid-body simulation to advance.
    // Keep all Colliders for native export, and leave desktop physics unchanged.
    public sealed class SwanJourneyPhysicsBudget : MonoBehaviour
    {
        public SteamJourney journey;
        public bool validatedStaticPhysics;
        static SwanJourneyPhysicsBudget owner;
        bool previousAutoSimulation, subscribed;
        public bool OwnsPolicy => owner == this;

        void Start()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (TryEnable())
            {
                SceneManager.sceneLoaded += CheckLoadedScene;
                subscribed = true;
                Debug.Log("SWAN_JOURNEY_PHYSICS_BUDGET unityAutoSimulation=false nativeCollidersPreserved=true actualFps=unmeasured");
            }
#endif
        }

        public static bool HasDynamics(GameObject root)
            => root && (root.GetComponentInChildren<Rigidbody>(true)
                || root.GetComponentInChildren<ArticulationBody>(true)
                || root.GetComponentInChildren<CharacterController>(true)
                || root.GetComponentInChildren<Cloth>(true));

        public bool TryEnable()
        {
            if (OwnsPolicy) return true;
            if (owner || !validatedStaticPhysics || !journey || !journey.SwanInputActive) return false;
            for (int i = 0; i < SceneManager.sceneCount; i++)
                foreach (var root in SceneManager.GetSceneAt(i).GetRootGameObjects())
                    if (HasDynamics(root)) return false;
            previousAutoSimulation = Physics.autoSimulation;
            Physics.autoSimulation = false;
            owner = this;
            return true;
        }

        void CheckLoadedScene(Scene scene, LoadSceneMode mode)
        {
            foreach (var root in scene.GetRootGameObjects())
                if (HasDynamics(root))
                {
                    Restore();
                    Debug.LogWarning("SWAN_JOURNEY_PHYSICS_BUDGET restored: loaded scene requires Unity dynamics");
                    return;
                }
        }

        public void Restore()
        {
            if (subscribed) { SceneManager.sceneLoaded -= CheckLoadedScene; subscribed = false; }
            if (!OwnsPolicy) return;
            Physics.autoSimulation = previousAutoSimulation;
            owner = null;
        }

        void OnDisable() => Restore();
        void OnDestroy() => Restore();
    }
}
