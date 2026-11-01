using UnityEngine;
using SwanAtelier;

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
            showing = true; surface.SetActive(!Application.isPlaying || Application.isEditor);
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
        void Awake()
        {
            Current = this;
            // Keep the authored TMP hierarchy active for mesh export, then hide
            // the placeholder before the first player Update until pose is valid.
            if (!Application.isEditor && surface) surface.SetActive(false);
        }
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
            if (!SwanHeadPose.TryRead(out Pose pose))
            {
                // Never expose the serialized floor-height placeholder on device.
                if (Application.isPlaying && !Application.isEditor) surface.SetActive(false);
                return;
            }
            ApplyHeadPose(pose);
        }
        public void ApplyHeadPose(Pose pose)
        {
            if (!SwanHeadPose.IsValid(pose)) return;
            transform.SetPositionAndRotation(pose.position + pose.rotation * new Vector3(0, 0, 1.1f), pose.rotation);
            if (showing && !surface.activeSelf) surface.SetActive(true);
        }
    }
}
