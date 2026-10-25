using UnityEngine;

namespace SwanAtelier
{
    public sealed partial class SwanAtelierController
    {
        // Serialized prefab references also admit these variants to the native export.
        public GameObject[] placementGhostPrefabs;
        public GameObject PlacementGhost { get; private set; }
        public int PlacementSlot { get; private set; } = -1;
        int placementHintPiece = -1;

        public void UpdatePlacementHint(int piece)
        {
            var loose = piece >= 0 && piece < assembly.looseObjects.Length ? assembly.looseObjects[piece] : null;
            int slot = loose ? NearestSlot(piece, loose.transform.position) : -1;
            if (slot < 0) { HidePlacementHint(); return; }
            placementHintPiece = piece;
            if (PlacementSlot != slot || !PlacementGhost) {
                if (PlacementGhost) { PlacementGhost.SetActive(false); Destroy(PlacementGhost); }
                PlacementSlot = slot;
                if (placementGhostPrefabs != null && slot < placementGhostPrefabs.Length && placementGhostPrefabs[slot]) {
                    PlacementGhost = Instantiate(placementGhostPrefabs[slot], engine.display);
                    PlacementGhost.name = "Swan placement hint " + assembly.pieces[slot].key;
                    Debug.Log("SWAN_PLACEMENT_HINT piece=" + piece + " slot=" + slot);
                }
            }
            if (PlacementGhost) {
                // Same parent, scale and pose convention as the draggable piece.
                // The destination stays on the locomotive rather than following the hand.
                PlacementGhost.transform.SetPositionAndRotation(assembly.SlotPosition(slot), engine.locomotive.rotation);
                PlacementGhost.transform.localScale = Vector3.one;
            }
            // Retain the old marker only as a fallback for an incomplete scene.
            if (socket) { socket.gameObject.SetActive(!PlacementGhost); socket.position = assembly.SlotPosition(slot); }
        }

        void HidePlacementHint()
        {
            if (PlacementGhost) { PlacementGhost.SetActive(false); Destroy(PlacementGhost); }
            PlacementGhost = null; PlacementSlot = placementHintPiece = -1;
            if (socket) socket.gameObject.SetActive(false);
        }
    }
}
