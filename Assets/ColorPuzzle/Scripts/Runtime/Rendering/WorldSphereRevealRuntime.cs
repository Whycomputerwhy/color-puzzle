using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;
using ColorPuzzle;

namespace ColorPuzzle.Rendering
{
    /// <summary>
    /// Per-frame state for the final world-space material reveal. The normal camera
    /// keeps rendering the monochrome meshes while DualViewMaterial renders a second,
    /// colour-material mesh only where it falls inside this sphere.
    /// </summary>
    public static class WorldSphereRevealRuntime
    {
        private const int SphereRevealLayer = 10;
        private const int ColorRevealLayer = 8;
        public static bool IsActive { get; private set; }
        public static Camera SourceCamera { get; private set; }
        public static Vector4 OriginAndRadius { get; private set; }
        public static float EdgeFeather { get; private set; }
        private static int originalSourceCullingMask;
        private static bool sourceMaskSaved;
        private static readonly int EnabledId = Shader.PropertyToID("_ColorPuzzleSphereRevealEnabled");
        private static readonly int OriginRadiusId = Shader.PropertyToID("_ColorPuzzleSphereRevealOriginRadius");
        private static readonly int FeatherId = Shader.PropertyToID("_ColorPuzzleSphereRevealFeather");
        private static readonly int RevealAerialColorId = Shader.PropertyToID("_ColorPuzzleRevealAerialColor");
        private static readonly int RevealSaturationId = Shader.PropertyToID("_ColorPuzzleRevealSaturation");
        private static readonly int RevealBrightnessId = Shader.PropertyToID("_ColorPuzzleRevealBrightness");

        // VISIBLE VOLUME PARAMETERS
        private static readonly Color VolumeColor = new Color(0.10f, 0.52f, 0.95f, 0.16f);
        private const float VolumeFresnelStrength = 0.35f;
        private const float VolumeFresnelPower = 3f;
        private static GameObject revealVolume;
        private static Material revealVolumeMaterial;
        private static readonly List<LegacyProxyState> legacyColorProxies = new();
        private static readonly List<RendererVisibilityState> hiddenGreyscaleRenderers = new();
        private static readonly int SphereRevealProxyId = Shader.PropertyToID("_ColorPuzzleSphereRevealProxy");

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetForPlaySession()
        {
            Hide();
        }

        public static void Show(
            Camera sourceCamera,
            Vector3 origin,
            float radius,
            float edgeFeather,
            float saturation = 1f,
            float brightness = 1f)
        {
            SourceCamera = sourceCamera;
            OriginAndRadius = new Vector4(origin.x, origin.y, origin.z, Mathf.Max(0f, radius));
            EdgeFeather = Mathf.Max(0.001f, edgeFeather);
            Shader.SetGlobalFloat(RevealSaturationId, Mathf.Max(0f, saturation));
            Shader.SetGlobalFloat(RevealBrightnessId, Mathf.Max(0f, brightness));
            IsActive = sourceCamera != null;
            if (!IsActive) return;

            if (!sourceMaskSaved)
            {
                originalSourceCullingMask = sourceCamera.cullingMask;
                sourceMaskSaved = true;
            }
            // Authored level colour lives on ColorReveal. Reuse those exact meshes
            // and material assignments for the finale rather than recreating a
            // partial subset at runtime.
            sourceCamera.cullingMask |= (1 << SphereRevealLayer) | (1 << ColorRevealLayer);

            // The ocean backdrop has its own authored final-camera gradient. Do
            // animate a separate approximation here. Instead, reveal the exact
            // authored final-gradient card with the same sphere clip as the
            // colour meshes.
            UnderwaterOceanBackdrop.SetFinalRevealBackdropVisible(true);
            ActivateAuthoredColorProxies();
            // DualViewMaterial owns an exact mesh copy for its colour reveal.
            // Unlike an authored child proxy, it is drawn after the monochrome
            // base, which prevents large coplanar surfaces (notably the ocean
            // floor) from losing their colour copy to the depth buffer.
            DualViewMaterial.SetSphereRevealProxiesVisible(true);
            // Keep the reveal sphere as a material clip only.  The temporary
            // blue interior-volume renderer was effectively a second ocean
            // background, so it produced a mismatched reveal before the real
            // final camera/background became visible.
            if (revealVolume != null) revealVolume.SetActive(false);
            RenderPipelineManager.beginCameraRendering -= ConfigureForCamera;
            RenderPipelineManager.beginCameraRendering += ConfigureForCamera;
        }

        public static void Hide()
        {
            IsActive = false;
            RenderPipelineManager.beginCameraRendering -= ConfigureForCamera;
            if (SourceCamera != null && sourceMaskSaved)
                SourceCamera.cullingMask = originalSourceCullingMask;
            sourceMaskSaved = false;
            UnderwaterOceanBackdrop.SetFinalRevealBackdropVisible(false);
            RestoreAuthoredColorProxies();
            DualViewMaterial.SetSphereRevealProxiesVisible(false);
            RestoreGreyscaleRenderers();
            if (revealVolume != null) revealVolume.SetActive(false);
            Shader.SetGlobalFloat(EnabledId, 0f);
            SourceCamera = null;
            OriginAndRadius = Vector4.zero;
            EdgeFeather = 0.8f;
        }

