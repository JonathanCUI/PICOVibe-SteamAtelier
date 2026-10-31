using System.Collections.Generic;
using ByteDance.PICO.SpatialAdapter;
using SwanAtelier;
using UnityEngine;
using UnityEngine.InputSystem;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace SwanJourney
{
    [DefaultExecutionOrder(2100)]
    public sealed class SwanJourneyInteraction : MonoBehaviour
    {
        public SteamJourney journey;
        public Transform workbenchRoot;
        public GameObject moveLesson, moveCaption;
        public MesaDayNightV13 sky;
        public bool Ready { get; private set; }
        public bool TwoHandActive => manipulation != null && manipulation.Active;
        public bool QuickReturning => returning != null;
        public int QuickReturnCount { get; private set; }
        public bool Exploding => explosion.Count > 0;
        public int ExplodingCount => explosion.Count;
        public int ExplodeCount { get; private set; }
        public float SceneScale => manipulation == null ? 1 : manipulation.Scale;
        bool WorkbenchInputBlocked => journey.verificationInputLock || journey.SwanSceneLoading || journey.SwanTransitionActive || journey.CompletionActive;

        const double JoinDelay = .28, ShortPinch = .40, DoubleGap = .50;
        const float TapTravel = .025f;
        const float QuickPinchTravel = .04f;
        sealed class Contact
        {
            public long id;
            public bool down, pending, blocked, ended, canceled, pinch, ownsRoot;
            public bool wasLoose, grabbing, continuousMoved, useHand, celestialActive, skyAutomatic;
            public double began;
            public GameObject target;
            public SwanJourneyTargetKind? kind;
            public JourneyModule module;
            public JourneyControl control;
            public JourneyAction action;
            public Vector3 startHit, hit, startHand, hand, objectStart, previousMove;
            public Vector3 controlAxis;
            public float maxTravel, initialValue, controlTravel;
            public SwanJourneyMoveResponse moveResponse;
            public int lastDirectionRequest;
        }
        readonly Dictionary<long, Contact> contacts = new Dictionary<long, Contact>();
        readonly Queue<SwanGestureSample> samples = new Queue<SwanGestureSample>();
        readonly HashSet<long> live = new HashSet<long>();
        readonly List<long> retired = new List<long>();
        InputAction primary, secondary;
        SwanTwoHandManipulation manipulation;
        SwanTabletopPlacement tabletop;
        long firstHand, secondHand;
        bool paused, previousSandbox;
        JourneyModule quickTap, returning;
        double quickTapEnded;
        Vector3 returnFrom;
        Quaternion returnRotation;
        float returnElapsed;
        sealed class ExplosionMotion
        {
            public JourneyModule module;
            public Vector3 from, to;
            public Quaternion rotation;
        }
        readonly List<ExplosionMotion> explosion = new List<ExplosionMotion>(18);
        float explosionElapsed;
#if UNITY_ANDROID && !UNITY_EDITOR
        float nextInputHealth;
        int receivedInputCallbacks, beganInputCallbacks;
#endif

        void Awake()
        {
            if (!workbenchRoot) workbenchRoot = transform;
            if (!journey) journey = FindObjectOfType<SteamJourney>();
            if (journey) journey.SwanInputActive = true;
        }
        void OnEnable()
        {
            primary = new InputAction("Journey primary", InputActionType.PassThrough, "<SpatialInputDevice>/primary");
            secondary = new InputAction("Journey secondary", InputActionType.PassThrough, "<SpatialInputDevice>/secondary");
            primary.performed += Receive; secondary.performed += Receive;
            primary.Enable(); secondary.Enable();
        }
        void Start()
        {
            if (!journey || !workbenchRoot) { Debug.LogError("SWAN_JOURNEY_INPUT_MISSING_BINDING"); enabled = false; return; }
            if (!sky) sky = FindObjectOfType<MesaDayNightV13>(true);
            tabletop = workbenchRoot.GetComponent<SwanTabletopPlacement>();
            manipulation = new SwanTwoHandManipulation(workbenchRoot);
            previousSandbox = journey.inSandbox;
            Ready = true;
            Debug.Log("SWAN_JOURNEY_INPUT_READY modules=" + journey.modules.Length + " finishing=" + journey.finishing.Length);
        }
        static long Id(int device, int interaction) => ((long)device << 32) | (uint)interaction;
        static bool Down(TouchPhase phase) => phase == TouchPhase.Began || phase == TouchPhase.Moved || phase == TouchPhase.Stationary;
        static bool ValidHand(Vector3 p) => SwanTwoHandManipulation.Finite(p) && p.sqrMagnitude > .000001f;
        void Receive(InputAction.CallbackContext context)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            receivedInputCallbacks++;
#endif
            if (!Ready || paused) return;
            var state = context.ReadValue<SpatialInputState>();
#if UNITY_ANDROID && !UNITY_EDITOR
            if (state.phase == TouchPhase.Began)
            {
                beganInputCallbacks++;
                Debug.Log("SWAN_JOURNEY_INPUT_NATIVE_BEGIN type=" + state.type + " target=" +
                    (state.targetObject ? state.targetObject.name : "no-hit") + " blocked=" + WorkbenchInputBlocked);
            }
#endif
            if (state.phase == TouchPhase.None) return;
            QueueGestureSample(new SwanGestureSample {
                id = Id(context.control.device.deviceId, state.interactionId), phase = state.phase,
                pinch = state.type == InteractionType.Pinch || state.type == InteractionType.IndirectPinch,
                target = state.targetObject, handPosition = state.devicePosition, hitPosition = state.currentPosition
            });
        }
        public void QueueGestureSample(SwanGestureSample sample) { samples.Enqueue(sample); }
        void Update() { using (SwanJourney.SwanJourneyCpuTimings.Measure(SwanJourney.SwanJourneyCpuTimings.Stage.Input)) CpuMeasuredUpdate(); }
    void CpuMeasuredUpdate()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            LogInputHealth();
#endif
            if (!Ready || paused) return;
            if (previousSandbox != journey.inSandbox)
            {
                ResetSpatialGestures(); previousSandbox = journey.inSandbox;
            }
            live.Clear();
            foreach (var device in InputSystem.devices) if (device is SpatialInputDevice spatial)
            {
                Remember(device.deviceId, spatial.primary.ReadValue());
                Remember(device.deviceId, spatial.secondary.ReadValue());
            }
            Drain(Time.unscaledTimeAsDouble);
            foreach (var contact in contacts.Values) if (contact.down && !live.Contains(contact.id))
            { contact.down = false; contact.ended = contact.canceled = true; }
            Resolve(Time.unscaledTimeAsDouble, Time.unscaledDeltaTime);
            TickQuickReturn(Time.unscaledDeltaTime);
            TickExplosion(Time.unscaledDeltaTime);
        }
        void Remember(int device, SpatialInputState value) { if (Down(value.phase)) live.Add(Id(device, value.interactionId)); }
