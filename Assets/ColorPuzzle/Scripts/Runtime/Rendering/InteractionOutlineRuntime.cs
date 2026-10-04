using UnityEngine;

namespace ColorPuzzle.Rendering
{
    /// <summary>
    /// Small runtime bridge between interaction logic and the URP renderer feature.
    /// The renderer feature reads this state independently for every camera.
    /// </summary>
    public static class InteractionOutlineRuntime
    {
        public static Renderer GreyscaleTargetRenderer { get; private set; }
        public static Renderer ColorTargetRenderer { get; private set; }
        public static Camera ColorCamera { get; private set; }
        public static Color GreyscaleColor { get; private set; } = new Color(0f, 0f, 0f, 0.9f);
        public static Color ColorViewColor { get; private set; } = new Color(1f, 1f, 1f, 0.9f);
        public static float Opacity { get; private set; }
        public static int WidthPixels { get; private set; } = 18;
        public static float Softness { get; private set; } = 0.8f;
        public static float ColorViewScale { get; private set; } = 0.5f;

        public static bool HasVisibleTarget => GreyscaleTargetRenderer != null && Opacity > 0.001f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetForPlaySession()
        {
            GreyscaleTargetRenderer = null;
            ColorTargetRenderer = null;
            ColorCamera = null;
            Opacity = 0f;
            WidthPixels = 18;
            Softness = 0.8f;
            ColorViewScale = 0.5f;
        }

        public static void SetColorCamera(Camera camera)
        {
            ColorCamera = camera;
        }

        public static void Show(
            Renderer greyscaleTargetRenderer,
            Renderer colorTargetRenderer,
            float opacity,
            float widthPixels,
            float softness,
            Color greyscaleColor,
            Color colorViewColor,
            float colorViewScale)
        {
            GreyscaleTargetRenderer = greyscaleTargetRenderer;
            ColorTargetRenderer = colorTargetRenderer != null ? colorTargetRenderer : greyscaleTargetRenderer;
            Opacity = Mathf.Clamp01(opacity);
            WidthPixels = Mathf.Clamp(Mathf.RoundToInt(widthPixels), 1, 60);
            Softness = Mathf.Clamp01(softness);
            GreyscaleColor = greyscaleColor;
            ColorViewColor = colorViewColor;
            ColorViewScale = Mathf.Clamp01(colorViewScale);
        }

        public static void Hide()
        {
            GreyscaleTargetRenderer = null;
            ColorTargetRenderer = null;
            Opacity = 0f;
        }

        public static bool IsColorCamera(Camera camera)
        {
            return camera != null && camera == ColorCamera;
        }
    }
}
