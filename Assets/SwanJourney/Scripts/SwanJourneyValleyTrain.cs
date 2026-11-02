using System;
using UnityEngine;

namespace SwanJourney
{
    [DefaultExecutionOrder(3900)]
    public sealed class SwanJourneyValleyTrain : MonoBehaviour
    {
        [Serializable] public struct PoseLink { public Transform source, display; }
        public SteamJourney journey;
        public Transform[] originals, vehicles;
        public PoseLink[] pose;
        public ParticleSystem drivingSparks;
        public SwanJourneyChimneySmoke drivingChimney;
        ParticleSystem workshopSparks;
        bool[] wasActive;
        public bool Driving { get; private set; }

        public void Enter()
        {
            if (Driving) return;
            if (wasActive == null) wasActive = new bool[originals.Length];
            workshopSparks = journey.sparks;
            journey.sparks = drivingSparks;
            for (int i = 0; i < originals.Length; i++)
            {
                wasActive[i] = originals[i].gameObject.activeSelf;
                originals[i].gameObject.SetActive(false);
            }
            CopyPose();
            foreach (var vehicle in vehicles) vehicle.gameObject.SetActive(true);
            Driving = true;
        }

        public void Leave()
        {
            if (!Driving) return;
            Driving = false;
            foreach (var vehicle in vehicles) vehicle.gameObject.SetActive(false);
            journey.sparks = workshopSparks;
            for (int i = 0; i < originals.Length; i++) originals[i].gameObject.SetActive(wasActive[i]);
        }

        void LateUpdate()
        {
            if (Driving) CopyPose();
        }

        public void CopyPose()
        {
            // Both hierarchies use the same local coordinate frames. Static ancestors
            // are authored once. Only the route, wheel and mechanism pivots are linked.
            foreach (var link in pose)
            {
                link.source.GetLocalPositionAndRotation(out var p, out var q);
                link.display.GetLocalPositionAndRotation(out var currentPosition, out var currentRotation);
                if (!currentPosition.Equals(p) || !currentRotation.Equals(q)) link.display.SetLocalPositionAndRotation(p, q);
                var scale = link.source.localScale;
                if (!link.display.localScale.Equals(scale)) link.display.localScale = scale;
            }
        }
    }
}
