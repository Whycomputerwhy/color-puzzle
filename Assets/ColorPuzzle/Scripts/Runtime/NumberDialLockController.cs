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
        [Tooltip("Optional Puzzle 4 center slots. When assigned, each dial also requires its matching shape key.")]
        [SerializeField] private FinalDialKeySlot[] keySlots;
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
        [SerializeField] private FinalHeartVictory finalHeartVictory;

        [Header("Puzzle 3 solved dial appearance")]
        [Tooltip("Applies only to a number-only lock (Puzzle 3), not the final key-and-dial puzzle.")]
        [SerializeField] private bool colorNumberDialsOnSuccess = true;
        [Tooltip("Keeps Puzzle 3's dial geometry monochrome in the magnifier until the lock is solved.")]
        [SerializeField] private bool hideDialColorUntilSolved = true;
        [Tooltip("One colour per dial, left to right. Extra dials repeat this sequence.")]
        [SerializeField] private Color[] completedDialColors =
        {
            // Shared Puzzle Blue / Yellow / Red / Green palette, matching the
            // pearl, fish, and key-stone materials used throughout the campaign.
            new Color(0.25f, 0.77f, 0.96f, 1f),
            new Color(0.97f, 0.79f, 0.28f, 1f),
            new Color(0.94f, 0.28f, 0.28f, 1f),
            new Color(0.22f, 0.78f, 0.36f, 1f)
        };

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

            if (ShouldHideDialColorUntilSolved())
                SetNumberDialColorVisible(false);
        }

        public bool CanRotate(NumberDial dial) =>
            !solved && dial != null && System.Array.IndexOf(dials, dial) >= 0;

        public bool TryRotate(NumberDial dial)
        {
            OceanCampaignManager campaign = OceanCampaignManager.Active;
            if (campaign != null && !campaign.CanAttemptStage(campaignStageIndex))
            {
                campaign.NotifyBlockedPuzzleInteraction(campaignStageIndex);
                return false;
            }
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
            // Puzzle 4 adds one key input per dial. Do not call an incomplete
            // board a failure until every center slot has received a key.
            if (!AllKeySlotsAreOccupied()) return;

            hasSubmittedCompleteCombination = true;
            if (!HasCorrectCombination())
            {
                PlayFailedAttemptFeedback();
                return;
            }

            solved = true;
            if (lockWhenSolved)
                foreach (NumberDial candidate in dials)
                    candidate.enabled = false;
            if (revealPuzzleColorOnSuccess) RevealPuzzleColor();
            GameAudioMaster master = GameAudioMaster.Instance;
            if (master == null || master.Library == null || !master.PlaySfx(master.Library.puzzleCorrect))
            if (correctCombinationClip != null)
                audioSource.PlayOneShot(correctCombinationClip, correctCombinationVolume);
            master?.PlaySfx(master.Library?.puzzleElementColorReveal);
            if (finalHeartVictory != null)
                finalHeartVictory.Play();
            else
                OceanCampaignManager.Active?.CompleteStage(campaignStageIndex);
        }

        private bool HasCorrectCombination()
        {
            foreach (NumberDial candidate in dials)
                if (candidate == null || candidate.CurrentValue != candidate.TargetValue) return false;
            if (keySlots == null || keySlots.Length == 0) return true;
            if (keySlots.Length != dials.Length) return false;
            foreach (FinalDialKeySlot slot in keySlots)
                if (slot == null || !slot.IsCorrectlyFilled) return false;
            return true;
        }

        private bool AllKeySlotsAreOccupied()
        {
            if (keySlots == null || keySlots.Length == 0) return true;
            foreach (FinalDialKeySlot slot in keySlots)
                if (slot == null || slot.PlacedKey == null) return false;
            return true;
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
            foreach (FinalDialKeyColorLock keyColor in puzzleRoot.GetComponentsInChildren<FinalDialKeyColorLock>(true))
                keyColor.SetRevealed(true);
            if (keySlots != null)
                foreach (FinalDialKeySlot slot in keySlots)
                    if (slot != null && slot.IsCorrectlyFilled) slot.RevealColorInNormalView();

            RevealCompletedNumberDials();
        }

        /// <summary>
        /// Puzzle 3's wheel labels use a depth-writing baked font rather than a
        /// DualViewMaterial, so revealing only the mesh materials left the dials
        /// visually monochrome. Reveal both the geometry and its label ink here.
        /// </summary>
        private void RevealCompletedNumberDials()
        {
            // Puzzle 4 already reveals each combined dial individually. This is
            // specifically the all-at-once success presentation for Puzzle 3.
            if (!colorNumberDialsOnSuccess || (keySlots != null && keySlots.Length > 0) ||
                dials == null || completedDialColors == null || completedDialColors.Length == 0)
                return;

            for (int index = 0; index < dials.Length; index++)
            {
                NumberDial dial = dials[index];
                if (dial == null) continue;
                Color tint = completedDialColors[index % completedDialColors.Length];

                foreach (DualViewMaterial visual in dial.GetComponentsInChildren<DualViewMaterial>(true))
                {
                    visual.SetColorViewEnabled(true);
                    visual.SetColorInGreyscaleCamera(true);
                }

                foreach (TextMesh label in dial.GetComponentsInChildren<TextMesh>(true))
                    if (label != null) label.color = tint;
            }
        }

        private bool ShouldHideDialColorUntilSolved()
        {
            // Stage 3 is the number-only turtle lock. Puzzle 4 deliberately
            // manages its own key and dial colour state as inputs are solved.
            return hideDialColorUntilSolved && campaignStageIndex == 2 &&
                   (keySlots == null || keySlots.Length == 0);
        }

        private void SetNumberDialColorVisible(bool visible)
        {
            if (dials == null) return;
            foreach (NumberDial dial in dials)
            {
                if (dial == null) continue;
                foreach (DualViewMaterial visual in dial.GetComponentsInChildren<DualViewMaterial>(true))
                {
                    visual.SetColorViewEnabled(visible);
                    visual.SetColorInGreyscaleCamera(visible);
                }
            }
        }

        private void PlayFailedAttemptFeedback()
        {
            TelemetryManager.RecordFailedAttempt();
            OceanCampaignManager.Active?.NotifyPuzzleFailedAttempt(campaignStageIndex);
            GameAudioMaster master = GameAudioMaster.Instance;
            if ((master == null || master.Library == null || !master.PlaySfx(master.Library.puzzleFail)) &&
                wrongCombinationClip != null)
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
