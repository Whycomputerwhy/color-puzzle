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

        [Header("Campaign Structure")]
        [SerializeField] private FirstPersonPlayer player;
        [Tooltip("Scene parent containing the puzzle stage prefabs in play order.")]
        [SerializeField] private Transform stageContainer;
        [Tooltip("The magnifier is the reward revealed by Puzzle 1, not a prerequisite for interaction.")]
        [SerializeField] private MagnifierPickup magnifierPickup;
        [SerializeField] private Vector3 checkpointSpawnOffset = new Vector3(0f, 2f, 2f);

        [Header("Future Stage Streaming")]
        [Tooltip("Reserved for later additive loading. It intentionally does nothing for now; all four editable stages stay loaded.")]
        [SerializeField] private bool loadUpcomingStageWhenPreviousIsCompleted;

        [Header("Checkpoint Behaviour")]
        [SerializeField] private bool resumeFromSavedCheckpoint = true;

        private readonly List<OceanCampaignStage> stages = new List<OceanCampaignStage>();
        private int currentStageIndex;
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
            magnifierSkillUnlocked = PlayerPrefs.GetInt(MagnifierSkillKey, 0) != 0 || currentStageIndex > 0;
            player?.GetComponent<MagnifierView>()?.SetMagnifierUnlocked(magnifierSkillUnlocked);
            // Stage 1 reveals this reward only after the shell combination succeeds.
            magnifierPickup?.SetAvailable(false);
            ConfigureAuthoredProgression();
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
                if (stages[index].CheckpointSpawn == null ||
                    (index < stages.Count - 1 && !stages[index].HasCompletionInteraction) ||
                    (stages[index].ShellPuzzle != null && !stages[index].ShellPuzzle.IsReady))
                {
                    Debug.LogError($"Puzzle stage {index + 1} needs a checkpoint and a valid completion interaction (except on the final stage).", stages[index]);
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
            }
        }

        public bool CompleteStage(int stageIndex)
        {
            if (stageIndex != currentStageIndex || stageIndex >= stages.Count - 1) return false;
            ReachNextStage(stageIndex);
            return true;
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
            PlayerPrefs.SetInt(CheckpointKey, currentStageIndex);
            PlayerPrefs.Save();
            TelemetryManager.BeginCampaignStage(currentStageIndex + 1);
            RequestUpcomingStageLoad(currentStageIndex + 1);
            RestoreColorForFinalStageIfNeeded();
        }

        /// <summary>Developer-panel shortcut. It moves within the continuous level; it does not load a scene.</summary>
        public void TeleportToStage(int stageNumber)
        {
            currentStageIndex = Mathf.Clamp(stageNumber - 1, 0, stages.Count - 1);
            CompletePreviousStagesForDeveloperJump(currentStageIndex);
            player?.GetComponent<MagnifierView>()?.UnlockMagnifier();
            magnifierSkillUnlocked = true;
            magnifierPickup?.SetAvailable(false);
            MovePlayerToCheckpoint(currentStageIndex);
            TelemetryManager.BeginCampaignStage(currentStageIndex + 1);
            RestoreColorForFinalStageIfNeeded();
        }

        private void CompletePreviousStagesForDeveloperJump(int destinationStageIndex)
        {
            for (int index = 0; index < destinationStageIndex; index++)
            {
                stages[index].ShellPuzzle?.CompleteForDeveloperSkip();
                stages[index].CompletionBeacon?.CompleteForDeveloperSkip();
            }
        }

        public void ReloadCheckpoint()
        {
            if (!enabled) return;
            currentStageIndex = Mathf.Clamp(PlayerPrefs.GetInt(CheckpointKey, currentStageIndex), 0, stages.Count - 1);
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
            PlayerPrefs.Save();
        }

        public void ResetProgress()
        {
            ResetSavedProgress();
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        private void RestoreColorForFinalStageIfNeeded()
        {
            if (currentStageIndex < stages.Count - 1) return;
            campaignComplete = true;
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
            player.transform.SetPositionAndRotation(checkpoint.position + checkpointSpawnOffset, checkpoint.rotation);
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
