using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ColorPuzzle.Rendering;

namespace ColorPuzzle
{
    /// <summary>Stage 1's four-pair completion rule. It uses the authored scene instances.</summary>
    public sealed class ShellPuzzleController : MonoBehaviour
    {
        [Header("Completion audio")]
        [SerializeField] private AudioClip correctPlacementMusic;
        [SerializeField, Range(0f, 1f)] private float correctPlacementVolume = 0.3f;
        [SerializeField] private AudioClip wrongPlacementClip;
        [SerializeField, Range(0f, 1f)] private float wrongPlacementVolume = 0.42f;

        // CENTRALIZED COMBINATION-FEEDBACK PARAMETERS — shared by Puzzle 1 and Puzzle 2.
        [Header("Combination feedback")]
        [SerializeField, Min(0f)] private float pearlGlowDuration = 0.8f;
        [SerializeField, Range(0f, 2f)] private float pearlGlowBrightness = 1.1f;
        [SerializeField, Range(0f, 0.5f)] private float pearlGlowScale = 0.12f;
        [SerializeField, Min(0f)] private float failedCombinationGlowDuration = 0.28f;
        [SerializeField, Range(0f, 2f)] private float failedCombinationGlowBrightness = 0.55f;
        [SerializeField, Range(0f, 0.5f)] private float failedCombinationGlowScale = 0.04f;
        [SerializeField, Min(0f)] private float casingFadeDuration = 0.9f;

        private static readonly int FresnelBrightness = Shader.PropertyToID("_FresnelBrightness");
        private static readonly int FresnelPower = Shader.PropertyToID("_FresnelPower");
        private static readonly int FresnelColor = Shader.PropertyToID("_FresnelColor");

        private readonly List<ShellPuzzlePiece> keyholes = new List<ShellPuzzlePiece>();
        private OceanCampaignManager campaign;
        private AudioSource audioSource;
        private GameObject casing;
        private int stageIndex;
        private bool complete;
        public bool IsReady { get; private set; }

        private void Awake()
        {
            foreach (ShellPuzzlePiece piece in GetComponentsInChildren<ShellPuzzlePiece>(true))
                if (piece.Kind == ShellPuzzlePiece.PieceKind.Keyhole)
                    keyholes.Add(piece);

            Transform casingTransform = FindChildNamed(transform, "Clam Casing");
            casing = casingTransform != null ? casingTransform.gameObject : null;
            SetPuzzleAppearance(false, false);
            IsReady = ValidatePairs();
            if (!IsReady)
                Debug.LogError("Shell puzzle needs exactly four unique key/keyhole pairs.", this);

            audioSource = GetComponent<AudioSource>();
            if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
        }

        public void Configure(OceanCampaignManager owner, int index)
        {
            campaign = owner;
            stageIndex = index;
            // Puzzle 1 teaches the magnifier: its pieces are colourful through the
            // lens from the beginning, but remain monochrome in the normal view.
            SetPuzzleAppearance(stageIndex == 0, false);
        }

        public bool Owns(ShellPuzzlePiece piece) =>
            piece != null && piece.transform.IsChildOf(transform);

        /// <summary>Marks this authored puzzle as solved when using the developer stage jump.</summary>
        public void CompleteForDeveloperSkip()
        {
            if (complete || !IsReady) return;

            ShellPuzzlePiece[] pieces = GetComponentsInChildren<ShellPuzzlePiece>(true);
            foreach (ShellPuzzlePiece keyhole in keyholes)
            {
                if (keyhole.IsOccupied) continue;
                foreach (ShellPuzzlePiece key in pieces)
                {
                    if (key.Kind != ShellPuzzlePiece.PieceKind.Key ||
                        key.PairNumber != keyhole.PairNumber ||
                        !Mathf.Approximately(key.FitDiameter, keyhole.FitDiameter)) continue;
                    key.PlaceForDeveloper(keyhole);
                    break;
                }
            }

            complete = true;
            foreach (ShellPuzzlePiece keyhole in keyholes)
            {
                keyhole.LockPlacedKey();
                keyhole.RevealKeyholeColor();
            }
            SetPuzzleAppearance(true, true);
            RevealPuzzleTwoFishInNormalView();
            if (casing != null) casing.SetActive(false);
        }

