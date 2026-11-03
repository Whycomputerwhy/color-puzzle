using System.Collections;
using System.Collections.Generic;
using ColorPuzzle.Rendering;
using UnityEngine;

namespace ColorPuzzle
{
    /// <summary>Completes Puzzle 3 when its four dial values match their authored target.</summary>
    public sealed class NumberDialLockController : MonoBehaviour
    {
        [SerializeField] private NumberDial[] dials;
        [SerializeField, Min(0)] private int campaignStageIndex = 2;
        [SerializeField] private bool lockWhenSolved = true;

        // DIAL ATTEMPT FEEDBACK — evaluate the entered combination when the
        // player releases a dial, rather than while they are still changing it.
        [Header("Attempt feedback")]
        [SerializeField] private AudioClip wrongCombinationClip;
        [SerializeField, Range(0f, 1f)] private float wrongCombinationVolume = 0.42f;
        [SerializeField, Min(0f)] private float failureFlashDuration = 0.24f;
        [SerializeField, Range(0f, 2f)] private float failureFlashBrightness = 0.55f;
        [SerializeField] private Color failureFlashColor = new Color(1f, 0.35f, 0.2f, 1f);
        [SerializeField, Range(0f, 1f)] private float failureFlashRimSize = 0.65f;

        [Header("Success feedback")]
        [SerializeField] private AudioClip correctCombinationClip;
        [SerializeField, Range(0f, 1f)] private float correctCombinationVolume = 0.3f;
        [SerializeField] private bool revealPuzzleColorOnSuccess = true;

        private bool solved;
        // The first answer is only valid once every dial has been moved away
        // from its authored starting value of 1. Once a complete answer has
        // failed, every later dial release is still an answer check.
        private bool hasSubmittedCompleteCombination;
        private AudioSource audioSource;
        private Coroutine feedbackRoutine;

        private static readonly int FresnelBrightness = Shader.PropertyToID("_FresnelBrightness");
        private static readonly int FresnelPower = Shader.PropertyToID("_FresnelPower");
        private static readonly int FresnelColor = Shader.PropertyToID("_FresnelColor");

        private void Awake()
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;

            // Existing authored lock prefabs receive the same success cue without
            // requiring a prefab rebuild that could disturb their manual layout.
            if (correctCombinationClip == null)
                correctCombinationClip = Resources.Load<AudioClip>("Audio/CorrectShellPlacementAndReveal");
        }

        public bool CanRotate(NumberDial dial) =>
            !solved && dial != null && System.Array.IndexOf(dials, dial) >= 0;

        public bool TryRotate(NumberDial dial)
        {
            OceanCampaignManager campaign = OceanCampaignManager.Active;
            if (campaign != null && !campaign.CanAttemptStage(campaignStageIndex)) return false;
            campaign?.NotifyPuzzleInteraction(campaignStageIndex);
            if (!CanRotate(dial)) return false;
            dial.Advance();
            return true;
        }

        /// <summary>Restores this authored lock's solved presentation from a checkpoint.</summary>
        public void CompleteForProgressRestore()
        {
            if (solved) return;
            solved = true;
            if (lockWhenSolved)
                foreach (NumberDial candidate in dials)
                    if (candidate != null) candidate.enabled = false;
            if (revealPuzzleColorOnSuccess) RevealPuzzleColor();
        }

        /// <summary>
        /// Called once when the player releases a captured dial. Every completed
        /// hold is a fresh answer check, including holds made after a failure.
        /// </summary>
        public void EvaluateDialInput()
        {
            if (solved) return;
            if (!hasSubmittedCompleteCombination && !AllDialsHaveLeftStartingValue()) return;

            hasSubmittedCompleteCombination = true;
            foreach (NumberDial candidate in dials)
                if (candidate == null || candidate.CurrentValue != candidate.TargetValue)
                {
                    PlayFailedAttemptFeedback();
                    return;
                }

            solved = true;
            if (lockWhenSolved)
                foreach (NumberDial candidate in dials)
                    candidate.enabled = false;
            if (revealPuzzleColorOnSuccess) RevealPuzzleColor();
            if (correctCombinationClip != null)
                audioSource.PlayOneShot(correctCombinationClip, correctCombinationVolume);
            OceanCampaignManager.Active?.CompleteStage(campaignStageIndex);
        }

        private bool AllDialsHaveLeftStartingValue()
        {
            if (dials == null || dials.Length == 0) return false;
            foreach (NumberDial dial in dials)
                if (dial == null || dial.CurrentValue == 1) return false;
            return true;
        }

        private void RevealPuzzleColor()
        {
            // The lock is one child of the authored Puzzle 3 blockout; reveal its
            // sibling turtle and pattern-wall visuals too.
            Transform puzzleRoot = transform.parent != null ? transform.parent : transform;
            foreach (DualViewMaterial visual in puzzleRoot.GetComponentsInChildren<DualViewMaterial>(true))
            {
                visual.SetColorViewEnabled(true);
                visual.SetColorInGreyscaleCamera(true);
            }
        }

        private void PlayFailedAttemptFeedback()
        {
            TelemetryManager.RecordFailedAttempt();
            if (wrongCombinationClip != null)
                audioSource.PlayOneShot(wrongCombinationClip, wrongCombinationVolume);
            if (feedbackRoutine != null) StopCoroutine(feedbackRoutine);
            feedbackRoutine = StartCoroutine(FlashFailure());
        }

        private IEnumerator FlashFailure()
        {
            List<Renderer> renderers = new List<Renderer>();
            foreach (NumberDial dial in dials)
                if (dial != null) renderers.AddRange(dial.GetComponentsInChildren<Renderer>(true));

            MaterialPropertyBlock[] originals = new MaterialPropertyBlock[renderers.Count];
            for (int index = 0; index < renderers.Count; index++)
            {
                originals[index] = new MaterialPropertyBlock();
                renderers[index].GetPropertyBlock(originals[index]);
            }

            float elapsed = 0f;
            do
            {
                float phase = failureFlashDuration <= 0f ? 1f : Mathf.Clamp01(elapsed / failureFlashDuration);
                float brightness = Mathf.Sin(phase * Mathf.PI) * failureFlashBrightness;
                for (int index = 0; index < renderers.Count; index++)
                {
                    MaterialPropertyBlock properties = new MaterialPropertyBlock();
                    renderers[index].GetPropertyBlock(properties);
                    properties.SetColor(FresnelColor, failureFlashColor);
                    properties.SetFloat(FresnelBrightness, brightness);
                    properties.SetFloat(FresnelPower, failureFlashRimSize);
                    renderers[index].SetPropertyBlock(properties);
                }
                if (failureFlashDuration <= 0f) break;
                yield return null;
                elapsed += Time.deltaTime;
            } while (elapsed < failureFlashDuration);

            for (int index = 0; index < renderers.Count; index++)
                if (renderers[index] != null) renderers[index].SetPropertyBlock(originals[index]);
            feedbackRoutine = null;
        }
    }
}
