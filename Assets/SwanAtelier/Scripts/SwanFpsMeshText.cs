using UnityEngine;

namespace SwanAtelier
{
    // Static glyph meshes use the same transform/visibility path as the locomotive.
    // Only the active digit variants change, at the counter's 2 Hz sample rate.
    public sealed class SwanFpsMeshText : MonoBehaviour
    {
        public GameObject[] variants = new GameObject[33];
        public int ShownValue { get; private set; } = -1;

        public void Show(double? fps)
        {
            ShownValue = fps.HasValue ? Mathf.Clamp(Mathf.RoundToInt((float)fps.Value), 0, 999) : -1;
            for (int slot = 0; slot < 3; slot++) {
                int selected = ShownValue < 0 ? (slot == 0 ? -1 : 10) :
                    slot == 0 ? (ShownValue >= 100 ? ShownValue / 100 : -1) :
                    slot == 1 ? (ShownValue >= 10 ? ShownValue / 10 % 10 : -1) : ShownValue % 10;
                for (int digit = 0; digit < 11; digit++) {
                    var item = variants[slot * 11 + digit];
                    if (item && item.activeSelf != (digit == selected)) item.SetActive(digit == selected);
                }
            }
        }
    }
}
