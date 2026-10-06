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

        [Header("Completion Door")]
        [SerializeField] private bool createCompletionDoor = true;
        [SerializeField] private Vector3 completionDoorWorldPosition = new Vector3(10.4f, 1.5f, 5.5f);
        [SerializeField] private Vector3 completionDoorWorldEulerAngles = Vector3.zero;
        [SerializeField] private Vector3 completionDoorWorldScale = new Vector3(0.35f, 3f, 2f);
        [SerializeField] private Color completionDoorBaseColor = new Color(0.35f, 0.35f, 0.35f, 1f);
        [SerializeField] private Color completionDoorRevealColor = new Color(0.12f, 0.72f, 0.38f, 1f);
        [SerializeField] private string completionDoorName = "RestoredColorDoor";

        private bool complete;
        private DiscoverableObject temporarilyPlacedItem;
        private GameObject completionDoor;
        private Material completionDoorBaseMaterial;
        private Material completionDoorRevealMaterial;
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
            if (createCompletionDoor) CreateCompletionDoor();
        }

        private void OnDestroy()
        {
            if (completionDoorBaseMaterial != null) Destroy(completionDoorBaseMaterial);
            if (completionDoorRevealMaterial != null) Destroy(completionDoorRevealMaterial);
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
            if (completionDoor != null) completionDoor.SetActive(true);
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
            if (wrongPlacementClip == null)
                wrongPlacementClip = Resources.Load<AudioClip>("Audio/WrongShellPlacement");
            if (correctPlacementAndRevealClip == null)
                correctPlacementAndRevealClip = Resources.Load<AudioClip>("Audio/CorrectShellPlacementAndReveal");

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

        private void CreateCompletionDoor()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null) return;

            completionDoor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            completionDoor.name = completionDoorName;
            completionDoor.transform.SetPositionAndRotation(
                completionDoorWorldPosition,
                Quaternion.Euler(completionDoorWorldEulerAngles));
            completionDoor.transform.localScale = completionDoorWorldScale;
            completionDoorBaseMaterial = CreateDoorMaterial(shader, completionDoorBaseColor, 2000);
            completionDoor.GetComponent<Renderer>().material = completionDoorBaseMaterial;

            GameObject revealSurface = GameObject.CreatePrimitive(PrimitiveType.Cube);
            revealSurface.name = "ColorProxy";
            revealSurface.transform.SetParent(completionDoor.transform, false);
            revealSurface.transform.localPosition = Vector3.zero;
            revealSurface.transform.localRotation = Quaternion.identity;
            revealSurface.transform.localScale = Vector3.one * 1.002f;
            Collider revealCollider = revealSurface.GetComponent<Collider>();
            if (revealCollider != null) Destroy(revealCollider);
            int revealLayer = LayerMask.NameToLayer("ColorReveal");
            if (revealLayer >= 0) revealSurface.layer = revealLayer;
            completionDoorRevealMaterial = CreateDoorMaterial(shader, completionDoorRevealColor, 2001);
            revealSurface.GetComponent<Renderer>().material = completionDoorRevealMaterial;
            completionDoor.SetActive(false);
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

        private static Material CreateDoorMaterial(Shader shader, Color color, int renderQueue)
        {
            Material material = new Material(shader)
            {
                name = "RuntimeDoorMaterial",
                color = color,
                renderQueue = renderQueue
            };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            return material;
        }
    }
}
