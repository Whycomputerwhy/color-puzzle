using System.Collections;
using UnityEngine;

namespace ColorPuzzle
{
    /// <summary>A numbered shell key or keyhole, with static pickup/placement state.</summary>
    public sealed class ShellPuzzlePiece : MonoBehaviour
    {
        public enum PieceKind { Keyhole, Key }

        [Header("Matching pair")]
        [SerializeField] private PieceKind kind;
        [SerializeField, Range(1, 4)] private int pairNumber = 1;
        [SerializeField, Min(0.1f)] private float fitDiameter = 0.5f;
        [SerializeField] private Color pearlColor = Color.white;

        [Header("Key audio")]
        [SerializeField] private AudioClip pickupClip;
        [SerializeField, Range(0f, 1f)] private float pickupVolume = 0.16f;
        [SerializeField] private AudioClip dropClip;
        [SerializeField, Range(0f, 1f)] private float dropVolume = 0.18f;

        private Renderer[] visualRenderers;
        private Collider[] colliders;
        private ShellPuzzlePiece placedKey;
        private ShellPuzzlePiece temporaryKeyhole;
        private bool correctlyMatched;
        private Transform originalParent;
        private bool carried;
        private bool placed;
        private Collider ignoredPlayerCollider;
        private Coroutine collisionRestore;
        private AudioSource audioSource;

        public PieceKind Kind => kind;
        public int PairNumber => pairNumber;
        public float FitDiameter => fitDiameter;
        public Color PearlColor => pearlColor;
        public bool IsOccupied => placedKey != null;
        public ShellPuzzlePiece PlacedKey => placedKey;
        public bool IsCorrectlyFilled => placedKey != null && placedKey.correctlyMatched;
        public bool CanPickUp => kind == PieceKind.Key && !carried && !placed;
        public Renderer CueRenderer => transform.Find("Beige Cube")?.GetComponent<Renderer>();
        public Renderer PearlRenderer => transform.Find("Color Pearl")?.GetComponent<Renderer>();
        public Vector3 SlotPosition
        {
            get
            {
                Transform slot = transform.Find("Slot Recess");
                Renderer slotRenderer = slot != null ? slot.GetComponent<Renderer>() : null;
                return slotRenderer != null
                    ? new Vector3(slotRenderer.bounds.center.x, slotRenderer.bounds.max.y, slotRenderer.bounds.center.z)
                    : transform.position + Vector3.up * 1.5f;
            }
        }

        private void Awake()
        {
            originalParent = transform.parent;
            visualRenderers = GetComponentsInChildren<Renderer>(true);
            colliders = GetComponentsInChildren<Collider>(true);
            if (kind == PieceKind.Key)
            {
                audioSource = GetComponent<AudioSource>();
                if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
                audioSource.spatialBlend = 0f;
            }
        }

        public bool PickUp()
        {
            if (!CanPickUp) return false;
            RestorePlayerCollision();
            if (temporaryKeyhole != null)
            {
                temporaryKeyhole.placedKey = null;
                temporaryKeyhole = null;
                correctlyMatched = false;
                transform.SetParent(originalParent, true);
            }
            carried = true;
            SetVisibleAndSolid(false);
            Play(pickupClip, pickupVolume);
            return true;
        }

        public void DropAt(Vector3 floorPoint, Collider playerCollider)
        {
            if (!carried) return;
            carried = false;
            FinishDrop(floorPoint + Vector3.up * 0.02f, playerCollider);
        }

        public void DropAnimated(Vector3 startPosition, Vector3 floorPoint,
            float duration, float fallExponent, Collider playerCollider)
        {
            if (!carried) return;
            carried = false;
            transform.position = startPosition;
            SetRenderersEnabled(true);
            SetCollidersEnabled(false);
            Vector3 restingPosition = floorPoint + Vector3.up * 0.02f;
            if (duration <= 0f)
                FinishDrop(restingPosition, playerCollider);
            else
                StartCoroutine(AnimateDrop(restingPosition, duration,
                    Mathf.Max(1f, fallExponent), playerCollider));
        }

