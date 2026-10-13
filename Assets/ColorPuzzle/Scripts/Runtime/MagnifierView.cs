using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using ColorPuzzle.Rendering;

namespace ColorPuzzle
{
    public sealed class MagnifierView : MonoBehaviour
    {
        private const string GreyscaleButtonLabel = "Greyscale";
        private const string FullColorButtonLabel = "Full Color";
        private const string ColorSearchButtonLabel = "Color Search";
        private const string ValueSearchButtonLabel = "Value Search";
        private const string BackGlowButtonLabel = "Back Glow";
        private const string CenterTextButtonLabel = "Center Text";
        private const string ChilankaFontLabel = "Chilanka";
        private const string ComingSoonFontLabel = "Coming Soon";
        private const string MargarineFontLabel = "Margarine";
        private const string NerkoOneFontLabel = "Nerko One";
        private const string ForestLevelButtonLabel = "Forest";
        private const string OceanLevelButtonLabel = "Ocean";
        private const string VillageLevelButtonLabel = "Village";
        private const int MinimumRenderTextureDimension = 128;
        private const float DefaultRenderAspect = 16f / 9f;
        private const float MinimumUsableRenderAspect = 0.25f;
        private const float MaximumUsableRenderAspect = 4f;

        public enum DevViewMode
        {
            PuzzleView,
            FullColor
        }

        public enum PuzzleMode
        {
            ColorSearch,
            ValueSearch
        }

        public enum MagnifierMouseButton
        {
            Right,
            Middle
        }

        public enum DisplayFont
        {
            Default,
            Chilanka,
            ComingSoon,
            Margarine,
            NerkoOne
        }

        private static bool hasPersistedDeveloperSettings;
        private static DevViewMode persistedDevViewMode = DevViewMode.PuzzleView;
        private static PuzzleMode persistedPuzzleMode = PuzzleMode.ColorSearch;
        private static PlayerInteraction.InteractionCueMode persistedInteractionCueMode =
            PlayerInteraction.InteractionCueMode.BackGlow;
        private static DisplayFont persistedDisplayFont = DisplayFont.Chilanka;

        // CENTRALIZED VISUAL / INTERACTION PARAMETERS (all art-directable values are Inspector-visible)
        [Header("Interaction")]
        [SerializeField] private MagnifierMouseButton revealMouseButton = MagnifierMouseButton.Right;

        [Header("Puzzle Mode")]
        [SerializeField] private PuzzleMode defaultPuzzleMode = PuzzleMode.ColorSearch;
        [SerializeField, TextArea(2, 4)] private string colorSearchObjectiveText =
            "The ocean's colors have vanished. Peer through your magnifier to reveal them, then find the object that belongs atop the stone pillar.";
        [SerializeField, TextArea(2, 4)] private string valueSearchObjectiveText =
            "The ocean remembers color only as light and shadow. Compare each object's shade, then return the matching one to the stone pillar.";

        [Header("Value Search Palette (dark to light)")]
        [SerializeField] private Color valueDarkBlue = new Color(0.03f, 0.05f, 0.12f, 1f);
        [SerializeField] private Color valuePurple = new Color(0.28f, 0.05f, 0.36f, 1f);
        [SerializeField] private Color valueTargetRed = new Color(0.9f, 0.03f, 0.03f, 1f);
        [SerializeField] private Color valueGreen = new Color(0.05f, 0.58f, 0.18f, 1f);
        [SerializeField] private Color valueOrange = new Color(1f, 0.52f, 0.06f, 1f);
        [SerializeField] private Color valueCyan = new Color(0.35f, 0.92f, 1f, 1f);

        [Header("Composition")]
        [SerializeField] private Camera sourceCamera;
        [SerializeField] private Camera colorCamera;
        [SerializeField] private RectTransform circularViewport;
        [SerializeField, Range(0.08f, 0.5f)] private float viewportScreenFraction = 0.25f;
        [SerializeField, Min(128)] private int renderTextureHeight = 720;

        [Header("Sky Colors")]
        [SerializeField] private Color regularViewSkyColor = Color.white;
        [SerializeField] private Color revealViewSkyColor = new Color(0.08f, 0.34f, 0.9f);

        [Header("Background Music")]
        [SerializeField] private AudioClip backgroundMusicClip;
        [SerializeField, Range(0f, 1f)] private float backgroundMusicVolume = 0.12f;
        [SerializeField, Min(0.5f)] private float backgroundMusicLoopDuration = 37f;
        [SerializeField, Range(0.05f, 5f)] private float backgroundMusicLoopCrossfadeDuration = 1f;
        [SerializeField, Min(0.05f)] private float backgroundMusicFadeOutDuration = 1.5f;

        [Header("World Color Reveal")]
        [SerializeField, Min(0.1f)] private float worldRevealDuration = 2.4f;
        [SerializeField, Min(0f)] private float worldRevealStartRadius = 0.18f;
        [SerializeField, Min(0.1f)] private float worldRevealEndRadius = 42f;
        [SerializeField, Min(0.001f)] private float worldRevealEdgeFeather = 0.8f;
        [SerializeField] private AnimationCurve worldRevealEasing = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Lens Motion")]
        [SerializeField, Min(0f)] private float lensMoveSensitivity = 1f;
        [SerializeField, Min(0f)] private float lensMoveEasing = 18f;
        [SerializeField, Min(0f)] private float screenEdgePadding = 8f;
        [SerializeField] private bool resetLensToCenterOnOpen = true;

        [Header("Developer Panel")]
        [SerializeField] private bool enableDeveloperPanel = true;
        [SerializeField] private Key developerPanelToggleKey = Key.Enter;
        [SerializeField] private bool unlockCursorWhilePanelOpen = true;
        [SerializeField] private bool showLevelSequenceInDeveloperPanel = true;
        [SerializeField] private string forestTutorialSceneName = "ColorPuzzleForestTutorial";
        [SerializeField] private string oceanSceneName = "ColorPuzzleUnderwaterDemo";
        [SerializeField] private string villageEndingSceneName = "ColorPuzzleVillageEnding";
        [SerializeField] private Vector2 modeButtonSize = new Vector2(140f, 36f);
        [SerializeField, Min(0f)] private float modeButtonGap = 8f;
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

