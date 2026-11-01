using System;
using System.Linq;
using UnityEngine;

// The simulation still decides when gas/steam reaches downstream components.
// A visible packet follows the authored polyline tangent, including bends.
public sealed partial class JourneyFluidField
{
    bool arrowAlive;
    float arrowDistance;
    int arrowSegment = 1;
    public void AdvanceArrow(float dt, float strength)
    {
        if (dt <= 0 || !system || points == null || points.Length < 2) return;
        bool fed = strength > .01f && (!upstream || upstream.Arrived);
        if (!fed && !arrowAlive) return;
        Init();
        if (length < .0001f) return;
        float step = velocity * dt;
        if (fed)
        {
            TravelFront = Mathf.Min(length, TravelFront + step);
            if (!arrowAlive) { arrowAlive = true; arrowDistance = 0; arrowSegment = 1; }
        }
        arrowDistance += step;
        if (arrowDistance >= length)
        {
            if (!fed) { arrowAlive = false; return; }
            arrowDistance %= length;
            arrowSegment = 1;
        }
    }

    public bool TryArrow(out Vector3 position, out Vector3 direction, out float span)
    {
        position = direction = Vector3.zero; span = 0;
        if (!arrowAlive || lengths == null) return false;
        span = Mathf.Min(Mathf.Clamp(radius * 7.5f, .30f, .68f), length * .58f);
        float d = Mathf.Clamp(arrowDistance, 0, length);
        while (arrowSegment < lengths.Length-1 && lengths[arrowSegment] < d) arrowSegment++;
        int k = arrowSegment;
        direction = tangents[k];
        position = Vector3.Lerp(points[k-1], points[k], Mathf.InverseLerp(lengths[k-1], lengths[k], d));
        return direction.sqrMagnitude > .5f && span > .00001f;
    }
}

namespace SwanJourney
{
    public sealed partial class SwanJourneyFlowParticles
    {
        public const int ArrowsPerKind = 12;
        const int KindCount = 4;
        public Material[] arrowMaterials;
        JourneyFluidField[][] arrowFields;
        readonly int[] usedArrows = new int[KindCount];
        bool arrowsShown;
        public bool UsesArrows => arrowMaterials != null && arrowMaterials.Length == KindCount;

        void InitializeArrows()
        {
            arrowFields = new JourneyFluidField[KindCount][];
            for (int kind = 0; kind < KindCount; kind++)
            {
                // Give every stage and valve port a representative route before filling parallel tubes.
                var groups = sources.Where(s => s.field && (int)s.field.kind == kind)
                    .GroupBy(s => (s.field.stage,s.field.port))
                    .SelectMany(g => g.Select((s,i) => (field:s.field,rank:i))).OrderBy(x => x.rank);
                arrowFields[kind] = groups.Select(x => x.field).ToArray();
            }
        }

        void DrawArrows(bool show)
        {
            if (!show && !arrowsShown) return;
            ActiveMarkerCount = RepresentedFieldCount = ActiveFieldCount = LastSourceParticleCount = 0;
            var toPool = poolRoot.worldToLocalMatrix;
            for (int kind = 0; kind < KindCount; kind++)
            {
                int used = 0;
                if (show) foreach (var field in arrowFields[kind])
                {
                    if (!field.gameObject.activeInHierarchy || !field.TryArrow(out var p,out var d,out float span)) continue;
                    ActiveFieldCount++;
                    if (used >= ArrowsPerKind) continue;
                    var src = field.system.transform;
                    var matrix = toPool * src.localToWorldMatrix;
                    var forward = matrix.MultiplyVector(d);
                    if (forward.sqrMagnitude < 1e-12f) continue;
                    Vector3 up = Mathf.Abs(Vector3.Dot(forward.normalized,Vector3.up)) < .96f ? Vector3.up : Vector3.right;
                    var target = markerTransforms[kind*ArrowsPerKind+used++];
                    target.SetLocalPositionAndRotation(matrix.MultiplyPoint3x4(p), Quaternion.LookRotation(forward,up));
                    float width = Mathf.Min(Mathf.Max(field.radius * 3.4f,.108f),span*.66f);
                    float scale = forward.magnitude;
                    target.localScale = new Vector3(width*scale,width*scale,span*scale);
                    if (!target.gameObject.activeSelf) target.gameObject.SetActive(true);
                }
                for (int n=used;n<usedArrows[kind];n++) markerTransforms[kind*ArrowsPerKind+n].gameObject.SetActive(false);
                usedArrows[kind]=used; ActiveMarkerCount+=used;
            }
            RepresentedFieldCount=ActiveMarkerCount;
            LastSourceParticleCount=ActiveFieldCount;
            arrowsShown=show;
        }
    }
}
