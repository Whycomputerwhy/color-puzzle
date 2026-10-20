using System.Collections.Generic;
using UnityEngine;

namespace ColorPuzzle
{
    public sealed class GoalPedestal : MonoBehaviour
    {
        private static readonly HashSet<GoalPedestal> ActivePedestals = new HashSet<GoalPedestal>();

        [Header("Matching")]
        [SerializeField] private string acceptedObjectId = "red-ball";
        [SerializeField] private Transform placementSocket;
        [SerializeField, Min(0f)] private float placedObjectSurfaceClearance = 0.015f;

        [Header("Completion Appearance")]
        [SerializeField] private Renderer pedestalRenderer;
        [SerializeField] private Material completedMaterial;
        [SerializeField] private GameObject colorProxy;

        [Header("Placement Audio")]
        [SerializeField] private AudioClip wrongPlacementClip;
        [SerializeField] private AudioClip correctPlacementAndRevealClip;
        [SerializeField, Range(0f, 1f)] private float wrongPlacementVolume = 0.24f;
        [SerializeField, Range(0f, 1f)] private float correctPlacementVolume = 0.3f;

        [Header("Interaction Hitbox")]
        [SerializeField] private bool useVisualBoundsHitbox = true;
        [SerializeField, Min(0f)] private float hitboxPadding = 0.08f;

        [Header("Completion Portal")]
        [SerializeField] private GameObject completionPortal;

        private bool complete;
        private DiscoverableObject temporarilyPlacedItem;
        private MagnifierView magnifierView;
        private BoxCollider runtimeInteractionHitbox;
        private GameObject placementTargetColorProxy;
        private AudioSource placementAudioSource;
        public bool IsComplete => complete;
        public Bounds PlacementBounds => pedestalRenderer != null ? pedestalRenderer.bounds : GetComponent<Collider>().bounds;
        public Vector3 PlacementAimPosition
        {
            get
            {
                Bounds bounds = PlacementBounds;
                Vector3 anchor = placementSocket != null ? placementSocket.position : bounds.center;
                return new Vector3(anchor.x, bounds.max.y, anchor.z);
            }
        }
        public static IEnumerable<GoalPedestal> ActiveGoals => ActivePedestals;

        private void OnEnable() => ActivePedestals.Add(this);
        private void OnDisable() => ActivePedestals.Remove(this);

        private void Awake()
        {
            magnifierView = FindAnyObjectByType<MagnifierView>(FindObjectsInactive.Include);
            EnsureColorProxy();
            EnsurePlacementTargetColorProxy();
            ConfigureInteractionHitbox();
            ConfigurePlacementAudio();
            if (completionPortal != null) completionPortal.SetActive(false);
        }

        public bool CanAccept(DiscoverableObject item)
        {
            return !complete && item != null && item.ObjectId == acceptedObjectId;
        }

        public bool CanPlace(DiscoverableObject item)
        {
            return !complete && temporarilyPlacedItem == null && item != null;
        }

        public bool TryPlace(DiscoverableObject item)
        {
            if (!CanPlace(item)) return false;
            Transform socket = placementSocket != null ? placementSocket : transform;
            Vector3 placedPosition = GetPlacedObjectPosition(item, socket);
            if (!CanAccept(item))
            {
                item.PlaceTemporarily(socket);
                item.transform.position = placedPosition;
                PlayPlacementAudio(wrongPlacementClip, wrongPlacementVolume);
                temporarilyPlacedItem = item;
                return true;
            }

            item.Place(socket);
            item.transform.position = placedPosition;
            complete = true;
            if (pedestalRenderer != null && completedMaterial != null)
                pedestalRenderer.sharedMaterial = completedMaterial;
            if (colorProxy != null) colorProxy.SetActive(true);
            if (completionPortal != null) completionPortal.SetActive(true);
            TelemetryManager.RecordPortalUnlocked();
            PlayPlacementAudio(correctPlacementAndRevealClip, correctPlacementVolume);
            magnifierView?.FadeOutBackgroundMusic();
            magnifierView?.RestoreWorldColor(PlacementAimPosition);
            return true;
        }

