using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SwanJourney.Editor
{
    public static class SwanJourneyEmptyNodes
    {
        public static void Prune(Transform root)
        {
            var protectedNodes=new HashSet<Transform>();
            foreach(var c in root.GetComponentsInChildren<Component>(true))
            {
                if(!c || c is Transform)continue;
                using(var so=new SerializedObject(c))
                {
                    var p=so.GetIterator();while(p.Next(true))
                    {
                        if(p.propertyType!=SerializedPropertyType.ObjectReference||p.propertyPath=="m_GameObject"||p.propertyPath=="m_Script")continue;
                        var target=p.objectReferenceValue is GameObject go?go.transform:p.objectReferenceValue is Component component?component.transform:null;
                        if(target)protectedNodes.Add(target);
                    }
                }
            }
            int removed=0;
            // This data-only provenance marker is consumed exclusively by the
            // partner's editor builder. Source scene retains the authoring data.
            foreach(var marker in root.GetComponentsInChildren<FlatTraySourceV22>(true))
                if(!protectedNodes.Contains(marker.transform))UnityEngine.Object.DestroyImmediate(marker);
            // Reverse DFS removes only empty, component-free, unreferenced leaves.
            foreach(var t in root.GetComponentsInChildren<Transform>(true).Reverse())
                if(t && t!=root && t.childCount==0 && !protectedNodes.Contains(t) && t.GetComponents<Component>().Length==1)
                {UnityEngine.Object.DestroyImmediate(t.gameObject);removed++;}
            Debug.Log("SWAN_JOURNEY_EMPTY_NODES root="+root.name+" removed="+removed+" remaining="+root.GetComponentsInChildren<Transform>(true).Length);
        }
    }
}
