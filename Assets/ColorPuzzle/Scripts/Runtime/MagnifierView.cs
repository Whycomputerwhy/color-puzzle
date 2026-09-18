using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ColorPuzzle
{
    public sealed class MagnifierView : MonoBehaviour
    {
        // CENTRALIZED VISUAL / INTERACTION PARAMETERS (all art-directable values are Inspector-visible)
        [Header("Interaction")]
        [SerializeField] private Key revealKey = Key.Q;
        [SerializeField, Min(0.1f)] private float discoveryDistance = 12f;
        [SerializeField, Min(0f)] private float discoveryScreenPadding = 4f;
        [SerializeField] private LayerMask discoveryLayers = ~0;

        [Header("Composition")]
        [SerializeField] private Camera sourceCamera;
        [SerializeField] private Camera colorCamera;
        [SerializeField] private RectTransform circularViewport;
        [SerializeField, Range(0.08f, 0.5f)] private float viewportScreenFraction = 0.25f;
        [SerializeField, Min(128)] private int renderTextureHeight = 720;

        [Header("Sky Colors")]
        [SerializeField] private Color regularViewSkyColor = Color.white;
        [SerializeField] private Color revealViewSkyColor = new Color(0.08f, 0.34f, 0.9f);

        [Header("Lens Motion")]
        [SerializeField, Min(0f)] private float lensMoveSensitivity = 1f;
        [SerializeField, Min(0f)] private float screenEdgePadding = 8f;
        [SerializeField] private bool resetLensToCenterOnOpen = true;

        [Header("Layers")]
        [SerializeField] private GameObject viewportRoot;
        [SerializeField] private FirstPersonPlayer player;

        private RenderTexture revealTexture;
        private RawImage revealImage;
        private RectTransform revealImageRect;
        private RectTransform canvasRect;
        private Canvas canvas;
        private Vector2 lensScreenPosition;
        private int textureWidth;
        private int textureHeight;
        private bool active;

        private void Awake()
        {
            revealImage = circularViewport != null ? circularViewport.GetComponentInChildren<RawImage>(true) : null;
            revealImageRect = revealImage != null ? revealImage.rectTransform : null;
            canvas = circularViewport != null ? circularViewport.GetComponentInParent<Canvas>() : null;
            canvasRect = canvas != null ? canvas.GetComponent<RectTransform>() : null;
            lensScreenPosition = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            ApplySolidSkyColors();
            EnsureRenderTexture();
            SetActive(false);
        }

        private void OnDestroy()
        {
            if (revealTexture != null) revealTexture.Release();
        }

        private void Update()
        {
            bool held = Keyboard.current != null && Keyboard.current[revealKey].isPressed;
            if (held != active) SetActive(held);
            if (!active || sourceCamera == null) return;

            EnsureRenderTexture();
            if (colorCamera != null)
            {
                colorCamera.transform.SetPositionAndRotation(sourceCamera.transform.position, sourceCamera.transform.rotation);
                colorCamera.fieldOfView = sourceCamera.fieldOfView;
            }

            UpdateLensPosition();
            TryDiscoverObjectUnderLens();
            UpdateViewportLayout();
        }

        private void SetActive(bool value)
        {
            active = value;
            if (value && resetLensToCenterOnOpen)
                lensScreenPosition = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            if (viewportRoot != null) viewportRoot.SetActive(value);
            if (colorCamera != null) colorCamera.enabled = value;
            if (player != null) player.SetControlsLocked(value);
        }

        private void TryDiscoverObjectUnderLens()
        {
            float lensRadius = Mathf.Min(Screen.width, Screen.height) * viewportScreenFraction * 0.5f;
            foreach (DiscoverableObject discoverable in DiscoverableObject.ActiveObjects)
            {
                if (discoverable == null || discoverable.IsDiscovered) continue;
                Bounds bounds = discoverable.DiscoveryBounds;
                float distance = Vector3.Distance(sourceCamera.transform.position, bounds.center);
                if (distance > discoveryDistance) continue;
                if (!ScreenBoundsIntersectLens(bounds, lensRadius)) continue;
                if (!HasLineOfSight(discoverable, bounds.center, distance)) continue;
                discoverable.Discover();
            }
        }

        private bool ScreenBoundsIntersectLens(Bounds bounds, float lensRadius)
        {
            Vector3 min = bounds.min;
            Vector3 max = bounds.max;
            Vector2 screenMin = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            Vector2 screenMax = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            bool anyPointInFront = false;

            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 worldPoint = new Vector3(
                    (corner & 1) == 0 ? min.x : max.x,
                    (corner & 2) == 0 ? min.y : max.y,
                    (corner & 4) == 0 ? min.z : max.z);
                Vector3 screenPoint = sourceCamera.WorldToScreenPoint(worldPoint);
                if (screenPoint.z <= 0f) continue;
                anyPointInFront = true;
                screenMin = Vector2.Min(screenMin, screenPoint);
                screenMax = Vector2.Max(screenMax, screenPoint);
            }

            if (!anyPointInFront) return false;
            Vector2 closestPoint = new Vector2(
                Mathf.Clamp(lensScreenPosition.x, screenMin.x, screenMax.x),
                Mathf.Clamp(lensScreenPosition.y, screenMin.y, screenMax.y));
            float effectiveRadius = lensRadius + discoveryScreenPadding;
            return (closestPoint - lensScreenPosition).sqrMagnitude <= effectiveRadius * effectiveRadius;
        }

        private bool HasLineOfSight(DiscoverableObject target, Vector3 targetCenter, float distance)
        {
            Vector3 origin = sourceCamera.transform.position;
            Vector3 direction = (targetCenter - origin).normalized;
            if (!Physics.Raycast(origin, direction, out RaycastHit hit, distance + 0.1f, discoveryLayers, QueryTriggerInteraction.Ignore))
                return false;
            return hit.collider.GetComponentInParent<DiscoverableObject>() == target;
        }

        private void UpdateLensPosition()
        {
            if (Mouse.current == null) return;
            lensScreenPosition += Mouse.current.delta.ReadValue() * lensMoveSensitivity;
            float radius = Mathf.Min(Screen.width, Screen.height) * viewportScreenFraction * 0.5f;
            float margin = radius + screenEdgePadding;
            lensScreenPosition.x = Mathf.Clamp(lensScreenPosition.x, margin, Screen.width - margin);
            lensScreenPosition.y = Mathf.Clamp(lensScreenPosition.y, margin, Screen.height - margin);
        }

        private void UpdateViewportLayout()
        {
            if (circularViewport == null || canvasRect == null || canvas == null) return;
            float sizePixels = Mathf.Min(Screen.width, Screen.height) * viewportScreenFraction;
            float scale = Mathf.Max(0.0001f, canvas.scaleFactor);
            circularViewport.sizeDelta = Vector2.one * (sizePixels / scale);

            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, lensScreenPosition, null, out Vector2 localLensPosition);
            circularViewport.anchoredPosition = localLensPosition;

            if (revealImageRect == null) return;
            revealImageRect.anchorMin = revealImageRect.anchorMax = new Vector2(0.5f, 0.5f);
            revealImageRect.pivot = new Vector2(0.5f, 0.5f);
            revealImageRect.sizeDelta = canvasRect.rect.size;
            // Counter-move the full-screen color render so the circle samples the same screen-space pixels.
            revealImageRect.anchoredPosition = -localLensPosition;
        }

        private void EnsureRenderTexture()
        {
            int desiredHeight = Mathf.Max(128, renderTextureHeight);
            int desiredWidth = Mathf.Max(128, Mathf.RoundToInt(desiredHeight * (Screen.width / (float)Mathf.Max(1, Screen.height))));
            if (revealTexture != null && desiredWidth == textureWidth && desiredHeight == textureHeight) return;

            if (revealTexture != null)
            {
                if (colorCamera != null) colorCamera.targetTexture = null;
                revealTexture.Release();
            }

            textureWidth = desiredWidth;
            textureHeight = desiredHeight;
            revealTexture = new RenderTexture(textureWidth, textureHeight, 16, RenderTextureFormat.ARGB32)
            {
                name = "MagnifierFullViewColorRT",
                filterMode = FilterMode.Bilinear
            };
            if (colorCamera != null) colorCamera.targetTexture = revealTexture;
            if (revealImage != null) revealImage.texture = revealTexture;
        }

        private void ApplySolidSkyColors()
        {
            if (sourceCamera != null)
            {
                sourceCamera.clearFlags = CameraClearFlags.SolidColor;
                sourceCamera.backgroundColor = regularViewSkyColor;
            }
            if (colorCamera != null)
            {
                colorCamera.clearFlags = CameraClearFlags.SolidColor;
                colorCamera.backgroundColor = revealViewSkyColor;
            }
        }
    }
}