        private static void ConfigureForCamera(ScriptableRenderContext context, Camera camera)
        {
            bool revealThisCamera = IsActive && camera == SourceCamera;
            Shader.SetGlobalFloat(EnabledId, revealThisCamera ? 1f : 0f);
            if (!revealThisCamera) return;

            Shader.SetGlobalVector(OriginRadiusId, OriginAndRadius);
            Shader.SetGlobalFloat(FeatherId, EdgeFeather);
            // Keep reveal proxies in the same water haze as the final colour
            // camera. The colour is configured on UnderwaterOceanBackdrop.
            Shader.SetGlobalColor(RevealAerialColorId, UnderwaterOceanBackdrop.RevealAerialColor);
        }

        private static void UpdateVisibleVolume(Vector3 origin, float radius)
        {
            if (revealVolume == null)
            {
                Shader shader = Resources.Load<Shader>("Shaders/WorldRevealInteriorSphere");
                if (shader == null) return;
                revealVolumeMaterial = new Material(shader) { name = "World Reveal Interior Volume" };
                revealVolumeMaterial.SetColor("_Color", VolumeColor);
                revealVolumeMaterial.SetFloat("_FresnelStrength", VolumeFresnelStrength);
                revealVolumeMaterial.SetFloat("_FresnelPower", VolumeFresnelPower);
                revealVolume = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                revealVolume.name = "WorldRevealInteriorVolume";
                Object.Destroy(revealVolume.GetComponent<Collider>());
                revealVolume.GetComponent<Renderer>().sharedMaterial = revealVolumeMaterial;
            }

            revealVolume.transform.SetPositionAndRotation(origin, Quaternion.identity);
            revealVolume.transform.localScale = Vector3.one * Mathf.Max(0.01f, radius * 2f);
            revealVolume.SetActive(true);
        }

        private static void ActivateAuthoredColorProxies()
        {
            if (legacyColorProxies.Count > 0) return;

            Renderer[] renderers = Object.FindObjectsByType<Renderer>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            foreach (Renderer renderer in renderers)
            {
                if (renderer == null || renderer.gameObject.layer != ColorRevealLayer ||
                    renderer.gameObject.name == "Ocean Gradient - Color" ||
                    !SupportsSphereClip(renderer.sharedMaterials))
                    continue;

                Material[] original = renderer.sharedMaterials;
                Material[] clipped = new Material[original.Length];
                for (int i = 0; i < original.Length; i++)
                {
                    if (original[i] == null) continue;
                    clipped[i] = new Material(original[i])
                    {
                        name = original[i].name + " (Final Reveal)",
                        // Render after the monochrome base at the same depth.
                        // This keeps authored legacy proxies reliable too.
                        renderQueue = Mathf.Max(2501, original[i].renderQueue)
                    };
                    clipped[i].SetFloat(SphereRevealProxyId, 1f);
                }

                legacyColorProxies.Add(new LegacyProxyState(renderer, renderer.gameObject.activeSelf, original, clipped));
                renderer.sharedMaterials = clipped;
                renderer.gameObject.SetActive(true);
            }
        }

        private static bool SupportsSphereClip(Material[] materials)
        {
            if (materials == null || materials.Length == 0) return false;
            bool found = false;
            foreach (Material material in materials)
            {
                if (material == null) continue;
                if (material.shader == null || material.shader.name != "ColorPuzzle/Flexible Cel URP/Outline")
                    return false;
                found = true;
            }
            return found;
        }

        private static void RestoreAuthoredColorProxies()
        {
            foreach (LegacyProxyState state in legacyColorProxies)
            {
                if (state.Renderer != null) state.Renderer.sharedMaterials = state.OriginalMaterials;
                if (state.Renderer != null) state.Renderer.gameObject.SetActive(state.WasActive);
                foreach (Material material in state.ClippedMaterials)
                    if (material != null) Object.Destroy(material);
            }
            legacyColorProxies.Clear();
        }

        /// <summary>
        /// Turns the temporary composite into a clean final picture without
        /// changing cameras. Each active colour proxy replaces its DualView base
        /// renderer, preventing slightly different grey meshes/outlines from
        /// peeking through at the edges.
        /// </summary>
        public static void HideGreyscaleBasesForCompletedReveal()
        {
            if (hiddenGreyscaleRenderers.Count > 0) return;
            foreach (LegacyProxyState state in legacyColorProxies)
            {
                if (state.Renderer == null) continue;
                DualViewMaterial dualView = state.Renderer.GetComponentInParent<DualViewMaterial>();
                Renderer baseRenderer = dualView != null ? dualView.TargetRenderer : null;
                if (baseRenderer == null || !baseRenderer.enabled) continue;

                hiddenGreyscaleRenderers.Add(new RendererVisibilityState(baseRenderer, true));
                baseRenderer.enabled = false;
            }
        }

        private static void RestoreGreyscaleRenderers()
        {
            foreach (RendererVisibilityState state in hiddenGreyscaleRenderers)
                if (state.Renderer != null) state.Renderer.enabled = state.WasEnabled;
            hiddenGreyscaleRenderers.Clear();
        }

        private readonly struct LegacyProxyState
        {
            public readonly Renderer Renderer;
            public readonly bool WasActive;
            public readonly Material[] OriginalMaterials;
            public readonly Material[] ClippedMaterials;

            public LegacyProxyState(Renderer renderer, bool wasActive, Material[] originalMaterials, Material[] clippedMaterials)
            {
                Renderer = renderer;
                WasActive = wasActive;
                OriginalMaterials = originalMaterials;
                ClippedMaterials = clippedMaterials;
            }
        }

        private readonly struct RendererVisibilityState
        {
            public readonly Renderer Renderer;
            public readonly bool WasEnabled;

            public RendererVisibilityState(Renderer renderer, bool wasEnabled)
            {
                Renderer = renderer;
                WasEnabled = wasEnabled;
            }
        }
    }
}
