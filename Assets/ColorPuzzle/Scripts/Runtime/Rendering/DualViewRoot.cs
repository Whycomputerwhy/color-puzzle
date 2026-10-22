using UnityEngine;

namespace ColorPuzzle.Rendering
{
    /// <summary>
    /// Guarantees that authored and newly added MeshRenderers under a campaign prefab
    /// remain visible in both camera passes. Missing colour art falls back to the
    /// existing material until a designer assigns a colour material per object.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DualViewRoot : MonoBehaviour
    {
        // CENTRALIZED ADJUSTABLE DISCOVERY PARAMETERS
        [Header("Future Objects")]
        [SerializeField, Min(0.1f)] private float scanIntervalSeconds = 1f;
        [SerializeField] private bool includeInactiveChildren = true;
        [SerializeField] private bool logMissingColorMaterials = true;

        private float nextScanTime;

        private void Awake() => RegisterRenderers();

        private void OnEnable() => RegisterRenderers();

        private void LateUpdate()
        {
            if (Time.unscaledTime < nextScanTime) return;
            RegisterRenderers();
            nextScanTime = Time.unscaledTime + scanIntervalSeconds;
        }

        private void RegisterRenderers()
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>(includeInactiveChildren);
            int proxyLayer = LayerMask.NameToLayer("ColorReveal");
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null || renderer.gameObject.layer == proxyLayer ||
                    renderer is not MeshRenderer || renderer.sharedMaterials.Length == 0) continue;

                DualViewMaterial appearance = renderer.GetComponent<DualViewMaterial>();
                if (appearance == null)
                {
                    appearance = renderer.gameObject.AddComponent<DualViewMaterial>();
                    if (logMissingColorMaterials && appearance.UsesFallbackColor)
                        Debug.LogWarning($"{renderer.name} has no authored color material yet. It remains visible in both views with the same material; assign Color Materials on Dual View Material.", renderer);
                }
                else if (appearance.enabled && !appearance.IsInitialized)
                    appearance.Initialize(renderer, null);
            }
        }
    }
}
