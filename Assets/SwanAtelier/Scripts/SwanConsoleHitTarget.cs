using UnityEngine;

namespace SwanAtelier
{
    public sealed class SwanConsoleHitTarget:MonoBehaviour
    {
        public BoxCollider hitBox;
        public Vector3 authoredSize;
        Vector3 lastScale;
        void OnEnable(){RefreshSize();}
        void Update(){if(transform.lossyScale!=lastScale)RefreshSize();}
        public void RefreshSize()
        {
            if(!hitBox)return;
            lastScale=transform.lossyScale;
            // Keep tiny switches targetable at the 26% scene scale. The collider
            // remains on the visible highlighted mesh, not an empty ancestor.
            hitBox.size=Vector3.Max(authoredSize,new Vector3(
                .014f/Mathf.Max(.000001f,Mathf.Abs(lastScale.x)),
                .014f/Mathf.Max(.000001f,Mathf.Abs(lastScale.y)),
                .014f/Mathf.Max(.000001f,Mathf.Abs(lastScale.z))));
        }
    }
}
