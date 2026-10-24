using UnityEngine;

namespace SwanAtelier
{
    // One root transform keeps the locomotive, controls and loose parts together.
    // The view-following FPS is intentionally outside that root.
    public sealed class SwanTwoHandManipulation
    {
        public const float MinimumScale = .26f;
        public const float MaximumScale = 1.5f;
        public bool Active { get; private set; }
        public float Scale => target.localScale.x / originalScale.x;
        readonly Transform target;
        readonly Vector3 originalScale;
        Vector3 previousA, previousB, desiredPosition, previousPlanar;
        Quaternion desiredRotation;
        float desiredScale, previousDistance;
        bool hasPlanar;

        public SwanTwoHandManipulation(Transform target)
        {
            this.target = target;
            originalScale = target.localScale;
        }

        public bool Begin(Vector3 a, Vector3 b)
        {
            End();
            if (!Finite(a) || !Finite(b) || Vector3.Distance(a, b) < .10f) return false;
            previousA = a; previousB = b; previousDistance = Vector3.Distance(a, b);
            previousPlanar = Vector3.ProjectOnPlane(b - a, Vector3.up);
            hasPlanar = previousPlanar.sqrMagnitude >= .0025f;
            desiredPosition = target.position; desiredRotation = target.rotation;
            desiredScale = Mathf.Clamp(Scale, MinimumScale, MaximumScale);
            Active = true;
            return true;
        }

        public bool Step(Vector3 a, Vector3 b, float dt)
        {
            if (!Active) return false;
            if (!Finite(a) || !Finite(b) || !Finite(dt) || dt < 0 ||
                Vector3.Distance(a, b) < .06f ||
                Vector3.Distance(a, previousA) > .4f || Vector3.Distance(b, previousB) > .4f) {
                End(); return false;
            }
            // Translation uses the two-hand midpoint. Rotation and scale keep the
            // workbench's own centre fixed, so a turn cannot make it orbit the user.
            desiredPosition += (a + b - previousA - previousB) * .5f;
            Vector3 planar = Vector3.ProjectOnPlane(b - a, Vector3.up);
            bool usablePlanar = planar.sqrMagnitude >= .0025f;
            if (hasPlanar && usablePlanar)
                desiredRotation = Quaternion.AngleAxis(Vector3.SignedAngle(previousPlanar, planar, Vector3.up), Vector3.up) * desiredRotation;
            hasPlanar = usablePlanar; previousPlanar = planar;
            float distance = Vector3.Distance(a, b);
            // Rebase every sample, including at either limit. Moving back inward
            // responds immediately instead of leaving a dead zone at the limit.
            desiredScale = Mathf.Clamp(desiredScale * distance / previousDistance, MinimumScale, MaximumScale);
            previousA = a; previousB = b; previousDistance = distance;
            float blend = 1 - Mathf.Exp(-28 * Mathf.Min(dt, .1f));
            target.SetPositionAndRotation(Vector3.Lerp(target.position, desiredPosition, blend), Quaternion.Slerp(target.rotation, desiredRotation, blend));
            target.localScale = originalScale * Mathf.Clamp(Mathf.Lerp(Scale, desiredScale, blend), MinimumScale, MaximumScale);
            return true;
        }

        public void End() { Active = false; }
        static bool Finite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
        public static bool Finite(Vector3 v) => Finite(v.x) && Finite(v.y) && Finite(v.z);
    }
}