        [Header("Telemetry Guide")]
        [SerializeField] private bool showTelemetryGuideInDeveloperPanel = true;
        [SerializeField, TextArea(3, 6)] private string telemetryGuideText =
            "Auto-saves on portal entry, level exit, and quit.\nF9 writes a snapshot. Exact file path is printed in Console.";
        [SerializeField, Min(8)] private int telemetryGuideFontSize = 14;
        [SerializeField, Min(50f)] private float telemetryGuideContentHeight = 76f;

        [Header("Typography")]
        [SerializeField] private DisplayFont defaultDisplayFont = DisplayFont.Chilanka;

        [Header("Story Objective HUD")]
        [SerializeField] private bool showStoryObjective = true;
        [SerializeField] private Vector2 storyCardSize = new Vector2(500f, 96f);
        [SerializeField] private Vector2 storyCardMargin = new Vector2(20f, 20f);
        [SerializeField] private Vector2 storyCardPadding = new Vector2(22f, 14f);
        [SerializeField, Min(8)] private int storyFontSize = 19;
        [SerializeField] private Color storyCardFillColor = new Color(1f, 1f, 1f, 0.94f);
        [SerializeField] private Color storyTextColor = new Color(0.07f, 0.10f, 0.15f, 1f);
        [SerializeField] private Color storyCardShadowColor = new Color(0.03f, 0.08f, 0.14f, 0.32f);
        [SerializeField] private Vector2 storyCardShadowOffset = new Vector2(3f, -4f);

        [Header("Layers")]
        [SerializeField] private GameObject viewportRoot;
        [SerializeField] private FirstPersonPlayer player;

        private RenderTexture revealTexture;
        private RawImage revealImage;
        private RectTransform revealImageRect;
        private RectTransform canvasRect;
        private Canvas canvas;
        private Vector2 lensScreenPosition;
        private Vector2 lensTargetScreenPosition;
        private int textureWidth;
        private int textureHeight;
        private bool active;
        private bool worldRestored;
        private PuzzleMode currentPuzzleMode;
        private DevViewMode currentDevViewMode;
        private DisplayFont currentDisplayFont;
        private float originalColorCameraDepth;
        private int originalColorCameraCullingMask;
        private Button puzzleViewButton;
        private Button fullColorButton;
        private Button colorSearchButton;
        private Button valueSearchButton;
        private Button backGlowButton;
        private Button centerTextButton;
        private Button forestLevelButton;
        private Button oceanLevelButton;
        private Button villageLevelButton;
        private GameObject developerPanelRoot;
        private bool developerPanelOpen;
        private PlayerInteraction playerInteraction;
        private GameObject storyObjectiveRoot;
        private Text storyObjectiveLabel;
        private GameObject valueSearchOverlayRoot;
        private RawImage valueSearchOverlay;
        private Material valueSearchGreyscaleMaterial;
        private GameObject worldRevealRoot;
        private RawImage worldRevealImage;
        private Material worldRevealMaterial;
        private bool worldRevealAnimating;
        private AudioSource backgroundMusicSource;
        private AudioSource backgroundMusicAlternateSource;
        private Coroutine backgroundMusicFadeCoroutine;
        private Coroutine backgroundMusicLoopCoroutine;
        private readonly Dictionary<DisplayFont, Button> displayFontButtons = new();
        private readonly Dictionary<Renderer, Material[]> valueOriginalMaterials = new();
        private readonly List<Material> valueRuntimeMaterials = new();

        public bool IsActive => active;
        public bool IsFullColorView => worldRestored || currentDevViewMode == DevViewMode.FullColor;
        public bool IsValueSearchMode => !worldRestored && currentPuzzleMode == PuzzleMode.ValueSearch;
        public bool IsDeveloperPanelOpen => developerPanelOpen;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetDeveloperSettingsForNewPlaySession()
        {
            hasPersistedDeveloperSettings = false;
            persistedDevViewMode = DevViewMode.PuzzleView;
            persistedPuzzleMode = PuzzleMode.ColorSearch;
            persistedInteractionCueMode = PlayerInteraction.InteractionCueMode.BackGlow;
            persistedDisplayFont = DisplayFont.Chilanka;
        }

        private void Awake()
        {
            revealImage = circularViewport != null ? circularViewport.GetComponentInChildren<RawImage>(true) : null;
            revealImageRect = revealImage != null ? revealImage.rectTransform : null;
            canvas = circularViewport != null ? circularViewport.GetComponentInParent<Canvas>() : null;
            canvasRect = canvas != null ? canvas.GetComponent<RectTransform>() : null;
            playerInteraction = GetComponent<PlayerInteraction>();
            lensScreenPosition = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            lensTargetScreenPosition = lensScreenPosition;
            // A new play session starts in the authored defaults. Scene reloads caused by R
            // restore only developer selections; gameplay objects still reset with the scene.
            currentDevViewMode = hasPersistedDeveloperSettings
                ? persistedDevViewMode
                : DevViewMode.PuzzleView;
            currentPuzzleMode = hasPersistedDeveloperSettings
                ? persistedPuzzleMode
                : defaultPuzzleMode;
            currentDisplayFont = hasPersistedDeveloperSettings && persistedDisplayFont != DisplayFont.Default
                ? persistedDisplayFont
                : DisplayFont.Chilanka;
            if (hasPersistedDeveloperSettings && playerInteraction != null)
                playerInteraction.SetInteractionCueMode(persistedInteractionCueMode);
            if (colorCamera != null)
            {
                originalColorCameraDepth = colorCamera.depth;
                originalColorCameraCullingMask = colorCamera.cullingMask;
            }
            if (sourceCamera != null)
                sourceCamera.depthTextureMode |= DepthTextureMode.Depth;
            InteractionOutlineRuntime.SetColorCamera(colorCamera);
            ApplySolidSkyColors();
            ConfigureBackgroundMusic();
            PrepareCompleteColorView();
            EnsureRenderTexture();
            CreateValueSearchOverlay();
            CreateWorldRevealOverlay();
            CreateStoryObjectiveCard();
            CreateDeveloperPanel();
            ApplyDisplayFont(currentDisplayFont);
            ApplyPuzzleMode();
            SetActive(false);
        }

