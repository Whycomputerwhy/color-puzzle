using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ColorPuzzle
{
    /// <summary>Tracks puzzle progress in one continuous, authored ocean room.</summary>
    public sealed class OceanCampaignManager : MonoBehaviour
    {
        private const string CheckpointKey = "ColorPuzzle.OceanCampaign.Checkpoint";
        private const string MagnifierSkillKey = "ColorPuzzle.OceanCampaign.MagnifierSkill";
        private const string CompletedStagesKey = "ColorPuzzle.OceanCampaign.CompletedStages";

        [Header("Campaign Structure")]
        [SerializeField] private FirstPersonPlayer player;
        [Tooltip("Scene parent containing the puzzle stage prefabs in play order.")]
        [SerializeField] private Transform stageContainer;
        [Tooltip("The magnifier is the reward revealed by Puzzle 1, not a prerequisite for interaction.")]
        [SerializeField] private MagnifierPickup magnifierPickup;
        [SerializeField] private Vector3 checkpointSpawnOffset = new Vector3(0f, 2f, 2f);

        [Header("Opening Tutorial")]
        [Tooltip("Moves the first spawn back along the authored checkpoint's facing direction, leaving a short walk into Puzzle 1.")]
        [SerializeField] private bool useOpeningTutorialSpawn = true;
        [SerializeField] private Vector3 openingTutorialAdditionalOffset = new Vector3(0f, 0f, -5f);

        [Header("Spawn Grounding")]
        [SerializeField, Min(0f)] private float spawnGroundProbeStartHeight = 8f;
        [SerializeField, Min(0.1f)] private float spawnGroundProbeDistance = 20f;

        [Header("Future Stage Streaming")]
        [Tooltip("Reserved for later additive loading. It intentionally does nothing for now; all four editable stages stay loaded.")]
        [SerializeField] private bool loadUpcomingStageWhenPreviousIsCompleted;

        [Header("Checkpoint Behaviour")]
        [SerializeField] private bool resumeFromSavedCheckpoint = true;

        private readonly List<OceanCampaignStage> stages = new List<OceanCampaignStage>();
        private static int pendingDeveloperStageNumber = -1;
        private int currentStageIndex;
        private bool[] completedStages;
        private bool campaignComplete;
        private bool magnifierSkillUnlocked;
        public static OceanCampaignManager Active { get; private set; }
        public int CurrentStageNumber => currentStageIndex + 1;
        public bool IsComplete => campaignComplete;
        public bool HasMagnifierSkill => magnifierSkillUnlocked;

        private void Awake() => Active = this;

        private void Start()
        {
            if (player == null) player = FindAnyObjectByType<FirstPersonPlayer>();
            if (!CollectAuthoredStages() || !ValidateStages())
            {
                enabled = false;
                return;
            }

            currentStageIndex = Mathf.Clamp(resumeFromSavedCheckpoint ? PlayerPrefs.GetInt(CheckpointKey, 0) : 0, 0, stages.Count - 1);
            completedStages = new bool[stages.Count];
            RestoreCompletedStageProgress();
            magnifierSkillUnlocked = PlayerPrefs.GetInt(MagnifierSkillKey, 0) != 0 || currentStageIndex > 0;
            player?.GetComponent<MagnifierView>()?.SetMagnifierUnlocked(magnifierSkillUnlocked);
            // Stage 1 reveals this reward only after the shell combination succeeds.
            magnifierPickup?.SetAvailable(false);
            ConfigureAuthoredProgression();
            ApplyPendingDeveloperStageJump();
            StartCoroutine(PlacePlayerAfterSetup());
        }

        private IEnumerator PlacePlayerAfterSetup()
        {
            yield return null;
            MovePlayerToCheckpoint(currentStageIndex);
            TelemetryManager.BeginCampaignStage(currentStageIndex + 1);
            RestoreColorForFinalStageIfNeeded();
        }

        private bool CollectAuthoredStages()
        {
            stages.Clear();
            if (stageContainer == null)
            {
                Debug.LogError("Assign the scene's OCEAN CAMPAIGN STAGES parent to Stage Container.", this);
                return false;
            }
            foreach (Transform child in stageContainer)
                if (child.gameObject.activeInHierarchy && child.TryGetComponent(out OceanCampaignStage stage))
                    stages.Add(stage);
            if (stages.Count == 4) return true;
            Debug.LogError($"Ocean campaign expected 4 active puzzle stages under {stageContainer.name}, found {stages.Count}.", this);
            return false;
        }

        private bool ValidateStages()
        {
            for (int index = 0; index < stages.Count; index++)
            {
                if (stages[index].CheckpointSpawn == null)
                {
                    Debug.LogError($"Puzzle stage {index + 1} needs a checkpoint spawn.", stages[index]);
                    return false;
                }
            }
            return true;
        }

        private void ConfigureAuthoredProgression()
        {
            for (int index = 0; index < stages.Count; index++)
            {
                stages[index].CompletionBeacon?.Configure(this, index);
                stages[index].ShellPuzzle?.Configure(this, index);
                if (!IsStageComplete(index)) continue;
                stages[index].ShellPuzzle?.CompleteForDeveloperSkip();
                stages[index].DialLock?.CompleteForProgressRestore();
                stages[index].CompletionBeacon?.CompleteForDeveloperSkip();
            }
        }

        public bool CompleteStage(int stageIndex)
        {
            if (!CanAttemptStage(stageIndex)) return false;
            completedStages[stageIndex] = true;

            // Puzzle 2 and Puzzle 3 form an unordered pair. Their telemetry is
            // finalized against the puzzle actually solved, not a fixed campaign slot.
            if (stageIndex == 1 || stageIndex == 2)
                TelemetryManager.CompleteCampaignPuzzle(stageIndex + 1);

            if (stageIndex >= stages.Count - 1)
            {
                campaignComplete = true;
                TelemetryManager.CompleteCampaignStage(stageIndex + 1);
                RestoreColorForFinalStageIfNeeded();
                return true;
            }

            if (stageIndex == 0)
                currentStageIndex = 1;
            else if (stageIndex == 1 || stageIndex == 2)
                currentStageIndex = !IsStageComplete(1) ? 1 : !IsStageComplete(2) ? 2 : 3;
            else
                currentStageIndex = Mathf.Min(stageIndex + 1, stages.Count - 1);

            SaveProgress();
            return true;
        }

        /// <summary>
        /// Puzzle 2 and Puzzle 3 unlock together after Puzzle 1, so either can be
        /// attempted first. Later stages remain ordered after both are complete.
        /// </summary>
        public bool CanAttemptStage(int stageIndex)
        {
            if (stageIndex < 0 || completedStages == null || stageIndex >= completedStages.Length ||
                IsStageComplete(stageIndex)) return false;
            if (stageIndex == 0) return !IsStageComplete(0);
            if (stageIndex == 1 || stageIndex == 2) return IsStageComplete(0);
            if (stageIndex == 3) return IsStageComplete(1) && IsStageComplete(2);
            return stageIndex == currentStageIndex;
        }

        /// <summary>Called when a player first manipulates a currently available puzzle.</summary>
        public void NotifyPuzzleInteraction(int stageIndex)
        {
            if (!CanAttemptStage(stageIndex)) return;
            if (stageIndex == 1 || stageIndex == 2)
                TelemetryManager.BeginCampaignPuzzle(stageIndex + 1);
        }

        /// <summary>Called by Puzzle 1 after its casing has opened.</summary>
        public void RevealMagnifierReward()
        {
            if (!magnifierSkillUnlocked)
                magnifierPickup?.SetAvailable(true);
        }

        public bool AcquireMagnifier()
        {
            if (magnifierSkillUnlocked || player == null) return false;
            magnifierSkillUnlocked = true;
            PlayerPrefs.SetInt(MagnifierSkillKey, 1);
            PlayerPrefs.Save();
            player.GetComponent<MagnifierView>()?.UnlockMagnifier();
            magnifierPickup?.SetAvailable(false);
            if (currentStageIndex == 0)
                TelemetryManager.BeginStageOnePearlPuzzle();
            return true;
        }

        public void ReachNextStage(int doorStageIndex)
        {
            if (doorStageIndex != currentStageIndex) return;
            TelemetryManager.CompleteCampaignStage(doorStageIndex + 1);
            currentStageIndex = Mathf.Min(doorStageIndex + 1, stages.Count - 1);
            if (completedStages != null && doorStageIndex >= 0 && doorStageIndex < completedStages.Length)
                completedStages[doorStageIndex] = true;
            SaveProgress();
            TelemetryManager.BeginCampaignStage(currentStageIndex + 1);
            RequestUpcomingStageLoad(currentStageIndex + 1);
            RestoreColorForFinalStageIfNeeded();
        }

        /// <summary>Developer-panel shortcut. Forward jumps stay in place; backward jumps reload the shared room to reset later puzzle state.</summary>
        public void TeleportToStage(int stageNumber)
        {
            int destinationStageIndex = Mathf.Clamp(stageNumber - 1, 0, stages.Count - 1);
            if (destinationStageIndex < currentStageIndex)
            {
                // All four stages live in one scene. Reloading only for a backward
                // developer jump restores the selected stage and every later stage to
                // their authored start state, without changing saved player progress.
                pendingDeveloperStageNumber = destinationStageIndex + 1;
                SceneManager.LoadScene(SceneManager.GetActiveScene().name);
                return;
            }

            currentStageIndex = destinationStageIndex;
            CompletePreviousStagesForDeveloperJump(currentStageIndex);
            player?.GetComponent<MagnifierView>()?.UnlockMagnifier();
            magnifierSkillUnlocked = true;
            magnifierPickup?.SetAvailable(false);
            MovePlayerToCheckpoint(currentStageIndex);
            TelemetryManager.BeginCampaignStage(currentStageIndex + 1);
            RestoreColorForFinalStageIfNeeded();
        }

        private void ApplyPendingDeveloperStageJump()
        {
            if (pendingDeveloperStageNumber < 1) return;

            currentStageIndex = Mathf.Clamp(pendingDeveloperStageNumber - 1, 0, stages.Count - 1);
            pendingDeveloperStageNumber = -1;
            CompletePreviousStagesForDeveloperJump(currentStageIndex);
            magnifierSkillUnlocked = true;
            player?.GetComponent<MagnifierView>()?.UnlockMagnifier();
            magnifierPickup?.SetAvailable(false);
        }

        private void CompletePreviousStagesForDeveloperJump(int destinationStageIndex)
        {
            for (int index = 0; index < destinationStageIndex; index++)
            {
                if (completedStages != null && index < completedStages.Length)
                    completedStages[index] = true;
                stages[index].ShellPuzzle?.CompleteForDeveloperSkip();
                stages[index].DialLock?.CompleteForProgressRestore();
                stages[index].CompletionBeacon?.CompleteForDeveloperSkip();
            }
        }

        public void ReloadCheckpoint()
        {
            if (!enabled) return;
            currentStageIndex = Mathf.Clamp(PlayerPrefs.GetInt(CheckpointKey, currentStageIndex), 0, stages.Count - 1);
            RestoreCompletedStageProgress();
            magnifierSkillUnlocked = PlayerPrefs.GetInt(MagnifierSkillKey, 0) != 0 || currentStageIndex > 0;
            player?.GetComponent<MagnifierView>()?.SetMagnifierUnlocked(magnifierSkillUnlocked);
            if (magnifierSkillUnlocked)
            {
                magnifierPickup?.SetAvailable(false);
                MovePlayerToCheckpoint(currentStageIndex);
            }
            else
            {
                magnifierPickup?.SetAvailable(false);
                MovePlayerToCheckpoint(currentStageIndex);
            }
            TelemetryManager.BeginCampaignStage(currentStageIndex + 1);
            RestoreColorForFinalStageIfNeeded();
        }

        public void ResetSavedProgress()
        {
            PlayerPrefs.DeleteKey(CheckpointKey);
            PlayerPrefs.DeleteKey(MagnifierSkillKey);
            PlayerPrefs.DeleteKey(CompletedStagesKey);
            PlayerPrefs.Save();
        }

        private bool IsStageComplete(int stageIndex) =>
            completedStages != null && stageIndex >= 0 && stageIndex < completedStages.Length && completedStages[stageIndex];

        private void RestoreCompletedStageProgress()
        {
            if (completedStages == null) return;
            int progressMask = PlayerPrefs.GetInt(CompletedStagesKey, 0);
            for (int index = 0; index < completedStages.Length; index++)
                completedStages[index] = (progressMask & (1 << index)) != 0;

            // Existing saves used only a checkpoint. Treat prior checkpoint stages
            // as complete so they continue to load coherently after this upgrade.
            for (int index = 0; index < currentStageIndex; index++)
                completedStages[index] = true;
        }

        private void SaveProgress()
        {
            int progressMask = 0;
            if (completedStages != null)
                for (int index = 0; index < completedStages.Length; index++)
                    if (completedStages[index]) progressMask |= 1 << index;
            PlayerPrefs.SetInt(CompletedStagesKey, progressMask);
            PlayerPrefs.SetInt(CheckpointKey, currentStageIndex);
            PlayerPrefs.Save();
        }

        public void ResetProgress()
        {
            ResetSavedProgress();
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        private void RestoreColorForFinalStageIfNeeded()
        {
            if (currentStageIndex < stages.Count - 1 || !campaignComplete) return;
            MagnifierView magnifier = player != null ? player.GetComponent<MagnifierView>() : FindAnyObjectByType<MagnifierView>();
            magnifier?.RestoreWorldColor(player != null ? player.transform.position : transform.position);
        }

        private void MovePlayerToCheckpoint(int stageIndex)
        {
            if (player == null || stageIndex < 0 || stageIndex >= stages.Count) return;
            Transform checkpoint = stages[stageIndex].CheckpointSpawn;
            if (checkpoint == null) return;
            CharacterController controller = player.GetComponent<CharacterController>();
            if (controller != null) controller.enabled = false;
            Vector3 spawnOffset = checkpointSpawnOffset;
            if (stageIndex == 0 && useOpeningTutorialSpawn)
                spawnOffset += checkpoint.TransformDirection(openingTutorialAdditionalOffset);
            Vector3 spawnPosition = checkpoint.position + spawnOffset;
            if (Physics.Raycast(
                    spawnPosition + Vector3.up * spawnGroundProbeStartHeight,
                    Vector3.down,
                    out RaycastHit groundHit,
                    spawnGroundProbeStartHeight + spawnGroundProbeDistance,
                    ~0,
                    QueryTriggerInteraction.Ignore))
            {
                spawnPosition.y = groundHit.point.y;
            }
            player.transform.SetPositionAndRotation(spawnPosition, checkpoint.rotation);
            if (controller != null) controller.enabled = true;
        }

        private void RequestUpcomingStageLoad(int stageIndex)
        {
            // Streaming will be added after the stage content is established. Keeping this hook here
            // lets us add additive loading later without changing the progression/checkpoint flow.
            if (!loadUpcomingStageWhenPreviousIsCompleted) return;
            Debug.Log($"Stage streaming is reserved for a future pass (requested stage {stageIndex + 1}).", this);
        }

        private void OnDestroy()
        {
            if (Active == this) Active = null;
        }

        private void OnApplicationQuit()
        {
            if (campaignComplete) ResetSavedProgress();
        }
    }
}
