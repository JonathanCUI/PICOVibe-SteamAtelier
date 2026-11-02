using System.Collections.Generic;
using ByteDance.PICO.XR;
using ByteDance.PICO.SpatialAdapter.Component;
using SwanJourney;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR;

// Resolve one visible focus target for both hover feedback and a bare-hand click.
public sealed class SteamXrFocus : MonoBehaviour
{
    public Shader highlightShader;
    public SteamJourney journey;
    public RaycastHit Hit { get; private set; }
    readonly RaycastHit[] hits = new RaycastHit[64];
    readonly Collider[] nearby = new Collider[32];
    readonly List<InputDevice> eyes = new List<InputDevice>();
    InputDevice eye;
    float nextEyeCheck;
    Material highlight;
    Transform highlighted;
    readonly List<MeshFilter> filters = new List<MeshFilter>();
    readonly List<MeshRenderer> surfaces = new List<MeshRenderer>();
    readonly HashSet<Transform> moduleRoots = new HashSet<Transform>();
    readonly Dictionary<Transform,JourneyModule> previews = new Dictionary<Transform,JourneyModule>();
    bool startedEyeTracking;
    bool eyeServiceReady;
    EyeTrackingData eyeData;
    EyeTrackingDataGetInfo eyeQuery;
    string lastSource;

    void Awake()
    {
        highlight = new Material(highlightShader);
        eyeQuery.flags=EyeTrackingDataGetFlags.PXR_EYE_ORIENTATION;
        eyeData.eyeDatas=new PerEyeData[(int)PerEyeUsage.EyeCount];
        for (int i=0;i<eyeData.eyeDatas.Length;i++)
            eyeData.eyeDatas[i].SetVersion(PXR_MotionTracking.PXR_EYE_TRACKING_API_VERSION);
        foreach (var module in journey.modules) RememberModule(module);
        foreach (var module in journey.finishing) RememberModule(module);
    }
    void RememberModule(JourneyModule module)
    {
        if (module.preview)
        {
            moduleRoots.Add(module.preview.transform);
            previews[module.preview.transform]=module;
        }
        if (module.objects!=null)
            foreach (var part in module.objects) if (part) moduleRoots.Add(part.transform);
    }
    JourneyModule LoosePreview(Collider collider)
    {
        for (var t=collider ? collider.transform : null;t;t=t.parent)
            if (previews.TryGetValue(t,out var part) && !part.installed) return part;
        return null;
    }
    public GameObject GestureTarget(RaycastHit hit)
    {
        var part=LoosePreview(hit.collider);
        return part!=null ? part.preview : hit.collider ? hit.collider.gameObject : null;
    }