        private void OnDestroy()
        {
            if (revealTexture != null) revealTexture.Release();
            RestoreValueSearchPalette();
            if (valueSearchGreyscaleMaterial != null) Destroy(valueSearchGreyscaleMaterial);
            if (worldRevealMaterial != null) Destroy(worldRevealMaterial);
        }

        private void Update()
        {
            if (PauseMenuController.IsPaused) return;
            if (enableDeveloperPanel && developerPanelToggleKey != Key.None && Keyboard.current != null &&
                Keyboard.current[developerPanelToggleKey].wasPressedThisFrame)
                SetDeveloperPanelOpen(!developerPanelOpen);

            bool held = !developerPanelOpen &&
                        !worldRestored &&
                        currentPuzzleMode == PuzzleMode.ColorSearch &&
                        currentDevViewMode == DevViewMode.PuzzleView &&
                        IsRevealButtonHeld() &&
                        player != null &&
                        player.IsGrounded;
            if (held != active) SetActive(held);
        }

        private void LateUpdate()
        {
            if (PauseMenuController.IsPaused) return;
            bool fullColor = worldRestored || currentDevViewMode == DevViewMode.FullColor;
            bool valueSearch = !worldRestored && currentPuzzleMode == PuzzleMode.ValueSearch;
            if ((!active && !fullColor && !valueSearch && !worldRevealAnimating) || sourceCamera == null) return;

            if (!fullColor || valueSearch || worldRevealAnimating) EnsureRenderTexture();
            if (colorCamera != null)
            {
                colorCamera.transform.SetPositionAndRotation(sourceCamera.transform.position, sourceCamera.transform.rotation);
                colorCamera.fieldOfView = sourceCamera.fieldOfView;
            }

            if (fullColor || valueSearch || worldRevealAnimating) return;
            UpdateLensPosition();
            UpdateViewportLayout();
        }

        private void SetActive(bool value)
        {
            bool wasActive = active;
            active = value &&
                     !worldRestored &&
                     currentPuzzleMode == PuzzleMode.ColorSearch &&
                     currentDevViewMode == DevViewMode.PuzzleView;
            if (active && !wasActive)
                TelemetryManager.RecordMagnifierUse();
            if (value && resetLensToCenterOnOpen)
            {
                lensScreenPosition = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
                lensTargetScreenPosition = lensScreenPosition;
            }
            ApplyDevViewMode();
            ApplyPlayerControlMode();
        }

