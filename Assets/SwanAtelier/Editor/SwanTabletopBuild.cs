using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SwanAtelier.Editor
{
    public static partial class SwanAtelierBuild
    {
        static void ConfigureTabletop(SwanAtelierController controller, Transform atelierRoot)
        {
            var rails=atelierRoot.Find("Desktop rail installation");
            var console=atelierRoot.Cast<Transform>().Single(t=>t.name.Contains("Precision mechanical console"));
            var tray=atelierRoot.Cast<Transform>().Single(t=>t.name.Contains("Low parts instrument tray"));
            if(!rails)throw new InvalidOperationException("Authored rail installation missing");
            var railBounds=SupportBounds(rails,controller.transform);
            foreach(var item in new[]{console,tray}) {
                var before=SupportBounds(item,controller.transform);
                item.position+=Vector3.up*(railBounds.min.y-before.min.y);
            }
            var placement=controller.gameObject.AddComponent<SwanTabletopPlacement>();
            placement.supportSamplesPerFootprint=15;
            placement.baseHeight=railBounds.min.y;
            placement.contactCentre=new Vector3(railBounds.center.x,railBounds.min.y,railBounds.center.z);
            var points=new List<Vector3>();
            foreach(var item in new[]{rails,console,tray}) {
                var bounds=SupportBounds(item,controller.transform);
                // Cover each real supporting footprint, not the empty gap between
                // console and rails. Interior samples also reject holes/gaps.
                for(int x=0;x<=4;x++) for(int z=0;z<=2;z++)
                    points.Add(new Vector3(Mathf.Lerp(bounds.min.x,bounds.max.x,x/4f),placement.baseHeight,Mathf.Lerp(bounds.min.z,bounds.max.z,z/2f)));
                Debug.Log("SWAN_TABLE_FOOTPRINT "+item.name+" min="+bounds.min.ToString("F3")+" max="+bounds.max.ToString("F3"));
            }
            placement.supportPoints=points.ToArray();controller.tabletop=placement;
        }
        static Bounds SupportBounds(Transform root,Transform relativeTo)
        {
            bool initialized=false;Bounds bounds=default;
            foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>(false)) {
                if(!renderer.enabled||renderer.GetComponent<TMPro.TMP_Text>())continue;
                var filter=renderer.GetComponent<MeshFilter>();if(!filter||!filter.sharedMesh)continue;
                var local=filter.sharedMesh.bounds;
                for(int i=0;i<8;i++) {
                    var corner=local.center+Vector3.Scale(local.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));
                    var p=relativeTo.InverseTransformPoint(renderer.transform.TransformPoint(corner));
                    if(!initialized){bounds=new Bounds(p,Vector3.zero);initialized=true;}else bounds.Encapsulate(p);
                }
            }
            if(!initialized)throw new InvalidOperationException("No support geometry for "+root.name);
            return bounds;
        }
    }
}
