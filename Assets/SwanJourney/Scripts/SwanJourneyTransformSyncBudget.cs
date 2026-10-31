using UnityEngine;

namespace SwanJourney
{
    // Unity propagates hasChanged down an entire moved hierarchy; the native
    // adapter sends local TRS, whose parent already carries that movement.
    // Run after app animations, before adapter frame-end synchronization.
    // Never suppress initial/activated/reparented/local changes.
    [DefaultExecutionOrder(9998)]
    public sealed class SwanJourneyTransformSyncBudget : MonoBehaviour
    {
        struct Entry
        {
            public Transform target,parent;
            public GameObject gameObject;
            public Vector3 position,scale;
            public Quaternion rotation;
            public bool active;
            public int warmup;
        }
        Entry[] entries;
        Transform root;
        public int SuppressedLastFrame { get; private set; }
        public long SuppressedTotal { get; private set; }
        double nextReport;
        int passes;
        void OnEnable() { entries=null;root=transform;passes=0; }
        void LateUpdate() { using (SwanJourney.SwanJourneyCpuTimings.Measure(SwanJourney.SwanJourneyCpuTimings.Stage.TransformFilter)) CpuMeasuredLateUpdate(); }
        void CpuMeasuredLateUpdate() { FilterNow(); }
        public void FilterNow()
        {
            if(entries==null)
            {
                root=transform;
                var nodes=GetComponentsInChildren<Transform>(true);
                entries=new Entry[nodes.Length];
                for(int i=0;i<nodes.Length;i++)entries[i]=new Entry{target=nodes[i],gameObject=nodes[i].gameObject};
            }
            SuppressedLastFrame=0;
            // This filter is only needed for inherited movement. Genuine local
            // animation remains dirty and is handled by the adapter directly.
            // Avoid a second traversal of thousands of nodes while the exhibit rests.
            if(passes>=2 && !root.hasChanged)return;
            passes++;
            for(int i=0;i<entries.Length;i++)
            {
                ref var entry=ref entries[i];
                var t=entry.target;if(!t)continue;
                bool active=entry.gameObject.activeInHierarchy;
                // Inactive branches are not synchronized by the adapter. Their
                // dirty flag can stay set indefinitely; do not read their TRS
                // every frame. Activation starts a fresh unsuppressed warmup.
                if(!active){entry.active=false;entry.warmup=0;continue;}
                if(!t.hasChanged && entry.warmup>=2 && active==entry.active)continue;
                // One native read for local position + rotation, and cached object
                // handles avoid thousands of Component lookups while moving.
                t.GetLocalPositionAndRotation(out var position,out var rotation);
                var scale=t.localScale;var parent=t.parent;
                if(entry.warmup>=2 && active && entry.active && !ReferenceEquals(t,root) && parent==entry.parent
                    &&position.Equals(entry.position)&&rotation.Equals(entry.rotation)&&scale.Equals(entry.scale))
                {t.hasChanged=false;SuppressedLastFrame++;}
                entry.position=position;entry.rotation=rotation;entry.scale=scale;entry.parent=parent;
                entry.active=active;if(entry.warmup<2)entry.warmup++;
            }
            SuppressedTotal+=SuppressedLastFrame;
            if(Application.isPlaying&&Time.realtimeSinceStartupAsDouble>=nextReport)
            {
                nextReport=Time.realtimeSinceStartupAsDouble+10;
                Debug.Log("SWAN_JOURNEY_TRANSFORM_BUDGET nodes="+entries.Length+" redundantLocalSyncsAvoided="+SuppressedTotal+" lastFrame="+SuppressedLastFrame);
            }
        }
    }
}
