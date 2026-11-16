using System.Collections.Generic;
using UnityEngine;

namespace ColorPuzzle.Rendering
{
    /// <summary>
    /// Small runtime bridge between interaction logic and the URP renderer feature.
    /// The renderer feature reads this state independently for every camera.
    /// </summary>
    public static class InteractionOutlineRuntime
    {
        private static readonly List<Renderer> greyscaleTargetRenderers = new List<Renderer>();
        private static readonly List<Renderer> colorTargetRenderers = new List<Renderer>();

        /// <summary>All meshes that make up the currently focused pickup in the normal view.</summary>
        public static IReadOnlyList<Renderer> GreyscaleTargetRenderers => greyscaleTargetRenderers;

        /// <summary>All meshes that make up the currently focused pickup in the colour view.</summary>
        public static IReadOnlyList<Renderer> ColorTargetRenderers => colorTargetRenderers;
        public static Camera ColorCamera { get; private set; }
        public static Color GreyscaleColor { get; private set; } = new Color(0f, 0f, 0f, 0.9f);
        public static Color ColorViewColor { get; private set; } = new Color(1f, 1f, 1f, 0.9f);
        public static float Opacity { get; private set; }
        public static int WidthPixels { get; private set; } = 18;
        public static float Softness { get; private set; } = 0.8f;
        public static float ColorViewScale { get; private set; } = 0.5f;

        public static bool HasVisibleTarget => greyscaleTargetRenderers.Count > 0 && Opacity > 0.001f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetForPlaySession()
        {
            greyscaleTargetRenderers.Clear();
            colorTargetRenderers.Clear();
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
            Show(
                new[] { greyscaleTargetRenderer },
                new[] { colorTargetRenderer != null ? colorTargetRenderer : greyscaleTargetRenderer },
                opacity,
                widthPixels,
                softness,
                greyscaleColor,
                colorViewColor,
                colorViewScale);
        }

        public static void Show(
            IReadOnlyList<Renderer> greyscaleRenderers,
            IReadOnlyList<Renderer> colorRenderers,
            float opacity,
            float widthPixels,
            float softness,
            Color greyscaleColor,
            Color colorViewColor,
            float colorViewScale)
        {
            CopyValidRenderers(greyscaleRenderers, greyscaleTargetRenderers);
            CopyValidRenderers(colorRenderers, colorTargetRenderers);
            if (colorTargetRenderers.Count == 0)
                CopyValidRenderers(greyscaleTargetRenderers, colorTargetRenderers);
            Opacity = Mathf.Clamp01(opacity);
            WidthPixels = Mathf.Clamp(Mathf.RoundToInt(widthPixels), 1, 60);
            Softness = Mathf.Clamp01(softness);
            GreyscaleColor = greyscaleColor;
            ColorViewColor = colorViewColor;
            ColorViewScale = Mathf.Clamp01(colorViewScale);
        }

        public static void Hide()
        {
            greyscaleTargetRenderers.Clear();
            colorTargetRenderers.Clear();
            Opacity = 0f;
        }

        private static void CopyValidRenderers(
            IReadOnlyList<Renderer> source,
            List<Renderer> destination)
        {
            destination.Clear();
            if (source == null) return;

            for (int i = 0; i < source.Count; i++)
            {
                Renderer renderer = source[i];
                if (renderer != null && !destination.Contains(renderer))
                    destination.Add(renderer);
            }
        }

        public static bool IsColorCamera(Camera camera)
        {
            return camera != null && camera == ColorCamera;
        }
    }
}
