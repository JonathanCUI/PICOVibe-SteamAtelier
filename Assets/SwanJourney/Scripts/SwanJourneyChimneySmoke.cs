using UnityEngine;

namespace SwanJourney
{
    // Each exported mesh samples a different cell of the same looping smoke atlas.
    // Only one mesh is visible. The chimney and its exhaust path own the origin and timing.
    [DefaultExecutionOrder(3510)]
    public sealed class SwanJourneyChimneySmoke : MonoBehaviour
    {
        public SteamJourney journey;
        public GameObject[] frames;
        public MesaSteamTunnelV17 tunnel;
        public const float Height = 2.45f;
        const float SampleInterval = 1f / 15f;
        int showing = -1;
        float elapsed, phase, strength;
        float lastSize = -1f, lastLean = float.NaN;
        bool lastSandbox;

        void LateUpdate()
        {
            if (journey.inSandbox != lastSandbox || !journey.ChimneyConnected)
            {
                Clear();
                lastSandbox = journey.inSandbox;
                if (!journey.ChimneyConnected) return;
            }
            if (journey.paused) return;
            elapsed += Time.deltaTime;
            if (elapsed < SampleInterval) return;
            float dt = elapsed;
            elapsed %= SampleInterval;
            // Keep the animation clock independent of the sampling remainder.
            dt = Mathf.Min(dt - elapsed, .25f) * (journey.slow && !journey.inSandbox ? .25f : 1f);
            float power = journey.inSandbox ? 1f : journey.ChimneyExhaustPower;
            strength = Mathf.MoveTowards(strength, power, dt * (power > strength ? 1.2f : .85f));
            if (strength < .025f) { Hide(); return; }

            float size = Mathf.Lerp(.12f, 1f, strength);
            float speed = journey.inSandbox ? Mathf.Clamp01(journey.speed / 2.6f) : 0f;
            float lean = journey.inSandbox ? journey.direction * (6f + 40f * speed) : 8f;
            var tilt = Quaternion.Euler(0f, 0f, lean);
            if (journey.inSandbox)
            {
                size *= 1.4f;
                if (tunnel && tunnel.protectionEnabled)
                    size = LimitAtRock(size, tilt);
            }
            if (size < .08f) { Hide(); return; }
            if (lean != lastLean) { transform.localRotation = tilt; lastLean = lean; }
            if (size != lastSize) { transform.localScale = Vector3.one * size; lastSize = size; }
            phase = Mathf.Repeat(phase + dt * (12f + 3f * speed), frames.Length);
            int next = (int)phase;
            if (next == showing) return;
            Hide();
            frames[next].SetActive(true);
            showing = next;
        }

        // Check the envelope in the same baked free-air field as the canyon exhaust.
        float LimitAtRock(float size, Quaternion tilt)
        {
            var origin = transform.localPosition;
            var parent = transform.parent;
            var field = new Bounds(tunnel.fieldMin + tunnel.fieldSize * .5f, tunnel.fieldSize);
            float range = (Height + 1.4f) * size * parent.lossyScale.x / journey.SpatialScale;
            if (field.SqrDistance(journey.WorldPointToScene(transform.position)) > range * range) return size;
            if (tunnel.AirWorld(parent.TransformPoint(origin)) < .9f) return 0f;
            for (int i = 1; i <= 6; i++)
            {
                float u = i / 6f;
                var center = origin + tilt * new Vector3(-.36f * u * u, Height * u, 0f) * size;
                float radius = (.13f + .52f * u) * size;
                if (tunnel.AirWorld(parent.TransformPoint(center)) < .9f
                    || tunnel.AirWorld(parent.TransformPoint(center + Vector3.forward * radius)) < .9f
                    || tunnel.AirWorld(parent.TransformPoint(center - Vector3.forward * radius)) < .9f
                    || tunnel.AirWorld(parent.TransformPoint(center + Vector3.right * radius)) < .9f
                    || tunnel.AirWorld(parent.TransformPoint(center - Vector3.right * radius)) < .9f)
                    return size * (i - 1) / 6f;
            }
            return size;
        }

        void Hide()
        {
            if (showing < 0) return;
            frames[showing].SetActive(false);
            showing = -1;
        }

        public void Clear() { Hide(); elapsed = phase = strength = 0f; }
        void OnDisable() { Clear(); }
    }
}
