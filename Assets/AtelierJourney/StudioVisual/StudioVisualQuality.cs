using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Rendering-only settings; assembly, thermal state and kinematics remain owned by SteamJourney.
[DefaultExecutionOrder(-1000)]
public sealed class StudioVisualQuality : MonoBehaviour
{
    Material captionMaterial;
    public UniversalRenderPipelineAsset desktopPipeline, androidPipeline;
    public Volume grading;
    public VolumeProfile desktopGrade, androidGrade;
    public Camera mainCamera;
    public bool forceMobilePreview;
    void Awake() {
        bool mobile = Application.platform == RuntimePlatform.Android || forceMobilePreview;
        ApplyQuality(mobile);
    }
    public void ApplyQuality(bool mobile) {
        var pipeline = mobile ? androidPipeline : desktopPipeline;
        if (pipeline) { GraphicsSettings.defaultRenderPipeline = pipeline; QualitySettings.renderPipeline = pipeline; }
        if (grading) grading.sharedProfile = mobile ? androidGrade : desktopGrade;
        if (mainCamera) {
            mainCamera.allowHDR = true;
            var data = mainCamera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.None; // Pipeline MSAA; avoids softening mechanical markings.
        }
    }
    System.Collections.IEnumerator Start() {
        yield return null;
        var root=GameObject.Find("Journey · State and contextual controls");if(!root)yield break;
        foreach(var text in root.GetComponentsInChildren<TMPro.TextMeshProUGUI>(true)) {
            if(text.transform.parent!=root.transform || text.rectTransform.anchorMax.y>.16f)continue;
            if(!captionMaterial) {
                captionMaterial=new Material(text.fontSharedMaterial);
                captionMaterial.EnableKeyword("OUTLINE_ON");captionMaterial.SetColor("_OutlineColor",new Color(.025f,.035f,.04f,1));captionMaterial.SetFloat("_OutlineWidth",.15f);
            }
            text.fontSharedMaterial=captionMaterial;
        }
    }
    void OnDestroy(){if(captionMaterial)Destroy(captionMaterial);}
}
