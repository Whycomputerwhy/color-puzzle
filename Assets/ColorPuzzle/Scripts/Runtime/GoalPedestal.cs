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

        [Header("Completion Appearance")]
        [SerializeField] private Renderer pedestalRenderer;
        [SerializeField] private Material completedMaterial;
        [SerializeField] private GameObject colorProxy;

        [Header("Completion Door")]
        [SerializeField] private bool createCompletionDoor = true;
        [SerializeField] private Vector3 completionDoorWorldPosition = new Vector3(10.4f, 1.5f, 5.5f);
        [SerializeField] private Vector3 completionDoorWorldEulerAngles = Vector3.zero;
        [SerializeField] private Vector3 completionDoorWorldScale = new Vector3(0.35f, 3f, 2f);
        [SerializeField] private Color completionDoorBaseColor = new Color(0.35f, 0.35f, 0.35f, 1f);
        [SerializeField] private Color completionDoorRevealColor = new Color(0.12f, 0.72f, 0.38f, 1f);
        [SerializeField] private string completionDoorName = "RestoredColorDoor";

        private bool complete;
        private GameObject completionDoor;
        private Material completionDoorBaseMaterial;
        private Material completionDoorRevealMaterial;
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

        public bool TryPlace(DiscoverableObject item)
        {
            if (!CanAccept(item)) return false;
            item.Place(placementSocket != null ? placementSocket : transform);
            complete = true;
            if (pedestalRenderer != null && completedMaterial != null)
                pedestalRenderer.sharedMaterial = completedMaterial;
            if (colorProxy != null) colorProxy.SetActive(false);
            if (completionDoor != null) completionDoor.SetActive(true);
            return true;
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