        private IEnumerator AnimateDrop(Vector3 restingPosition, float duration,
            float fallExponent, Collider playerCollider)
        {
            Vector3 startPosition = transform.position;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                transform.position = Vector3.Lerp(startPosition, restingPosition,
                    Mathf.Pow(Mathf.Clamp01(elapsed / duration), fallExponent));
                yield return null;
            }
            FinishDrop(restingPosition, playerCollider);
        }

        private void FinishDrop(Vector3 restingPosition, Collider playerCollider)
        {
            transform.position = restingPosition;
            SetVisibleAndSolid(true);
            Play(dropClip, dropVolume);
            if (playerCollider != null && OverlapsPlayer(playerCollider))
            {
                ignoredPlayerCollider = playerCollider;
                foreach (Collider pieceCollider in colliders)
                    Physics.IgnoreCollision(pieceCollider, playerCollider, true);
                collisionRestore = StartCoroutine(RestoreCollisionAfterPlayerExits());
            }
        }

        public void PlaceOn(ShellPuzzlePiece keyhole, bool correctMatch)
        {
            if (!carried || kind != PieceKind.Key || keyhole == null || keyhole.kind != PieceKind.Keyhole)
                return;
            RestorePlayerCollision();
            transform.SetParent(keyhole.transform, true);
            transform.position = keyhole.SlotPosition + Vector3.up * 0.02f;
            transform.rotation = keyhole.transform.rotation;
            carried = false;
            correctlyMatched = correctMatch;
            temporaryKeyhole = keyhole;
            keyhole.placedKey = this;
            // Every key stays pickable until the complete combination is confirmed.
            SetRenderersEnabled(true);
            SetCollidersEnabled(true);
            Play(dropClip, dropVolume);
        }

        public void LockPlacedKey()
        {
            if (kind != PieceKind.Keyhole || !IsCorrectlyFilled) return;
            placedKey.placed = true;
            placedKey.temporaryKeyhole = null;
            placedKey.SetCollidersEnabled(false);
        }

        public Vector3 GetVisualCenterOffset()
        {
            bool found = false;
            Bounds bounds = default;
            foreach (Renderer visual in visualRenderers)
            {
                if (!visual.gameObject.activeInHierarchy) continue;
                if (!found) { bounds = visual.bounds; found = true; }
                else bounds.Encapsulate(visual.bounds);
            }
            return found ? bounds.center - transform.position : Vector3.up * 0.25f;
        }

        private void SetVisibleAndSolid(bool visible)
        {
            SetRenderersEnabled(visible);
            SetCollidersEnabled(visible);
        }

        private void SetRenderersEnabled(bool enabled)
        {
            foreach (Renderer visual in visualRenderers) visual.enabled = enabled;
        }

        private void SetCollidersEnabled(bool enabled)
        {
            foreach (Collider pieceCollider in colliders) pieceCollider.enabled = enabled;
        }

        private bool OverlapsPlayer(Collider playerCollider)
        {
            foreach (Collider pieceCollider in colliders)
                if (pieceCollider.enabled && pieceCollider.bounds.Intersects(playerCollider.bounds))
                    return true;
            return false;
        }

        private IEnumerator RestoreCollisionAfterPlayerExits()
        {
            while (ignoredPlayerCollider != null && OverlapsPlayer(ignoredPlayerCollider))
                yield return new WaitForFixedUpdate();
            collisionRestore = null;
            RestorePlayerCollision();
        }

        private void RestorePlayerCollision()
        {
            if (collisionRestore != null)
            {
                StopCoroutine(collisionRestore);
                collisionRestore = null;
            }
            if (ignoredPlayerCollider != null)
                foreach (Collider pieceCollider in colliders)
                    Physics.IgnoreCollision(pieceCollider, ignoredPlayerCollider, false);
            ignoredPlayerCollider = null;
        }

        private void OnDisable() => RestorePlayerCollision();

        private void Play(AudioClip clip, float volume)
        {
            if (audioSource != null && clip != null)
                audioSource.PlayOneShot(clip, volume);
        }
    }
}
