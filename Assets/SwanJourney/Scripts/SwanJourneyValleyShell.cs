using UnityEngine;

namespace SwanJourney
{
    public sealed class SwanJourneyValleyShell : MonoBehaviour
    {
        [System.Serializable] public struct Part { public MeshRenderer original, exterior; }
        public Part[] parts = System.Array.Empty<Part>();
        bool[] wasEnabled;
        bool driving;

        // Run after the ordinary visibility logic. Renderer visibility and authored
        // child meshes are both exported by the native adapter.
        public void ApplyDriving()
        {
            if (!driving) wasEnabled = new bool[parts.Length];
            for (int i=0;i<parts.Length;i++)
            {
                var p=parts[i];
                if (!driving) wasEnabled[i]=p.original.enabled;
                p.original.enabled=false;
                if (p.exterior) p.exterior.gameObject.SetActive(wasEnabled[i]);
            }
            driving=true;
        }
        public void Restore()
        {
            if (!driving) return;
            for(int i=0;i<parts.Length;i++)
            {
                var p=parts[i];
                if(p.exterior)p.exterior.gameObject.SetActive(false);
                p.original.enabled=wasEnabled[i];
            }
            driving=false;
        }
    }
}
