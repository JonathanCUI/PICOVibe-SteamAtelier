using UnityEngine;

namespace SwanJourney
{
    // Loaded on demand, then adopted by the live workbench. The loader explicitly
    // owns/destroys this payload before unloading its now-empty source scene.
    public sealed class SwanJourneyEnvironment : MonoBehaviour
    {
        public Transform[] supports = new Transform[0];
        public float bakedGeometryScale = 1f;
        public Vector3 EffectiveScale => transform.lossyScale * bakedGeometryScale;

        public void Follow(Transform anchor, Transform scalePivot = null, float scaleRatio = 1f)
        {
            if (!anchor) return;
            var position = scalePivot ? scalePivot.position + (anchor.position - scalePivot.position) * scaleRatio : anchor.position;
            if (transform.position != position || transform.rotation != anchor.rotation)
                transform.SetPositionAndRotation(position, anchor.rotation);
            var scale = anchor.lossyScale * (scaleRatio / bakedGeometryScale);
            if(transform.parent)
            {
                var parentScale=transform.parent.lossyScale;
                scale=new Vector3(scale.x/parentScale.x,scale.y/parentScale.y,scale.z/parentScale.z);
            }
            if (transform.localScale != scale) transform.localScale = scale;
        }
    }
}
