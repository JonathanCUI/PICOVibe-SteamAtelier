using UnityEngine;

// One binding per independently moving/static material batch; meshes are shared assets.
public sealed class LocomotiveXRMesh : MonoBehaviour {
 public Mesh[] levels;
 public bool track;
 [System.NonSerialized] public MeshFilter filter;
 [System.NonSerialized] public Renderer visual;
 [System.NonSerialized] public int currentLevel=-1;
 void Awake(){filter=GetComponent<MeshFilter>();visual=GetComponent<Renderer>();}
 void OnEnable(){if(!filter)Awake();LocomotiveXRQuality.Register(this);}
 void OnDisable(){LocomotiveXRQuality.Unregister(this);}
 public void Select(int level){level=Mathf.Clamp(level,0,levels.Length-1);if(currentLevel==level&&filter.sharedMesh==levels[level])return;filter.sharedMesh=levels[level];currentLevel=level;}
}
