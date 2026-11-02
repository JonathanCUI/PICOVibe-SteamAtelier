using System;
using UnityEngine;

namespace SwanJourney
{
    public sealed class SwanJourneyValveViews : MonoBehaviour
    {
        public enum View { Fixed, Shell, Inspection }
        [Serializable] public struct Surface
        {
            public MeshRenderer renderer;
            public View view;
            // When hidden, a second awake surface of the same rigid part
            // covers this entire hit box in the indicated mode.
            public bool sleepInSolid, sleepInXray, sleepInSection;
        }
        public Surface[] surfaces = Array.Empty<Surface>();
        public void Apply(bool section, bool xray)
        {
            foreach (var surface in surfaces)
            {
                bool visible = surface.view == View.Fixed ||
                    (surface.view == View.Shell ? !section : xray);
                var renderer = surface.renderer;
                bool active = visible || !(xray ? surface.sleepInXray : section ? surface.sleepInSection : surface.sleepInSolid);
                if (renderer.enabled != visible) renderer.enabled = visible;
                if (renderer.gameObject.activeSelf != active) renderer.gameObject.SetActive(active);
            }
        }
    }
}
