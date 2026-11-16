using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ColorPuzzle
{
    /// <summary>Positions the authored ocean-gradient cards; their prefab owns the visible geometry and materials.</summary>
    [DisallowMultipleComponent]
    public sealed class UnderwaterOceanBackdrop : MonoBehaviour
    {
        /// <summary>The final colour-view water haze, shared by the sphere reveal.</summary>
        public static Color RevealAerialColor { get; private set; } = new Color(0.045f, 0.22f, 0.32f, 1f);
        // CENTRALIZED OCEAN-VOLUME PARAMETERS — all visual tuning lives here.
        [Header("Cameras")]
        [SerializeField] private Camera greyscaleCamera;
        [SerializeField] private Camera colorCamera;
        [SerializeField] private string greyscaleCameraName = "MainCamera";
        [SerializeField] private string colorCameraName = "ColorRevealCamera";

        [Header("Gradient Geometry")]
        [SerializeField, Min(10f)] private float backdropDistance = 90f;
        [SerializeField, Min(1f)] private float backdropOverscan = 1.2f;

        [Header("Greyscale Water")]
        [SerializeField] private Color greyscaleUpperWater = new Color(0.30f, 0.30f, 0.30f, 1f);
        [SerializeField] private Color greyscaleLowerWater = new Color(0.06f, 0.06f, 0.06f, 1f);

        [Header("Color Water")]
        [SerializeField] private Color colorUpperWater = new Color(0.10f, 0.42f, 0.56f, 1f);
        [SerializeField] private Color colorLowerWater = new Color(0.025f, 0.12f, 0.22f, 1f);

        // CENTRALIZED AERIAL-PERSPECTIVE PARAMETERS — world-distance fog is
        // supplied to the cel shaders per camera, so the two gameplay views can
        // fade naturally into their own authored water palettes.
        [Header("Aerial Perspective")]
        [Tooltip("Blends distant cel-shaded objects into the underwater backdrop. This affects only the two gameplay cameras, not the editor Scene view.")]
        [SerializeField] private bool aerialPerspectiveEnabled = true;
        [SerializeField, Min(0f), Tooltip("World-space distance in metres at which the water haze begins.")]
        private float aerialPerspectiveStartDistance = 16f;
        [SerializeField, Min(0.1f), Tooltip("World-space distance in metres at which objects are fully blended into the water colour.")]
        private float aerialPerspectiveEndDistance = 58f;
        [SerializeField] private Color greyscaleAerialColor = new Color(0.075f, 0.075f, 0.075f, 1f);
        [SerializeField] private Color colorAerialColor = new Color(0.045f, 0.22f, 0.32f, 1f);

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
        private const int SphereRevealLayer = 10;
        private readonly List<Renderer> hiddenBoundaryRenderers = new();
        private readonly Dictionary<Renderer, ShadowCastingMode> colorShadowCasters = new();
        private readonly Dictionary<Renderer, GameObject> colorShadowOnlyCopies = new();
        private GameObject greyscaleBackdrop;
        private GameObject colorBackdrop;
        private Material greyscaleBackdropMaterial;
        private Material colorBackdropMaterial;
        private float nextColorShadowCasterRefreshTime;

        private static readonly int AerialEnabledId = Shader.PropertyToID("_ColorPuzzleAerialEnabled");
        private static readonly int AerialStartId = Shader.PropertyToID("_ColorPuzzleAerialStart");
        private static readonly int AerialEndId = Shader.PropertyToID("_ColorPuzzleAerialEnd");
        private static readonly int AerialColorId = Shader.PropertyToID("_ColorPuzzleAerialColor");
        private static readonly int SphereRevealProxyId = Shader.PropertyToID("_ColorPuzzleSphereRevealProxy");

        /// <summary>
        /// The colour gradient is deliberately hidden during normal greyscale play.
        /// The world-sphere reveal temporarily brings it back on the ColorReveal layer.
        /// </summary>
        public static void SetFinalRevealBackdropVisible(bool visible)
        {
            foreach (Transform transform in FindObjectsByType<Transform>(
                         FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
            {
                if (transform != null && transform.name == "Ocean Gradient - Color")
                {
                    transform.gameObject.layer = visible ? SphereRevealLayer : ColorRevealLayer;
                    transform.gameObject.SetActive(visible);
                    // This card can start inactive, in which case Awake cannot
                    // assign its property block. Set it here as well so its
                    // shader is definitely clipped by the reveal sphere.
                    SetBackdropRevealProxy(transform.gameObject, true);
                    SetColorBackdropRenderOrder(transform, visible);
                }
            }
        }

        /// <summary>
        /// Leaves the authored colour gradient available to the full-colour camera
        /// after the reveal has finished, avoiding a snap to its solid clear colour.
        /// </summary>
        public static void SetCompletedColorBackdropVisible(bool visible)
        {
            foreach (Transform transform in FindObjectsByType<Transform>(
                         FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
            {
                if (transform != null && transform.name == "Ocean Gradient - Color")
                {
                    transform.gameObject.layer = ColorRevealLayer;
                    transform.gameObject.SetActive(visible);
                    // This is the normal final colour card, so preserve its
                    // authored render order rather than treating it as an
                    // intermediate reveal overlay.
                    SetColorBackdropRenderOrder(transform, false);
                }
            }
        }

        private static void SetColorBackdropRenderOrder(Transform backdrop, bool useColorOverlayOrder)
        {
            if (backdrop == null) return;
            foreach (Renderer renderer in backdrop.GetComponentsInChildren<Renderer>(true))
            {
                // The grey and colour cards otherwise share the same Background
                // queue. Render the colour card one step later during the sphere
                // transition, then restore its authored ordering afterwards.
                Material material = renderer.material;
                if (material != null)
                    material.renderQueue = useColorOverlayOrder ? 1001 : -1;
            }
        }

        private void Awake()
        {
            RevealAerialColor = colorAerialColor;
            ResolveCameras();
            ResolveBackdrops();
            if (hideBoundaryRenderers) HideBoundaryRenderers();
        }

        private void OnEnable() => RenderPipelineManager.beginCameraRendering += ConfigureAerialPerspective;

        private void OnValidate()
        {
            RevealAerialColor = colorAerialColor;
        }

        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= ConfigureAerialPerspective;
            Shader.SetGlobalFloat(AerialEnabledId, 0f);
        }

        private void Start()
        {
            RefreshColorProxyShadowCasters();
        }

        private void LateUpdate()
        {
            ResolveCameras();
            ResolveBackdrops();
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

        }

        private void ResolveCameras()
        {
            if (greyscaleCamera == null)
                greyscaleCamera = FindCamera(greyscaleCameraName);
            if (colorCamera == null)
                colorCamera = FindCamera(colorCameraName);
        }

        private void ResolveBackdrops()
        {
            if (greyscaleBackdrop == null) greyscaleBackdrop = GameObject.Find("Ocean Gradient - Greyscale");
            if (colorBackdrop == null) colorBackdrop = GameObject.Find("Ocean Gradient - Color");
            SetBackdropRevealProxy(greyscaleBackdrop, false);
            SetBackdropRevealProxy(colorBackdrop, true);
        }

        private static void SetBackdropRevealProxy(GameObject backdrop, bool isColorProxy)
        {
            if (backdrop == null) return;
            foreach (Renderer renderer in backdrop.GetComponentsInChildren<Renderer>(true))
            {
                MaterialPropertyBlock properties = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(properties);
                properties.SetFloat(SphereRevealProxyId, isColorProxy ? 1f : 0f);
                renderer.SetPropertyBlock(properties);
            }
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

        private void ConfigureAerialPerspective(ScriptableRenderContext context, Camera camera)
        {
            if (camera == null || (camera != greyscaleCamera && camera != colorCamera)) return;

            float endDistance = Mathf.Max(aerialPerspectiveStartDistance + 0.1f, aerialPerspectiveEndDistance);
            Shader.SetGlobalFloat(AerialEnabledId, aerialPerspectiveEnabled ? 1f : 0f);
            Shader.SetGlobalFloat(AerialStartId, aerialPerspectiveStartDistance);
            Shader.SetGlobalFloat(AerialEndId, endDistance);
            Shader.SetGlobalColor(AerialColorId, camera == colorCamera
                ? colorAerialColor
                : greyscaleAerialColor);
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
                bool visible = renderer.enabled && renderer.gameObject.activeInHierarchy;
                if (colorShadowOnlyCopies.TryGetValue(renderer, out GameObject existingCopy) && existingCopy != null)
                    existingCopy.SetActive(visible);
                if (!visible) continue;

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
