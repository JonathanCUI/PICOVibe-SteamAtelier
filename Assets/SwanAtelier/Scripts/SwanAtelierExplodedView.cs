using UnityEngine;

namespace SwanAtelier
{
    public sealed partial class SwanAtelierController
    {
        public void ExplodeAllPieces()
        {
            CancelPartMotions(); StopImmediately(); CancelReset(); ClearQuickInstallTap(); HidePlacementHint();
            thermal.fireEnabled = false; doors.CloseAll();
            engine.principle = engine.exploded = engine.assemblyLesson = false;
            selected = assembly.selected = -1;
            ReleasePiecePointers();
            Vector3 centre = Vector3.zero;
            for (int i=0; i<assembly.pieces.Length; i++) {
                EnsureLoosePiece(i,assembly.pieceSlots[i]>=0?assembly.pieceSlots[i]:i);
                centre += assembly.pieces[i].centre;
                assembly.pieces[i].installed = false;
                assembly.slotOccupants[i] = assembly.pieceSlots[i] = -1;
            }
            centre /= assembly.pieces.Length;
            // Hide installed geometry once, then create/reuse only the 45 mobile
            // assembly groups. Avoid 45 repeated mechanism/tray rebuilds.
            engine.ApplyPose();
            for (int i=0; i<assembly.pieces.Length; i++) {
                // Return interchangeable pieces to their canonical display lane;
                // otherwise an installed swapped wheel and a detached wheel can
                // occupy the same exploded position.
                var template = assembly.pieces[i];
                var loose = EnsureLoosePiece(i, i);
                loose.transform.localScale = Vector3.one;
                AddPartMotion(i,template.centre+ExplosionOffset(template,centre),.28f);
            }
            batchMotion=true;
            RefreshTray(); presentation.PlayPick();
            SetMessage("45 个部件已拆开；捏住查看，双击归位，或点“全部装好”恢复。");
            Debug.Log("SWAN_EXPLODE_ALL pieces=" + assembly.pieces.Length + " duration=0.28");
        }

        public static Vector3 ExplosionOffset(SteamPuzzlePiece piece, Vector3 modelCentre)
        {
            Vector3 authored = Vector3.zero; int members = 0;
            foreach (var part in piece.members) if (part) { authored += part.explodeOffset; members++; }
            if (members > 0) authored /= members;
            float distance = Mathf.Clamp(authored.magnitude * .91f, 1.19f, 2.52f);
            Vector3 direction = authored;
            if (direction.sqrMagnitude < .01f) direction = piece.centre - modelCentre;
            direction.y = Mathf.Max(direction.y, .15f);
            // At the default .12 model scale, travel is about 14–30 cm. It scales
            // with the workbench, preserving a compact exploded composition.
            return direction.normalized * distance;
        }
    }
}
