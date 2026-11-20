using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ColorPuzzle.Rendering
{
    /// <summary>
    /// Renders one mesh with an authored material set for each gameplay camera.
    /// The old ColorReveal child supplies the initial colour materials but is never rendered.
    /// No offset copy of the geometry is needed for the lens.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Renderer))]
    public sealed class DualViewMaterial : MonoBehaviour
    {
        // Reserved runtime-only render layer. It keeps the final reveal proxies
        // separate from legacy ColorReveal objects used by the magnifier.
        private const int SphereRevealLayer = 10;
        private static readonly HashSet<DualViewMaterial> Instances = new();
        private static Camera currentGreyscaleCamera;
        private static Camera currentColorCamera;

        // CENTRALIZED ADJUSTABLE APPEARANCE PARAMETERS
        [Header("Camera-specific materials")]
        [Tooltip("Optional override. Empty uses the renderer's original materials.")]
        [SerializeField] private Material[] greyscaleMaterials;
        [Tooltip("Optional override. Empty uses a ColorProxy child's materials, or the greyscale materials when no colour is authored yet.")]
        [SerializeField] private Material[] colorMaterials;
        [Tooltip("The existing color child is retained in the prefab but hidden during play.")]
        [SerializeField] private GameObject legacyColorProxy;

        [Header("Optional color-view tint")]
        [Tooltip("Used by authored gameplay pieces such as shell pearls. The greyscale view keeps its own material color.")]
        [SerializeField] private bool useColorViewTint;
        [SerializeField] private Color colorViewTint = Color.white;

        private Renderer targetRenderer;
        private Material[] originalMaterials;
        private Material[] resolvedGreyscaleMaterials;
        private Material[] resolvedColorMaterials;
        private Material[] unfadedGreyscaleMaterials;
        private Material[] unfadedColorMaterials;
        private readonly List<Material> opacityInstances = new();
        private readonly List<Color> opacityBaseColors = new();
        private readonly List<int> opacityColorProperties = new();
        private Camera greyscaleCamera;
        private Camera colorCamera;
        private int originalLayer;
        private bool initialized;
        private bool colorInGreyscaleCamera;
        private bool colorViewEnabled = true;
        private bool usesFallbackColor;
        private GameObject sphereRevealProxy;
        private Material[] sphereRevealProxyMaterials;

        private static readonly int GlobalColor = Shader.PropertyToID("_Color");
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int SphereRevealProxy = Shader.PropertyToID("_ColorPuzzleSphereRevealProxy");

        public bool IsInitialized => initialized;
        public Renderer TargetRenderer => targetRenderer;
        public bool UsesFallbackColor => usesFallbackColor;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCameraRegistration()
        {
            Instances.Clear();
            currentGreyscaleCamera = null;
            currentColorCamera = null;
        }

        public static void ConfigureCameras(Camera mono, Camera color)
        {
            currentGreyscaleCamera = mono;
            currentColorCamera = color;
            foreach (DualViewMaterial instance in Instances)
                if (instance != null) instance.SetCameras(mono, color);
        }

        /// <summary>
        /// Shows an extra colour-material mesh on the ColorReveal layer. That layer is
        /// temporarily added to the normal camera only for the final sphere reveal.
        /// </summary>
        public static void SetSphereRevealProxiesVisible(bool visible)
        {
            // Creating a previously missing proxy can run Unity lifecycle hooks.
            // Use a snapshot so those hooks cannot alter this enumeration.
            DualViewMaterial[] snapshot = new DualViewMaterial[Instances.Count];
            Instances.CopyTo(snapshot);
            foreach (DualViewMaterial instance in snapshot)
                if (instance != null) instance.SetSphereRevealProxyVisible(visible);
        }

        private void Awake()
        {
            if (isActiveAndEnabled)
                Initialize(GetComponent<Renderer>(), legacyColorProxy);
        }

        public void Initialize(Renderer renderer, GameObject colorProxy)
        {
            if (initialized || !isActiveAndEnabled) return;
            targetRenderer = renderer != null ? renderer : GetComponent<Renderer>();
            legacyColorProxy = colorProxy != null ? colorProxy : legacyColorProxy;
            if (legacyColorProxy == null)
            {
                foreach (Transform child in transform)
                {
                    if (child.gameObject.layer != LayerMask.NameToLayer("ColorReveal") ||
                        child.GetComponent<Renderer>() == null) continue;
                    legacyColorProxy = child.gameObject;
                    break;
                }
            }
            if (targetRenderer == null) return;

            originalMaterials = targetRenderer.sharedMaterials;
            resolvedGreyscaleMaterials = HasMaterials(greyscaleMaterials) ? greyscaleMaterials : originalMaterials;
            Renderer proxyRenderer = legacyColorProxy != null
                ? legacyColorProxy.GetComponentInChildren<Renderer>(true)
                : null;
            resolvedColorMaterials = HasMaterials(colorMaterials)
                ? colorMaterials
                : proxyRenderer != null && HasMaterials(proxyRenderer.sharedMaterials)
                    ? proxyRenderer.sharedMaterials
                    : resolvedGreyscaleMaterials;
            usesFallbackColor = !HasMaterials(colorMaterials) &&
                                (proxyRenderer == null || !HasMaterials(proxyRenderer.sharedMaterials));
            if (!HasMaterials(resolvedGreyscaleMaterials))
            {
                Debug.LogWarning("Dual View Material needs at least one greyscale material.", this);
                return;
            }

            int dualViewLayer = LayerMask.NameToLayer("DualView");
            if (dualViewLayer < 0)
            {
                Debug.LogError("Add the DualView layer before using DualViewMaterial.", this);
                return;
            }

            originalLayer = targetRenderer.gameObject.layer;
            targetRenderer.gameObject.layer = dualViewLayer;
            if (legacyColorProxy != null) legacyColorProxy.SetActive(false);
            targetRenderer.sharedMaterials = resolvedGreyscaleMaterials;
            initialized = true;
            Instances.Add(this);
            CreateSphereRevealProxy();
            SetCameras(currentGreyscaleCamera, currentColorCamera);
            RenderPipelineManager.beginCameraRendering += BeforeCameraRendering;
        }

        public void SetCameras(Camera mono, Camera color)
        {
            greyscaleCamera = mono;
            colorCamera = color;
        }

        public void SetColorInGreyscaleCamera(bool enabled)
        {
            colorInGreyscaleCamera = enabled;
            if (initialized && targetRenderer != null && !enabled)
                targetRenderer.sharedMaterials = resolvedGreyscaleMaterials;
        }

        /// <summary>Temporarily keeps this mesh monochrome in every gameplay view.</summary>
        public void SetColorViewEnabled(bool enabled)
        {
            colorViewEnabled = enabled;
            if (initialized && targetRenderer != null && !enabled)
                targetRenderer.sharedMaterials = resolvedGreyscaleMaterials;
        }

        /// <summary>Sets an authored tint for only the colour-camera material set.</summary>
        public void SetColorViewTint(Color tint)
        {
            useColorViewTint = true;
            colorViewTint = tint;
            ApplyAuthoringPreviewTint();
        }

        /// <summary>
        /// Assigns authored material sets to a runtime-created visual. This keeps
        /// small generated details in the same two-view system as prefab meshes.
        /// </summary>
        public void ConfigureMaterialSets(Material[] greyscale, Material[] color)
        {
            if (!HasMaterials(greyscale) || !HasMaterials(color)) return;

            greyscaleMaterials = greyscale;
            colorMaterials = color;
            if (!initialized)
            {
                Initialize(targetRenderer != null ? targetRenderer : GetComponent<Renderer>(), legacyColorProxy);
                return;
            }

            resolvedGreyscaleMaterials = greyscaleMaterials;
            resolvedColorMaterials = colorMaterials;
            usesFallbackColor = false;
            if (targetRenderer != null)
                targetRenderer.sharedMaterials = resolvedGreyscaleMaterials;
            CreateSphereRevealProxy();
        }

        /// <summary>Fade both authored views without editing shared material assets.</summary>
        public void SetOpacityMultiplier(float multiplier)
        {
            if (!initialized) return;
            if (opacityInstances.Count == 0)
            {
                unfadedGreyscaleMaterials = resolvedGreyscaleMaterials;
                unfadedColorMaterials = resolvedColorMaterials;
                resolvedGreyscaleMaterials = MakeOpacityCopies(unfadedGreyscaleMaterials);
                resolvedColorMaterials = MakeOpacityCopies(unfadedColorMaterials);
            }

            multiplier = Mathf.Clamp01(multiplier);
            for (int i = 0; i < opacityInstances.Count; i++)
            {
                Color tint = opacityBaseColors[i];
                tint.a *= multiplier;
                opacityInstances[i].SetColor(opacityColorProperties[i], tint);
            }
        }

        private Material[] MakeOpacityCopies(Material[] source)
        {
            Material[] copies = new Material[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                if (source[i] == null) continue;
                Material copy = new Material(source[i]);
                copies[i] = copy;
                int colorProperty = copy.HasProperty("_BaseColor")
                    ? Shader.PropertyToID("_BaseColor")
                    : Shader.PropertyToID("_Color");
                opacityInstances.Add(copy);
                opacityColorProperties.Add(colorProperty);
                opacityBaseColors.Add(copy.HasProperty(colorProperty)
                    ? copy.GetColor(colorProperty) : Color.white);
            }
            return copies;
        }

        private void BeforeCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            if (!initialized || targetRenderer == null) return;
            if (camera == colorCamera)
            {
                targetRenderer.sharedMaterials = colorViewEnabled
                    ? resolvedColorMaterials
                    : resolvedGreyscaleMaterials;
                ApplyCameraTint(colorViewEnabled);
            }
            else if (camera == greyscaleCamera)
            {
                bool useColor = colorViewEnabled && colorInGreyscaleCamera;
                targetRenderer.sharedMaterials = useColor
                    ? resolvedColorMaterials
                    : resolvedGreyscaleMaterials;
                ApplyCameraTint(useColor);
            }
        }

        private void ApplyCameraTint(bool useColor)
        {
            if (!useColorViewTint || targetRenderer == null) return;
            Material activeMaterial = targetRenderer.sharedMaterial;
            int property = activeMaterial != null && activeMaterial.HasProperty(GlobalColor)
                ? GlobalColor : BaseColor;
            if (activeMaterial == null || !activeMaterial.HasProperty(property)) return;

            MaterialPropertyBlock properties = new MaterialPropertyBlock();
            targetRenderer.GetPropertyBlock(properties);
            properties.SetColor(property, useColor ? colorViewTint : activeMaterial.GetColor(property));
            targetRenderer.SetPropertyBlock(properties);
        }

        private void ApplyAuthoringPreviewTint()
        {
            if (Application.isPlaying) return;
            Renderer renderer = targetRenderer != null ? targetRenderer : GetComponent<Renderer>();
            Material previewMaterial = renderer != null ? renderer.sharedMaterial : null;
            int property = previewMaterial != null && previewMaterial.HasProperty(GlobalColor)
                ? GlobalColor : BaseColor;
            if (renderer == null || previewMaterial == null || !previewMaterial.HasProperty(property)) return;
            MaterialPropertyBlock properties = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(properties);
            properties.SetColor(property, colorViewTint);
            renderer.SetPropertyBlock(properties);
        }

        private void CreateSphereRevealProxy()
        {
            if (sphereRevealProxy != null || targetRenderer == null || usesFallbackColor ||
                !SupportsSphereReveal(resolvedColorMaterials)) return;

            MeshFilter sourceFilter = targetRenderer.GetComponent<MeshFilter>();
            if (sourceFilter == null || sourceFilter.sharedMesh == null) return;

            sphereRevealProxy = new GameObject("ColorSphereRevealProxy")
            {
                layer = SphereRevealLayer
            };
            sphereRevealProxy.transform.SetParent(targetRenderer.transform, false);
            MeshFilter proxyFilter = sphereRevealProxy.AddComponent<MeshFilter>();
            proxyFilter.sharedMesh = sourceFilter.sharedMesh;
            MeshRenderer proxyRenderer = sphereRevealProxy.AddComponent<MeshRenderer>();
            proxyRenderer.shadowCastingMode = ShadowCastingMode.Off;
            proxyRenderer.receiveShadows = false;
            proxyRenderer.lightProbeUsage = targetRenderer.lightProbeUsage;
            proxyRenderer.reflectionProbeUsage = targetRenderer.reflectionProbeUsage;
            sphereRevealProxyMaterials = MakeSphereRevealMaterialCopies(resolvedColorMaterials);
            proxyRenderer.sharedMaterials = sphereRevealProxyMaterials;
            sphereRevealProxy.SetActive(false);
        }

        private static bool SupportsSphereReveal(Material[] materials)
        {
            if (!HasMaterials(materials)) return false;
            bool foundSupportedMaterial = false;
            for (int i = 0; i < materials.Length; i++)
            {
                Material material = materials[i];
                // Imported meshes can retain empty sub-material slots. They do not
                // draw anything and must not disqualify the valid colour slots.
                if (material == null) continue;
                if (material != null && material.shader != null &&
                    material.shader.name == "ColorPuzzle/Flexible Cel URP/Outline")
                {
                    foundSupportedMaterial = true;
                    continue;
                }
                return false;
            }
            return foundSupportedMaterial;
        }

        private Material[] MakeSphereRevealMaterialCopies(Material[] source)
        {
            if (!HasMaterials(source)) return System.Array.Empty<Material>();
            Material[] copies = new Material[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                if (source[i] == null) continue;
                copies[i] = new Material(source[i])
                {
                    name = source[i].name + " (Sphere Reveal)",
                    // Draw after the opaque monochrome original at the same depth.
                    renderQueue = Mathf.Max(2501, source[i].renderQueue)
                };
                copies[i].SetFloat(SphereRevealProxy, 1f);
            }
            return copies;
        }

        private void SetSphereRevealProxyVisible(bool visible)
        {
            if (!initialized || usesFallbackColor) return;
            // The original mesh is the greyscale base. The clipped proxy only
            // overlays it, so nothing vanishes before the sphere reaches it.
            if (targetRenderer != null) targetRenderer.enabled = true;
            if (sphereRevealProxy == null) CreateSphereRevealProxy();
            if (sphereRevealProxy != null)
                sphereRevealProxy.SetActive(visible);
        }

        private void OnEnable()
        {
            // Stages can be disabled and re-enabled without a second Awake call.
            if (!initialized)
                Initialize(targetRenderer != null ? targetRenderer : GetComponent<Renderer>(), legacyColorProxy);
        }

        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= BeforeCameraRendering;
            Instances.Remove(this);
            if (!initialized) return;
            if (targetRenderer != null)
            {
                targetRenderer.sharedMaterials = originalMaterials;
                targetRenderer.gameObject.layer = originalLayer;
            }
            if (legacyColorProxy != null) legacyColorProxy.SetActive(true);
            if (sphereRevealProxy != null) Destroy(sphereRevealProxy);
            sphereRevealProxy = null;
            if (sphereRevealProxyMaterials != null)
                foreach (Material material in sphereRevealProxyMaterials)
                    if (material != null) Destroy(material);
            sphereRevealProxyMaterials = null;
            foreach (Material instance in opacityInstances)
                if (instance != null) Destroy(instance);
            opacityInstances.Clear();
            opacityBaseColors.Clear();
            opacityColorProperties.Clear();
            if (unfadedGreyscaleMaterials != null)
                resolvedGreyscaleMaterials = unfadedGreyscaleMaterials;
            if (unfadedColorMaterials != null)
                resolvedColorMaterials = unfadedColorMaterials;
            unfadedGreyscaleMaterials = null;
            unfadedColorMaterials = null;
            initialized = false;
        }

        private static bool HasMaterials(Material[] materials)
        {
            if (materials == null || materials.Length == 0) return false;
            for (int i = 0; i < materials.Length; i++)
                if (materials[i] != null) return true;
            return false;
        }
    }
}