#if UNITY_ANDROID && !UNITY_EDITOR
        // Read-only, once per five seconds. Distinguishes device/action binding,
        // paused lifecycle, and native-event delivery without changing arbitration.
        void LogInputHealth()
        {
            if (Time.unscaledTime < nextInputHealth) return;
            nextInputHealth = Time.unscaledTime + 5f;
            int devices = 0, enabledDevices = 0;
            TouchPhase first = TouchPhase.None, second = TouchPhase.None;
            foreach (var device in InputSystem.devices) if (device is SpatialInputDevice spatial)
            {
                devices++;
                if (device.enabled) enabledDevices++;
                first = spatial.primary.ReadValue().phase;
                second = spatial.secondary.ReadValue().phase;
            }
            Debug.Log("SWAN_JOURNEY_INPUT_HEALTH ready=" + Ready + " paused=" + paused +
                " focused=" + Application.isFocused + " blocked=" + (journey && WorkbenchInputBlocked) +
                " devices=" + devices + " enabledDevices=" + enabledDevices +
                " primaryEnabled=" + (primary != null && primary.enabled) +
                " secondaryEnabled=" + (secondary != null && secondary.enabled) +
                " primaryBindings=" + (primary == null ? 0 : primary.controls.Count) +
                " secondaryBindings=" + (secondary == null ? 0 : secondary.controls.Count) +
                " callbacks=" + receivedInputCallbacks + " began=" + beganInputCallbacks +
                " rawPhases=" + first + "/" + second + " live=" + live.Count);
            receivedInputCallbacks = beganInputCallbacks = 0;
        }
