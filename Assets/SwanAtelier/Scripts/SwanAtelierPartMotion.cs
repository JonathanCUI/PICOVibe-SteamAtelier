using System.Collections.Generic;
using UnityEngine;

namespace SwanAtelier
{
    public sealed partial class SwanAtelierController
    {
        sealed class PartMotion
        {
            public int piece, slot;
            public Transform target;
            public Vector3 from, to;
            public Quaternion rotation;
            public float elapsed, duration;
        }
        readonly List<PartMotion> partMotions = new List<PartMotion>(45);
        bool batchMotion, restoreAfterMotion;
        public bool PartsAnimating => partMotions.Count > 0;
        bool PieceAnimating(int piece) => partMotions.Exists(m => m.piece == piece);
        bool SlotReserved(int slot, int exceptPiece) => partMotions.Exists(m => m.slot == slot && m.piece != exceptPiece);

        void AddPartMotion(int piece, Vector3 destination, float duration, int installSlot = -1)
        {
            var target = assembly.looseObjects[piece].transform;
            partMotions.Add(new PartMotion { piece=piece, slot=installSlot, target=target,
                from=engine.locomotive.InverseTransformPoint(target.position), to=destination,
                rotation=Quaternion.Inverse(engine.locomotive.rotation)*target.rotation, duration=duration });
        }
        void CancelPartMotions()
        {
            partMotions.Clear(); batchMotion = restoreAfterMotion = false;
        }
        void ReleasePiecePointers()
        {
            foreach(var pointer in pointers.Values) if(pointer!=null) pointer.active=false;
            if(mouse!=null) mouse.active=false;
        }
        public void TickPartMotionFrame(float dt)
        {
            // First-time native prefab upload can stall a frame. Do not consume
            // an entire visible flight in that one long frame.
            TickPartMotions(Mathf.Min(dt,1f/30));
        }
        public void TickPartMotions(float dt)
        {
            if(!PartsAnimating) return;
            for(int i=partMotions.Count-1;i>=0;i--) {
                var motion=partMotions[i];
                if(!motion.target) { partMotions.RemoveAt(i); continue; }
                motion.elapsed+=Mathf.Max(0,dt);
                float t=Mathf.Clamp01(motion.elapsed/motion.duration);
                float ease=t*t*(3-2*t);
                motion.target.SetPositionAndRotation(
                    engine.locomotive.TransformPoint(Vector3.LerpUnclamped(motion.from,motion.to,ease)),
                    engine.locomotive.rotation*Quaternion.Slerp(motion.rotation,Quaternion.identity,ease));
                if(t<1) continue;
                partMotions.RemoveAt(i);
                if(motion.slot>=0) {
                    // A moving part reserves its slot until the visible flight ends.
                    motion.target.position=assembly.SlotPosition(motion.slot);
                    if(ReleasePiece(motion.piece)) {
                        QuickInstallCount++;
                        SetMessage("双击归位，安装完成。");
                        Debug.Log("SWAN_QUICK_INSTALL piece="+motion.piece+" slot="+motion.slot+" animated=0.22");
                    }
                }
            }
            if(partMotions.Count==0) {
                bool restore=restoreAfterMotion;
                batchMotion=restoreAfterMotion=false;
                if(restore) { CompleteAssembly(false); presentation.PlaySnap(); }
                Debug.Log("SWAN_PART_MOTION_END restore="+restore);
            }
        }
        public void CompleteAssemblyAnimated()
        {
            CancelPartMotions(); ClearQuickInstallTap(); HidePlacementHint(); ReleasePiecePointers();
            StopImmediately(); CancelReset(); thermal.fireEnabled=false; doors.CloseAll();
            engine.principle=engine.exploded=engine.assemblyLesson=false;
            for(int i=0;i<assembly.pieces.Length;i++) {
                if(assembly.pieces[i].installed&&assembly.pieceSlots[i]==i) continue;
                int slot=assembly.pieceSlots[i];
                EnsureLoosePiece(i,slot>=0?slot:i);
                if(slot>=0) assembly.slotOccupants[slot]=-1;
                assembly.pieceSlots[i]=-1; assembly.pieces[i].installed=false;
                AddPartMotion(i,assembly.pieces[i].centre,.28f);
            }
            engine.ApplyPose();
            if(!PartsAnimating) { CompleteAssembly(false); return; }
            batchMotion=restoreAfterMotion=true;
            SetMessage("零件归位中…");
            Debug.Log("SWAN_ASSEMBLE_ANIMATION pieces="+partMotions.Count+" duration=0.28");
        }
    }
}
