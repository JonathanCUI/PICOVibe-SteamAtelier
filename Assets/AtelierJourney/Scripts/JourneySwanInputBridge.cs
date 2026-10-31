using System;
using UnityEngine;

// Device input delegates to the Journey state machine. Installation, process
// invalidation, completion effects and persistence stay owned by SteamJourney.
public sealed partial class SteamJourney
{
    public bool SwanInputActive;
    [NonSerialized] public Func<bool, bool> SwanModeRequest;
    [NonSerialized] public bool SwanSceneLoading;
    public bool SwanTransitionActive => GetComponent<JourneySceneArrival>()?.Active ?? false;
    public bool SwanInputBlocked => verificationInputLock || SwanSceneLoading || SwanTransitionActive || AutoAssembling || CompletionActive;
    public JourneyModule SwanHeldModule => heldModule;

    public JourneyModule SwanModule(int index)
    {
        if (index < 0 || modules == null || finishing == null) return null;
        return index < modules.Length ? modules[index] : index - modules.Length < finishing.Length ? finishing[index - modules.Length] : null;
    }

    public bool SwanCanInstall(JourneyModule module)
    {
        if (module == null || module.installed || inSandbox || SwanInputBlocked) return false;
        return module == Current || !Teaching && Array.IndexOf(finishing, module) >= 0;
    }

    public bool SwanBeginGrab(JourneyModule module)
    {
        if (SwanInputBlocked || inSandbox || module == null || module.installed || !module.preview ||
            !module.preview.activeInHierarchy || held && heldModule != module) return false;
        return BeginGrabModule(module);
    }

    public bool SwanDetach(JourneyModule module)
    {
        if (SwanInputBlocked || held) return false;
        return DetachModule(module);
    }

    public bool SwanEndGrab(bool cancel = false)
    {
        if (!held) return false;
        if (!cancel && !SwanInputBlocked) return EndGrab();
        // A canceled/paused gesture only releases ownership. It must never
        // silently install the part or erase its current spatial placement.
        if (heldModule != null) heldModule.parked = true;
        held = null; heldModule = null; snapReady = false;
        pendingDetach = null; rotatingPart = null;
        RefreshPreview();
        return false;
    }

    public bool SwanAct(JourneyAction action)
    {
        if (SwanSceneLoading) return false;
        if (verificationInputLock || SwanTransitionActive && action != JourneyAction.Reset) return false;
        if (AutoAssembling && action != JourneyAction.Reset && action != JourneyAction.Verify && action != JourneyAction.Pause) return false;
        bool lessonToggle = action == JourneyAction.Pause || action == JourneyAction.Slow;
        bool toggle = lessonToggle || action == JourneyAction.Lights;
        // These are the source lesson's process controls; Drive(dt) does not
        // consume pause/slow, and headlamps only illuminate the sandbox.
        if (toggle && (SwanInputBlocked || lessonToggle && inSandbox || action == JourneyAction.Lights && !inSandbox)) return false;
        // View is already a no-op in the V23 source. The native user's view
        // follows their head; do not report a successful camera change.
        if (action == JourneyAction.View) return false;
        Act(action);
        if (toggle)
        {
            if (action == JourneyAction.Pause) Notify(paused ? "原理演示已暂停" : "原理演示已继续");
            else if (action == JourneyAction.Slow) Notify(slow ? "原理演示 · 四分之一速度" : "原理演示 · 正常速度");
            else { ApplyVisibility(); Notify(lamps ? "机车前照灯已开启" : "机车前照灯已关闭"); }
            Debug.Log("SWAN_JOURNEY_TOGGLE action=" + action + " value=" +
                (action == JourneyAction.Pause ? paused : action == JourneyAction.Slow ? slow : lamps));
        }
        return true;
    }

    public bool SwanSetControl(JourneyAction action, float value)
    {
        if (SwanInputBlocked || float.IsNaN(value) || float.IsInfinity(value)) return false;
        if (action == JourneyAction.Throttle && inSandbox) { throttle = Mathf.Clamp01(value); return true; }
        if (action == JourneyAction.Brake) { brake = Mathf.Clamp01(value); return true; }
        if (action == JourneyAction.Reverse && inSandbox)
        {
            int requested = value < 0 ? -1 : 1;
            if (requested != direction) Act(JourneyAction.Reverse); // Retains the stopped-train check.
            return direction == requested;
        }
        return false;
    }

    public void SwanNotify(string message) { Notify(message); }
}
