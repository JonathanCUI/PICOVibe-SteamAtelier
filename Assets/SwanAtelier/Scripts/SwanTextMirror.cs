using ByteDance.PICO.SpatialAdapter.Component;
using TMPro;
using UnityEngine;
namespace SwanAtelier
{
    public sealed class SwanTextMirror : MonoBehaviour
    {
        public TMP_Text source;
        public SpatialAdapterNativeText output;
        string previous;
        Color previousColor;
        void LateUpdate()
        {
            if (!source || !output) return;
            if (source.text != previous) { previous = source.text; output.Text = previous; }
            if (source.color != previousColor) { previousColor = source.color; output.TextColor = previousColor; }
        }
    }
}
