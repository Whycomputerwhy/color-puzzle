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

        // CENTRALIZED ADJUSTABLE VICTORY EFFECT PARAMETERS
        [Header("Victory effect")]
        [SerializeField, Min(0f)] private float pearlGlowDuration = 0.8f;
        [SerializeField, Range(0f, 2f)] private float pearlGlowBrightness = 1.1f;
        [SerializeField, Range(0f, 0.5f)] private float pearlGlowScale = 0.12f;
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
            SetPuzzleColorVisible(false);
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
        }

        public bool Owns(ShellPuzzlePiece piece) =>
            piece != null && piece.transform.IsChildOf(transform);

        public bool TryPlace(ShellPuzzlePiece key, ShellPuzzlePiece keyhole)
        {
            if (!IsReady || complete || campaign == null || key == null || keyhole == null ||
                !Owns(key) || !Owns(keyhole) || keyhole.IsOccupied ||
                campaign.CurrentStageNumber != stageIndex + 1)
                return false;

            if (key.Kind != ShellPuzzlePiece.PieceKind.Key ||
                keyhole.Kind != ShellPuzzlePiece.PieceKind.Keyhole)
                return false;

            bool correct = key.PairNumber == keyhole.PairNumber &&
                           Mathf.Approximately(key.FitDiameter, keyhole.FitDiameter);
            key.PlaceOn(keyhole, correct);
            TelemetryManager.RecordObjectDrop();
            if (!correct)
            {
                TelemetryManager.RecordFailedAttempt();
                Play(wrongPlacementClip, wrongPlacementVolume);
                return true;
            }

            foreach (ShellPuzzlePiece hole in keyholes)
                if (!hole.IsCorrectlyFilled) return true;

            // The shell puzzle can be reused by later stages; only Puzzle 1 owns this telemetry milestone.
            if (stageIndex == 0)
                TelemetryManager.CompleteStageOnePearlPuzzle();
            else if (stageIndex == 1)
                // Stage 2's record ends on the final correct pattern/keyhole match,
                // not on a later door interaction.
                TelemetryManager.CompleteCampaignPuzzle(stageIndex + 1);
            if (!campaign.CompleteStage(stageIndex)) return true;
            complete = true;
            foreach (ShellPuzzlePiece hole in keyholes)
            {
                hole.LockPlacedKey();
                hole.RevealKeyholeColor();
            }
            SetPuzzleColorVisible(true);
            Play(correctPlacementMusic, correctPlacementVolume);
            StartCoroutine(PlayVictoryEffect());
            return true;
        }

        private IEnumerator PlayVictoryEffect()
        {
            // MaterialPropertyBlocks affect each actual pearl mesh in both camera views;
            // the cel shader's Fresnel term gives a small rim without a covering proxy.
            Renderer[] pearls = new Renderer[keyholes.Count];
            Vector3[] originalScales = new Vector3[keyholes.Count];
            MaterialPropertyBlock[] originalBlocks = new MaterialPropertyBlock[keyholes.Count];
            MaterialPropertyBlock[] glowBlocks = new MaterialPropertyBlock[keyholes.Count];
            for (int i = 0; i < keyholes.Count; i++)
            {
                Renderer pearl = keyholes[i].PlacedKey?.PearlRenderer;
                pearls[i] = pearl;
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
                float phase = pearlGlowDuration <= 0f ? 1f : Mathf.Clamp01(elapsed / pearlGlowDuration);
                float glow = Mathf.Sin(phase * Mathf.PI);
                for (int i = 0; i < pearls.Length; i++)
                {
                    if (pearls[i] == null) continue;
                    glowBlocks[i].SetFloat(FresnelBrightness, glow * pearlGlowBrightness);
                    pearls[i].SetPropertyBlock(glowBlocks[i]);
                    pearls[i].transform.localScale = originalScales[i] * (1f + glow * pearlGlowScale);
                }
                if (pearlGlowDuration <= 0f) break;
                yield return null;
                elapsed += Time.deltaTime;
            } while (elapsed < pearlGlowDuration);

            for (int i = 0; i < pearls.Length; i++)
            {
                if (pearls[i] == null) continue;
                pearls[i].transform.localScale = originalScales[i];
                pearls[i].SetPropertyBlock(originalBlocks[i]);
            }

            if (casing == null)
            {
                if (stageIndex == 0) campaign?.RevealMagnifierReward();
                yield break;
            }
            DualViewMaterial[] casingViews = casing.GetComponentsInChildren<DualViewMaterial>(true);
            elapsed = 0f;
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

        private void SetPuzzleColorVisible(bool visible)
        {
            // Only puzzle pieces and the optional casing participate in this reveal.
            // Other decorative children of a stage, such as Puzzle 2's fish school, stay colorful.
            foreach (ShellPuzzlePiece piece in GetComponentsInChildren<ShellPuzzlePiece>(true))
                foreach (DualViewMaterial visual in piece.GetComponentsInChildren<DualViewMaterial>(true))
                {
                    visual.SetColorViewEnabled(visible);
                    visual.SetColorInGreyscaleCamera(visible);
                }

            if (casing != null)
                foreach (DualViewMaterial visual in casing.GetComponentsInChildren<DualViewMaterial>(true))
                {
                    visual.SetColorViewEnabled(visible);
                    visual.SetColorInGreyscaleCamera(visible);
                }
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
