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

        public void Follow(Transform anchor)
        {
            if (!anchor) return;
            if (transform.position != anchor.position || transform.rotation != anchor.rotation)
                transform.SetPositionAndRotation(anchor.position, anchor.rotation);
            var scale = anchor.lossyScale / bakedGeometryScale;
            if(transform.parent)
            {
                var parentScale=transform.parent.lossyScale;
                scale=new Vector3(scale.x/parentScale.x,scale.y/parentScale.y,scale.z/parentScale.z);
            }
            if (transform.localScale != scale) transform.localScale = scale;
        }
    }
}
