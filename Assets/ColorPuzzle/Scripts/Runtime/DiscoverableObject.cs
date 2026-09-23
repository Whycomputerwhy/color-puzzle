using System.Collections.Generic;
using UnityEngine;

namespace ColorPuzzle
{
    [RequireComponent(typeof(Collider), typeof(Rigidbody))]
    public sealed class DiscoverableObject : MonoBehaviour
    {
        private static readonly HashSet<DiscoverableObject> ActiveDiscoverables = new HashSet<DiscoverableObject>();

        [Header("Identity")]
        [SerializeField] private string objectId = "red-ball";
        [SerializeField] private string displayName = "Red Ball";
        [SerializeField] private Color inventoryColor = new Color(0.85f, 0.08f, 0.08f, 1f);

        [Header("Discovery Appearance")]
        [SerializeField] private Renderer grayscaleRenderer;
        [SerializeField] private Material discoveredMaterial;
        [SerializeField] private GameObject colorProxy;

        private Rigidbody body;
        private Collider hitCollider;
        private RigidbodyInterpolation originalInterpolation;
        private bool discovered;

        public string ObjectId => objectId;
        public string DisplayName => displayName;
        public Color InventoryColor => inventoryColor;
        public bool IsDiscovered => discovered;
        public Bounds DiscoveryBounds => grayscaleRenderer != null ? grayscaleRenderer.bounds : hitCollider.bounds;
        public static IEnumerable<DiscoverableObject> ActiveObjects => ActiveDiscoverables;

        private void OnEnable() => ActiveDiscoverables.Add(this);
        private void OnDisable() => ActiveDiscoverables.Remove(this);

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            hitCollider = GetComponent<Collider>();
            originalInterpolation = body.interpolation;
        }

        public void Discover()
        {
            if (discovered) return;
            discovered = true;
            if (grayscaleRenderer != null && discoveredMaterial != null)
                grayscaleRenderer.sharedMaterial = discoveredMaterial;
        }

        public void PickUp()
        {
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.isKinematic = true;
            body.detectCollisions = false;
            body.interpolation = RigidbodyInterpolation.None;
            hitCollider.enabled = false;
            transform.SetParent(null, true);
            if (grayscaleRenderer != null) grayscaleRenderer.enabled = false;
            if (colorProxy != null) colorProxy.SetActive(false);
        }

        public void Drop(Vector3 position, Vector3 impulse)
        {
            transform.SetParent(null, true);
            transform.position = position;
            if (grayscaleRenderer != null) grayscaleRenderer.enabled = true;
            hitCollider.enabled = true;
            body.detectCollisions = true;
            body.isKinematic = false;
            body.interpolation = originalInterpolation;
            body.AddForce(impulse, ForceMode.VelocityChange);
            if (colorProxy != null) colorProxy.SetActive(!discovered);
        }

        public void Place(Transform socket)
        {
            transform.SetParent(socket, false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            body.isKinematic = true;
            body.detectCollisions = false;
            body.interpolation = RigidbodyInterpolation.None;
            hitCollider.enabled = false;
            if (grayscaleRenderer != null) grayscaleRenderer.enabled = true;
            if (colorProxy != null) colorProxy.SetActive(false);
            Discover();
        }
    }
}
