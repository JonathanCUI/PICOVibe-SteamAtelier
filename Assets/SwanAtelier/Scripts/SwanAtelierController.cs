using System;
using System.Collections.Generic;
using System.Linq;
using ByteDance.PICO.SpatialAdapter;
using ByteDance.PICO.SpatialAdapter.Component;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SwanAtelier
{
    [DefaultExecutionOrder(1400)]
    public sealed partial class SwanAtelierController : MonoBehaviour
    {
        public AtelierExperience atelier;
        public LocomotiveController engine;
        public LocomotiveFreeAssembly assembly;
        public LocomotiveDriveV9 drive;
        public LocomotiveThermalEffects thermal;
        public LocomotivePresentationV4 presentation;
        public LocomotiveDoorsV11 doors;
        public Camera previewCamera;
        public Transform content, socket;
        public SpatialAdapterNativeText help;
        public bool Ready { get; private set; }
        public string Message { get; private set; }
        public int selected = -1;
        public int PoseUpdates { get; private set; }
        float nextLabels, nextWhistle, secondBlend;
        Material secondLens;
        string oldHelp;
        bool resetHeld, whistleHeld;
        readonly Dictionary<LocomotivePart, int> slots = new Dictionary<LocomotivePart, int>();
        readonly Dictionary<long, Pointer> pointers = new Dictionary<long, Pointer>();
        sealed class Pointer
        {
            public bool active = true, dragging, moveStage, reset;
            public AtelierControl control;
            public LocomotivePart part;
            public int piece = -1;
            public Vector3 start, objectStart;
            public float initialThrottle;
        }
        Pointer mouse;
        float mouseDepth;

        void Start()
        {
            engine.enabled = drive.enabled = doors.enabled = atelier.enabled = assembly.enabled = false;
            engine.externalView = true; engine.inputEnabled = false; assembly.desktopInput = false;
            drive.limit = 4.4f;
            for (int i = 0; i < assembly.pieces.Length; i++) foreach (var part in assembly.pieces[i].members) if (part) slots[part] = i;
            if (atelier.secondLens) secondLens = atelier.secondLens.material;
            CompleteAssembly(); Application.targetFrameRate = 90;
            foreach (var audio in engine.locomotive.GetComponentsInChildren<AudioSource>(true)) { audio.minDistance = .25f; audio.maxDistance = 6; }
            manipulation = new SwanTwoHandManipulation(transform);
            Ready = true; SetMessage("双手捏合：移动 · 旋转 · 缩放。单手捏住零件向外拉可拆下。");
            Debug.Log("SWAN_ATELIER_READY pieces=" + assembly.pieces.Length);
        }

        public void SetThrottle(float value)
        {
            atelier.throttle = Mathf.Clamp01(value); atelier.brake = 0;
            if (!assembly.Complete || atelier.direction == 0) { Brake(); SetMessage("需要装好机车并选择前进或后退方向。"); return; }
            assembly.requestedRpm = 6 + 54 * atelier.throttle;
            thermal.fireEnabled = atelier.throttle > .03f;
            if (atelier.throttle <= .03f) { drive.powered = false; if (!engine.travel) engine.running = false; return; }
            engine.direction = atelier.direction;
            if (atelier.travelling) { engine.travel = true; drive.requestedDirection = atelier.direction; drive.powered = true; }
            else { engine.travel = false; drive.powered = false; engine.rpm = assembly.requestedRpm; engine.running = true; }
        }
        public void Brake()
        {
            atelier.throttle = 0; atelier.brake = 1; drive.powered = false;
            if (!engine.travel) { engine.running = false; drive.signedRpm = 0; }
        }
        void StopImmediately() { Brake(); drive.signedRpm = 0; engine.travel = engine.running = false; }
        public void Activate(AtelierAction action, int index = 0)
        {
            Debug.Log("SWAN_ATELIER_ACTION " + action);
            switch (action)
            {
                case AtelierAction.Reverse:
                    Brake(); atelier.direction = atelier.direction == 1 ? 0 : atelier.direction == 0 ? -1 : 1;
                    if (atelier.direction != 0 && Mathf.Abs(drive.signedRpm) < .01f) engine.direction = atelier.direction;
                    SetMessage("方向：" + (atelier.direction == 1 ? "前进" : atelier.direction == 0 ? "空挡" : "后退") + "；重新拉动汽门加汽。"); break;
                case AtelierAction.Brake: Brake(); SetMessage("制动停车。"); break;
                case AtelierAction.Whistle: presentation.Whistle(); nextWhistle = Time.unscaledTime + .85f; break;
                case AtelierAction.Lights: presentation.ToggleHeadlight(); break;
                case AtelierAction.Cutaway:
                    engine.principle = !engine.principle; engine.ApplyPose(); SetMessage(engine.principle ? "剖面模式：查看锅炉、活塞与蒸汽通路。" : "完整外观。"); break;
                case AtelierAction.Observation:
                    if (Mathf.Abs(drive.signedRpm) > .1f) { Brake(); SetMessage("正在停车；停稳后再切换原地 / 行驶。"); break; }
                    StopImmediately(); atelier.travelling = !atelier.travelling;
                    SetMessage(atelier.travelling ? "轨道行驶：拉动汽门开始。" : "原地演示：拉动汽门观察连杆。"); break;
                case AtelierAction.ResetGuard:
                    atelier.guardOpen = !atelier.guardOpen; CancelReset(); SetMessage(atelier.guardOpen ? "保护盖已打开；持续捏住红按钮 1.2 秒重新装配。" : "保护盖已关闭。"); break;
                case AtelierAction.Assembly:
                    resetHeld = atelier.guardOpen; atelier.resetProgress = 0; break;
                case AtelierAction.Category:
                    atelier.trayCategory = index > 0 ? Mathf.Clamp(index - 1, 0, 3) : (atelier.trayCategory + 1) % 4;
                    atelier.trayPage = 0; RefreshTray(); break;
                case AtelierAction.TrayPrevious: atelier.trayPage = (atelier.trayPage + atelier.PageCount - 1) % atelier.PageCount; RefreshTray(); break;
                case AtelierAction.TrayNext: atelier.trayPage = (atelier.trayPage + 1) % atelier.PageCount; RefreshTray(); break;
                case AtelierAction.Home: SetMessage("看向展台，双手捏合可移动、旋转和缩放；单手也可拖动下方把手。"); break;
                case AtelierAction.Interior: doors.OpenAll(); SetMessage("驾驶室车门已打开，可走近观察内饰。"); break;
            }
            if (action != AtelierAction.Whistle) presentation.PlayPick();
            UpdateLabels();
        }
        public bool HoldReset(float dt)
        {
            if (!resetHeld || !atelier.guardOpen) return false;
            atelier.resetProgress += Mathf.Max(0, dt);
            if (atelier.resetProgress < 1.2f) return false;
            CancelReset(); atelier.guardOpen = false; StopImmediately(); engine.principle = false; engine.travelled = 0; engine.phase = 25;
            thermal.fireEnabled = false; doors.CloseAll(); ClearLoose();
            for (int i = 0; i < assembly.pieces.Length; i++) { assembly.pieces[i].installed = false; assembly.slotOccupants[i] = assembly.pieceSlots[i] = -1; }
            atelier.direction = 1; atelier.trayCategory = atelier.trayPage = 0;
            engine.ApplyPose(); RefreshTray(); SetMessage("开始重新装配：从零件盘捏取零件，移到金色接口后松手。"); return true;
        }
        public void CancelReset() { resetHeld = false; atelier.resetProgress = 0; }
        void ClearLoose()
        {
            foreach (var go in assembly.looseObjects) if (go) Destroy(go);
            Array.Clear(assembly.looseObjects, 0, assembly.looseObjects.Length); socket.gameObject.SetActive(false);
            foreach (var p in pointers.Values) if (p != null) p.active = false;
        }
        public void CompleteAssembly()
        {
            StopImmediately(); ClearLoose(); CancelReset(); doors.CloseAll();
            engine.phase = 25; engine.travelled = 0; engine.principle = engine.exploded = engine.assemblyLesson = false;
            for (int i = 0; i < assembly.pieces.Length; i++) { assembly.pieces[i].installed = true; assembly.slotOccupants[i] = assembly.pieceSlots[i] = i; }
            atelier.direction = 1; atelier.travelling = false; atelier.guardOpen = false; thermal.fireEnabled = false;
            engine.ApplyPose(); RefreshTray(); SetMessage("45 个部件已装好，可拉动汽门开始演示。");
        }
        public GameObject TakePiece(int index)
        {
            if (index < 0 || index >= assembly.pieces.Length) return null;
            StopImmediately(); thermal.fireEnabled = false; engine.principle = false;
            selected = index; assembly.selected = index; var piece = assembly.pieces[index];
            int slot = assembly.pieceSlots[index];
            if (piece.installed)
            {
                doors.CloseGroup(piece.key); assembly.slotOccupants[slot] = -1; assembly.pieceSlots[index] = -1; piece.installed = false;
            }
            engine.ApplyPose();
            if (!assembly.looseObjects[index])
            {
                var go = Instantiate(piece.prefab, engine.display);
                go.name = "Swan loose " + piece.key;
                go.transform.SetPositionAndRotation(assembly.SlotPosition(slot >= 0 ? slot : index), engine.locomotive.rotation);
                go.transform.localScale = Vector3.one;
                var tag = go.AddComponent<SwanAtelierLoosePiece>(); tag.index = index;
                assembly.looseObjects[index] = go;
            }
            socket.gameObject.SetActive(true); socket.position = assembly.SlotPosition(slot >= 0 ? slot : index);
            RefreshTray(); return assembly.looseObjects[index];
        }
        int NearestSlot(int piece, Vector3 position)
        {
            int best = -1; float distance = float.MaxValue;
            for (int i = 0; i < assembly.pieces.Length; i++) if (assembly.slotOccupants[i] < 0 && assembly.pieces[piece].fitKey == assembly.pieces[i].fitKey)
            { float d = Vector3.SqrMagnitude(position - assembly.SlotPosition(i)); if (d < distance) { distance = d; best = i; } }
            return best;
        }
        public bool ReleasePiece(int index, bool cancel = false)
        {
            var go = assembly.looseObjects[index]; if (!go) return false;
            int slot = NearestSlot(index, go.transform.position);
            float tolerance = Mathf.Clamp(content.lossyScale.x * .6f, .045f, .085f);
            bool fits = !cancel && slot >= 0 && Vector3.Distance(go.transform.position, assembly.SlotPosition(slot)) <= tolerance;
            if (fits)
            {
                assembly.slotOccupants[slot] = index; assembly.pieceSlots[index] = slot; assembly.pieces[index].installed = true;
                Destroy(go); assembly.looseObjects[index] = null; engine.ApplyPose(); presentation.PlaySnap(); SetMessage("接口对齐，安装完成。");
            }
            else SetMessage("零件保留在当前位置，可再次捏住移动到对应接口。");
            socket.gameObject.SetActive(false); RefreshTray(); return fits;
        }
        void RefreshTray() { atelier.RefreshTray(); }
        void SetMessage(string value) { Message = value; }
        public void TickSimulation(float dt)
        {
            bool moving = engine.running;
            drive.Tick(dt); engine.Advance(dt);
            if (moving || engine.running) { engine.ApplyPose(); PoseUpdates++; }
            if (moving || engine.running || Mathf.Abs(doors.angles[0] - (doors.open[0] ? 60 : 0)) > .001f ||
                Mathf.Abs(doors.angles[1] - (doors.open[1] ? 60 : 0)) > .001f || Mathf.Abs(doors.fireAngle - (thermal.fireDoorOpen ? 78 : 0)) > .001f) doors.Tick(dt);
        }
        void Update()
        {
            if (!Ready) return;
            TickSimulation(Time.deltaTime);
            PollSpatialInput();
#if UNITY_EDITOR || UNITY_STANDALONE
            DesktopInput();
#endif
            foreach (var p in pointers.Values.ToArray()) Hold(p);
            Hold(mouse);
            AnimateControls(Time.deltaTime);
            if (Time.unscaledTime >= nextLabels) { nextLabels = Time.unscaledTime + .15f; UpdateLabels(); }
        }
        void Hold(Pointer p)
        {
            if (p == null || !p.active || !p.control) return;
            if (p.control.action == AtelierAction.Assembly) HoldReset(Time.unscaledDeltaTime);
            if (p.control.action == AtelierAction.Whistle && Time.unscaledTime >= nextWhistle) Activate(AtelierAction.Whistle);
        }
        void LateUpdate()
        {
            if (!Ready || !thermal.smoke) return;
            var em = thermal.smoke.emission; em.rateOverTime = thermal.fireEnabled && assembly.Complete ? 12 : 0;
            var velocity = thermal.smoke.velocityOverLifetime; velocity.enabled = false;
        }
        static void Rotate(Transform t, Quaternion q) { if (t && t.localRotation != q) t.localRotation = q; }
        void AnimateControls(float dt)
        {
            Rotate(atelier.throttlePivot, Quaternion.Euler(6 - 52 * atelier.throttle, 0, 0));
            Rotate(atelier.brakePivot, Quaternion.Euler(-28 + 56 * atelier.brake, 0, 0));
            Rotate(atelier.reversePivot, Quaternion.Euler(-atelier.direction * 25, 0, 0));
            if (atelier.resetGuard) Rotate(atelier.resetGuard, Quaternion.RotateTowards(atelier.resetGuard.localRotation, Quaternion.Euler(atelier.guardOpen ? -110 : 0, 0, 0), dt * 260));
            Rotate(atelier.lightToggle, Quaternion.Euler(presentation.lightOn ? -22 : 22, 0, 0));
            Rotate(atelier.sectionToggle, Quaternion.Euler(engine.principle ? -22 : 22, 0, 0));
            Rotate(atelier.modeToggle, Quaternion.Euler(0, atelier.travelling ? -40 : 40, 0));
            Rotate(atelier.categoryPivot, Quaternion.Euler(0, atelier.trayCategory * 90, 0));
            if (atelier.whistleCap) atelier.whistleCap.localPosition = new Vector3(0, whistleHeld ? -.035f : 0, 0);
            atelier.pressure = Mathf.MoveTowards(atelier.pressure, assembly.Complete && thermal.fireEnabled ? 9.2f + (engine.running ? .65f : 0) : 0, dt * 1.8f);
            Rotate(atelier.pressureNeedle, Quaternion.Euler(0, 0, -130 + atelier.pressure * 26));
            Rotate(atelier.speedNeedle, Quaternion.Euler(0, 0, -130 + drive.SpeedKmh * 13));
            secondBlend = Mathf.MoveTowards(secondBlend, presentation.lightOn && assembly.HasPiece("lheadlamp") ? 1 : 0, dt * 2);
            if (atelier.secondLamp) { atelier.secondLamp.enabled = secondBlend > .01f; atelier.secondLamp.intensity = 5 * secondBlend * secondBlend; }
            if (secondLens) secondLens.SetColor("_EmissionColor", new Color(1, .73f, .34f) * 3.5f * secondBlend);
        }
        void UpdateLabels()
        {
            if (atelier.status) atelier.status.text = !assembly.Complete ? "装配中" : engine.running ? (atelier.travelling ? "轨道行驶" : "原地演示") : "整备就绪";
            if (atelier.speedText) atelier.speedText.text = drive.SpeedKmh.ToString("00.0") + " km/h";
            if (atelier.pressureText) atelier.pressureText.text = atelier.pressure.ToString("0.0") + " bar";
            string value = "蒸汽工坊·新版  |  " + assembly.InstalledCount + " / " + assembly.pieces.Length + "\n" +
                (resetHeld ? "保持捏合：" + Mathf.Clamp01(atelier.resetProgress / 1.2f).ToString("P0") : Message);
            if (help && value != oldHelp) { help.Text = value; oldHelp = value; }
        }
        Pointer Begin(GameObject hit, Vector3 position)
        {
            if (!hit) return null;
            var utility = hit.GetComponentInParent<SwanAtelierTarget>();
            if (utility) { if (!utility.moveStage) { CompleteAssembly(); return null; } return new Pointer { moveStage = true, start = position, objectStart = transform.position }; }
            var control = hit.GetComponentInParent<AtelierControl>();
            if (control)
            {
                control.PressFeedback(); var p = new Pointer { control = control, start = position, initialThrottle = atelier.throttle };
                if (control.action == AtelierAction.TrayPart)
                {
                    int i = control.index; if (i < 0 || i >= assembly.pieces.Length || assembly.pieces[i].installed || IsDragged(i)) return null;
                    p.piece = i; var go = TakePiece(i); go.transform.position = position; p.objectStart = position; p.dragging = true;
                }
                else if (control.action != AtelierAction.Throttle) Activate(control.action, control.index);
                if (control.action == AtelierAction.Whistle) whistleHeld = true;
                return p;
            }
            var loose = hit.GetComponentInParent<SwanAtelierLoosePiece>();
            if (loose) { if (IsDragged(loose.index)) return null; return new Pointer { piece = loose.index, dragging = true, start = position, objectStart = loose.transform.position }; }
            var part = hit.GetComponentInParent<LocomotivePart>();
            if (part && slots.TryGetValue(part, out int slot) && assembly.slotOccupants[slot] >= 0)
            {
                int piece = assembly.slotOccupants[slot]; if (IsDragged(piece)) return null;
                selected = piece; SetMessage(assembly.pieces[piece].title + "：点击车门可开关，向外拖动可拆下。");
                return new Pointer { piece = piece, part = part, start = position, objectStart = assembly.SlotPosition(slot) };
            }
            return null;
        }
        bool IsDragged(int piece) => pointers.Values.Any(p => p != null && p.active && p.piece == piece);
        void Move(Pointer p, Vector3 position)
        {
            if (p == null || !p.active) return;
            Vector3 delta = Vector3.ClampMagnitude(position - p.start, 2);
            if (p.moveStage) { transform.position = p.objectStart + delta; return; }
            if (p.control && p.control.action == AtelierAction.Throttle) { SetThrottle(p.initialThrottle + Vector3.Dot(delta, transform.up) / .16f); return; }
            if (p.piece < 0) return;
            if (!p.dragging && delta.magnitude > .025f) { TakePiece(p.piece); p.dragging = true; }
            if (!p.dragging || !assembly.looseObjects[p.piece]) return;
            var go = assembly.looseObjects[p.piece]; go.transform.position = p.objectStart + delta;
            int slot = NearestSlot(p.piece, go.transform.position);
            socket.gameObject.SetActive(slot >= 0); if (slot >= 0) socket.position = assembly.SlotPosition(slot);
        }
        void End(Pointer p, bool cancel)
        {
            if (p == null || !p.active) return; p.active = false;
            if (p.piece >= 0) { if (p.dragging) ReleasePiece(p.piece, cancel); else if (!cancel && p.part) doors.Toggle(p.part); }
            if (p.control && p.control.action == AtelierAction.Assembly) CancelReset();
            if (p.control && p.control.action == AtelierAction.Whistle) whistleHeld = false;
        }
#if UNITY_EDITOR || UNITY_STANDALONE
        void DesktopInput()
        {
            var input = Mouse.current; if (input == null || !previewCamera) return;
            var ray = previewCamera.ScreenPointToRay(input.position.ReadValue());
            if (input.leftButton.wasPressedThisFrame && Physics.Raycast(ray, out var hit, 20, ~(1 << 2)))
            { mouseDepth = Vector3.Dot(hit.point - previewCamera.transform.position, previewCamera.transform.forward); mouse = Begin(hit.collider.gameObject, hit.point); }
            if (input.leftButton.isPressed && mouse != null)
            { var plane = new Plane(-previewCamera.transform.forward, previewCamera.transform.position + previewCamera.transform.forward * mouseDepth); if (plane.Raycast(ray, out float distance)) Move(mouse, ray.GetPoint(distance)); }
            if (input.leftButton.wasReleasedThisFrame) { End(mouse, false); mouse = null; }
        }
#endif
        void OnApplicationPause(bool paused) { inputPaused = paused; if (paused && Ready) { StopImmediately(); ResetSpatialGestures(); } }
        void OnDestroy() { if (secondLens) Destroy(secondLens); }
    }
}
