using UnityEngine;

namespace ColorPuzzle
{
    [RequireComponent(typeof(Collider), typeof(Rigidbody))]
    public sealed class DiscoverableObject : MonoBehaviour
    {
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
        private bool discovered;
        private Vector3 carriedBoundsCenterOffset;
        private Vector3 carriedBoundsExtents;

        public string ObjectId => objectId;
        public string DisplayName => displayName;
        public Color InventoryColor => inventoryColor;
        public bool IsDiscovered => discovered;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            hitCollider = GetComponent<Collider>();
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
            // Pickup, not magnifier inspection, is the commitment point that permanently
            // exposes this object's color in the grayscale world after it is dropped.
            Discover();
            Bounds pickupBounds = hitCollider.bounds;
            carriedBoundsCenterOffset = pickupBounds.center - transform.position;
            carriedBoundsExtents = pickupBounds.extents;
            StopDynamicMotion();
            body.isKinematic = true;
            body.detectCollisions = false;
            body.interpolation = RigidbodyInterpolation.None;
            hitCollider.enabled = false;
            transform.SetParent(null, true);
            if (grayscaleRenderer != null) grayscaleRenderer.enabled = false;
            if (colorProxy != null) colorProxy.SetActive(false);
        }

        public Vector3 GetRestingPosition(Vector3 surfacePoint, Vector3 surfaceNormal, float clearance)
        {
            Vector3 normal = surfaceNormal.sqrMagnitude > 0f ? surfaceNormal.normalized : Vector3.up;
            Vector3 absoluteNormal = new Vector3(Mathf.Abs(normal.x), Mathf.Abs(normal.y), Mathf.Abs(normal.z));
            float supportDistance = Vector3.Dot(carriedBoundsExtents, absoluteNormal);
            return surfacePoint + normal * (supportDistance + Mathf.Max(0f, clearance)) - carriedBoundsCenterOffset;
        }

        public void Drop(Vector3 position)
        {
            transform.SetParent(null, true);
            transform.position = position;
            if (grayscaleRenderer != null) grayscaleRenderer.enabled = true;
            hitCollider.enabled = true;
            StopDynamicMotion();
            body.useGravity = false;
            body.detectCollisions = true;
            body.isKinematic = true;
            body.interpolation = RigidbodyInterpolation.None;
            if (colorProxy != null) colorProxy.SetActive(!discovered);
        }

        public void Place(Transform socket)
        {
            // Keep the item outside the pedestal hierarchy. Placement sockets inherit the
            // pedestal's non-uniform scale, which would otherwise squash a placed sphere.
            transform.SetParent(null, true);
            transform.SetPositionAndRotation(socket.position, socket.rotation);
            StopDynamicMotion();
            body.isKinematic = true;
            body.useGravity = false;
            body.detectCollisions = false;
            body.interpolation = RigidbodyInterpolation.None;
            hitCollider.enabled = false;
            if (grayscaleRenderer != null) grayscaleRenderer.enabled = true;
            if (colorProxy != null) colorProxy.SetActive(false);
            Discover();
        }

        private void StopDynamicMotion()
        {
            // Unity warns when velocity is assigned to a kinematic Rigidbody. A kinematic
            // body cannot be moving under physics, so only dynamic bodies need clearing.
            if (body.isKinematic) return;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }
    }
}