#endif
        // Uses exactly the native input arbitration path, without requiring a
        // physical SpatialInputDevice in deterministic editor checks.
        public void ProcessGestureFrame(double now, float dt)
        {
            if (!Ready || paused) return;
            Drain(now); Resolve(now, dt); TickQuickReturn(dt); TickExplosion(dt);
        }
        void Drain(double now)
        {
            while (samples.Count > 0)
            {
                var sample = samples.Dequeue(); contacts.TryGetValue(sample.id, out var contact);
                if (sample.phase == TouchPhase.Began && (contact == null || !contact.down))
                {
                    bool own = OwnsTarget(sample.target);
                    if (!own && !(sample.pinch && !sample.target && ValidHand(sample.handPosition))) continue;
                    contact = new Contact { id = sample.id, down = true, pending = true, pinch = sample.pinch,
                        target = sample.target, ownsRoot = own, began = now, startHit = sample.hitPosition,
                        startHand = sample.handPosition, useHand = sample.pinch && ValidHand(sample.handPosition) };
                    ResolveTarget(contact);
                    if (contact.module != quickTap) ClearTap();
                    contact.wasLoose = contact.module != null && !contact.module.installed;
                    contacts[sample.id] = contact;
                    Debug.Log("SWAN_JOURNEY_INPUT_BEGIN id=" + sample.id + " target=" + (sample.target ? sample.target.name : "no-hit"));
                }
                if (contact == null || !contact.down) continue;
                if (Down(sample.phase)) contact.pinch = sample.pinch;
                // A released pinch may have no target and a zero hit/hand pose.
                // Keep its last valid pose. Gaze drift is not hand movement.
                if (sample.target && SwanTwoHandManipulation.Finite(sample.hitPosition)) contact.hit = sample.hitPosition;
                if (ValidHand(sample.handPosition)) contact.hand = sample.handPosition;
                else if (contact.useHand && Down(sample.phase))
                { contact.down = false; contact.ended = contact.canceled = true; }
                float travel = contact.useHand ? Vector3.Distance(contact.startHand, contact.hand)
                    : Vector3.Distance(contact.startHit, contact.hit);
                contact.maxTravel = Mathf.Max(contact.maxTravel, travel);
                if (sample.phase == TouchPhase.Ended || sample.phase == TouchPhase.Canceled)
                { contact.down = false; contact.ended = true; contact.canceled = sample.phase == TouchPhase.Canceled; }
            }
        }
        bool OwnsTarget(GameObject target)
        {
            if(!target)return false;
            if(target.transform.IsChildOf(workbenchRoot))return true;
            // The deferred canyon deliberately retains separate scene ownership.
            // Its gaze surfaces must still count as this application's targets.
            var loader=workbenchRoot.GetComponent<SwanJourneySceneLoader>();
            return journey.inSandbox && loader && loader.Environment
                && target.transform.IsChildOf(loader.Environment.transform);
        }
        void ResolveTarget(Contact c)
        {
            if (!c.target) return;
            var tag = c.target.GetComponentInParent<SwanJourneyTarget>();
            if (tag)
            {
                c.kind = tag.kind;
                if (tag.kind == SwanJourneyTargetKind.Module) c.module = journey.SwanModule(tag.moduleIndex);
                if (tag.kind == SwanJourneyTargetKind.Control)
                {
                    c.control = tag.control ? tag.control : c.target.GetComponentInParent<JourneyControl>();
                    c.action = tag.useActionOverride || !c.control ? tag.action : c.control.action;
                }
                return;
            }
            c.control = c.target.GetComponentInParent<JourneyControl>();
            if (c.control && c.control.caption != "Inspection shell" && c.control.caption != "Ember")
            { c.kind = SwanJourneyTargetKind.Control; c.action = c.control.action; return; }
            c.control = null;
            if (sky && sky.sun && c.target.transform.IsChildOf(sky.sun)) { c.kind = SwanJourneyTargetKind.Sun; return; }
            if (sky && sky.moon && c.target.transform.IsChildOf(sky.moon)) { c.kind = SwanJourneyTargetKind.Moon; return; }
            int total = journey.modules.Length + journey.finishing.Length;
            for (int i = 0; i < total; i++)
            {
                var module = journey.SwanModule(i);
                if (module.preview && c.target.transform.IsChildOf(module.preview.transform))
                { c.kind = SwanJourneyTargetKind.Module; c.module = module; return; }
                if (module.objects == null) continue;
                foreach (var item in module.objects) if (item && c.target.transform.IsChildOf(item.transform))
                { c.kind = SwanJourneyTargetKind.Module; c.module = module; return; }
            }
        }
        void Resolve(double now, float dt)
        {
            Contact a = null, b = null;
            if (TwoHandActive)
            {
                contacts.TryGetValue(firstHand, out a); contacts.TryGetValue(secondHand, out b);
                if (WorkbenchInputBlocked || a == null || b == null || !a.down || !b.down || !a.pinch || !b.pinch) manipulation.End();
            }
            if (!TwoHandActive && !WorkbenchInputBlocked)
            {
                a = b = null;
                foreach (var c in contacts.Values) if (c.down && c.pinch)
                { if (a == null || !a.down) a = c; else if (c != a) { b = c; break; } }
                if (a != null && b != null && a.down && b.down && (a.ownsRoot || b.ownsRoot))
                {
                    Cancel(a); Cancel(b); CancelQuickReturn(); CancelExplosion("two-hand-takeover"); ClearTap();
                    if (manipulation.Begin(a.hand, b.hand))
                    { firstHand = a.id; secondHand = b.id; Debug.Log("SWAN_JOURNEY_TWO_HAND_BEGIN"); }
                }
            }
            if (TwoHandActive) manipulation.Step(a.hand, b.hand, dt);
            retired.Clear();
            foreach (var c in contacts.Values)
            {
                if (!c.blocked)
                {
                    bool quickTapCandidate = c.ended && IsQuickTap(c, now);
                    if (c.pending && quickTapCandidate) c.pending = false;
                    else if (c.pending && (c.down && (!c.pinch || c.kind == SwanJourneyTargetKind.MoveWorkbench || now - c.began >= JoinDelay) || c.ended && !c.canceled)) Begin(c);
                    if (!c.pending && !c.canceled && !quickTapCandidate) Move(c);
                    if (c.ended && !c.blocked)
                    {
                        // Do not snap a short tap as a normal grab release:
                        // the second tap owns the animated installation.
                        End(c, c.canceled || quickTapCandidate); RegisterTap(c, now);
                    }
                }
                if (!c.down) retired.Add(c.id);
            }
            foreach (long id in retired) contacts.Remove(id);
        }
        bool OtherOwner(Contact c)
        {
            foreach (var other in contacts.Values)
                if (other != c && !other.blocked && other.down && (other.grabbing || other.celestialActive || other.kind == SwanJourneyTargetKind.Control && !other.pending)) return true;
            return false;
        }
        void Begin(Contact c)
        {
            c.pending = false;
            if (!c.kind.HasValue || OtherOwner(c)) { c.blocked = true; return; }
            if (c.kind == SwanJourneyTargetKind.MoveWorkbench)
            {
                if (WorkbenchInputBlocked) { c.blocked = true; return; }
                CancelExplosion("move-handle");
                c.previousMove = c.useHand ? c.startHand : c.startHit;
                c.objectStart=workbenchRoot.position;
                c.moveResponse=new SwanJourneyMoveResponse();
                if(TryHead(out var head))c.moveResponse.Begin(Vector3.Distance(head,workbenchRoot.position));
                Debug.Log("SWAN_JOURNEY_MOVE_BEGIN mode="+(journey.inSandbox?"sandbox":"workshop"));
                tabletop?.BeginManipulation(); return;
            }
            if (c.kind == SwanJourneyTargetKind.Control)
            {
                if (journey.verificationInputLock || journey.SwanTransitionActive) { c.blocked = true; return; }
                if (Continuous(c.action))
                {
                    if (journey.SwanInputBlocked) { c.blocked = true; return; }
                    var refined = journey.GetComponent<StudioSpatialRefineV19>();
                    var frame = refined && refined.face ? refined.face : c.control ? c.control.transform.parent : workbenchRoot;
                    c.controlAxis = c.action == JourneyAction.Throttle ? -frame.forward : frame.up;
                    c.controlTravel = Mathf.Clamp(frame.TransformVector(Vector3.forward * (c.action == JourneyAction.Throttle ? 1.04f : .7f)).magnitude, .035f, .3f);
                    c.initialValue = c.action == JourneyAction.Throttle ? journey.throttle : c.action == JourneyAction.Brake ? journey.brake : journey.direction;
                    c.lastDirectionRequest = journey.direction;
                }
                return;
            }
            if (c.kind == SwanJourneyTargetKind.ExplodeAll)
            {
                if (journey.inSandbox || journey.verificationInputLock || journey.SwanTransitionActive || journey.AutoAssembling) c.blocked = true;
                return;
            }
            if (journey.SwanInputBlocked) { c.blocked = true; return; }
            if (c.kind == SwanJourneyTargetKind.Sun || c.kind == SwanJourneyTargetKind.Moon)
            {
                if (!sky || !journey.inSandbox) { c.blocked = true; return; }
                c.objectStart = (c.kind == SwanJourneyTargetKind.Moon ? sky.moon : sky.sun).position;
                c.skyAutomatic = sky.automatic; sky.automatic = false; c.celestialActive = true; return;
            }
            if (c.module == null || QuickReturning) { c.blocked = true; return; }
            CancelExplosion("part-grab");
            if (!c.module.installed)
            {
                c.objectStart = c.module.preview.transform.position;
                c.grabbing = journey.SwanBeginGrab(c.module);
                if (!c.grabbing) c.blocked = true;
            }
        }
        static bool Continuous(JourneyAction action) => action == JourneyAction.Throttle || action == JourneyAction.Brake || action == JourneyAction.Reverse;
        Vector3 Delta(Contact c) => c.useHand && ValidHand(c.hand) ? c.hand - c.startHand : c.hit - c.startHit;
        void Move(Contact c)
        {
            if (c.blocked) return;
            Vector3 delta = Delta(c);
            if (!SwanTwoHandManipulation.Finite(delta) || delta.magnitude > 2) { Cancel(c); return; }
            if (c.kind == SwanJourneyTargetKind.MoveWorkbench)
            {
                if (WorkbenchInputBlocked) { Cancel(c); return; }
                Vector3 point = c.useHand ? c.hand : c.hit, movement = point - c.previousMove; c.previousMove = point;
                if (!SwanTwoHandManipulation.Finite(movement) || movement.magnitude > .4f) return;
                Vector3 displacement=movement;var rotation=workbenchRoot.rotation;
                if(TryHead(out var head))
                {
                    if(c.useHand)displacement=c.moveResponse.Step(movement,Vector3.Distance(head,workbenchRoot.position),Time.unscaledDeltaTime);
                    if(movement.sqrMagnitude>.00000001f)
                        rotation=SwanJourneyMoveResponse.FacingPlayer(workbenchRoot.position+displacement,rotation,head,Time.unscaledDeltaTime);
                }
                if (tabletop) tabletop.ApplyPose(workbenchRoot.position + displacement, rotation, workbenchRoot.localScale, movement, Time.unscaledTime);
                else workbenchRoot.SetPositionAndRotation(workbenchRoot.position+displacement,rotation);
                return;
            }
            // Act owns the exceptions for Verify/Pause/Reset during automatic
            // assembly. A button has no continuous work to perform here.
            if (c.kind == SwanJourneyTargetKind.ExplodeAll || c.kind == SwanJourneyTargetKind.Control && !Continuous(c.action)) return;
            if (journey.SwanInputBlocked) { Cancel(c); return; }
            if (c.kind == SwanJourneyTargetKind.Control && Continuous(c.action))
            {
                float amount = Vector3.Dot(delta, c.controlAxis) / c.controlTravel;
                if (Mathf.Abs(amount) < .035f && !c.continuousMoved) return;
                c.continuousMoved = true;
                float value = c.initialValue + amount * (c.action == JourneyAction.Reverse ? 4 : 1);
                if (c.action == JourneyAction.Reverse)
                {
                    int requested = value < 0 ? -1 : 1;
                    if (requested == c.lastDirectionRequest) return;
                    c.lastDirectionRequest = requested;
                }
                journey.SwanSetControl(c.action, value); return;
            }
            if (c.celestialActive)
            {
                Vector3 desired = c.objectStart + delta;
                float distance = float.PositiveInfinity, time = sky.timeOfDay;
                for (int i = 0; i < 720; i++)
                {
                    float t = i / 720f, d = (sky.Orbit(t, c.kind == SwanJourneyTargetKind.Moon) - desired).sqrMagnitude;
                    if (d < distance) { distance = d; time = t; }
                }
                sky.SetTime(time); return;
            }
            if (c.module == null) return;
            if (c.module.installed && delta.magnitude > TapTravel)
            {
                if (!journey.SwanDetach(c.module)) { Cancel(c); return; }
                c.objectStart = c.module.preview.transform.position;
                c.grabbing = journey.SwanBeginGrab(c.module);
                if (!c.grabbing) { Cancel(c); return; }
            }
            if (c.grabbing && journey.SwanHeldModule == c.module) journey.MoveGrab(c.objectStart + delta);
        }
        static bool TryHead(out Vector3 position)
        {
            var head=UnityEngine.XR.InputDevices.GetDeviceAtXRNode(UnityEngine.XR.XRNode.Head);
            position=default;
            return head.isValid&&head.TryGetFeatureValue(UnityEngine.XR.CommonUsages.devicePosition,out position)
                &&SwanTwoHandManipulation.Finite(position);
        }
        void End(Contact c, bool cancel)
        {
            if (c.celestialActive)
            { if (sky) sky.automatic = c.skyAutomatic; c.celestialActive = false; }
            if (c.kind == SwanJourneyTargetKind.MoveWorkbench) {
                tabletop?.EndManipulation();
                if(!cancel && !c.blocked && c.moveResponse!=null && moveCaption && moveLesson
                    && !moveCaption.activeSelf && Vector3.Distance(c.objectStart,workbenchRoot.position)>.025f)
                {
                    moveLesson.SetActive(false);
                    moveCaption.SetActive(true);
                }
                Debug.Log("SWAN_JOURNEY_MOVE_END canceled="+cancel+" position="+workbenchRoot.position.ToString("F3"));
            }
            if (!journey) { c.grabbing = false; return; }
            if (!cancel && c.kind == SwanJourneyTargetKind.ExplodeAll && c.maxTravel <= TapTravel)
            { ExplodeAll(); return; }
            if (c.grabbing && journey.SwanHeldModule == c.module) journey.SwanEndGrab(cancel);
            c.grabbing = false;
            if (!cancel && c.kind == SwanJourneyTargetKind.Control && !c.continuousMoved && c.maxTravel <= TapTravel)
            {
                // The authored slide has no stepped click; brake and direction
                // retain their ordinary button actions for short pinches.
                if (c.action != JourneyAction.Throttle) journey.SwanAct(c.action);
            }
        }
        void Cancel(Contact c) { End(c, true); c.pending = false; c.blocked = true; ClearTap(); }
        void ClearTap() { quickTap = null; quickTapEnded = double.NegativeInfinity; }
        bool IsQuickTap(Contact c, double now) => c.pinch && !c.canceled && !c.blocked && c.wasLoose && c.module != null &&
            !c.module.installed && c.module.preview && c.module.preview.activeInHierarchy &&
            !journey.SwanInputBlocked && !journey.inSandbox && now - c.began <= ShortPinch && c.maxTravel <= QuickPinchTravel;
        void RegisterTap(Contact c, double now)
        {
            if (!IsQuickTap(c, now)) { ClearTap(); return; }
            if (quickTap == c.module && c.began >= quickTapEnded && c.began - quickTapEnded <= DoubleGap)
            { ClearTap(); QuickReturn(c.module); }
            else { quickTap = c.module; quickTapEnded = now; }
        }
        public bool QuickReturn(JourneyModule module)
        {
            if (Exploding || QuickReturning || !journey.SwanCanInstall(module) || !journey.SwanBeginGrab(module))
            { journey.SwanNotify("请先装回当前原理模块，再归位后续部件。"); return false; }
            returning = module; returnElapsed = 0;
            returnFrom = journey.trainAnchor.InverseTransformPoint(module.preview.transform.position);
            returnRotation = Quaternion.Inverse(journey.trainAnchor.rotation) * module.preview.transform.rotation;
            Debug.Log("SWAN_JOURNEY_QUICK_RETURN_BEGIN title=" + module.title);
            return true;
        }
        public void TickQuickReturn(float dt)
        {
            if (!QuickReturning) return;
            if (!journey.SwanCanInstall(returning) || !returning.preview || journey.SwanHeldModule != returning)
            { CancelQuickReturn(); return; }
            returnElapsed += Mathf.Clamp(dt, 0, 1f / 30);
            float t = Mathf.Clamp01(returnElapsed / .22f), ease = t * t * (3 - 2 * t);
            var preview = returning.preview.transform;
            preview.SetPositionAndRotation(journey.trainAnchor.TransformPoint(Vector3.Lerp(returnFrom, returning.previewCentre, ease)),
                journey.trainAnchor.rotation * Quaternion.Slerp(returnRotation, Quaternion.identity, ease));
            preview.localScale = Vector3.Lerp(preview.localScale, Vector3.one, ease);
            if (t < 1) return;
            journey.MoveGrab(journey.trainAnchor.TransformPoint(returning.previewCentre));
            bool installed = journey.SwanEndGrab(); returning = null;
            if (installed) { QuickReturnCount++; Debug.Log("SWAN_JOURNEY_QUICK_RETURN_COMPLETE"); }
        }
        void CancelQuickReturn()
        {
            if (returning != null && journey && journey.SwanHeldModule == returning) journey.SwanEndGrab(true);
            returning = null; returnElapsed = 0;
        }
        public bool ExplodeAll()
        {
            if (!Ready || !journey || journey.inSandbox || journey.verificationInputLock || journey.SwanTransitionActive || journey.AutoAssembling) return false;
            // Do not clear the dictionary while called from a button's End()
            // inside Resolve's contact enumeration. Block contacts until release.
            foreach (var contact in contacts.Values) Cancel(contact);
            manipulation?.End(); tabletop?.EndManipulation();
            CancelQuickReturn(); CancelExplosion("restart"); ClearTap();
            journey.SwanEndGrab(true);
            int total = journey.modules.Length + journey.finishing.Length;
            Vector3 centre = Vector3.zero;
            for (int i = 0; i < total; i++) centre += journey.SwanModule(i).previewCentre;
            if (total > 0) centre /= total;
            for (int i = 0; i < total; i++)
            {
                var module = journey.SwanModule(i);
                if (!module.installed || !journey.DetachModule(module)) continue;
                var preview = module.preview.transform;
                explosion.Add(new ExplosionMotion { module = module,
                    from = journey.trainAnchor.InverseTransformPoint(preview.position),
                    to = module.previewCentre + ExplosionOffset(module, centre, i),
                    rotation = Quaternion.Inverse(journey.trainAnchor.rotation) * preview.rotation });
            }
            if (!Exploding)
            { journey.SwanNotify("部件已拆开，可再次捏合拿起，双击捏合快速归位。"); return false; }
            explosionElapsed = 0; ExplodeCount++;
            journey.SwanNotify("部件展开中 · 松开后可逐件观察，双击捏合按顺序归位。");
            Debug.Log("SWAN_JOURNEY_EXPLODE_BEGIN pieces=" + explosion.Count + " duration=0.30");
            return true;
        }
        static Vector3 ExplosionOffset(JourneyModule module, Vector3 centre, int index)
        {
            Vector3 authored = Vector3.zero; int count = 0;
            var seen = new HashSet<LocomotivePart>();
            if (module.objects != null) foreach (var item in module.objects) if (item)
                foreach (var part in item.GetComponentsInChildren<LocomotivePart>(true)) if (seen.Add(part))
                { authored += part.explodeOffset; count++; }
            if (count > 0) authored /= count;
            Vector3 direction = authored.sqrMagnitude > .01f ? authored : module.previewCentre - centre;
            direction.y = Mathf.Max(.4f, direction.y);
            // Merged left/right totals can cancel the original lateral offsets.
            // A small deterministic fan separates these 18 assembly groups.
            direction.x += (index % 5 - 2) * .14f;
            direction.z += (index % 3 - 1) * .35f;
            return direction.normalized * Mathf.Clamp(authored.magnitude * .9f, 1.4f, 2.5f);
        }
        public void TickExplosion(float dt)
        {
            if (!Exploding) return;
            if (!journey || journey.inSandbox || journey.verificationInputLock || journey.SwanTransitionActive || journey.AutoAssembling)
            { CancelExplosion("state-change"); return; }
            foreach (var motion in explosion)
                if (motion.module.installed || !motion.module.parked || !motion.module.preview)
                { CancelExplosion("module-state-change"); return; }
            explosionElapsed += Mathf.Clamp(dt, 0, 1f / 30);
            float t = Mathf.Clamp01(explosionElapsed / .30f), ease = t * t * (3 - 2 * t);
            foreach (var motion in explosion)
            {
                var preview = motion.module.preview.transform;
                preview.SetPositionAndRotation(journey.trainAnchor.TransformPoint(Vector3.Lerp(motion.from, motion.to, ease)),
                    journey.trainAnchor.rotation * motion.rotation);
            }
            if (t < 1) return;
            int count = explosion.Count; explosion.Clear();
            journey.SwanNotify("部件已拆开 · 再次捏合拿起，双击捏合按顺序快速归位；也可一键组装。");
            Debug.Log("SWAN_JOURNEY_EXPLODE_COMPLETE pieces=" + count);
        }
        void CancelExplosion(string reason)
        {
            if (!Exploding) return;
            int count = explosion.Count;
            // DetachModule already marked every preview parked. Keep its exact
            // current pose; cancellation never installs or sends it to the tray.
            explosion.Clear(); explosionElapsed = 0;
            Debug.Log("SWAN_JOURNEY_EXPLODE_CANCEL pieces=" + count + " reason=" + reason + " keptCurrentPose=true");
        }
        public void ResetSpatialGestures()
        {
            foreach (var c in contacts.Values) End(c, true);
            contacts.Clear(); samples.Clear(); live.Clear();
            manipulation?.End(); tabletop?.EndManipulation(); CancelQuickReturn(); CancelExplosion("input-reset"); ClearTap();
        }
        void OnApplicationPause(bool value) { paused = value; if (value) ResetSpatialGestures(); }
        void OnDisable()
        {
            if (primary != null) { primary.performed -= Receive; primary.Dispose(); primary = null; }
            if (secondary != null) { secondary.performed -= Receive; secondary.Dispose(); secondary = null; }
            ResetSpatialGestures();
        }
    }
}
