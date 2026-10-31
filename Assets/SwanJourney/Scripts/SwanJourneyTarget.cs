using UnityEngine;

namespace SwanJourney
{
    public enum SwanJourneyTargetKind { Module, Control, Sun, Moon, MoveWorkbench, ExplodeAll }

    // Put a target on a visible mesh (with its native hover effect and collider),
    // or on its semantic parent. The bridge can also infer existing Journey data.
    public sealed class SwanJourneyTarget : MonoBehaviour
    {
        public SwanJourneyTargetKind kind;
        public int moduleIndex = -1;
        public JourneyControl control;
        public JourneyAction action;
        public bool useActionOverride;
    }
}
