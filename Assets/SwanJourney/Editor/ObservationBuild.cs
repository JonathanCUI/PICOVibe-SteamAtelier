using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace SwanJourney.Editor
{
    internal static class ObservationBuild
    {
        internal const string ArrowArt="Assets/SwanJourney/Branding/FlowDirection.asset";
        internal static void ConfigureArrows(SteamJourney j)
        {
            var flow=j.GetComponent<SwanJourneyFlowParticles>();
            var all=AssetDatabase.LoadAllAssetsAtPath(ArrowArt);
            var arrow=all.OfType<Mesh>().Single();
            var materials=Enum.GetNames(typeof(JourneyFluidKind)).Select(n=>all.OfType<Material>().Single(m=>m.name=="Flow "+n)).ToArray();
            const int per=SwanJourneyFlowParticles.ArrowsPerKind;
            foreach(var spare in flow.markers.Skip(per*materials.Length)) Object.DestroyImmediate(spare.gameObject);
            flow.markers=flow.markers.Take(per*materials.Length).ToArray();
            for(int i=0;i<flow.markers.Length;i++)
            {
                var r=flow.markers[i];
                r.GetComponent<MeshFilter>().sharedMesh=arrow;r.sharedMaterial=materials[i/per];
                r.enabled=true;r.gameObject.SetActive(false);
            }
            j.directionalFlow=true;flow.arrowMaterials=materials;
            EditorUtility.SetDirty(j);EditorUtility.SetDirty(flow);
        }

        internal static void PairHeadlamps(SteamJourney j)
        {
            if(j.finishing.Length==10 && j.finishing[9].title=="左右前照灯")return;
            if(j.finishing.Length!=11)throw new InvalidOperationException("Unexpected finishing groups");
            var r=j.finishing[9];var l=j.finishing[10];
            if(r.title!="右前照灯"||l.title!="左前照灯")throw new InvalidOperationException("Headlamps missing");
            var center=(r.previewCentre+l.previewCentre)*.5f;
            var group=new GameObject("左右前照灯 · 配对安装预览").transform;
            group.SetParent(r.preview.transform.parent,false);
            group.localPosition=r.preview.transform.localPosition;group.localRotation=r.preview.transform.localRotation;group.localScale=r.preview.transform.localScale;
            foreach(var part in new[]{r,l})
            {
                var t=part.preview.transform;t.SetParent(group,false);t.localPosition=part.previewCentre-center;
                t.localRotation=Quaternion.identity;t.localScale=Vector3.one;t.gameObject.SetActive(true);
            }
            group.gameObject.SetActive(false);
            var outline=new GameObject("左右前照灯 · 安装位置").transform;
            int last=j.modules.Length+9;
            outline.SetParent(j.socketGhosts[last].transform.parent,false);
            foreach(var ghost in j.socketGhosts.Skip(last))
            {
                ghost.transform.SetParent(outline,true);ghost.SetActive(true);
            }
            outline.gameObject.SetActive(false);
            j.socketGhosts=j.socketGhosts.Take(last).Concat(new[]{outline.gameObject}).ToArray();
            foreach(var target in j.spatialRoot.GetComponentsInChildren<SwanJourneyTarget>(true))
                if(target.kind==SwanJourneyTargetKind.Module&&target.moduleIndex==last+1)target.moduleIndex=last;
            r.title="左右前照灯";r.explanation="两盏前照灯分别安装在前缓冲梁两侧。拿起灯组，对准车头两侧的灯座后松手。";
            r.observation="观察左右前照灯是否同时就位，并在驾驶时通过车灯开关控制。";
            r.objects=r.objects.Concat(l.objects).Distinct().ToArray();r.preview=group.gameObject;
            r.socket=(r.socket+l.socket)*.5f;r.previewCentre=center;r.installed=r.installed&&l.installed;
            j.finishing=j.finishing.Take(10).ToArray();j.finishIndex=Mathf.Min(j.finishIndex,9);
            EditorUtility.SetDirty(j);
        }
    }
}
