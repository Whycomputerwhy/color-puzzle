using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ColorPuzzle
{
    /// <summary>
    /// Builds the low-cost visual volume behind the level with a camera-facing vertical
    /// gradient. Physical wall colliders remain active while their renderers are hidden,
    /// so the ocean reads as open space without adding extra floor geometry.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UnderwaterOceanBackdrop : MonoBehaviour
    {
        // CENTRALIZED OCEAN-VOLUME PARAMETERS — all visual tuning lives here.
        [Header("Cameras")]
        [SerializeField] private Camera greyscaleCamera;
        [SerializeField] private Camera colorCamera;
        [SerializeField] private string greyscaleCameraName = "MainCamera";
        [SerializeField] private string colorCameraName = "ColorRevealCamera";

        [Header("Gradient Geometry")]
        [SerializeField, Min(10f)] private float backdropDistance = 90f;
        [SerializeField, Min(1f)] private float backdropOverscan = 1.2f;
        [SerializeField, Range(0.25f, 4f)] private float verticalBlendPower = 1.15f;

        [Header("Greyscale Water")]
        [SerializeField] private Color greyscaleUpperWater = new Color(0.30f, 0.30f, 0.30f, 1f);
        [SerializeField] private Color greyscaleLowerWater = new Color(0.06f, 0.06f, 0.06f, 1f);

        [Header("Color Water")]
        [SerializeField] private Color colorUpperWater = new Color(0.10f, 0.42f, 0.56f, 1f);
        [SerializeField] private Color colorLowerWater = new Color(0.025f, 0.12f, 0.22f, 1f);

        [Header("Invisible Play Boundaries")]
        [SerializeField] private bool hideBoundaryRenderers = true;
        [SerializeField] private string[] boundaryRendererNames =
        {
            "SideWallRight", "SideWallLeft", "BackWall", "FrontWall",
            "LeftNiche_Wall", "RightNiche_Wall", "RearRight_PocketWall"
        };

        [Header("Shadow Casting")]
        [Tooltip("Creates stable, shadow-only copies for the colour-reveal camera. The visible colour proxies never cast directly, preventing cross-camera shadow conflicts.")]
        [SerializeField] private bool createColorProxyShadowCasters = true;
        [SerializeField, Min(0.1f)] private float colorShadowCasterRefreshInterval = 0.5f;

        private const int GreyscaleLayer = 0;
        private const int ColorRevealLayer = 8;
        private const string GradientShaderName = "ColorPuzzle/Underwater Gradient Backdrop";

        private readonly List<Renderer> hiddenBoundaryRenderers = new();
        private readonly List<Material> runtimeMaterials = new();
        private readonly Dictionary<Renderer, ShadowCastingMode> colorShadowCasters = new();
        private readonly Dictionary<Renderer, GameObject> colorShadowOnlyCopies = new();
        private GameObject greyscaleBackdrop;
        private GameObject colorBackdrop;
        private Material greyscaleBackdropMaterial;
        private Material colorBackdropMaterial;
        private float nextColorShadowCasterRefreshTime;

        private void Awake()
        {
            ResolveCameras();
            BuildOceanVolume();
        }

        private void Start()
        {
            RefreshColorProxyShadowCasters();
        }

        private void LateUpdate()
        {
            ResolveCameras();
            UpdateBackdrop(greyscaleBackdrop, greyscaleCamera);
            UpdateBackdrop(colorBackdrop, colorCamera);

            if (Time.unscaledTime >= nextColorShadowCasterRefreshTime)
            {
                RefreshColorProxyShadowCasters();
                nextColorShadowCasterRefreshTime = Time.unscaledTime + colorShadowCasterRefreshInterval;
            }
        }

        private void OnDestroy()
        {
            RestoreColorProxyShadowCasters();
            DestroyColorShadowOnlyCopies();

            foreach (Renderer renderer in hiddenBoundaryRenderers)
            {
                if (renderer != null) renderer.enabled = true;
            }

            DestroyRuntimeObject(greyscaleBackdrop);
            DestroyRuntimeObject(colorBackdrop);
            foreach (Material runtimeMaterial in runtimeMaterials)
                DestroyRuntimeObject(runtimeMaterial);
        }

        private void ResolveCameras()
        {
            if (greyscaleCamera == null)
                greyscaleCamera = FindCamera(greyscaleCameraName);
            if (colorCamera == null)
                colorCamera = FindCamera(colorCameraName);
        }

        private void BuildOceanVolume()
        {
            Shader gradientShader = Shader.Find(GradientShaderName);
            if (gradientShader == null)
            {
                Debug.LogWarning($"Missing underwater backdrop shader '{GradientShaderName}'.", this);
                return;
            }

            greyscaleBackdropMaterial = CreateGradientMaterial(
                gradientShader,
                "Runtime Greyscale Ocean Gradient",
                greyscaleUpperWater,
                greyscaleLowerWater);
            colorBackdropMaterial = CreateGradientMaterial(
                gradientShader,
                "Runtime Color Ocean Gradient",
                colorUpperWater,
                colorLowerWater);

            greyscaleBackdrop = CreateBackdrop("Ocean Gradient - Greyscale", GreyscaleLayer, greyscaleBackdropMaterial);
            colorBackdrop = CreateBackdrop("Ocean Gradient - Color", ColorRevealLayer, colorBackdropMaterial);

            if (hideBoundaryRenderers) HideBoundaryRenderers();
        }

        private Material CreateGradientMaterial(Shader shader, string materialName, Color upperColor, Color lowerColor)
        {
            Material material = new Material(shader) { name = materialName };
            runtimeMaterials.Add(material);
            material.SetColor("_UpperColor", upperColor);
            material.SetColor("_LowerColor", lowerColor);
            material.SetFloat("_VerticalBlendPower", verticalBlendPower);
            return material;
        }

        private GameObject CreateBackdrop(string objectName, int layer, Material material)
        {
            GameObject backdrop = GameObject.CreatePrimitive(PrimitiveType.Quad);
            backdrop.name = objectName;
            backdrop.layer = layer;
            Destroy(backdrop.GetComponent<Collider>());
            Renderer backdropRenderer = backdrop.GetComponent<Renderer>();
            backdropRenderer.sharedMaterial = material;
            // These are camera-facing atmosphere cards, not physical world geometry.
            // Letting one enter the directional-light shadow map creates a moving,
            // scene-wide shadow sheet as the player looks around.
            backdropRenderer.shadowCastingMode = ShadowCastingMode.Off;
            backdropRenderer.receiveShadows = false;
            return backdrop;
        }

        private void UpdateBackdrop(GameObject backdrop, Camera camera)
        {
            if (backdrop == null || camera == null) return;

            float safeDistance = Mathf.Min(backdropDistance, camera.farClipPlane * 0.85f);
            float height = 2f * safeDistance * Mathf.Tan(camera.fieldOfView * Mathf.Deg2Rad * 0.5f) * backdropOverscan;
            backdrop.transform.SetPositionAndRotation(
                camera.transform.position + camera.transform.forward * safeDistance,
                camera.transform.rotation);
            backdrop.transform.localScale = new Vector3(height * camera.aspect, height, 1f);
        }

        private void HideBoundaryRenderers()
        {
            foreach (string boundaryName in boundaryRendererNames)
            {
                GameObject boundary = GameObject.Find(boundaryName);
                if (boundary == null) continue;

                foreach (Renderer renderer in boundary.GetComponentsInChildren<Renderer>(true))
                {
                    renderer.enabled = false;
                    hiddenBoundaryRenderers.Add(renderer);
                }
            }
        }

        private void RefreshColorProxyShadowCasters()
        {
            if (!createColorProxyShadowCasters) return;

            foreach (Renderer renderer in FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (renderer == null) continue;
                if (renderer.gameObject.layer != ColorRevealLayer || renderer == colorBackdrop?.GetComponent<Renderer>()) continue;
                if (renderer.name.StartsWith("ColorShadowOnly_")) continue;

                // Boundary visuals are intentionally disabled to make the ocean feel
                // open. A disabled renderer must not leave an invisible shadow-only
                // counterpart behind in the colour view.
                if (colorShadowOnlyCopies.TryGetValue(renderer, out GameObject existingCopy) && existingCopy != null)
                    existingCopy.SetActive(renderer.enabled);
                if (!renderer.enabled) continue;

                CacheShadowCaster(renderer, colorShadowCasters);
                if (colorShadowCasters[renderer] != ShadowCastingMode.Off &&
                    !colorShadowOnlyCopies.ContainsKey(renderer))
                    CreateColorShadowOnlyCopy(renderer);
            }

            foreach (KeyValuePair<Renderer, ShadowCastingMode> entry in colorShadowCasters)
                if (entry.Key != null) entry.Key.shadowCastingMode = ShadowCastingMode.Off;
        }

        private void CreateColorShadowOnlyCopy(Renderer sourceRenderer)
        {
            // The level only uses MeshRenderers for its colour proxies. Copying just
            // the mesh renderer avoids duplicating gameplay scripts, colliders, or UI.
            if (sourceRenderer is not MeshRenderer sourceMeshRenderer) return;
            MeshFilter sourceFilter = sourceMeshRenderer.GetComponent<MeshFilter>();
            if (sourceFilter == null || sourceFilter.sharedMesh == null) return;

            GameObject copy = new GameObject($"ColorShadowOnly_{sourceRenderer.name}");
            Transform sourceTransform = sourceRenderer.transform;
            copy.layer = ColorRevealLayer;
            copy.transform.SetParent(sourceTransform.parent, false);
            copy.transform.localPosition = sourceTransform.localPosition;
            copy.transform.localRotation = sourceTransform.localRotation;
            copy.transform.localScale = sourceTransform.localScale;

            MeshFilter copyFilter = copy.AddComponent<MeshFilter>();
            copyFilter.sharedMesh = sourceFilter.sharedMesh;
            MeshRenderer copyRenderer = copy.AddComponent<MeshRenderer>();
            copyRenderer.sharedMaterials = sourceMeshRenderer.sharedMaterials;
            copyRenderer.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
            copyRenderer.receiveShadows = false;
            copyRenderer.renderingLayerMask = sourceMeshRenderer.renderingLayerMask;
            copyRenderer.allowOcclusionWhenDynamic = false;
            colorShadowOnlyCopies.Add(sourceRenderer, copy);
        }

        private static void CacheShadowCaster(Renderer renderer, Dictionary<Renderer, ShadowCastingMode> cache)
        {
            if (!cache.ContainsKey(renderer)) cache.Add(renderer, renderer.shadowCastingMode);
        }

        private void RestoreColorProxyShadowCasters()
        {
            foreach (KeyValuePair<Renderer, ShadowCastingMode> entry in colorShadowCasters)
            {
                if (entry.Key != null) entry.Key.shadowCastingMode = entry.Value;
            }
        }

        private void DestroyColorShadowOnlyCopies()
        {
            foreach (GameObject copy in colorShadowOnlyCopies.Values)
                DestroyRuntimeObject(copy);
            colorShadowOnlyCopies.Clear();
        }

        private static Camera FindCamera(string cameraName)
        {
            foreach (Camera camera in FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (camera.name == cameraName) return camera;
            }

            return null;
        }

        private static void DestroyRuntimeObject(Object target)
        {
            if (target == null) return;
            if (Application.isPlaying) Destroy(target);
            else DestroyImmediate(target);
        }
    }
}