        private void UpdateLensPosition()
        {
            if (Mouse.current != null)
                lensTargetScreenPosition += Mouse.current.delta.ReadValue() * lensMoveSensitivity;

            float radius = Mathf.Min(Screen.width, Screen.height) * viewportScreenFraction * 0.5f;
            float margin = radius + screenEdgePadding;
            float maximumX = Mathf.Max(margin, Screen.width - margin);
            float maximumY = Mathf.Max(margin, Screen.height - margin);
            lensTargetScreenPosition.x = Mathf.Clamp(lensTargetScreenPosition.x, margin, maximumX);
            lensTargetScreenPosition.y = Mathf.Clamp(lensTargetScreenPosition.y, margin, maximumY);

            // Exponential response gives the same feel at different frame rates. Zero is
            // intentionally an immediate response for easy A/B tuning in the Inspector.
            float blend = lensMoveEasing <= 0f
                ? 1f
                : 1f - Mathf.Exp(-lensMoveEasing * Time.unscaledDeltaTime);
            lensScreenPosition = Vector2.Lerp(lensScreenPosition, lensTargetScreenPosition, blend);
            lensScreenPosition.x = Mathf.Clamp(lensScreenPosition.x, margin, maximumX);
            lensScreenPosition.y = Mathf.Clamp(lensScreenPosition.y, margin, maximumY);
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
            int maximumDimension = Mathf.Max(MinimumRenderTextureDimension, SystemInfo.maxTextureSize);
            int desiredHeight = Mathf.Clamp(renderTextureHeight, MinimumRenderTextureDimension, maximumDimension);
            float aspect = Screen.width / (float)Mathf.Max(1, Screen.height);
            if (aspect < MinimumUsableRenderAspect || aspect > MaximumUsableRenderAspect)
                aspect = sourceCamera != null ? sourceCamera.aspect : DefaultRenderAspect;
            if (aspect < MinimumUsableRenderAspect || aspect > MaximumUsableRenderAspect)
                aspect = DefaultRenderAspect;
            int desiredWidth = Mathf.Clamp(
                Mathf.RoundToInt(desiredHeight * aspect),
                MinimumRenderTextureDimension,
                maximumDimension);
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
            if (valueSearchOverlay != null) valueSearchOverlay.texture = revealTexture;
            if (worldRevealImage != null) worldRevealImage.texture = revealTexture;
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

        private void ConfigureBackgroundMusic()
        {
            if (backgroundMusicClip == null)
                backgroundMusicClip = Resources.Load<AudioClip>("Audio/UnderwaterBackgroundMusic");
            if (backgroundMusicClip == null) return;

            GameObject musicObject = new GameObject("BackgroundMusic", typeof(AudioSource));
            musicObject.transform.SetParent(transform, false);
            backgroundMusicSource = musicObject.GetComponent<AudioSource>();
            backgroundMusicSource.clip = backgroundMusicClip;
            backgroundMusicSource.volume = backgroundMusicVolume;
            backgroundMusicSource.loop = false;
            backgroundMusicSource.playOnAwake = false;
            backgroundMusicSource.spatialBlend = 0f;

            GameObject alternateMusicObject = new GameObject("BackgroundMusicLoopBridge", typeof(AudioSource));
            alternateMusicObject.transform.SetParent(transform, false);
            backgroundMusicAlternateSource = alternateMusicObject.GetComponent<AudioSource>();
            backgroundMusicAlternateSource.clip = backgroundMusicClip;
            backgroundMusicAlternateSource.volume = 0f;
            backgroundMusicAlternateSource.loop = false;
            backgroundMusicAlternateSource.playOnAwake = false;
            backgroundMusicAlternateSource.spatialBlend = 0f;

            backgroundMusicLoopCoroutine = StartCoroutine(LoopBackgroundMusicSegment());
        }

        private IEnumerator LoopBackgroundMusicSegment()
        {
            if (backgroundMusicSource == null || backgroundMusicAlternateSource == null || backgroundMusicClip == null)
                yield break;

            float segmentDuration = Mathf.Min(backgroundMusicLoopDuration, backgroundMusicClip.length);
            float crossfadeDuration = Mathf.Min(backgroundMusicLoopCrossfadeDuration, segmentDuration * 0.5f);
            AudioSource currentSource = backgroundMusicSource;
            AudioSource nextSource = backgroundMusicAlternateSource;
            currentSource.time = 0f;
            currentSource.volume = backgroundMusicVolume;
            currentSource.Play();

            while (true)
            {
                yield return new WaitForSecondsRealtime(segmentDuration - crossfadeDuration);

                nextSource.time = 0f;
                nextSource.volume = 0f;
                nextSource.Play();
                float elapsed = 0f;
                while (elapsed < crossfadeDuration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    float blend = Mathf.Clamp01(elapsed / crossfadeDuration);
                    currentSource.volume = backgroundMusicVolume * (1f - blend);
                    nextSource.volume = backgroundMusicVolume * blend;
                    yield return null;
                }

                currentSource.Stop();
                currentSource.volume = 0f;
                AudioSource previousSource = currentSource;
                currentSource = nextSource;
                nextSource = previousSource;
            }
        }

        public void FadeOutBackgroundMusic()
        {
            if (backgroundMusicSource == null && backgroundMusicAlternateSource == null) return;

            if (backgroundMusicFadeCoroutine != null)
                StopCoroutine(backgroundMusicFadeCoroutine);
            if (backgroundMusicLoopCoroutine != null)
                StopCoroutine(backgroundMusicLoopCoroutine);
            backgroundMusicFadeCoroutine = StartCoroutine(FadeOutBackgroundMusicRoutine());
        }

        private IEnumerator FadeOutBackgroundMusicRoutine()
        {
            float primaryStartingVolume = backgroundMusicSource != null ? backgroundMusicSource.volume : 0f;
            float alternateStartingVolume = backgroundMusicAlternateSource != null ? backgroundMusicAlternateSource.volume : 0f;
            float elapsed = 0f;
            while (elapsed < backgroundMusicFadeOutDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float fade = 1f - Mathf.Clamp01(elapsed / backgroundMusicFadeOutDuration);
                if (backgroundMusicSource != null)
                    backgroundMusicSource.volume = primaryStartingVolume * fade;
                if (backgroundMusicAlternateSource != null)
                    backgroundMusicAlternateSource.volume = alternateStartingVolume * fade;
                yield return null;
            }

            if (backgroundMusicSource != null)
            {
                backgroundMusicSource.volume = 0f;
                backgroundMusicSource.Stop();
            }
            if (backgroundMusicAlternateSource != null)
            {
                backgroundMusicAlternateSource.volume = 0f;
                backgroundMusicAlternateSource.Stop();
            }
            backgroundMusicFadeCoroutine = null;
        }

        private void CreateWorldRevealOverlay()
        {
            if (canvasRect == null || worldRevealRoot != null) return;

            Shader revealShader = Shader.Find("ColorPuzzle/UI/WorldSphereReveal");
            if (revealShader == null)
            {
                Debug.LogWarning("World color reveal shader could not be found.");
                return;
            }

            worldRevealMaterial = new Material(revealShader)
            {
                name = "WorldSphereReveal_Runtime"
            };
            worldRevealRoot = new GameObject(
                "WorldColorReveal",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(RawImage));
            RectTransform rect = worldRevealRoot.GetComponent<RectTransform>();
            rect.SetParent(canvasRect, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.SetAsFirstSibling();

            worldRevealImage = worldRevealRoot.GetComponent<RawImage>();
            worldRevealImage.texture = revealTexture;
            worldRevealImage.material = worldRevealMaterial;
            worldRevealImage.raycastTarget = false;
            worldRevealRoot.SetActive(false);
        }

        private void SetDevViewMode(DevViewMode mode)
        {
            if (worldRestored) return;
            if (currentPuzzleMode == PuzzleMode.ValueSearch && mode == DevViewMode.FullColor)
                return;
            currentDevViewMode = mode;
            PersistDeveloperSettings();
            active = currentPuzzleMode == PuzzleMode.ColorSearch &&
                     mode == DevViewMode.PuzzleView &&
                     IsRevealButtonHeld() &&
                     player != null &&
                     player.IsGrounded;
            ApplyDevViewMode();
            ApplyPlayerControlMode();
            RefreshModeSelector();
        }

        private void ApplyDevViewMode()
        {
            bool valueSearch = !worldRestored && currentPuzzleMode == PuzzleMode.ValueSearch;
            bool fullColor = worldRestored || (!valueSearch && currentDevViewMode == DevViewMode.FullColor);
            if (viewportRoot != null) viewportRoot.SetActive(active && !fullColor);
            if (valueSearchOverlayRoot != null) valueSearchOverlayRoot.SetActive(valueSearch);
            if (worldRevealRoot != null) worldRevealRoot.SetActive(worldRevealAnimating);
            if (colorCamera == null) return;

            if (worldRevealAnimating)
            {
                EnsureRenderTexture();
                colorCamera.targetTexture = revealTexture;
                // Render the colour texture before the main camera. The following UI
                // pass reconstructs positions from the main camera's depth buffer to
                // form a world-space sphere, so it must not inherit the colour
                // camera's depth state on the same frame.
                colorCamera.depth = sourceCamera != null
                    ? sourceCamera.depth - 1f
                    : originalColorCameraDepth;
                colorCamera.enabled = true;
            }
            else if (valueSearch)
            {
                EnsureRenderTexture();
                colorCamera.targetTexture = revealTexture;
                colorCamera.depth = originalColorCameraDepth;
                colorCamera.enabled = true;
            }
            else if (fullColor)
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
            bool valueSearch = !worldRestored && currentPuzzleMode == PuzzleMode.ValueSearch;
            if (player != null)
            {
                bool magnifierActive = !valueSearch &&
                                       active &&
                                       currentDevViewMode == DevViewMode.PuzzleView;
                player.SetMagnifierControls(magnifierActive, false);
            }
            if (playerInteraction != null)
                playerInteraction.SetValueSearchMode(valueSearch);
        }

        /// <summary>
        /// Completes the colour-search state for the current run. This is intentionally
        /// transient: reloading the scene with R starts the authored puzzle state again.
        /// </summary>
        public void RestoreWorldColor()
        {
            Vector3 fallbackOrigin = sourceCamera != null ? sourceCamera.transform.position : transform.position;
            RestoreWorldColor(fallbackOrigin);
        }

        public void RestoreWorldColor(Vector3 revealOrigin)
        {
            if (worldRestored || worldRevealAnimating) return;

            active = false;
            RestoreValueSearchPalette();
            ApplyDevViewMode();
            ApplyPlayerControlMode();
            RefreshStoryObjective();
            RefreshModeSelector();
            StartCoroutine(AnimateWorldColorReveal(revealOrigin));
        }

        private IEnumerator AnimateWorldColorReveal(Vector3 revealOrigin)
        {
            worldRevealAnimating = true;
            // Seed the material before enabling its UI overlay. This avoids one
            // unmasked frame and makes the expansion visibly begin at the pillar.
            UpdateWorldRevealMask(revealOrigin, 0f);
            ApplyDevViewMode();

            float elapsed = 0f;
            while (elapsed < worldRevealDuration)
            {
                float normalizedTime = worldRevealDuration <= 0f
                    ? 1f
                    : Mathf.Clamp01(elapsed / worldRevealDuration);
                UpdateWorldRevealMask(revealOrigin, normalizedTime);
                elapsed += Time.deltaTime;
                yield return null;
            }

            UpdateWorldRevealMask(revealOrigin, 1f);
            worldRevealAnimating = false;
            worldRestored = true;
            ApplyDevViewMode();
            ApplyPlayerControlMode();
            RefreshStoryObjective();
            RefreshModeSelector();
        }

        private void UpdateWorldRevealMask(Vector3 revealOrigin, float normalizedTime)
        {
            if (worldRevealMaterial == null || sourceCamera == null) return;

            float easedTime = worldRevealEasing != null
                ? Mathf.Clamp01(worldRevealEasing.Evaluate(normalizedTime))
                : normalizedTime;
            float radius = Mathf.Lerp(worldRevealStartRadius, worldRevealEndRadius, easedTime);
            Vector3 pillarViewportPosition = sourceCamera.WorldToViewportPoint(revealOrigin);
            float distanceToPillar = Mathf.Max(0.01f, Vector3.Distance(sourceCamera.transform.position, revealOrigin));
            float verticalProjectionScale = Mathf.Max(
                0.001f,
                2f * distanceToPillar * Mathf.Tan(sourceCamera.fieldOfView * Mathf.Deg2Rad * 0.5f));

            // The prior depth-reconstructed version depended on which of the two
            // cameras last wrote Unity's depth texture. Projecting the sphere's
            // centre and radius directly is robust across the normal, lens, and
            // render-texture cameras while preserving the intended expanding sphere.
            float screenRadius = radius / verticalProjectionScale;
            float screenFeather = worldRevealEdgeFeather / verticalProjectionScale;
            worldRevealMaterial.SetVector("_RevealOriginRadius", new Vector4(
                revealOrigin.x, revealOrigin.y, revealOrigin.z, radius));
            worldRevealMaterial.SetFloat("_RevealEdgeFeather", worldRevealEdgeFeather);
            worldRevealMaterial.SetVector("_RevealScreenCenterRadius", new Vector4(
                pillarViewportPosition.x, pillarViewportPosition.y, screenRadius, pillarViewportPosition.z));
            worldRevealMaterial.SetFloat("_RevealScreenFeather", screenFeather);
        }

        private void SetPuzzleMode(PuzzleMode mode)
        {
            if (currentPuzzleMode == mode) return;
            SetActive(false);
            currentPuzzleMode = mode;
            if (mode == PuzzleMode.ValueSearch)
                currentDevViewMode = DevViewMode.PuzzleView;
            ApplyPuzzleMode();
            PersistDeveloperSettings();
        }

        private void ApplyPuzzleMode()
        {
            bool valueSearch = !worldRestored && currentPuzzleMode == PuzzleMode.ValueSearch;
            ApplyValueSearchPalette(valueSearch);
            RefreshStoryObjective();
            ApplyDevViewMode();
            ApplyPlayerControlMode();
            RefreshModeSelector();
        }

        private void CreateDeveloperPanel()
        {
            if (!enableDeveloperPanel || canvasRect == null) return;
            EnsureEventSystem();

            GameObject selectorObject = new GameObject("DeveloperViewPanel", typeof(RectTransform));
            developerPanelRoot = selectorObject;
            RectTransform selector = selectorObject.GetComponent<RectTransform>();
            selector.SetParent(canvasRect, false);
            selector.anchorMin = selector.anchorMax = new Vector2(1f, 1f);
            selector.pivot = new Vector2(1f, 1f);
            selector.anchoredPosition = new Vector2(-modeSelectorMargin.x, -modeSelectorMargin.y);
            float groupWidth = modeButtonSize.x * 2f + modeButtonGap + selectorGroupPadding * 2f;
            float groupHeight = selectorGroupPadding * 3f + selectorGroupLabelHeight + modeButtonSize.y;
            float levelSequenceGroupHeight = selectorGroupPadding * 3f + selectorGroupLabelHeight +
                                             modeButtonSize.y * 2f + modeButtonGap;
            float fontGroupHeight = selectorGroupPadding * 3f + selectorGroupLabelHeight +
                                    modeButtonSize.y * 2f + modeButtonGap;
            float telemetryGroupHeight = selectorGroupPadding * 3f + selectorGroupLabelHeight +
                                         telemetryGuideContentHeight;
            float puzzleModeGroupOffset = showLevelSequenceInDeveloperPanel
                ? levelSequenceGroupHeight + modeButtonGap
                : 0f;
            float devViewGroupOffset = puzzleModeGroupOffset + groupHeight + modeButtonGap;
            float interactionGroupOffset = devViewGroupOffset + groupHeight + modeButtonGap;
            float fontGroupOffset = (playerInteraction != null ? interactionGroupOffset + groupHeight : devViewGroupOffset + groupHeight) + modeButtonGap;
            float telemetryGroupOffset = fontGroupOffset + fontGroupHeight + modeButtonGap;
            float panelHeight = showTelemetryGuideInDeveloperPanel
                ? telemetryGroupOffset + telemetryGroupHeight
                : fontGroupOffset + fontGroupHeight;
            selector.sizeDelta = new Vector2(groupWidth, panelHeight);
            selector.SetAsLastSibling();

            Font font = canvas.GetComponentInChildren<Text>(true)?.font;
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            if (showLevelSequenceInDeveloperPanel)
            {
                RectTransform levelSequenceGroup = CreateSelectorGroup(
                    selector, "LEVEL SEQUENCE", 0f, groupWidth, levelSequenceGroupHeight, font);
                forestLevelButton = CreateModeButton(levelSequenceGroup, ForestLevelButtonLabel, selectorGroupPadding, font);
                oceanLevelButton = CreateModeButton(
                    levelSequenceGroup, OceanLevelButtonLabel,
                    selectorGroupPadding + modeButtonSize.x + modeButtonGap, font);
                villageLevelButton = CreateModeButton(levelSequenceGroup, VillageLevelButtonLabel, selectorGroupPadding, font, 1);
                forestLevelButton.onClick.AddListener(() => LoadDevelopmentLevel(forestTutorialSceneName));
                oceanLevelButton.onClick.AddListener(() => LoadDevelopmentLevel(oceanSceneName));
                villageLevelButton.onClick.AddListener(() => LoadDevelopmentLevel(villageEndingSceneName));
            }

            RectTransform puzzleModeGroup = CreateSelectorGroup(
                selector, "PUZZLE MODE", puzzleModeGroupOffset, groupWidth, groupHeight, font);
            colorSearchButton = CreateModeButton(puzzleModeGroup, ColorSearchButtonLabel, selectorGroupPadding, font);
            valueSearchButton = CreateModeButton(
                puzzleModeGroup,
                ValueSearchButtonLabel,
                selectorGroupPadding + modeButtonSize.x + modeButtonGap,
                font);
            colorSearchButton.onClick.AddListener(() => SetPuzzleMode(PuzzleMode.ColorSearch));
            valueSearchButton.onClick.AddListener(() => SetPuzzleMode(PuzzleMode.ValueSearch));

            RectTransform devViewGroup = CreateSelectorGroup(
                selector,
                "DEVELOPER VIEW",
                devViewGroupOffset,
                groupWidth,
                groupHeight,
                font);
            puzzleViewButton = CreateModeButton(devViewGroup, GreyscaleButtonLabel, selectorGroupPadding, font);
            fullColorButton = CreateModeButton(devViewGroup, FullColorButtonLabel, selectorGroupPadding + modeButtonSize.x + modeButtonGap, font);
            puzzleViewButton.onClick.AddListener(() => SetDevViewMode(DevViewMode.PuzzleView));
            fullColorButton.onClick.AddListener(() => SetDevViewMode(DevViewMode.FullColor));

            if (playerInteraction != null)
            {
                RectTransform interactionCueGroup = CreateSelectorGroup(
                    selector,
                    "INTERACTION CUE",
                    interactionGroupOffset,
                    groupWidth,
                    groupHeight,
                    font);
                backGlowButton = CreateModeButton(
                    interactionCueGroup,
                    BackGlowButtonLabel,
                    selectorGroupPadding,
                    font);
                centerTextButton = CreateModeButton(
                    interactionCueGroup,
                    CenterTextButtonLabel,
                    selectorGroupPadding + modeButtonSize.x + modeButtonGap,
                    font);
                backGlowButton.onClick.AddListener(() =>
                    SetInteractionCueMode(PlayerInteraction.InteractionCueMode.BackGlow));
                centerTextButton.onClick.AddListener(() =>
                    SetInteractionCueMode(PlayerInteraction.InteractionCueMode.CenterText));
            }

            RectTransform fontGroup = CreateSelectorGroup(
                selector,
                "DISPLAY FONT",
                fontGroupOffset,
                groupWidth,
                fontGroupHeight,
                font);
            AddDisplayFontButton(fontGroup, DisplayFont.Chilanka, ChilankaFontLabel, 0, 0, font);
            AddDisplayFontButton(fontGroup, DisplayFont.ComingSoon, ComingSoonFontLabel, 1, 0, font);
            AddDisplayFontButton(fontGroup, DisplayFont.Margarine, MargarineFontLabel, 0, 1, font);
            AddDisplayFontButton(fontGroup, DisplayFont.NerkoOne, NerkoOneFontLabel, 1, 1, font);
            if (showTelemetryGuideInDeveloperPanel)
            {
                RectTransform telemetryGroup = CreateSelectorGroup(
                    selector, "TELEMETRY", telemetryGroupOffset, groupWidth, telemetryGroupHeight, font);
                CreateTelemetryGuideText(telemetryGroup, groupWidth, font);
            }
            RefreshModeSelector();
            SetDeveloperPanelOpen(false);
        }

        private void CreateStoryObjectiveCard()
        {
            if (!showStoryObjective || canvasRect == null || canvas == null) return;

            storyObjectiveRoot = new GameObject(
                "StoryObjectiveHUD",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Shadow));
            RectTransform cardRect = storyObjectiveRoot.GetComponent<RectTransform>();
            cardRect.SetParent(canvasRect, false);
            cardRect.anchorMin = cardRect.anchorMax = new Vector2(0f, 1f);
            cardRect.pivot = new Vector2(0f, 1f);
            cardRect.anchoredPosition = new Vector2(storyCardMargin.x, -storyCardMargin.y);
            cardRect.sizeDelta = storyCardSize;

            Image cardImage = storyObjectiveRoot.GetComponent<Image>();
            cardImage.sprite = null;
            cardImage.type = Image.Type.Simple;
            cardImage.color = storyCardFillColor;
            cardImage.raycastTarget = false;

            Shadow shadow = storyObjectiveRoot.GetComponent<Shadow>();
            shadow.effectColor = storyCardShadowColor;
            shadow.effectDistance = storyCardShadowOffset;
            shadow.useGraphicAlpha = true;

            GameObject textObject = new GameObject(
                "StoryObjectiveText",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Text));
            RectTransform textRect = textObject.GetComponent<RectTransform>();
            textRect.SetParent(cardRect, false);
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = storyCardPadding;
            textRect.offsetMax = -storyCardPadding;

            storyObjectiveLabel = textObject.GetComponent<Text>();
            storyObjectiveLabel.font = canvas.GetComponentInChildren<Text>(true)?.font;
            if (storyObjectiveLabel.font == null)
                storyObjectiveLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            storyObjectiveLabel.fontSize = storyFontSize;
            storyObjectiveLabel.alignment = TextAnchor.MiddleLeft;
            storyObjectiveLabel.color = storyTextColor;
            storyObjectiveLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
            storyObjectiveLabel.verticalOverflow = VerticalWrapMode.Truncate;
            storyObjectiveLabel.raycastTarget = false;
            RefreshStoryObjective();
        }

