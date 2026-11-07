using ColorPuzzle.Rendering;
using UnityEngine;

namespace ColorPuzzle
{
    /// <summary>Central drop target for one of Puzzle 4's combined number/key dials.</summary>
    public sealed class FinalDialKeySlot : MonoBehaviour
    {
        [Header("Identity")]
        [SerializeField, Range(1, 4)] private int requiredKeyNumber = 1;
        [SerializeField] private Transform keyAnchor;
        [SerializeField] private Renderer recessRenderer;

        [Header("Interaction")]
        [Tooltip("How far from the slot's center the player can aim and still place a carried key.")]
        [SerializeField, Min(0.1f)] private float placementAimRadius = 0.9f;

        private ShellPuzzlePiece placedKey;

        public int RequiredKeyNumber => requiredKeyNumber;
        public ShellPuzzlePiece PlacedKey => placedKey;
        public bool IsCorrectlyFilled => placedKey != null && placedKey.PairNumber == requiredKeyNumber;
        public Vector3 KeyPosition => keyAnchor != null ? keyAnchor.position : transform.position + Vector3.up * 0.08f;
        public Quaternion KeyRotation => keyAnchor != null ? keyAnchor.rotation : transform.rotation;
        public float PlacementAimRadius => placementAimRadius;

        /// <summary>
        /// Places a carried key. When a removable key already occupies this slot,
        /// it is returned as the new carried item so the player can swap directly.
        /// </summary>
        public bool TryPlace(ShellPuzzlePiece key, out ShellPuzzlePiece swappedOutKey)
        {
            swappedOutKey = null;
            if (key == null || key == placedKey) return false;

            ShellPuzzlePiece previousKey = placedKey;
            if (!key.PlaceOnDialSlot(this)) return false;

            placedKey = key;
            if (previousKey != null)
            {
                if (!previousKey.PickUpForDialSlotSwap()) return false;
                swappedOutKey = previousKey;
            }
            return true;
        }

        public void RemovePlacedKey(ShellPuzzlePiece key)
        {
            if (placedKey == key) placedKey = null;
        }

        /// <summary>Turns this slot colourful when its own full dial combination succeeds.</summary>
        public void RevealColorInNormalView()
        {
            foreach (DualViewMaterial visual in GetComponentsInChildren<DualViewMaterial>(true))
                visual.SetColorInGreyscaleCamera(true);
        }

        private void OnValidate()
        {
            if (recessRenderer == null) recessRenderer = GetComponentInChildren<Renderer>();
        }
    }
}