        public bool TryPlace(ShellPuzzlePiece key, ShellPuzzlePiece keyhole,
            out ShellPuzzlePiece swappedOutKey)
        {
            swappedOutKey = null;
            if (!IsReady || complete || campaign == null || key == null || keyhole == null ||
                !Owns(key) || !Owns(keyhole) ||
                !campaign.CanAttemptStage(stageIndex))
                return false;

            campaign.NotifyPuzzleInteraction(stageIndex);

            if (key.Kind != ShellPuzzlePiece.PieceKind.Key ||
                keyhole.Kind != ShellPuzzlePiece.PieceKind.Keyhole)
                return false;

            bool correct = key.PairNumber == keyhole.PairNumber &&
                           Mathf.Approximately(key.FitDiameter, keyhole.FitDiameter);
            ShellPuzzlePiece previousKey = keyhole.PlacedKey;
            key.PlaceOn(keyhole, correct);
            TelemetryManager.RecordObjectDrop();
            if (previousKey != null)
            {
                if (!previousKey.PickUpForDialSlotSwap()) return false;
                swappedOutKey = previousKey;
                TelemetryManager.RecordObjectPickup();
            }

            // A placement is not an attempt by itself. Let the player construct the
            // complete four-piece combination first, including deliberately wrong
            // arrangements, then assess the result once every keyhole is occupied.
            foreach (ShellPuzzlePiece hole in keyholes)
                if (!hole.IsOccupied) return true;

            foreach (ShellPuzzlePiece hole in keyholes)
                if (!hole.IsCorrectlyFilled)
                {
                    TelemetryManager.RecordFailedAttempt();
                    Play(wrongPlacementClip, wrongPlacementVolume);
                    // Begins in the same frame as the audio: a short, restrained
                    // whole-combination response rather than a penalty per key.
                    StartCoroutine(PlayPearlGlow(
                        failedCombinationGlowDuration,
                        failedCombinationGlowBrightness,
                        failedCombinationGlowScale));
                    return true;
                }

            // The shell puzzle can be reused by later stages; only Puzzle 1 owns this telemetry milestone.
            if (stageIndex == 0)
                TelemetryManager.CompleteStageOnePearlPuzzle();
            if (!campaign.CompleteStage(stageIndex)) return true;
            complete = true;
            foreach (ShellPuzzlePiece hole in keyholes)
            {
                hole.LockPlacedKey();
                hole.RevealKeyholeColor();
            }
            SetPuzzleAppearance(true, true);
            RevealPuzzleTwoFishInNormalView();
            Play(correctPlacementMusic, correctPlacementVolume);
            StartCoroutine(PlayVictoryEffect());
            return true;
        }

        private IEnumerator PlayVictoryEffect()
        {
            yield return StartCoroutine(PlayPearlGlow(
                pearlGlowDuration, pearlGlowBrightness, pearlGlowScale));

            if (casing == null)
            {
                if (stageIndex == 0) campaign?.RevealMagnifierReward();
                yield break;
            }
            DualViewMaterial[] casingViews = casing.GetComponentsInChildren<DualViewMaterial>(true);
            float elapsed = 0f;
            do
            {
                float phase = casingFadeDuration <= 0f ? 1f : Mathf.Clamp01(elapsed / casingFadeDuration);
                float opacity = 1f - phase * phase * (3f - 2f * phase);
                foreach (DualViewMaterial view in casingViews)
                    if (view != null) view.SetOpacityMultiplier(opacity);
                if (casingFadeDuration <= 0f) break;
                yield return null;
                elapsed += Time.deltaTime;
            } while (elapsed < casingFadeDuration);
            casing.SetActive(false);
            if (stageIndex == 0) campaign?.RevealMagnifierReward();
        }

