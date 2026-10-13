using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ColorPuzzle
{
    public sealed class PauseMenuController : MonoBehaviour
    {
        public static bool IsPaused { get; private set; }

        // CENTRALIZED PAUSE-MENU PARAMETERS (all game-feel and presentation tuning is Inspector-visible)
        [Header("Navigation")]
        [SerializeField] private Key pauseKey = Key.Escape;
        [SerializeField] private string menuSceneName = "ColorPuzzleStartMenu";

        [Header("Copy")]
        [SerializeField] private string pauseTitle = "PAUSED";
        [SerializeField, TextArea(7, 12)] private string controlsText =
            "WASD  Move\nSPACE  Jump\nRMB  Magnifier\nLMB  Pick up\nQ  Drop / place\nR  Reset level\nENTER  Developer panel\nESC  Pause";
        [SerializeField] private string resumeLabel = "RESUME";
        [SerializeField] private string quitLabel = "QUIT TO MENU";
        [SerializeField] private string volumeLabel = "VOLUME";

        [Header("Layout")]
        [SerializeField] private Vector2 panelSize = new Vector2(600f, 700f);
        [SerializeField] private Vector2 buttonSize = new Vector2(330f, 58f);
        [SerializeField] private int titleFontSize = 48;
        [SerializeField] private int bodyFontSize = 23;

        [Header("Palette")]
        [SerializeField] private Color overlayColor = new Color(0f, 0.035f, 0.06f, 0.72f);
        [SerializeField] private Color panelColor = new Color(0.04f, 0.15f, 0.22f, 0.98f);
        [SerializeField] private Color textColor = Color.white;
        [SerializeField] private Color buttonColor = new Color(0.94f, 0.94f, 0.88f, 1f);
        [SerializeField] private Color buttonTextColor = new Color(0.04f, 0.12f, 0.18f, 1f);
        [SerializeField] private Color sliderTrackColor = new Color(0.15f, 0.28f, 0.34f, 1f);
        [SerializeField] private Color sliderFillColor = new Color(0.4f, 0.82f, 0.9f, 1f);

        [Header("Audio")]
        [SerializeField, Range(0f, 1f)] private float defaultMasterVolume = 0.75f;

        private GameObject menuRoot;
        private Text volumeValueText;
        private float timeScaleBeforePause = 1f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            IsPaused = false;
        }

        private void Awake()
        {
            AudioListener.volume = GameAudioSettings.LoadMasterVolume(defaultMasterVolume);
            BuildInterface();
            menuRoot.SetActive(false);
        }

        private void Update()
        {
            if (Keyboard.current == null || pauseKey == Key.None) return;
            if (!Keyboard.current[pauseKey].wasPressedThisFrame) return;
            if (IsPaused) ResumeGame();
            else PauseGame();
        }

        private void OnDisable()
        {
            if (IsPaused) RestoreRuntimeState(false);
        }

        public void PauseGame()
        {
            if (IsPaused) return;
            IsPaused = true;
            timeScaleBeforePause = Time.timeScale > 0f ? Time.timeScale : 1f;

            MagnifierView magnifier = GetComponent<MagnifierView>();
            if (magnifier != null) magnifier.PrepareForPause();

            Time.timeScale = 0f;
            AudioListener.pause = true;
            menuRoot.SetActive(true);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public void ResumeGame()
        {
            if (!IsPaused) return;
            RestoreRuntimeState(true);
        }

        public void QuitToMenu()
        {
            RestoreRuntimeState(false);
            if (!Application.CanStreamedLevelBeLoaded(menuSceneName))
            {
                Debug.LogError($"Pause menu cannot load '{menuSceneName}'. Add it to Build Settings.", this);
                return;
            }

            SceneManager.LoadScene(menuSceneName);
        }

        private void RestoreRuntimeState(bool lockCursor)
        {
            IsPaused = false;
            Time.timeScale = timeScaleBeforePause > 0f ? timeScaleBeforePause : 1f;
            AudioListener.pause = false;
            if (menuRoot != null) menuRoot.SetActive(false);
            Cursor.lockState = lockCursor ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !lockCursor;
        }

        private void SetVolume(float value)
        {
            GameAudioSettings.SetMasterVolume(value);
            if (volumeValueText != null) volumeValueText.text = $"{Mathf.RoundToInt(value * 100f)}%";
        }

        private void BuildInterface()
        {
            EnsureEventSystem();
            Font font = LoadDisplayFont();

            menuRoot = new GameObject("PauseMenuCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            menuRoot.transform.SetParent(transform, false);
            Canvas canvas = menuRoot.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 600;
            CanvasScaler scaler = menuRoot.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            RectTransform overlay = CreatePanel(menuRoot.transform, "Overlay", overlayColor);
            Stretch(overlay);
            RectTransform panel = CreatePanel(overlay, "PausePanel", panelColor);
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = panelSize;

            CreateText(panel, "Title", pauseTitle, font, titleFontSize, textColor,
                new Vector2(0f, 285f), new Vector2(520f, 75f), TextAnchor.MiddleCenter);
            CreateText(panel, "ControlsHeading", "CONTROLS", font, bodyFontSize, textColor,
                new Vector2(0f, 225f), new Vector2(470f, 42f), TextAnchor.MiddleLeft);
            CreateText(panel, "Controls", controlsText, font, bodyFontSize, textColor,
                new Vector2(0f, 65f), new Vector2(470f, 285f), TextAnchor.UpperLeft);
            CreateText(panel, "VolumeHeading", volumeLabel, font, bodyFontSize, textColor,
                new Vector2(-170f, -117f), new Vector2(130f, 42f), TextAnchor.MiddleLeft);
            volumeValueText = CreateText(panel, "VolumeValue", string.Empty, font, bodyFontSize, textColor,
                new Vector2(205f, -117f), new Vector2(90f, 42f), TextAnchor.MiddleRight);

            Slider slider = CreateSlider(panel, new Vector2(0f, -162f), new Vector2(470f, 30f));
            float volume = GameAudioSettings.LoadMasterVolume(defaultMasterVolume);
            slider.SetValueWithoutNotify(volume);
            SetVolume(volume);
            slider.onValueChanged.AddListener(SetVolume);

            CreateButton(panel, "ResumeButton", resumeLabel, font, new Vector2(0f, -245f), ResumeGame);
            CreateButton(panel, "QuitButton", quitLabel, font, new Vector2(0f, -315f), QuitToMenu);
        }

        private Slider CreateSlider(Transform parent, Vector2 position, Vector2 size)
        {
            RectTransform track = CreatePanel(parent, "VolumeSlider", sliderTrackColor);
            track.anchorMin = track.anchorMax = track.pivot = new Vector2(0.5f, 0.5f);
            track.anchoredPosition = position;
            track.sizeDelta = size;

            RectTransform fillArea = CreateRect(track, "FillArea");
            fillArea.anchorMin = Vector2.zero;
            fillArea.anchorMax = Vector2.one;
            fillArea.offsetMin = new Vector2(8f, 7f);
            fillArea.offsetMax = new Vector2(-8f, -7f);
            RectTransform fill = CreatePanel(fillArea, "Fill", sliderFillColor);
            Stretch(fill);

            RectTransform handleArea = CreateRect(track, "HandleArea");
            handleArea.anchorMin = Vector2.zero;
            handleArea.anchorMax = Vector2.one;
            handleArea.offsetMin = new Vector2(10f, 0f);
            handleArea.offsetMax = new Vector2(-10f, 0f);
            RectTransform handle = CreatePanel(handleArea, "Handle", buttonColor);
            handle.anchorMin = handle.anchorMax = new Vector2(0.5f, 0.5f);
            handle.sizeDelta = new Vector2(22f, 42f);

            Slider slider = track.gameObject.AddComponent<Slider>();
            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = handle.GetComponent<Image>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.wholeNumbers = false;
            slider.direction = Slider.Direction.LeftToRight;
            return slider;
        }

        private Button CreateButton(Transform parent, string name, string label, Font font,
            Vector2 position, UnityEngine.Events.UnityAction action)
        {
            RectTransform rect = CreatePanel(parent, name, buttonColor);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = buttonSize;
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = rect.GetComponent<Image>();
            button.onClick.AddListener(action);
            CreateText(rect, "Label", label, font, bodyFontSize, buttonTextColor, Vector2.zero,
                buttonSize, TextAnchor.MiddleCenter);
            return button;
        }

        private static RectTransform CreateRect(Transform parent, string name)
        {
            GameObject instance = new GameObject(name, typeof(RectTransform));
            instance.transform.SetParent(parent, false);
            return instance.GetComponent<RectTransform>();
        }

        private static RectTransform CreatePanel(Transform parent, string name, Color color)
        {
            GameObject instance = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            instance.transform.SetParent(parent, false);
            instance.GetComponent<Image>().color = color;
            return instance.GetComponent<RectTransform>();
        }

        private static Text CreateText(Transform parent, string name, string value, Font font, int size,
            Color color, Vector2 position, Vector2 dimensions, TextAnchor alignment)
        {
            GameObject instance = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            instance.transform.SetParent(parent, false);
            RectTransform rect = instance.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = dimensions;
            Text text = instance.GetComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.color = color;
            text.alignment = alignment;
            text.text = value;
            text.raycastTarget = false;
            return text;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private static Font LoadDisplayFont()
        {
            return Resources.Load<Font>("Fonts/Chilanka-Regular") ??
                   Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            GameObject instance = new GameObject(
                "EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            instance.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
        }
    }
}
