using System.Collections;
using UnityEngine;

namespace ColorPuzzle
{
    [RequireComponent(typeof(Collider), typeof(Rigidbody))]
    public sealed class DiscoverableObject : MonoBehaviour
    {
        [Header("Identity")]
        [SerializeField] private string objectId = "red-ball";
        [SerializeField] private string displayName = "Shell";

        [Header("Authored Start")]
        [SerializeField] private bool lockAuthoredStartUntilPickup = true;

        [Header("Discovery Appearance")]
        [SerializeField] private Renderer grayscaleRenderer;
        [SerializeField] private Material discoveredMaterial;
        [SerializeField] private GameObject colorProxy;

        [Header("Dropped Collision")]
        [SerializeField, Min(0f)] private float playerExitClearance = 0.03f;

        [Header("Drop Audio")]
        [SerializeField] private AudioClip dropClip;
        [SerializeField, Range(0f, 1f)] private float dropVolume = 0.18f;

        [Header("Pickup Audio")]
        [SerializeField] private AudioClip pickupClip;
        [SerializeField, Range(0f, 1f)] private float pickupVolume = 0.16f;

        private Rigidbody body;
        private Collider hitCollider;
        private Material grayscaleMaterial;
        private Renderer colorProxyRenderer;
        private bool discovered;
        private Vector3 carriedBoundsCenterOffset;
        private Vector3 carriedBoundsExtents;
        private Coroutine dropAnimation;
        private Coroutine playerCollisionRestore;
        private Collider ignoredPlayerCollider;
        private AudioSource dropAudioSource;

        public string ObjectId => objectId;
        public string DisplayName => displayName;
        public bool IsDiscovered => discovered;
        public Collider PhysicsCollider => hitCollider;
        public Renderer InteractionCueRenderer => grayscaleRenderer;
        public Renderer InteractionCueColorRenderer =>
            colorProxy != null && colorProxy.activeInHierarchy && colorProxyRenderer != null && colorProxyRenderer.enabled
                ? colorProxyRenderer
                : grayscaleRenderer;
        public Bounds InteractionCueBounds => grayscaleRenderer != null ? grayscaleRenderer.bounds : hitCollider.bounds;
        public Vector3 DropBoundsExtents => carriedBoundsExtents;
        public float HorizontalDropRadius => Mathf.Max(carriedBoundsExtents.x, carriedBoundsExtents.z);

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            hitCollider = GetComponent<Collider>();
            if (grayscaleRenderer != null) grayscaleMaterial = grayscaleRenderer.sharedMaterial;
            if (colorProxy != null)
                colorProxyRenderer = colorProxy.GetComponentInChildren<Renderer>(true);
            dropAudioSource = GetComponent<AudioSource>();
            if (dropAudioSource == null)
                dropAudioSource = gameObject.AddComponent<AudioSource>();
            dropAudioSource.playOnAwake = false;
            dropAudioSource.spatialBlend = 0f;
            if (lockAuthoredStartUntilPickup)
            {
                StopDynamicMotion();
                body.isKinematic = true;
                body.useGravity = false;
                body.detectCollisions = true;
                body.interpolation = RigidbodyInterpolation.None;
            }
        }

        public void Discover()
        {
            if (discovered) return;
            discovered = true;
            ApplyWorldAppearance(true);
        }

        public void PickUp()
        {
            // Pickup is inventory-only. Permanent color restoration happens exclusively
            // when the object reaches its matching goal.
            if (dropAnimation != null)
            {
                StopCoroutine(dropAnimation);
                dropAnimation = null;
            }
            RestorePlayerCollisionImmediately();
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
            PlayPickupAudio();
        }

        public Vector3 GetRestingPosition(Vector3 surfacePoint, Vector3 surfaceNormal, float clearance)
        {
            Vector3 normal = surfaceNormal.sqrMagnitude > 0f ? surfaceNormal.normalized : Vector3.up;
            Vector3 absoluteNormal = new Vector3(Mathf.Abs(normal.x), Mathf.Abs(normal.y), Mathf.Abs(normal.z));
            float supportDistance = Vector3.Dot(carriedBoundsExtents, absoluteNormal);
            return surfacePoint + normal * (supportDistance + Mathf.Max(0f, clearance)) - carriedBoundsCenterOffset;
        }

        public Vector3 GetDropBoundsCenter(Vector3 objectPosition)
        {
            return objectPosition + carriedBoundsCenterOffset;
        }

        public void Drop(Vector3 position, Collider playerCollider)
        {
            PrepareDropVisual(position);
            FinishDrop(position, playerCollider);
        }

        public void DropAnimated(
            Vector3 startPosition,
            Vector3 restingPosition,
            float duration,
            float fallExponent,
            Collider playerCollider)
        {
            if (dropAnimation != null)
            {
                StopCoroutine(dropAnimation);
                dropAnimation = null;
            }
            PrepareDropVisual(startPosition);
            if (duration <= 0f)
            {
                FinishDrop(restingPosition, playerCollider);
                return;
            }
            dropAnimation = StartCoroutine(AnimateDrop(
                startPosition,
                restingPosition,
                duration,
                Mathf.Max(1f, fallExponent),
                playerCollider));
        }

        private void PrepareDropVisual(Vector3 position)
        {
            transform.SetParent(null, true);
            transform.position = position;
            ApplyWorldAppearance(discovered);
            if (grayscaleRenderer != null) grayscaleRenderer.enabled = true;
            hitCollider.enabled = false;
            StopDynamicMotion();
            body.useGravity = false;
            body.detectCollisions = false;
            body.isKinematic = true;
            body.interpolation = RigidbodyInterpolation.None;
            if (colorProxy != null) colorProxy.SetActive(!discovered);
        }

