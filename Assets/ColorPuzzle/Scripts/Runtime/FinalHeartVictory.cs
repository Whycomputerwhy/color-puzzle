using System.Collections;
using ColorPuzzle.Rendering;
using UnityEngine;

namespace ColorPuzzle
{
    /// <summary>Single-use heart pulse and glass-casing fade for Puzzle 4 completion.</summary>
    public sealed class FinalHeartVictory : MonoBehaviour
    {
        // CENTRALIZED FINAL-REVEAL PARAMETERS
        [Header("Heart glow")]
        [SerializeField] private Renderer heartRenderer;
        [SerializeField] private Color heartGlowColor = new Color(0.25f, 0.88f, 1f, 1f);
        [SerializeField, Min(0f)] private float heartGlowDuration = 0.85f;
        [SerializeField, Range(0f, 3f)] private float heartGlowBrightness = 1.45f;

        [Header("Glass fade")]
        [SerializeField] private GameObject glassCasing;
        [SerializeField, Min(0f)] private float glassFadeDuration = 1.1f;
        [SerializeField, Min(0)] private int campaignStageIndex = 3;

        private static readonly int FresnelBrightness = Shader.PropertyToID("_FresnelBrightness");
        private static readonly int FresnelPower = Shader.PropertyToID("_FresnelPower");
        private static readonly int FresnelColor = Shader.PropertyToID("_FresnelColor");
        private bool played;

        public void Play()
        {
            if (played) return;
            played = true;
            StartCoroutine(PlaySequence());
        }

        private IEnumerator PlaySequence()
        {
            MaterialPropertyBlock original = new MaterialPropertyBlock();
            if (heartRenderer != null) heartRenderer.GetPropertyBlock(original);
            float elapsed = 0f;
            while (elapsed < heartGlowDuration)
            {
                elapsed += Time.deltaTime;
                float pulse = Mathf.Sin(Mathf.Clamp01(elapsed / Mathf.Max(0.001f, heartGlowDuration)) * Mathf.PI);
                if (heartRenderer != null)
                {
                    MaterialPropertyBlock properties = new MaterialPropertyBlock();
                    heartRenderer.GetPropertyBlock(properties);
                    properties.SetColor(FresnelColor, heartGlowColor);
                    properties.SetFloat(FresnelBrightness, pulse * heartGlowBrightness);
                    properties.SetFloat(FresnelPower, 0.75f);
                    heartRenderer.SetPropertyBlock(properties);
                }
                yield return null;
            }
            if (heartRenderer != null) heartRenderer.SetPropertyBlock(original);

            if (glassCasing == null)
            {
                CompleteCampaign();
                yield break;
            }
            DualViewMaterial[] glass = glassCasing.GetComponentsInChildren<DualViewMaterial>(true);
            elapsed = 0f;
            while (elapsed < glassFadeDuration)
            {
                elapsed += Time.deltaTime;
                float opacity = 1f - Mathf.SmoothStep(0f, 1f, elapsed / Mathf.Max(0.001f, glassFadeDuration));
                foreach (DualViewMaterial visual in glass) visual.SetOpacityMultiplier(opacity);
                yield return null;
            }
            glassCasing.SetActive(false);
            // The visible colour sphere must begin at the final heart, not at the
            // player's current position. Starting this before campaign completion
            // also prevents the manager's completion fallback from replacing it.
            MagnifierView magnifier = FindAnyObjectByType<MagnifierView>();
            Vector3 revealOrigin = heartRenderer != null ? heartRenderer.bounds.center : transform.position;
            magnifier?.RestoreWorldColor(revealOrigin);
            CompleteCampaign();
        }

        private void CompleteCampaign() => OceanCampaignManager.Active?.CompleteStage(campaignStageIndex);
    }
}
