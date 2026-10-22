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

        private Renderer targetRenderer;
        private Material[] originalMaterials;
        private Material[] resolvedGreyscaleMaterials;
        private Material[] resolvedColorMaterials;
        private Camera greyscaleCamera;
        private Camera colorCamera;
        private int originalLayer;
        private bool initialized;
        private bool colorInGreyscaleCamera;
        private bool usesFallbackColor;

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

        private void BeforeCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            if (!initialized || targetRenderer == null) return;
            if (camera == colorCamera)
                targetRenderer.sharedMaterials = resolvedColorMaterials;
            else if (camera == greyscaleCamera)
                targetRenderer.sharedMaterials = colorInGreyscaleCamera
                    ? resolvedColorMaterials
                    : resolvedGreyscaleMaterials;
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
