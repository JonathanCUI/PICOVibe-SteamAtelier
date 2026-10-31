using UnityEngine;
using UnityEngine.XR;

namespace SwanJourney
{
    // Exported meshes and fixed labels: progress never uploads a changing text
    // mesh. The resource bar represents Unity's resource phase only.
    public sealed class SwanJourneyLoadingView : MonoBehaviour
    {
        public static SwanJourneyLoadingView Current { get; private set; }
        public GameObject surface, workshopTitle, canyonTitle, resourceLabel, nativeLabel, failedLabel;
        public Transform fill;
        public float width = .48f;
        bool showing;
        public void Show(bool canyon)
        {
            showing = true; surface.SetActive(true);
            workshopTitle.SetActive(!canyon); canyonTitle.SetActive(canyon);
            SetResourceProgress(0); Place();
        }
        public void SetResourceProgress(float fraction)
        {
            resourceLabel.SetActive(true); nativeLabel.SetActive(false); failedLabel.SetActive(false);
            SetBar(Mathf.Clamp01(fraction) * .5f);
        }
        public void PreparingSpace()
        {
            resourceLabel.SetActive(false); nativeLabel.SetActive(true); failedLabel.SetActive(false);
            // The adapter has no documented native load-percentage callback.
            // A partial, phase-labelled bar must not claim a fabricated 100%.
            SetBar(.5f);
        }
        public void Fail()
        {
            showing = true; surface.SetActive(true);
            resourceLabel.SetActive(false); nativeLabel.SetActive(false); failedLabel.SetActive(true); SetBar(0);
        }
        public void Hide() { showing = false; surface.SetActive(false); }
        void Awake() { Current = this; }
        void OnDestroy() { if (Current == this) Current = null; }
        void Update() { if (showing) Place(); }
        void SetBar(float value)
        {
            float size = Mathf.Max(.001f, width * value);
            fill.localScale = new Vector3(size, .009f, .003f);
            fill.localPosition = new Vector3((size - width) * .5f, -.045f, -.011f);
        }
        void Place()
        {
            var device = InputDevices.GetDeviceAtXRNode(XRNode.Head);
            if (!device.isValid || !device.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 p)
                || !device.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion q)) return;
            transform.SetPositionAndRotation(p + q * new Vector3(0, -.08f, 1.1f), q);
        }
    }
}
