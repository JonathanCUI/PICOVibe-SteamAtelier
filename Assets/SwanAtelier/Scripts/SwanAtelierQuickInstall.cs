using UnityEngine;

namespace SwanAtelier
{
    public sealed partial class SwanAtelierController
    {
        const double ShortPinchDuration = .28, DoublePinchGap = .40;
        const float ShortPinchTravel = .025f;
        int quickTapPiece = -1;
        double quickTapEnded;
        public int QuickInstallCount { get; private set; }

        void ClearQuickInstallTap() { quickTapPiece = -1; quickTapEnded = double.NegativeInfinity; }

        void RegisterQuickInstallTap(Contact contact, double now)
        {
            int piece = contact.loosePiece;
            // Only an already-detached, natively hit/highlighted part can be tapped.
            // Detaching a mounted part or taking one from the tray is never tap #1.
            bool valid = contact.pinch && !contact.canceled && !contact.blocked &&
                piece >= 0 && contact.pointer != null && contact.pointer.piece == piece &&
                now - contact.beganAt <= ShortPinchDuration && contact.maxHitTravel <= ShortPinchTravel &&
                assembly.looseObjects[piece] && !assembly.pieces[piece].installed;
            if (piece >= 0) Debug.Log("SWAN_QUICK_TAP piece=" + piece + " duration=" + (now-contact.beganAt).ToString("F3") +
                " travel=" + contact.maxHitTravel.ToString("F3") + " accepted=" + valid);
            if (!valid) { ClearQuickInstallTap(); return; }
            if (quickTapPiece == piece && contact.beganAt - quickTapEnded >= 0 &&
                contact.beganAt - quickTapEnded <= DoublePinchGap) {
                ClearQuickInstallTap();
                QuickInstallPiece(piece);
            } else { quickTapPiece = piece; quickTapEnded = now; }
        }

        bool QuickInstallPiece(int piece)
        {
            var loose = assembly.looseObjects[piece];
            if (!loose || assembly.pieces[piece].installed || PieceAnimating(piece)) return false;
            int slot = NearestSlot(piece, loose.transform.position);
            if (slot < 0) { SetMessage("没有空闲且匹配的安装位置。"); return false; }
            AddPartMotion(piece,assembly.pieces[slot].centre,.22f,slot);
            UpdatePlacementHint(piece);
            SetMessage("零件归位中…");
            Debug.Log("SWAN_QUICK_INSTALL_ANIMATION piece="+piece+" slot="+slot+" duration=0.22");
            return true;
        }
    }
}
