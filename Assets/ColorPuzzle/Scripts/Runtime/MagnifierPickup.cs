using UnityEngine;

namespace ColorPuzzle
{
    /// <summary>One-time ability pickup. Its display model never enters the object inventory.</summary>
    public sealed class MagnifierPickup : MonoBehaviour
    {
        [SerializeField] private GameObject visualRoot;
        [SerializeField] private Collider pickupCollider;
        [SerializeField] private AudioClip pickupClip;
        [SerializeField, Range(0f, 1f)] private float pickupVolume = 0.18f;

        // MAGNIFIER PICKUP GLOW — centralized, inspector-tunable visual parameters.
        [Header("Pickup glow")]
        [SerializeField] private bool showPickupGlow = true;
        [SerializeField] private Color glowColor = new Color(0.22f, 0.85f, 1f, 1f);
        [SerializeField, Min(0f)] private float glowBaseIntensity = 1.15f;
        [SerializeField, Min(0f)] private float glowPulseIntensity = 0.65f;
        [SerializeField, Min(0f)] private float glowBaseRange = 2.2f;
        [SerializeField, Min(0f)] private float glowPulseRange = 0.25f;
        [SerializeField, Min(0f)] private float glowPulseSpeed = 1.25f;
        [SerializeField] private Vector3 glowOffset = new Vector3(0f, 0.18f, 0.18f);
        [SerializeField, Range(0f, 1f)] private float glowRimBaseBrightness = 0.16f;
        [SerializeField, Range(0f, 1f)] private float glowRimPulseBrightness = 0.12f;
        [SerializeField, Range(0f, 1f)] private float glowRimSize = 0.38f;

        private Light pickupGlow;
        private GameObject glowObject;
        private Renderer[] glowRenderers;
        private MaterialPropertyBlock[] originalRendererProperties;
        private static readonly int FresnelColor = Shader.PropertyToID("_FresnelColor");
        private static readonly int FresnelBrightness = Shader.PropertyToID("_FresnelBrightness");
        private static readonly int FresnelPower = Shader.PropertyToID("_FresnelPower");

        public bool IsAvailable => pickupCollider != null && pickupCollider.enabled;

        private void Awake()
        {
            CacheGlowRenderers();
            CreatePickupGlow();
        }

        private void Update()
        {
            if (pickupGlow == null || !pickupGlow.gameObject.activeSelf) return;

            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * glowPulseSpeed * Mathf.PI * 2f);
            pickupGlow.intensity = glowBaseIntensity + glowPulseIntensity * pulse;
            pickupGlow.range = glowBaseRange + glowPulseRange * pulse;
            ApplyModelGlow(glowRimBaseBrightness + glowRimPulseBrightness * pulse);
        }

        public bool TryAcquire(Transform player, float maximumDistance)
        {
            if (!IsAvailable || player == null ||
                Vector3.Distance(player.position, transform.position) > maximumDistance)
                return false;

            OceanCampaignManager campaign = OceanCampaignManager.Active;
            if (campaign == null || !campaign.AcquireMagnifier()) return false;

            if (pickupClip != null)
                AudioSource.PlayClipAtPoint(pickupClip, transform.position, pickupVolume);
            SetAvailable(false);
            return true;
        }

        public void SetAvailable(bool available)
        {
            if (visualRoot != null) visualRoot.SetActive(available);
            if (pickupCollider != null) pickupCollider.enabled = available;
            if (glowObject != null) glowObject.SetActive(available && showPickupGlow);
            if (!available) RestoreModelProperties();
        }

        private void CreatePickupGlow()
        {
            if (!showPickupGlow || glowObject != null) return;

            glowObject = new GameObject("Magnifier Pickup Glow");
            glowObject.transform.SetParent(transform, false);
            glowObject.transform.localPosition = glowOffset;

            pickupGlow = glowObject.AddComponent<Light>();
            pickupGlow.type = LightType.Point;
            pickupGlow.color = glowColor;
            pickupGlow.intensity = glowBaseIntensity;
            pickupGlow.range = glowBaseRange;
            pickupGlow.shadows = LightShadows.None;
            pickupGlow.renderMode = LightRenderMode.Auto;
            glowObject.SetActive(IsAvailable);
        }

        private void CacheGlowRenderers()
        {
            if (visualRoot == null) return;
            glowRenderers = visualRoot.GetComponentsInChildren<Renderer>(true);
            originalRendererProperties = new MaterialPropertyBlock[glowRenderers.Length];
            for (int i = 0; i < glowRenderers.Length; i++)
            {
                originalRendererProperties[i] = new MaterialPropertyBlock();
                glowRenderers[i].GetPropertyBlock(originalRendererProperties[i]);
            }
        }

        private void ApplyModelGlow(float brightness)
        {
            if (glowRenderers == null) return;
            for (int i = 0; i < glowRenderers.Length; i++)
            {
                Renderer renderer = glowRenderers[i];
                Material material = renderer != null ? renderer.sharedMaterial : null;
                if (material == null || !material.HasProperty(FresnelBrightness)) continue;

                MaterialPropertyBlock properties = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(properties);
                properties.SetColor(FresnelColor, glowColor);
                properties.SetFloat(FresnelBrightness, brightness);
                properties.SetFloat(FresnelPower, glowRimSize);
                renderer.SetPropertyBlock(properties);
            }
        }

        private void RestoreModelProperties()
        {
            if (glowRenderers == null || originalRendererProperties == null) return;
            for (int i = 0; i < glowRenderers.Length; i++)
                if (glowRenderers[i] != null)
                    glowRenderers[i].SetPropertyBlock(originalRendererProperties[i]);
        }
    }
}
