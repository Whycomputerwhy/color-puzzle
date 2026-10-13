using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ColorPuzzle
{
    public sealed class StartMenuController : MonoBehaviour
    {
        // CENTRALIZED MENU PARAMETERS (all presentation and navigation tuning is Inspector-visible)
        [Header("Navigation")]
        [SerializeField] private string firstLevelSceneName = "ColorPuzzleForestTutorial";

        [Header("Copy")]
        [SerializeField] private string gameTitle = "COLOR PUZZLE";
        [SerializeField] private string subtitle = "Restore color to a world beneath the waves";
        [SerializeField] private string playLabel = "PLAY";
        [SerializeField] private string quitLabel = "QUIT";

        [Header("Layout")]
        [SerializeField] private Vector2 panelSize = new Vector2(620f, 470f);
        [SerializeField] private Vector2 buttonSize = new Vector2(300f, 64f);
        [SerializeField] private int titleFontSize = 64;
        [SerializeField] private int bodyFontSize = 25;

        [Header("Palette")]
        [SerializeField] private Color backgroundColor = new Color(0.025f, 0.12f, 0.22f, 1f);
        [SerializeField] private Color panelColor = new Color(0.04f, 0.19f, 0.29f, 0.94f);
        [SerializeField] private Color textColor = Color.white;
        [SerializeField] private Color buttonColor = new Color(0.94f, 0.94f, 0.88f, 1f);
        [SerializeField] private Color buttonTextColor = new Color(0.04f, 0.12f, 0.18f, 1f);

        [Header("Audio")]
        [SerializeField, Range(0f, 1f)] private float defaultMasterVolume = 0.75f;

        private void Awake()
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
            AudioListener.volume = GameAudioSettings.LoadMasterVolume(defaultMasterVolume);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            EnsureMenuCamera();
            BuildInterface();
        }

        private void EnsureMenuCamera()
        {
            if (Camera.main != null) return;

            GameObject cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            Camera menuCamera = cameraObject.GetComponent<Camera>();
            menuCamera.clearFlags = CameraClearFlags.SolidColor;
            menuCamera.backgroundColor = backgroundColor;
            menuCamera.cullingMask = 0;
        }

        private void StartGame()
        {
            if (!Application.CanStreamedLevelBeLoaded(firstLevelSceneName))
            {
                Debug.LogError($"Start menu cannot load '{firstLevelSceneName}'. Add it to Build Settings.", this);
                return;
            }

            SceneManager.LoadScene(firstLevelSceneName);
        }

        private void QuitGame()
        {
            Application.Quit();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }

        private void BuildInterface()
        {
            EnsureEventSystem();
            Font font = LoadDisplayFont();

            GameObject canvasObject = new GameObject("StartMenuCanvas", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            RectTransform background = CreatePanel(canvasObject.transform, "Background", backgroundColor);
            Stretch(background);

            RectTransform panel = CreatePanel(background, "MenuPanel", panelColor);
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = panelSize;

            CreateText(panel, "Title", gameTitle, font, titleFontSize, textColor,
                new Vector2(0f, 135f), new Vector2(panelSize.x - 60f, 100f), TextAnchor.MiddleCenter);
            CreateText(panel, "Subtitle", subtitle, font, bodyFontSize, textColor,
                new Vector2(0f, 72f), new Vector2(panelSize.x - 80f, 60f), TextAnchor.MiddleCenter);

            Button playButton = CreateButton(
                panel, "PlayButton", playLabel, font, new Vector2(0f, -35f), StartGame);
            CreateButton(panel, "QuitButton", quitLabel, font, new Vector2(0f, -125f), QuitGame);
            EventSystem.current.SetSelectedGameObject(playButton.gameObject);
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
            GameObject instance = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            instance.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            DontDestroyOnLoad(instance);
        }
    }
}
