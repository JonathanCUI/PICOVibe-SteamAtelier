using System.Collections.Generic;
using System.Linq;
using SwanAtelier;
using UnityEngine;

namespace SwanJourney
{
    // Apply the mode's scale before JourneySceneArrival (2200) captures bounds.
    [DefaultExecutionOrder(2150)]
    public sealed class SwanJourneyPresentation : MonoBehaviour
    {
        public SteamJourney journey;
        public Transform content;
        public SwanTabletopPlacement tabletop;
        public float workshopScale=.12f,sandboxScale=.035f;
        public Transform[] workshopSupports,sandboxSupports;
        public Transform moveHandle;
        public Transform workshopMoveHandleAnchor, sandboxMoveHandleAnchor;
        // This is authored in stage metres and follows the stage's scale.
        // The indirect pinch target remains accessible at small exhibit sizes.
        public float moveHandleWorldGap = .025f;
        [System.NonSerialized] public System.Action SynchronizeEnvironmentPose;
        bool initialized,wasSandbox;
        Transform cachedMoveAnchor;
        Bounds cachedMoveAnchorBounds;
        Matrix4x4 cachedMoveAnchorToStage;
        Vector3 cachedMovePosition, cachedMoveScale;
        float cachedStageScale, cachedMinimumScale, cachedWorldGap;
        bool moveBoundsAttempted, moveBoundsValid, moveLayoutValid;
        public int MoveHandleBoundsScans { get; private set; }
        public int MoveHandleLayoutUpdates { get; private set; }
        void OnEnable(){moveBoundsAttempted=moveLayoutValid=false;}
        void Start(){if(tabletop&&journey)tabletop.Feedback+=journey.SwanNotify;}
        void OnDestroy(){if(tabletop&&journey)tabletop.Feedback-=journey.SwanNotify;}
        void LateUpdate() { using (SwanJourney.SwanJourneyCpuTimings.Measure(SwanJourney.SwanJourneyCpuTimings.Stage.Presentation)) CpuMeasuredLateUpdate(); }
    void CpuMeasuredLateUpdate()
        {
            ApplyModeNow();
            RefreshMoveHandle();
        }
        public void ApplyModeNow()
        {
            if(!journey||!content||initialized&&wasSandbox==journey.inSandbox)return;
            initialized=true;wasSandbox=journey.inSandbox;
            content.localScale=Vector3.one*(wasSandbox?sandboxScale:workshopScale);
            SynchronizeEnvironmentPose?.Invoke();
            RefreshFootprint();
            Debug.Log("SWAN_JOURNEY_MODE sandbox="+wasSandbox+" scale="+content.localScale.x);
            if(moveHandle)Debug.Log("SWAN_JOURNEY_MOVE_HANDLE mode="+(wasSandbox?"sandbox":"workshop")
                +" position="+moveHandle.position.ToString("F3")+" scale="+moveHandle.lossyScale.ToString("F3")
                +" anchor="+(MoveHandleAnchor?MoveHandleAnchor.name:"footprint"));
        }
        public void RefreshFootprint()
        {
            // Utility button geometry is static between footprint/mode changes.
            moveBoundsAttempted=moveLayoutValid=false;
            if(!tabletop)return;
            var supports=wasSandbox?sandboxSupports:workshopSupports;
            var boxes=new List<Bounds>();
            if(supports!=null)foreach(var support in supports)if(support&&TryBounds(support,transform,out var bounds))boxes.Add(bounds);
            if(boxes.Count==0)return;
            float bottom=boxes.Min(b=>b.min.y);
            var points=new List<Vector3>();
            foreach(var b in boxes) for(int x=0;x<=4;x++)for(int z=0;z<=2;z++)
                points.Add(new Vector3(Mathf.Lerp(b.min.x,b.max.x,x/4f),bottom,Mathf.Lerp(b.min.z,b.max.z,z/2f)));
            tabletop.baseHeight=bottom;tabletop.contactCentre=new Vector3(boxes[0].center.x,bottom,boxes[0].center.z);
            tabletop.supportSamplesPerFootprint=15;tabletop.supportPoints=points.ToArray();
            if(moveHandle&&!MoveHandleAnchor)moveHandle.localPosition=new Vector3(boxes.Min(b=>b.min.x)+.24f,bottom+.12f,boxes.Min(b=>b.min.z)-.10f);
            RefreshMoveHandle();
            if(Application.isPlaying&&tabletop.Attached&&!tabletop.Manipulating)
                tabletop.ApplyPose(transform.position,transform.rotation,transform.localScale,Vector3.zero,Time.unscaledTime);
            // A tabletop correction can move the common stage. Keep the independently
            // owned canyon in the same pose before arrival scans capture their bounds.
            SynchronizeEnvironmentPose?.Invoke();
        }
        Transform MoveHandleAnchor => journey&&journey.inSandbox?sandboxMoveHandleAnchor:workshopMoveHandleAnchor;
        public void RefreshMoveHandle()
        {
            if(!moveHandle)return;
            float stageScale=Mathf.Max(.001f,Mathf.Abs(transform.lossyScale.x));
            float handleScale=1f;
            var scale=Vector3.one*handleScale;
            var anchor=MoveHandleAnchor;
            if(!anchor){if(moveHandle.localScale!=scale)moveHandle.localScale=scale;return;}
            if(!moveBoundsAttempted||cachedMoveAnchor!=anchor)
            {
                cachedMoveAnchor=anchor;moveBoundsAttempted=true;moveLayoutValid=false;
                MoveHandleBoundsScans++;
                moveBoundsValid=TryBounds(anchor,anchor,out cachedMoveAnchorBounds);
            }
            if(!moveBoundsValid){if(moveHandle.localScale!=scale)moveHandle.localScale=scale;return;}
            var anchorToStage=transform.worldToLocalMatrix*anchor.localToWorldMatrix;
            if(moveLayoutValid&&anchorToStage==cachedMoveAnchorToStage
                &&Mathf.Approximately(moveHandleWorldGap,cachedWorldGap)
                &&moveHandle.localPosition==cachedMovePosition&&moveHandle.localScale==cachedMoveScale
                &&moveHandle.localRotation==Quaternion.identity)return;
            var neighbour=new Bounds(anchorToStage.MultiplyPoint3x4(cachedMoveAnchorBounds.center),Vector3.zero);
            for(int i=0;i<8;i++)
            {
                var corner=cachedMoveAnchorBounds.center+Vector3.Scale(cachedMoveAnchorBounds.extents,
                    new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));
                neighbour.Encapsulate(anchorToStage.MultiplyPoint3x4(corner));
            }
            // Dedicated foremost row. Every gap scales together with the scene;
            // moving/scaling the stage cannot slide the handle across other UI.
            var position=new Vector3(neighbour.center.x,neighbour.center.y,
                neighbour.min.z-.14f-moveHandleWorldGap);
            if(moveHandle.localScale!=scale)moveHandle.localScale=scale;
            if(moveHandle.localPosition!=position)moveHandle.localPosition=position;
            if(moveHandle.localRotation!=Quaternion.identity)moveHandle.localRotation=Quaternion.identity;
            cachedMoveAnchorToStage=anchorToStage;cachedStageScale=stageScale;
            cachedWorldGap=moveHandleWorldGap;
            cachedMovePosition=position;cachedMoveScale=scale;moveLayoutValid=true;
            MoveHandleLayoutUpdates++;
        }
        public static bool TryBounds(Transform root,Transform relativeTo,out Bounds bounds)
        {
            bool initialized=false;bounds=default;
            foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>(true)) {
                if(renderer.GetComponent<TMPro.TMP_Text>())continue;
                var filter=renderer.GetComponent<MeshFilter>();if(!filter||!filter.sharedMesh)continue;
                var local=filter.sharedMesh.bounds;
                for(int i=0;i<8;i++) {
                    var corner=local.center+Vector3.Scale(local.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));
                    var p=relativeTo.InverseTransformPoint(renderer.transform.TransformPoint(corner));
                    if(!initialized){bounds=new Bounds(p,Vector3.zero);initialized=true;}else bounds.Encapsulate(p);
                }
            }
            return initialized;
        }
    }
}