        private void RefreshStoryObjective()
        {
            if (storyObjectiveLabel == null) return;
            storyObjectiveLabel.text = currentPuzzleMode == PuzzleMode.ValueSearch
                ? valueSearchObjectiveText
                : colorSearchObjectiveText;
        }

        private void CreateValueSearchOverlay()
        {
            if (canvasRect == null) return;
            Shader shader = Resources.Load<Shader>("Shaders/ValueGreyscaleUI");
            if (shader == null)
            {
                Debug.LogError("Value Search requires Resources/Shaders/ValueGreyscaleUI.shader.", this);
                return;
            }

            valueSearchGreyscaleMaterial = new Material(shader)
            {
                name = "Runtime Value Search Greyscale",
                hideFlags = HideFlags.DontSave
            };
            valueSearchOverlayRoot = new GameObject(
                "ValueSearchGreyscaleView",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(RawImage));
            RectTransform overlayRect = valueSearchOverlayRoot.GetComponent<RectTransform>();
            overlayRect.SetParent(canvasRect, false);
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.offsetMin = Vector2.zero;
            overlayRect.offsetMax = Vector2.zero;
            overlayRect.SetAsFirstSibling();

            valueSearchOverlay = valueSearchOverlayRoot.GetComponent<RawImage>();
            valueSearchOverlay.texture = revealTexture;
            valueSearchOverlay.material = valueSearchGreyscaleMaterial;
            valueSearchOverlay.color = Color.white;
            valueSearchOverlay.raycastTarget = false;
            valueSearchOverlayRoot.SetActive(false);
        }

