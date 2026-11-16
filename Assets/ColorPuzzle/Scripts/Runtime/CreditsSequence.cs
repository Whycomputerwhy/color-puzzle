using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace ColorPuzzle
{
    /// <summary>Shared, runtime-built credits presentation for the menu and ending.</summary>
    public sealed class CreditsSequence : MonoBehaviour
    {
        private const int CreditsSortingOrder = 2000;
        public static bool IsPlaying { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRuntimeState() => IsPlaying = false;

        public static void PlayRollingSequence(
            MonoBehaviour host, string heading, string body, string closingLine,
            float rollDuration, float holdDuration, bool showBackdrop, Action onComplete = null)
        {
            if (host == null) return;
            if (IsPlaying) return;
            IsPlaying = true;
            GameObject sequenceObject = new GameObject("Credits Sequence");
            CreditsSequence sequence = sequenceObject.AddComponent<CreditsSequence>();
            sequence.StartCoroutine(sequence.RollCredits(
                heading,
                body,
                closingLine,
                Mathf.Max(1f, rollDuration),
                Mathf.Max(0f, holdDuration),
                showBackdrop,
                onComplete));
        }

        private IEnumerator RollCredits(
            string heading,
            string body,
            string closingLine,
            float rollDuration,
            float holdDuration,
            bool showBackdrop,
            Action onComplete)
        {
            Font font = LoadDisplayFont();
            GameObject canvasObject = new GameObject("EndCreditsCanvas", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = CreditsSortingOrder;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            RectTransform container;
            if (showBackdrop)
            {
                container = CreatePanel(canvasObject.transform, "CreditsBackdrop", new Color(0.005f, 0.025f, 0.05f, 0.94f));
            }
            else
            {
                GameObject containerObject = new GameObject("CreditsOverWorld", typeof(RectTransform));
                containerObject.transform.SetParent(canvasObject.transform, false);
                container = containerObject.GetComponent<RectTransform>();
            }
            Stretch(container);
            string formattedBody = FormatCreditBody(body);
            string fullText = $"{heading}\n\n{formattedBody}";
            Color creditsColor = showBackdrop ? Color.white : Color.black;
            Text credits = CreateText(
                container,
                "RollingCredits",
                fullText,
                font,
                35,
                creditsColor,
                Vector2.zero, new Vector2(1180f, 1300f), TextAnchor.MiddleCenter);
            RectTransform creditsRect = credits.rectTransform;
            float offscreenDistance = (creditsRect.sizeDelta.y * 0.5f) + 640f;
            creditsRect.anchoredPosition = new Vector2(0f, -offscreenDistance);

            float elapsed = 0f;
            while (elapsed < rollDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / rollDuration);
                creditsRect.anchoredPosition = new Vector2(
                    0f,
                    Mathf.Lerp(-offscreenDistance, offscreenDistance, progress));
                yield return null;
            }

            // The menu version has no closing line and returns as soon as its text
            // has entirely left the screen. The final version gives its thank-you a
            // separate, readable beat at centre screen.
            if (!string.IsNullOrWhiteSpace(closingLine))
            {
                Text thankYou = CreateText(
                    container,
                    "ThankYou",
                    closingLine,
                    font,
                    42,
                    creditsColor,
                    Vector2.zero,
                    new Vector2(1200f, 180f),
                    TextAnchor.MiddleCenter);
                yield return new WaitForSecondsRealtime(holdDuration);
                Destroy(thankYou.gameObject);
            }
            Destroy(canvasObject);
            IsPlaying = false;
            onComplete?.Invoke();
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            IsPlaying = false;
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
            text.supportRichText = true;
            text.raycastTarget = false;
            return text;
        }

        // Credits are authored as simple role/name pairs. Uppercase role lines become
        // more prominent automatically, so both menu and ending use identical styling.
        private static string FormatCreditBody(string body)
        {
            if (string.IsNullOrEmpty(body)) return string.Empty;

            string[] lines = body.Replace("\r\n", "\n").Split('\n');
            StringBuilder formatted = new StringBuilder(body.Length + 96);
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                bool isRole = IsRoleHeading(line);
                if (isRole) formatted.Append("<size=46>");
                else if (!string.IsNullOrWhiteSpace(line)) formatted.Append("<size=30>");
                formatted.Append(line);
                if (isRole || !string.IsNullOrWhiteSpace(line)) formatted.Append("</size>");
                if (i < lines.Length - 1) formatted.Append('\n');
            }
            return formatted.ToString();
        }

        private static bool IsRoleHeading(string value)
        {
            string trimmed = value.Trim();
            return trimmed.Length > 0 &&
                   trimmed == trimmed.ToUpperInvariant() &&
                   trimmed != trimmed.ToLowerInvariant();
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
    }
}
