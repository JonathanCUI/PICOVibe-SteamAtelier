using System.Collections.Generic;
using ByteDance.PICO.SpatialAdapter;
using UnityEngine;
using UnityEngine.InputSystem;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace SwanAtelier
{
    public struct SwanGestureSample
    {
        public long id;
        public TouchPhase phase;
        public bool pinch;
        public GameObject target;
        public Vector3 handPosition, hitPosition;
    }

    public sealed partial class SwanAtelierController
    {
        const double TwoHandJoinDelay = .28;
        sealed class Contact
        {
            public long id;
            public bool down, pending, blocked, ended, canceled, pinch, ownsWorkbench, stageHandle;
            public double beganAt;
            public int loosePiece = -1;
            public float maxHitTravel;
            public GameObject target;
            public Vector3 startHit, startHand, hit, hand;
            public Pointer pointer;
        }
        readonly Dictionary<long, Contact> contacts = new Dictionary<long, Contact>();
        readonly Queue<SwanGestureSample> gestureSamples = new Queue<SwanGestureSample>();
        readonly HashSet<long> liveContacts = new HashSet<long>();
        readonly List<long> retiredContacts = new List<long>();
        InputAction primaryGesture, secondaryGesture;
        SwanTwoHandManipulation manipulation;
        long firstHand, secondHand;
        bool inputPaused;
        double nextGestureLog;
        public bool TwoHandActive => manipulation != null && manipulation.Active;
        public float SceneScale => manipulation != null ? manipulation.Scale : 1;

        void OnEnable()
        {
            primaryGesture = new InputAction("Atelier primary", InputActionType.PassThrough, "<SpatialInputDevice>/primary");
            secondaryGesture = new InputAction("Atelier secondary", InputActionType.PassThrough, "<SpatialInputDevice>/secondary");
            primaryGesture.performed += SpatialGestureReceived; secondaryGesture.performed += SpatialGestureReceived;
            primaryGesture.Enable(); secondaryGesture.Enable();
        }
        void OnDisable()
        {
            if (primaryGesture != null) { primaryGesture.performed -= SpatialGestureReceived; primaryGesture.Dispose(); primaryGesture = null; }
            if (secondaryGesture != null) { secondaryGesture.performed -= SpatialGestureReceived; secondaryGesture.Dispose(); secondaryGesture = null; }
            ResetSpatialGestures();
        }
        static long ContactId(int deviceId, int interactionId) => ((long)deviceId << 32) | (uint)interactionId;
        static bool Down(TouchPhase phase) => phase == TouchPhase.Began || phase == TouchPhase.Moved || phase == TouchPhase.Stationary;
        void SpatialGestureReceived(InputAction.CallbackContext context)
        {
            if (!Ready || inputPaused) return;
            var state = context.ReadValue<SpatialInputState>();
            if (state.phase == TouchPhase.None) return;
            QueueGestureSample(new SwanGestureSample {
                id = ContactId(context.control.device.deviceId, state.interactionId), phase = state.phase,
                pinch = state.type == InteractionType.Pinch || state.type == InteractionType.IndirectPinch,
                target = state.targetObject, handPosition = state.devicePosition, hitPosition = state.currentPosition
            });
        }
        public void QueueGestureSample(SwanGestureSample sample) { gestureSamples.Enqueue(sample); }

        void PollSpatialInput()
        {
            if (inputPaused) return;
            // primary/secondary are ordered active contacts, not fixed left/right
            // hands. Stable interaction IDs prevent a slot swap from turning 180°.
            liveContacts.Clear();
            foreach (var device in InputSystem.devices) if (device is SpatialInputDevice spatial) {
                RememberLive(device.deviceId, spatial.primary.ReadValue());
                RememberLive(device.deviceId, spatial.secondary.ReadValue());
            }
            DrainGestureSamples(Time.unscaledTimeAsDouble);
            foreach (var contact in contacts.Values)
                if (contact.down && !liveContacts.Contains(contact.id)) { contact.down = false; contact.ended = contact.canceled = true; }
            ResolveGestureFrame(Time.unscaledTimeAsDouble, Time.unscaledDeltaTime);
        }
        void RememberLive(int device, SpatialInputState state)
        {
            if (Down(state.phase)) liveContacts.Add(ContactId(device, state.interactionId));
        }
        // Same processing path is exercised by Play Mode tests with deterministic
        // input samples. It does not emulate a physical-device acceptance test.
        public void ProcessGestureFrame(double now, float dt)
        {
            DrainGestureSamples(now); ResolveGestureFrame(now, dt);
        }
        void DrainGestureSamples(double now)
        {
            while (gestureSamples.Count > 0) {
                var sample = gestureSamples.Dequeue();
                contacts.TryGetValue(sample.id, out var contact);
                if (sample.phase == TouchPhase.Began && (contact == null || !contact.down)) {
                    var loose = sample.target ? sample.target.GetComponentInParent<SwanAtelierLoosePiece>() : null;
                    if (!loose || loose.index != quickTapPiece) ClearQuickInstallTap();
                    bool ownsWorkbench = sample.target && sample.target.transform.IsChildOf(transform);
                    // A second pinching hand can miss the small model. Retain a
                    // no-hit hand for pairing, but never turn it into a UI click.
                    bool emptyPinch = !sample.target && sample.pinch && SwanTwoHandManipulation.Finite(sample.handPosition) && sample.handPosition.sqrMagnitude > .000001f;
                    if (!ownsWorkbench && !emptyPinch) continue;
                    contact = new Contact { id = sample.id, down = true, pending = true, pinch = sample.pinch,
                        target = sample.target, startHit = sample.hitPosition, startHand = sample.handPosition, beganAt = now,
                        loosePiece = loose ? loose.index : -1, ownsWorkbench = ownsWorkbench };
                    var utility=sample.target ? sample.target.GetComponentInParent<SwanAtelierTarget>() : null;
                    contact.stageHandle=utility&&utility.moveStage;
                    contacts[sample.id] = contact;
                    Debug.Log("SWAN_ATELIER_INPUT_BEGIN id=" + sample.id + " pinch=" + sample.pinch + " target=" + (sample.target ? sample.target.name : "no-hit"));
                }
                if (contact == null || !contact.down) continue;
                // A near contact can transition into a pinch without a new Began.
                // Keep the live interaction kind instead of freezing its first type.
                if (Down(sample.phase)) contact.pinch = sample.pinch;
                if (sample.target && sample.target.transform.IsChildOf(transform)) contact.ownsWorkbench = true;
                contact.hand = sample.handPosition; contact.hit = sample.hitPosition;
                contact.maxHitTravel = Mathf.Max(contact.maxHitTravel, Vector3.Distance(contact.startHit, contact.hit));
                if (sample.phase == TouchPhase.Ended || sample.phase == TouchPhase.Canceled) {
                    contact.down = false; contact.ended = true; contact.canceled = sample.phase == TouchPhase.Canceled;
                }
            }
        }
        void ResolveGestureFrame(double now, float dt)
        {
            if (manipulation == null) manipulation = new SwanTwoHandManipulation(transform);
            Contact a = null, b = null;
            if (TwoHandActive) {
                contacts.TryGetValue(firstHand, out a); contacts.TryGetValue(secondHand, out b);
                if (a == null || b == null || !a.down || !b.down || !a.pinch || !b.pinch) StopTwoHands();
            }
            if (!TwoHandActive) {
                a = b = null;
                foreach (var contact in contacts.Values) if (contact.down && contact.pinch) {
                    if (a == null) a = contact; else { b = contact; break; }
                }
                if (a != null && b != null && (a.ownsWorkbench || b.ownsWorkbench)) {
                    CancelContact(a); CancelContact(b); End(mouse, true); mouse = null;
                    CancelReset(); whistleHeld = false;
                    if (manipulation.Begin(a.hand, b.hand)) {
                        firstHand = a.id; secondHand = b.id; nextGestureLog = now + 2;
                        Debug.Log("SWAN_ATELIER_TWO_HAND_BEGIN scale=" + SceneScale.ToString("F2") + " distance=" + Vector3.Distance(a.hand,b.hand).ToString("F2"));
                    }
                    else SetMessage("双手保持捏合，稍微分开后即可调整整个展台。");
                }
            }
            if (TwoHandActive) {
                if (!manipulation.Step(a.hand, b.hand, dt)) {
                    SetMessage("双手距离过近或追踪发生跳变，展台已停住；两手分开后继续。");
                    Debug.Log("SWAN_ATELIER_TWO_HAND_CANCEL invalid or discontinuous pose");
                }
                else {
                    SetMessage("双手调整 · " + (SceneScale * 100).ToString("F0") + "%  |  平移 · Y 轴旋转 · 缩放；松手固定");
                    if (now >= nextGestureLog) { nextGestureLog = now + 2;
                        Debug.Log("SWAN_ATELIER_TWO_HAND_UPDATE scale=" + SceneScale.ToString("F2") + " yaw=" + transform.eulerAngles.y.ToString("F1") + " position=" + transform.position.ToString("F2")); }
                }
            }
            retiredContacts.Clear();
            foreach (var contact in contacts.Values) {
                if (!contact.blocked) {
                    // The move handle has no click action to protect. Start it
                    // immediately; a second hand can still take over below. The
                    // pairing delay otherwise feels sticky even away from tables.
                    if (contact.pending && ((!contact.pinch || contact.stageHandle || now - contact.beganAt >= TwoHandJoinDelay) && contact.down || contact.ended && !contact.canceled))
                        CommitSingle(contact);
                    if (contact.pointer != null && !contact.canceled) {
                        if (contact.pointer.useHandForThrottle) MoveThrottleHand(contact.pointer,contact.hand);
                        else Move(contact.pointer, contact.pointer.moveStage&&contact.pinch ? contact.hand : contact.hit);
                    }
                    if (contact.ended) {
                        End(contact.pointer, contact.canceled);
                        RegisterQuickInstallTap(contact, now);
                    }
                }
                if (!contact.down) retiredContacts.Add(contact.id);
            }
            foreach (long id in retiredContacts) { contacts.Remove(id); pointers.Remove(id); }
        }
        void CommitSingle(Contact contact)
        {
            contact.pending = false;
            contact.pointer = Begin(contact.target, contact.startHit);
            ConfigureThrottleHand(contact.pointer,contact.startHand,contact.pinch);
            if(contact.pointer!=null&&contact.pointer.moveStage&&contact.pinch)
                contact.pointer.lastStageInput=contact.startHand;
            pointers[contact.id] = contact.pointer;
        }
        void CancelContact(Contact contact)
        {
            ClearQuickInstallTap();
            contact.pending = false; contact.blocked = true;
            End(contact.pointer, true); contact.pointer = null; pointers.Remove(contact.id);
        }
        void StopTwoHands()
        {
            if (!TwoHandActive) return;
            manipulation.End();
            SetMessage("展台已固定 · " + (SceneScale * 100).ToString("F0") + "%  |  双手再次捏合继续调整");
            Debug.Log("SWAN_ATELIER_TWO_HAND_END scale=" + SceneScale.ToString("F2"));
        }
        public void ResetSpatialGestures()
        {
            CancelPartMotions();
            ClearQuickInstallTap();
            StopTwoHands();
            foreach (var contact in contacts.Values) End(contact.pointer, true);
            contacts.Clear(); pointers.Clear(); gestureSamples.Clear();
            End(mouse, true); mouse = null; if (atelier) CancelReset(); whistleHeld = false;
            HidePlacementHint();
        }
    }
}