        private void ApplyValueSearchPalette(bool enabled)
        {
            if (!enabled)
            {
                RestoreValueSearchPalette();
                return;
            }
            if (valueOriginalMaterials.Count > 0) return;

            Renderer[] renderers = FindObjectsByType<Renderer>(FindObjectsInactive.Include);
            for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
            {
                Renderer candidate = renderers[rendererIndex];
                int layerBit = 1 << candidate.gameObject.layer;
                if ((originalColorCameraCullingMask & layerBit) == 0) continue;

                Material[] originals = candidate.sharedMaterials;
                Material[] replacements = new Material[originals.Length];
                bool replacedAny = false;
                for (int materialIndex = 0; materialIndex < originals.Length; materialIndex++)
                {
                    Material original = originals[materialIndex];
                    if (original == null)
                    {
                        replacements[materialIndex] = null;
                        continue;
                    }

                    Material replacement = new Material(original)
                    {
                        name = $"{original.name} (Value Search)",
                        hideFlags = HideFlags.DontSave
                    };
                    SetMaterialColor(replacement, ChooseValuePaletteColor(original.name));
                    replacements[materialIndex] = replacement;
                    valueRuntimeMaterials.Add(replacement);
                    replacedAny = true;
                }

                if (!replacedAny) continue;
                valueOriginalMaterials.Add(candidate, originals);
                candidate.sharedMaterials = replacements;
            }
        }