        /// <summary>
        /// Pulses every placed key's pearl. Material property blocks preserve the
        /// authored colour materials and avoid adding a proxy or another render pass.
        /// </summary>
        private IEnumerator PlayPearlGlow(float duration, float brightness, float scale)
        {
            // MaterialPropertyBlocks affect each actual pearl mesh in both camera views;
            // the cel shader's Fresnel term gives a small rim without a covering proxy.
            List<Renderer> feedbackRenderers = new List<Renderer>();
            foreach (ShellPuzzlePiece keyhole in keyholes)
            {
                ShellPuzzlePiece placedKey = keyhole.PlacedKey;
                if (placedKey == null) continue;
                feedbackRenderers.AddRange(placedKey.FeedbackRenderers);
            }

            Renderer[] pearls = feedbackRenderers.ToArray();
            Vector3[] originalScales = new Vector3[pearls.Length];
            MaterialPropertyBlock[] originalBlocks = new MaterialPropertyBlock[pearls.Length];
            MaterialPropertyBlock[] glowBlocks = new MaterialPropertyBlock[pearls.Length];
            for (int i = 0; i < pearls.Length; i++)
            {
                Renderer pearl = pearls[i];
                if (pearl == null) continue;
                originalScales[i] = pearl.transform.localScale;
                originalBlocks[i] = new MaterialPropertyBlock();
                pearl.GetPropertyBlock(originalBlocks[i]);
                glowBlocks[i] = new MaterialPropertyBlock();
                pearl.GetPropertyBlock(glowBlocks[i]);
                glowBlocks[i].SetColor(FresnelColor, Color.white);
                glowBlocks[i].SetFloat(FresnelPower, 0.8f);
            }

            float elapsed = 0f;
            do
            {
                float phase = duration <= 0f ? 1f : Mathf.Clamp01(elapsed / duration);
                float glow = Mathf.Sin(phase * Mathf.PI);
                for (int i = 0; i < pearls.Length; i++)
                {
                    if (pearls[i] == null) continue;
                    glowBlocks[i].SetFloat(FresnelBrightness, glow * brightness);
                    pearls[i].SetPropertyBlock(glowBlocks[i]);
                    pearls[i].transform.localScale = originalScales[i] * (1f + glow * scale);
                }
                if (duration <= 0f) break;
                yield return null;
                elapsed += Time.deltaTime;
            } while (elapsed < duration);

            for (int i = 0; i < pearls.Length; i++)
            {
                if (pearls[i] == null) continue;
                pearls[i].transform.localScale = originalScales[i];
                pearls[i].SetPropertyBlock(originalBlocks[i]);
            }

        }

        private bool ValidatePairs()
        {
            if (keyholes.Count != 4) return false;
            HashSet<int> holeIds = new HashSet<int>();
            HashSet<int> keyIds = new HashSet<int>();
            Dictionary<int, float> sizes = new Dictionary<int, float>();
            foreach (ShellPuzzlePiece piece in GetComponentsInChildren<ShellPuzzlePiece>(true))
            {
                if (piece.Kind == ShellPuzzlePiece.PieceKind.Keyhole)
                {
                    if (!holeIds.Add(piece.PairNumber)) return false;
                    sizes[piece.PairNumber] = piece.FitDiameter;
                }
                else if (!keyIds.Add(piece.PairNumber)) return false;
            }
            if (keyIds.Count != 4 || !keyIds.SetEquals(holeIds)) return false;
            foreach (ShellPuzzlePiece piece in GetComponentsInChildren<ShellPuzzlePiece>(true))
                if (piece.Kind == ShellPuzzlePiece.PieceKind.Key &&
                    !Mathf.Approximately(piece.FitDiameter, sizes[piece.PairNumber])) return false;
            return true;
        }

        private void SetPuzzleAppearance(bool colorViewVisible, bool normalViewVisible)
        {
            // Only puzzle pieces and the optional casing participate in this reveal.
            // Other decorative children of a stage, such as Puzzle 2's fish school, stay colorful.
            foreach (ShellPuzzlePiece piece in GetComponentsInChildren<ShellPuzzlePiece>(true))
                foreach (DualViewMaterial visual in piece.GetComponentsInChildren<DualViewMaterial>(true))
                {
                    visual.SetColorViewEnabled(colorViewVisible);
                    visual.SetColorInGreyscaleCamera(normalViewVisible);
                }

            if (casing != null)
                foreach (DualViewMaterial visual in casing.GetComponentsInChildren<DualViewMaterial>(true))
                {
                    visual.SetColorViewEnabled(colorViewVisible);
                    visual.SetColorInGreyscaleCamera(normalViewVisible);
                }
        }

        private void RevealPuzzleTwoFishInNormalView()
        {
            if (stageIndex != 1) return;
            foreach (FishSchoolController school in GetComponentsInChildren<FishSchoolController>(true))
                school.SetColorInNormalView(true);
        }

        private void Play(AudioClip clip, float volume)
        {
            if (clip != null) audioSource.PlayOneShot(clip, volume);
        }

        private static Transform FindChildNamed(Transform parent, string name)
        {
            foreach (Transform child in parent)
            {
                if (child.name == name) return child;
                Transform nested = FindChildNamed(child, name);
                if (nested != null) return nested;
            }
            return null;
        }
    }
}