        private IEnumerator AnimateDrop(
            Vector3 startPosition,
            Vector3 restingPosition,
            float duration,
            float fallExponent,
            Collider playerCollider)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float normalizedTime = Mathf.Clamp01(elapsed / duration);
                float fallProgress = Mathf.Pow(normalizedTime, fallExponent);
                transform.position = Vector3.LerpUnclamped(startPosition, restingPosition, fallProgress);
                yield return null;
            }

            dropAnimation = null;
            FinishDrop(restingPosition, playerCollider);
        }

        private void FinishDrop(Vector3 restingPosition, Collider playerCollider)
        {
            transform.position = restingPosition;
            PlayDropAudio();
            body.detectCollisions = true;
            hitCollider.enabled = true;

            if (playerCollider == null) return;
            ignoredPlayerCollider = playerCollider;
            Physics.IgnoreCollision(hitCollider, ignoredPlayerCollider, true);
            if (IsPlayerInsideDropVolume(ignoredPlayerCollider))
                playerCollisionRestore = StartCoroutine(RestorePlayerCollisionAfterExit());
            else
                RestorePlayerCollisionImmediately();
        }

        public void Place(Transform socket)
        {
            // Keep the item outside the pedestal hierarchy. Placement sockets inherit the
            // pedestal's non-uniform scale, which would otherwise squash a placed sphere.
            RestorePlayerCollisionImmediately();
            transform.SetParent(null, true);
            transform.SetPositionAndRotation(socket.position, socket.rotation);
            StopDynamicMotion();
            body.isKinematic = true;
            body.useGravity = false;
            body.detectCollisions = false;
            body.interpolation = RigidbodyInterpolation.None;
            hitCollider.enabled = false;
            if (grayscaleRenderer != null) grayscaleRenderer.enabled = true;
            // The completed world uses the ColorReveal camera, so the placed shell must retain
            // a colour-layer renderer rather than disappearing with the greyscale source mesh.
            // Use the completed source renderer directly: it has the final material and avoids
            // any duplicate-proxy depth conflict with the pedestal surface.
            if (colorProxy != null) colorProxy.SetActive(false);
            Discover();
            PlayDropAudio();
            int colorRevealLayer = LayerMask.NameToLayer("ColorReveal");
            if (grayscaleRenderer != null && colorRevealLayer >= 0)
                grayscaleRenderer.gameObject.layer = colorRevealLayer;
        }

        public void PlaceTemporarily(Transform socket)
        {
            // A non-matching object may be tested on a goal without becoming the solution.
            // It stays static but pickable, and returns to greyscale on its next normal drop.
            RestorePlayerCollisionImmediately();
            transform.SetParent(null, true);
            transform.SetPositionAndRotation(socket.position, socket.rotation);
            StopDynamicMotion();
            body.isKinematic = true;
            body.useGravity = false;
            body.detectCollisions = true;
            body.interpolation = RigidbodyInterpolation.None;
            hitCollider.enabled = true;
            ApplyWorldAppearance(true);
            if (grayscaleRenderer != null) grayscaleRenderer.enabled = true;
            // A wrong shell remains testable on the pillar. Keep its regular completed-looking
            // mesh for the main view and its ColourReveal proxy for the magnifier/full-colour view.
            if (colorProxy != null) colorProxy.SetActive(true);
            PlayDropAudio();
        }

        private void PlayDropAudio()
        {
            if (dropAudioSource != null && dropClip != null)
                dropAudioSource.PlayOneShot(dropClip, dropVolume);
        }

        private void PlayPickupAudio()
        {
            if (dropAudioSource != null && pickupClip != null)
                dropAudioSource.PlayOneShot(pickupClip, pickupVolume);
        }

        private void ApplyWorldAppearance(bool showColor)
        {
            if (grayscaleRenderer == null) return;
            Material colorMaterial = discoveredMaterial;
            if (colorMaterial == null && colorProxy != null)
            {
                Renderer proxyRenderer = colorProxy.GetComponentInChildren<Renderer>(true);
                if (proxyRenderer != null) colorMaterial = proxyRenderer.sharedMaterial;
            }

            if (showColor && colorMaterial != null)
                grayscaleRenderer.sharedMaterial = colorMaterial;
            else if (grayscaleMaterial != null)
                grayscaleRenderer.sharedMaterial = grayscaleMaterial;
        }

        private IEnumerator RestorePlayerCollisionAfterExit()
        {
            while (ignoredPlayerCollider != null && IsPlayerInsideDropVolume(ignoredPlayerCollider))
                yield return new WaitForFixedUpdate();

            playerCollisionRestore = null;
            RestorePlayerCollisionImmediately();
        }

        private bool IsPlayerInsideDropVolume(Collider playerCollider)
        {
            if (playerCollider == null || !hitCollider.enabled) return false;
            Bounds dropVolume = hitCollider.bounds;
            dropVolume.Expand(playerExitClearance * 2f);
            return dropVolume.Intersects(playerCollider.bounds);
        }

        private void RestorePlayerCollisionImmediately()
        {
            if (playerCollisionRestore != null)
            {
                StopCoroutine(playerCollisionRestore);
                playerCollisionRestore = null;
            }
            if (ignoredPlayerCollider != null && hitCollider != null)
                Physics.IgnoreCollision(hitCollider, ignoredPlayerCollider, false);
            ignoredPlayerCollider = null;
        }

        private void OnDisable()
        {
            RestorePlayerCollisionImmediately();
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