        private void RestoreValueSearchPalette()
        {
            foreach (KeyValuePair<Renderer, Material[]> pair in valueOriginalMaterials)
            {
                if (pair.Key != null) pair.Key.sharedMaterials = pair.Value;
            }
            valueOriginalMaterials.Clear();

            for (int i = 0; i < valueRuntimeMaterials.Count; i++)
            {
                if (valueRuntimeMaterials[i] != null) Destroy(valueRuntimeMaterials[i]);
            }
            valueRuntimeMaterials.Clear();
        }

        private Color ChooseValuePaletteColor(string materialName)
        {
            string normalized = materialName.ToLowerInvariant();
            if (normalized.Contains("red")) return valueTargetRed;
            if (normalized.Contains("deepblue") || normalized.Contains("blue")) return valueDarkBlue;
            if (normalized.Contains("purple")) return valuePurple;
            if (normalized.Contains("kelp") || normalized.Contains("green")) return valueGreen;
            if (normalized.Contains("orange") ||
                normalized.Contains("gold") ||
                normalized.Contains("yellow") ||
                normalized.Contains("sand")) return valueOrange;
            if (normalized.Contains("pink")) return valueGreen;
            return valueCyan;
        }

        private static void SetMaterialColor(Material material, Color color)
        {
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            else if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        }

        private void SetInteractionCueMode(PlayerInteraction.InteractionCueMode mode)
        {
            if (playerInteraction == null) return;
            playerInteraction.SetInteractionCueMode(mode);
            PersistDeveloperSettings();
            RefreshModeSelector();
        }

        private void SetDisplayFont(DisplayFont displayFont)
        {
            currentDisplayFont = displayFont;
            ApplyDisplayFont(displayFont);
            PersistDeveloperSettings();
            RefreshModeSelector();
        }

        private void ApplyDisplayFont(DisplayFont displayFont)
        {
            Font font = LoadDisplayFont(displayFont);
            if (font == null || canvas == null) return;

            Text[] labels = canvas.GetComponentsInChildren<Text>(true);
            for (int index = 0; index < labels.Length; index++)
                labels[index].font = font;
        }