        private Vector3 GetPlacedObjectPosition(DiscoverableObject item, Transform socket)
        {
            // Imported model sockets can be offset from the visible cap. Every shell uses the
            // same rendered-top support point so a decoy cannot sit at a different Y position.
            Bounds pillarBounds = PlacementBounds;
            Vector3 supportSurface = new Vector3(
                socket.position.x,
                pillarBounds.max.y,
                socket.position.z);
            return item.GetRestingPosition(
                supportSurface,
                Vector3.up,
                placedObjectSurfaceClearance);
        }

        private void ConfigurePlacementAudio()
        {
            placementAudioSource = GetComponent<AudioSource>();
            if (placementAudioSource == null)
                placementAudioSource = gameObject.AddComponent<AudioSource>();
            placementAudioSource.playOnAwake = false;
            placementAudioSource.spatialBlend = 0f;
        }

        private void PlayPlacementAudio(AudioClip clip, float volume)
        {
            if (placementAudioSource != null && clip != null)
                placementAudioSource.PlayOneShot(clip, volume);
        }

        private void Update()
        {
            // Picking a wrong ball back up disables its collider. That immediately frees the
            // target for another attempt without requiring a separate interaction on the base.
            if (temporarilyPlacedItem != null &&
                (temporarilyPlacedItem.PhysicsCollider == null ||
                 !temporarilyPlacedItem.PhysicsCollider.enabled))
                temporarilyPlacedItem = null;
        }

        private void EnsureColorProxy()
        {
            if (colorProxy != null || pedestalRenderer == null) return;

            GameObject visualClone = Instantiate(
                pedestalRenderer.gameObject,
                pedestalRenderer.transform.parent);
            visualClone.name = "ColorProxy_Pillar";
            visualClone.transform.SetPositionAndRotation(
                pedestalRenderer.transform.position,
                pedestalRenderer.transform.rotation);
            visualClone.transform.localScale = pedestalRenderer.transform.localScale * 1.002f;

            int colorRevealLayer = LayerMask.NameToLayer("ColorReveal");
            foreach (Transform child in visualClone.GetComponentsInChildren<Transform>(true))
                if (colorRevealLayer >= 0) child.gameObject.layer = colorRevealLayer;
            foreach (Collider collider in visualClone.GetComponentsInChildren<Collider>(true))
                Destroy(collider);

            colorProxy = visualClone;
        }

        private void ConfigureInteractionHitbox()
        {
            if (!useVisualBoundsHitbox || pedestalRenderer == null) return;

            Collider existingCollider = GetComponent<Collider>();
            if (existingCollider != null) existingCollider.enabled = false;

            runtimeInteractionHitbox = GetComponent<BoxCollider>();
            if (runtimeInteractionHitbox == null)
                runtimeInteractionHitbox = gameObject.AddComponent<BoxCollider>();

            Bounds visualBounds = pedestalRenderer.bounds;
            runtimeInteractionHitbox.center = transform.InverseTransformPoint(visualBounds.center);
            Vector3 localSize = transform.InverseTransformVector(visualBounds.size);
            runtimeInteractionHitbox.size = new Vector3(
                Mathf.Abs(localSize.x) + hitboxPadding * 2f,
                Mathf.Abs(localSize.y) + hitboxPadding * 2f,
                Mathf.Abs(localSize.z) + hitboxPadding * 2f);
        }

        private void EnsurePlacementTargetColorProxy()
        {
            Transform target = transform.Find("PlacementTargetFlatImage");
            Renderer targetRenderer = target != null ? target.GetComponent<Renderer>() : null;
            if (targetRenderer == null || placementTargetColorProxy != null) return;

            placementTargetColorProxy = Instantiate(targetRenderer.gameObject, targetRenderer.transform.parent);
            placementTargetColorProxy.name = "ColorProxy_PlacementTarget";
            placementTargetColorProxy.transform.SetPositionAndRotation(
                targetRenderer.transform.position + Vector3.up * 0.002f,
                targetRenderer.transform.rotation);
            placementTargetColorProxy.transform.localScale = targetRenderer.transform.localScale * 1.002f;

            int colorRevealLayer = LayerMask.NameToLayer("ColorReveal");
            foreach (Transform child in placementTargetColorProxy.GetComponentsInChildren<Transform>(true))
                if (colorRevealLayer >= 0) child.gameObject.layer = colorRevealLayer;
            foreach (Collider collider in placementTargetColorProxy.GetComponentsInChildren<Collider>(true))
                Destroy(collider);
        }

    }
}
