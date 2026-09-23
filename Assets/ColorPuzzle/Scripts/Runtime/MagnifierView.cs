using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace ColorPuzzle
{
    public sealed class MagnifierView : MonoBehaviour
    {
        public enum MagnifierControlMode
        {
            MoveLens,
            FixedLensCameraAim
        }

        public enum DevViewMode
        {
            PuzzleView,
            FullColor
        }

        public enum MagnifierMouseButton
        {
            Right,
            Middle
        }

        // CENTRALIZED VISUAL / INTERACTION PARAMETERS (all art-directable values are Inspector-visible)
        [Header("Interaction")]
        [SerializeField] private MagnifierMouseButton revealMouseButton = MagnifierMouseButton.Right;
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

        [Header("Mode")]
        [SerializeField] private MagnifierControlMode startingMode = MagnifierControlMode.MoveLens;
        [SerializeField] private DevViewMode startingDevViewMode = DevViewMode.FullColor;

        [Header("Mode Selector UI")]
        [SerializeField] private bool showModeSelector = true;
        [SerializeField] private Vector2 modeButtonSize = new Vector2(140f, 36f);
        [SerializeField, Min(0f)] private float modeButtonGap = 8f;
        [SerializeField, Min(0f)] private float selectorGroupGap = 10f;
        [SerializeField, Min(0f)] private float selectorGroupPadding = 8f;
        [SerializeField, Min(8f)] private float selectorGroupLabelHeight = 20f;
        [SerializeField] private Vector2 modeSelectorMargin = new Vector2(20f, 20f);
        [SerializeField, Min(8)] private int modeButtonFontSize = 16;
        [SerializeField, Min(8)] private int selectorGroupLabelFontSize = 13;
        [SerializeField] private Color selectedModeColor = new Color(0.12f, 0.42f, 0.92f, 0.95f);
        [SerializeField] private Color unselectedModeColor = new Color(0.12f, 0.12f, 0.12f, 0.82f);
        [SerializeField] private Color modeButtonTextColor = Color.white;
        [SerializeField] private Color selectorGroupLabelColor = Color.white;
        [SerializeField] private Color selectorGroupBackgroundColor = new Color(0f, 0f, 0f, 0.55f);

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
        private MagnifierControlMode currentMode;
        private DevViewMode currentDevViewMode;
        private float originalColorCameraDepth;
        private int originalColorCameraCullingMask;
        private Button moveLensButton;
        private Button fixedLensButton;
        private Button puzzleViewButton;
        private Button fullColorButton;

        public bool IsActive => active;

        private void Awake()
        {
            revealImage = circularViewport != null ? circularViewport.GetComponentInChildren<RawImage>(true) : null;
            revealImageRect = revealImage != null ? revealImage.rectTransform : null;
            canvas = circularViewport != null ? circularViewport.GetComponentInParent<Canvas>() : null;
            canvasRect = canvas != null ? canvas.GetComponent<RectTransform>() : null;
            lensScreenPosition = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            currentMode = startingMode;
            currentDevViewMode = startingDevViewMode;
            if (colorCamera != null)
            {
                originalColorCameraDepth = colorCamera.depth;
                originalColorCameraCullingMask = colorCamera.cullingMask;
            }
            ApplySolidSkyColors();
            PrepareCompleteColorView();
            EnsureRenderTexture();
            CreateModeSelector();
            SetActive(false);
        }

        private void OnDestroy()
        {
            if (revealTexture != null) revealTexture.Release();
        }

        private void Update()
        {
            bool held = currentDevViewMode == DevViewMode.PuzzleView &&
                        IsRevealButtonHeld() &&
                        player != null &&
                        player.IsGrounded;
            if (held != active) SetActive(held);
        }

        private void LateUpdate()
        {
            bool fullColor = currentDevViewMode == DevViewMode.FullColor;
            if ((!active && !fullColor) || sourceCamera == null) return;

            if (!fullColor) EnsureRenderTexture();
            if (colorCamera != null)
            {
                colorCamera.transform.SetPositionAndRotation(sourceCamera.transform.position, sourceCamera.transform.rotation);
                colorCamera.fieldOfView = sourceCamera.fieldOfView;
            }

            if (fullColor) return;
            UpdateLensPosition();
            TryDiscoverObjectUnderLens();
            UpdateViewportLayout();
        }

        private void SetActive(bool value)
        {
            active = value && currentDevViewMode == DevViewMode.PuzzleView;
            if (value && (resetLensToCenterOnOpen || currentMode == MagnifierControlMode.FixedLensCameraAim))
                lensScreenPosition = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            ApplyDevViewMode();
            ApplyPlayerControlMode();
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
            if (currentMode == MagnifierControlMode.FixedLensCameraAim)
            {
                lensScreenPosition = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
                return;
            }

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

        private void SetMode(MagnifierControlMode mode)
        {
            currentMode = mode;
            if (mode == MagnifierControlMode.FixedLensCameraAim)
                lensScreenPosition = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            ApplyPlayerControlMode();
            RefreshModeSelector();
        }

        private void SetDevViewMode(DevViewMode mode)
        {
            currentDevViewMode = mode;
            active = mode == DevViewMode.PuzzleView &&
                     IsRevealButtonHeld() &&
                     player != null &&
                     player.IsGrounded;
            ApplyDevViewMode();
            ApplyPlayerControlMode();
            RefreshModeSelector();
        }

        private void ApplyDevViewMode()
        {
            bool fullColor = currentDevViewMode == DevViewMode.FullColor;
            if (viewportRoot != null) viewportRoot.SetActive(active && !fullColor);
            if (colorCamera == null) return;

            if (fullColor)
            {
                colorCamera.targetTexture = null;
                colorCamera.depth = sourceCamera != null ? sourceCamera.depth + 1f : originalColorCameraDepth;
                colorCamera.enabled = true;
            }
            else
            {
                colorCamera.targetTexture = revealTexture;
                colorCamera.depth = originalColorCameraDepth;
                colorCamera.enabled = active;
            }
        }

        private void ApplyPlayerControlMode()
        {
            if (player == null) return;
            bool allowCameraLook = currentMode == MagnifierControlMode.FixedLensCameraAim;
            bool magnifierActive = active && currentDevViewMode == DevViewMode.PuzzleView;
            player.SetMagnifierControls(magnifierActive, allowCameraLook);
        }

        private void CreateModeSelector()
        {
            if (!showModeSelector || canvasRect == null) return;
            EnsureEventSystem();

            GameObject selectorObject = new GameObject("MagnifierModeSelector", typeof(RectTransform));
            RectTransform selector = selectorObject.GetComponent<RectTransform>();
            selector.SetParent(canvasRect, false);
            selector.anchorMin = selector.anchorMax = new Vector2(1f, 1f);
            selector.pivot = new Vector2(1f, 1f);
            selector.anchoredPosition = new Vector2(-modeSelectorMargin.x, -modeSelectorMargin.y);
            float groupWidth = modeButtonSize.x * 2f + modeButtonGap + selectorGroupPadding * 2f;
            float groupHeight = selectorGroupPadding * 3f + selectorGroupLabelHeight + modeButtonSize.y;
            selector.sizeDelta = new Vector2(groupWidth, groupHeight * 2f + selectorGroupGap);
            selector.SetAsLastSibling();

            Font font = canvas.GetComponentInChildren<Text>(true)?.font;
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            RectTransform controlGroup = CreateSelectorGroup(selector, "MAGNIFIER CONTROL", 0f, groupWidth, groupHeight, font);
            moveLensButton = CreateModeButton(controlGroup, "Move Lens", selectorGroupPadding, font);
            fixedLensButton = CreateModeButton(controlGroup, "Fixed Lens", selectorGroupPadding + modeButtonSize.x + modeButtonGap, font);
            moveLensButton.onClick.AddListener(() => SetMode(MagnifierControlMode.MoveLens));
            fixedLensButton.onClick.AddListener(() => SetMode(MagnifierControlMode.FixedLensCameraAim));

            RectTransform devViewGroup = CreateSelectorGroup(selector, "DEV VIEW", groupHeight + selectorGroupGap, groupWidth, groupHeight, font);
            puzzleViewButton = CreateModeButton(devViewGroup, "Puzzle View", selectorGroupPadding, font);
            fullColorButton = CreateModeButton(devViewGroup, "Full Color", selectorGroupPadding + modeButtonSize.x + modeButtonGap, font);
            puzzleViewButton.onClick.AddListener(() => SetDevViewMode(DevViewMode.PuzzleView));
            fullColorButton.onClick.AddListener(() => SetDevViewMode(DevViewMode.FullColor));
            RefreshModeSelector();
        }

        private RectTransform CreateSelectorGroup(RectTransform parent, string label, float offsetY, float width, float height, Font font)
        {
            GameObject groupObject = new GameObject(label, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform groupRect = groupObject.GetComponent<RectTransform>();
            groupRect.SetParent(parent, false);
            groupRect.anchorMin = groupRect.anchorMax = new Vector2(1f, 1f);
            groupRect.pivot = new Vector2(1f, 1f);
            groupRect.anchoredPosition = new Vector2(0f, -offsetY);
            groupRect.sizeDelta = new Vector2(width, height);
            groupObject.GetComponent<Image>().color = selectorGroupBackgroundColor;

            GameObject labelObject = new GameObject("GroupLabel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            RectTransform labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.SetParent(groupRect, false);
            labelRect.anchorMin = labelRect.anchorMax = new Vector2(0f, 1f);
            labelRect.pivot = new Vector2(0f, 1f);
            labelRect.anchoredPosition = new Vector2(selectorGroupPadding, -selectorGroupPadding);
            labelRect.sizeDelta = new Vector2(width - selectorGroupPadding * 2f, selectorGroupLabelHeight);

            Text text = labelObject.GetComponent<Text>();
            text.text = label;
            text.font = font;
            text.fontSize = selectorGroupLabelFontSize;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleLeft;
            text.color = selectorGroupLabelColor;
            text.raycastTarget = false;
            return groupRect;
        }

        private Button CreateModeButton(RectTransform parent, string label, float offsetX, Font font)
        {
            GameObject buttonObject = new GameObject(label, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
            buttonRect.SetParent(parent, false);
            buttonRect.anchorMin = buttonRect.anchorMax = new Vector2(0f, 1f);
            buttonRect.pivot = new Vector2(0f, 1f);
            float buttonOffsetY = selectorGroupPadding * 2f + selectorGroupLabelHeight;
            buttonRect.anchoredPosition = new Vector2(offsetX, -buttonOffsetY);
            buttonRect.sizeDelta = modeButtonSize;

            Button button = buttonObject.GetComponent<Button>();
            button.targetGraphic = buttonObject.GetComponent<Image>();
            button.navigation = new Navigation { mode = Navigation.Mode.None };

            GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            RectTransform labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.SetParent(buttonRect, false);
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            Text text = labelObject.GetComponent<Text>();
            text.text = label;
            text.font = font;
            text.fontSize = modeButtonFontSize;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = modeButtonTextColor;
            text.raycastTarget = false;
            return button;
        }

        private void RefreshModeSelector()
        {
            if (moveLensButton != null)
                moveLensButton.targetGraphic.color = currentMode == MagnifierControlMode.MoveLens ? selectedModeColor : unselectedModeColor;
            if (fixedLensButton != null)
                fixedLensButton.targetGraphic.color = currentMode == MagnifierControlMode.FixedLensCameraAim ? selectedModeColor : unselectedModeColor;
            if (puzzleViewButton != null)
                puzzleViewButton.targetGraphic.color = currentDevViewMode == DevViewMode.PuzzleView ? selectedModeColor : unselectedModeColor;
            if (fullColorButton != null)
                fullColorButton.targetGraphic.color = currentDevViewMode == DevViewMode.FullColor ? selectedModeColor : unselectedModeColor;
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            new GameObject("RuntimeEventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        }

        private bool IsRevealButtonHeld()
        {
            if (Mouse.current == null) return false;
            return revealMouseButton == MagnifierMouseButton.Right
                ? Mouse.current.rightButton.isPressed
                : Mouse.current.middleButton.isPressed;
        }

        private void PrepareCompleteColorView()
        {
            if (sourceCamera == null || colorCamera == null) return;

            // The color pass must contain the complete ordinary world. Color-proxy layers are
            // then drawn later to replace the gray surface where a colored counterpart exists.
            colorCamera.cullingMask = sourceCamera.cullingMask | originalColorCameraCullingMask;
            Renderer[] renderers = FindObjectsByType<Renderer>(FindObjectsInactive.Include);
            for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
            {
                Renderer candidate = renderers[rendererIndex];
                int layerBit = 1 << candidate.gameObject.layer;
                if ((originalColorCameraCullingMask & layerBit) == 0) continue;

                Material[] runtimeMaterials = candidate.materials;
                for (int materialIndex = 0; materialIndex < runtimeMaterials.Length; materialIndex++)
                {
                    Material material = runtimeMaterials[materialIndex];
                    if (material == null || material.shader == null) continue;
                    material.renderQueue = Mathf.Max(material.renderQueue, material.shader.renderQueue) + 1;
                }
                candidate.materials = runtimeMaterials;
            }
        }
    }
}