        private static Font LoadDisplayFont(DisplayFont displayFont)
        {
            string resourcePath = displayFont switch
            {
                DisplayFont.Chilanka => "Fonts/Chilanka-Regular",
                DisplayFont.ComingSoon => "Fonts/ComingSoon-Regular",
                DisplayFont.Margarine => "Fonts/Margarine-Regular",
                DisplayFont.NerkoOne => "Fonts/NerkoOne-Regular",
                _ => null
            };
            return resourcePath != null
                ? Resources.Load<Font>(resourcePath)
                : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        private void PersistDeveloperSettings()
        {
            persistedDevViewMode = currentDevViewMode;
            persistedPuzzleMode = currentPuzzleMode;
            persistedDisplayFont = currentDisplayFont;
            if (playerInteraction != null)
                persistedInteractionCueMode = playerInteraction.CurrentInteractionCueMode;
            hasPersistedDeveloperSettings = true;
        }

        private void SetDeveloperPanelOpen(bool open)
        {
            developerPanelOpen = open && developerPanelRoot != null;
            if (developerPanelRoot != null) developerPanelRoot.SetActive(developerPanelOpen);
            if (developerPanelOpen) SetActive(false);

            if (!unlockCursorWhilePanelOpen) return;
            Cursor.lockState = developerPanelOpen ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = developerPanelOpen;
        }

        public void PrepareForPause()
        {
            SetDeveloperPanelOpen(false);
            SetActive(false);
        }

        private void LoadDevelopmentLevel(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName)) return;
            if (SceneManager.GetActiveScene().name == sceneName)
            {
                SetDeveloperPanelOpen(false);
                return;
            }

            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogWarning($"Development level '{sceneName}' is not in the build settings.", this);
                return;
            }

            SceneManager.LoadScene(sceneName);
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

        private Button CreateModeButton(RectTransform parent, string label, float offsetX, Font font, int row = 0)
        {
            GameObject buttonObject = new GameObject(label, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
            buttonRect.SetParent(parent, false);
            buttonRect.anchorMin = buttonRect.anchorMax = new Vector2(0f, 1f);
            buttonRect.pivot = new Vector2(0f, 1f);
            float buttonOffsetY = selectorGroupPadding * 2f + selectorGroupLabelHeight +
                                  row * (modeButtonSize.y + modeButtonGap);
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

        private void CreateTelemetryGuideText(RectTransform parent, float groupWidth, Font font)
        {
            GameObject textObject = new GameObject(
                "TelemetryGuide", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            RectTransform textRect = textObject.GetComponent<RectTransform>();
            textRect.SetParent(parent, false);
            textRect.anchorMin = textRect.anchorMax = new Vector2(0f, 1f);
            textRect.pivot = new Vector2(0f, 1f);
            textRect.anchoredPosition = new Vector2(
                selectorGroupPadding,
                -(selectorGroupPadding * 2f + selectorGroupLabelHeight));
            textRect.sizeDelta = new Vector2(
                groupWidth - selectorGroupPadding * 2f,
                telemetryGuideContentHeight);

            Text text = textObject.GetComponent<Text>();
            text.text = telemetryGuideText;
            text.font = font;
            text.fontSize = telemetryGuideFontSize;
            text.alignment = TextAnchor.UpperLeft;
            text.color = modeButtonTextColor;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
        }

        private void AddDisplayFontButton(
            RectTransform parent,
            DisplayFont displayFont,
            string label,
            int column,
            int row,
            Font font)
        {
            float offsetX = selectorGroupPadding + column * (modeButtonSize.x + modeButtonGap);
            Button button = CreateModeButton(parent, label, offsetX, font, row);
            button.onClick.AddListener(() => SetDisplayFont(displayFont));
            displayFontButtons.Add(displayFont, button);
        }

        private void RefreshModeSelector()
        {
            if (colorSearchButton != null)
                colorSearchButton.targetGraphic.color = currentPuzzleMode == PuzzleMode.ColorSearch
                    ? selectedModeColor
                    : unselectedModeColor;
            if (valueSearchButton != null)
                valueSearchButton.targetGraphic.color = currentPuzzleMode == PuzzleMode.ValueSearch
                    ? selectedModeColor
                    : unselectedModeColor;
            if (puzzleViewButton != null)
                puzzleViewButton.targetGraphic.color = currentDevViewMode == DevViewMode.PuzzleView ? selectedModeColor : unselectedModeColor;
            if (fullColorButton != null)
            {
                fullColorButton.interactable = currentPuzzleMode == PuzzleMode.ColorSearch;
                fullColorButton.targetGraphic.color = currentDevViewMode == DevViewMode.FullColor ? selectedModeColor : unselectedModeColor;
            }
            if (backGlowButton != null && playerInteraction != null)
                backGlowButton.targetGraphic.color = playerInteraction.CurrentInteractionCueMode == PlayerInteraction.InteractionCueMode.BackGlow
                    ? selectedModeColor
                    : unselectedModeColor;
            if (centerTextButton != null && playerInteraction != null)
                centerTextButton.targetGraphic.color = playerInteraction.CurrentInteractionCueMode == PlayerInteraction.InteractionCueMode.CenterText
                    ? selectedModeColor
                    : unselectedModeColor;
            string activeSceneName = SceneManager.GetActiveScene().name;
            if (forestLevelButton != null)
                forestLevelButton.targetGraphic.color = activeSceneName == forestTutorialSceneName
                    ? selectedModeColor : unselectedModeColor;
            if (oceanLevelButton != null)
                oceanLevelButton.targetGraphic.color = activeSceneName == oceanSceneName
                    ? selectedModeColor : unselectedModeColor;
            if (villageLevelButton != null)
                villageLevelButton.targetGraphic.color = activeSceneName == villageEndingSceneName
                    ? selectedModeColor : unselectedModeColor;
            foreach (KeyValuePair<DisplayFont, Button> fontButton in displayFontButtons)
                fontButton.Value.targetGraphic.color = fontButton.Key == currentDisplayFont
                    ? selectedModeColor
                    : unselectedModeColor;
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

            // The authored ColorReveal layer is a complete coloured proxy world. Rendering it
            // alone prevents any grey source meshes from bleeding into the lens, while the
            // proxies retain normal URP/Lit depth testing and therefore occlude one another.
            colorCamera.cullingMask = originalColorCameraCullingMask;
        }
    }
}