    public void UpdateFocus(Camera view)
    {
        if (Time.unscaledTime >= nextEyeCheck)
        {
            nextEyeCheck = Time.unscaledTime + 2;
#if UNITY_ANDROID && !UNITY_EDITOR
            if (UnityEngine.Android.Permission.HasUserAuthorizedPermission("com.picovr.permission.EYE_TRACKING"))
            {
                bool tracking = false;
                EyeTrackingState state = default;
                eyeServiceReady=PXR_MotionTracking.GetEyeTrackingState(ref tracking,ref state)==0 && tracking;
                if (!eyeServiceReady)
                {
                    var start = new EyeTrackingStartInfo { mode=EyeTrackingMode.PXR_ETM_BOTH, needCalibration=1 };
                    if (PXR_MotionTracking.StartEyeTracking(ref start)==0) startedEyeTracking=eyeServiceReady=true;
                }
            }
#endif
            InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.EyeTracking, eyes);
            eye = eyes.Count > 0 ? eyes[0] : default;
        }
        Vector3 direction = view.transform.forward;
        string source="head";
        if (eye.isValid && eye.TryGetFeatureValue(PXR_Usages.combineEyeVector, out Vector3 gaze) &&
            gaze.sqrMagnitude > .00000001f && gaze.z > 0)
        { direction = view.transform.TransformDirection(gaze.normalized); source="combined-eye"; }
        else if (eye.isValid && eye.TryGetFeatureValue(CommonUsages.eyesData, out Eyes data) &&
            data.TryGetFixationPoint(out Vector3 fixation))
        {
            var delta = transform.TransformPoint(fixation) - view.transform.position;
            if (Vector3.Dot(delta,view.transform.forward) > .01f) { direction = delta.normalized; source="fixation"; }
        }
        if (source=="head" && eyeServiceReady && PXR_MotionTracking.GetEyeTrackingData(ref eyeQuery,ref eyeData)==0)
        {
            var combined=eyeData.eyeDatas[(int)PerEyeUsage.Combined];
            if (combined.isPoseValid!=0)
            {
                var q=combined.pose.orientation;
                var local=new Quaternion(q.x,q.y,-q.z,-q.w)*Vector3.forward;
                if (local.z>0) { direction=view.transform.TransformDirection(local); source="eye-pose"; }
            }
        }
        if (source!=lastSource) { lastSource=source; Debug.Log("XR_FOCUS_SOURCE "+source); }
        Hit = Pick(new Ray(view.transform.position, direction));
        Show(Hit.collider, view);
    }

    public RaycastHit Pick(Ray ray)
    {
        int count = Physics.RaycastNonAlloc(ray, hits, 8f, ~0, QueryTriggerInteraction.Collide);
        RaycastHit best = default;
        float nearest = float.PositiveInfinity;
        for (int i = 0; i < count; i++)
        {
            var hit = hits[i];
            if (hit.distance >= nearest || !Selectable(hit.collider)) continue;
            nearest = hit.distance;
            best = hit;
        }
        // A direct hit on a loose part owns the pinch even beside a button cap.
        var direct=best.collider;
        if (LoosePreview(direct)!=null) return best;
        // Button caps are only a few centimetres wide. Include a small angular
        // tolerance for gaze, with a short depth allowance above the panel face.
        float limit = best.collider ? Mathf.Min(8f, best.distance + .065f) : 8f;
        count = Physics.SphereCastNonAlloc(ray, .014f, hits, limit, ~0, QueryTriggerInteraction.Collide);
        float score = float.PositiveInfinity;
        for (int i = 0; i < count; i++)
        {
            var h = hits[i];
            var part=LoosePreview(h.collider);
            bool parked=part!=null && part.parked;
            if (!parked && !IsButton(h.collider)) continue;
            if (!Selectable(h.collider)) continue;
            // A tolerance hit may recover the edge of a floating part, but never
            // select it through a nearer object or replace a directly hit button.
            if (parked && direct && (h.distance+.014f>nearest || IsButton(direct))) continue;
            var offset = h.collider.bounds.center - ray.origin;
            float lateral = Vector3.Cross(ray.direction, offset).magnitude;
            float cost = h.distance + lateral * 8;
            if (cost < score) { best = h; score = cost; }
        }
        return best;
    }

    bool Selectable(Collider c)
    {
        var renderer = c.GetComponent<Renderer>();
        if (renderer && !renderer.enabled) return false;
        if (c.GetComponentInParent<JourneyControl>() || c.GetComponentInParent<SwanJourneyTarget>() ||
            c.GetComponentInParent<SpatialAdapterHoverEffect>()) return true;
        // The V23 interaction bridge also resolves preview and assembled part roots.
        for (var t=c.transform; t; t=t.parent) if (moduleRoots.Contains(t)) return true;
        return false;
    }

    static bool IsButton(Collider c)
    {
        var tag = c.GetComponentInParent<SwanJourneyTarget>();
        return (tag && (tag.kind==SwanJourneyTargetKind.Control || tag.kind==SwanJourneyTargetKind.ExplodeAll))
            || c.GetComponentInParent<JourneyControl>();
    }
    public Collider TouchButton(Vector3 tip, float radius)
    {
        int count = Physics.OverlapSphereNonAlloc(tip, radius, nearby, ~0, QueryTriggerInteraction.Collide);
        Collider best = null;
        float nearest = float.PositiveInfinity;
        for (int i = 0; i < count; i++)
        {
            var c = nearby[i];
            var tag = c.GetComponentInParent<SwanJourney.SwanJourneyTarget>();
            var control = c.GetComponentInParent<JourneyControl>();
            if (!IsButton(c)) continue;
            if (!Selectable(c)) continue;
            var action = tag && tag.useActionOverride ? tag.action : control ? control.action : tag.action;
            if (action == JourneyAction.Throttle || action == JourneyAction.Brake || action == JourneyAction.Reverse) continue;
            float distance = (c.bounds.center - tip).sqrMagnitude;
            if (distance < nearest) { nearest = distance; best = c; }
        }
        return best;
    }

    public static Transform HighlightRoot(Collider c)
    {
        if (!c) return null;
        var control = c.GetComponentInParent<JourneyControl>(true);
        var feedback = control ? control.GetComponent<JourneyButtonFeedback>() : null;
        if (feedback && feedback.cap) return feedback.cap;
        var mesh = c.GetComponentInParent<MeshFilter>(true);
        if (mesh && mesh.sharedMesh) return mesh.transform;
        var tag = c.GetComponentInParent<SwanJourneyTarget>(true);
        if (tag) return tag.transform;
        return control ? control.transform : c.transform;
    }

    void Show(Collider c, Camera view)
    {
        var root = HighlightRoot(c);
        if (root != highlighted)
        {
            highlighted = root;
            filters.Clear(); surfaces.Clear();
            if (root) root.GetComponentsInChildren<MeshFilter>(true,filters);
            foreach (var filter in filters) surfaces.Add(filter.GetComponent<MeshRenderer>());
        }
        if (!root) return;
        for (int i=0; i<filters.Count; i++)
        {
            var renderer=surfaces[i];
            if (!renderer || !renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
            var mesh=filters[i].sharedMesh;
            if (!mesh) continue;
            for (int sub=0; sub<mesh.subMeshCount; sub++)
                Graphics.DrawMesh(mesh,filters[i].transform.localToWorldMatrix,highlight,renderer.gameObject.layer,
                    view,sub,null,ShadowCastingMode.Off,false,null,LightProbeUsage.Off);
        }
    }

    void OnDisable()
    {
        Hit=default; highlighted=null; filters.Clear(); surfaces.Clear(); eyeServiceReady=false;
        if (startedEyeTracking)
        {
            EyeTrackingStopInfo stop=default;
            PXR_MotionTracking.StopEyeTracking(ref stop);
            startedEyeTracking=false;
        }
    }
    void OnDestroy() { if (highlight) Destroy(highlight); }
}
