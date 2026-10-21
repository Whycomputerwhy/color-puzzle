using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ColorPuzzle
{
    /// <summary>Runs the authored, four-stage ocean campaign placed directly in the scene.</summary>
    public sealed class OceanCampaignManager : MonoBehaviour
    {
        private const string CheckpointKey = "ColorPuzzle.OceanCampaign.Checkpoint";

        [Header("Campaign Structure")]
        [SerializeField] private FirstPersonPlayer player;
        [Tooltip("Scene parent containing Ocean Stage 01 through 04, in play order.")]
        [SerializeField] private Transform stageContainer;
        [SerializeField] private Vector3 checkpointSpawnOffset = new Vector3(0f, 2f, 2f);

        [Header("Future Stage Streaming")]
        [Tooltip("Reserved for later additive loading. It intentionally does nothing for now; all four editable stages stay loaded.")]
        [SerializeField] private bool loadUpcomingStageWhenPreviousIsCompleted;

        [Header("Door Behaviour")]
        [SerializeField, Min(0.1f)] private float doorOpenDuration = 0.65f;
        [SerializeField] private bool resumeFromSavedCheckpoint = true;

        private readonly List<GameObject> stages = new List<GameObject>();
        private int currentStageIndex;
        private bool campaignComplete;
        public static OceanCampaignManager Active { get; private set; }
        public int CurrentStageNumber => currentStageIndex + 1;
        public bool IsComplete => campaignComplete;

        private void Awake() => Active = this;

        private void Start()
        {
            if (player == null) player = FindAnyObjectByType<FirstPersonPlayer>();
            if (!CollectAuthoredStages() || !ValidateStageBoundaries())
            {
                enabled = false;
                return;
            }

            currentStageIndex = Mathf.Clamp(resumeFromSavedCheckpoint ? PlayerPrefs.GetInt(CheckpointKey, 0) : 0, 0, stages.Count - 1);
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
            foreach (Transform stage in stageContainer)
                if (stage.gameObject.activeInHierarchy) stages.Add(stage.gameObject);
            if (stages.Count == 4) return true;
            Debug.LogError($"Ocean campaign expected 4 active stage instances under {stageContainer.name}, found {stages.Count}.", this);
            return false;
        }

        private bool ValidateStageBoundaries()
        {
            for (int index = 0; index < stages.Count - 1; index++)
            {
                GameObject exit = FindNamedTransform(stages[index].transform, "BackWall")?.gameObject;
                GameObject entrance = FindNamedTransform(stages[index + 1].transform, "FrontWall")?.gameObject;
                if (exit == null || entrance == null || exit.activeInHierarchy || entrance.activeInHierarchy ||
                    stages[index].GetComponentInChildren<StageDoor>(true) == null ||
                    stages[index].GetComponentInChildren<StageGateBeacon>(true) == null ||
                    stages[index].GetComponentInChildren<StageDoorwayTrigger>(true) == null)
                {
                    Debug.LogError($"Stage {index + 1} doorway is incomplete or blocked. Check both boundary walls and its door, beacon, and crossing trigger in the stage prefabs.", this);
                    return false;
                }
            }
            return true;
        }

        private void ConfigureAuthoredProgression()
        {
            for (int index = 0; index < stages.Count; index++)
            {
                StageDoor door = stages[index].GetComponentInChildren<StageDoor>(true);
                if (door == null) continue; // Final stage intentionally has no exit door.
                door.Configure(this, index, doorOpenDuration);
                StageGateBeacon beacon = stages[index].GetComponentInChildren<StageGateBeacon>(true);
                beacon?.Configure(this, index);
                foreach (StageDoorwayTrigger trigger in stages[index].GetComponentsInChildren<StageDoorwayTrigger>(true))
                    trigger.Configure(door);
            }
        }

        public bool UnlockDoor(int stageIndex)
        {
            if (stageIndex != currentStageIndex || stageIndex >= stages.Count - 1) return false;
            StageDoor door = stages[stageIndex].GetComponentInChildren<StageDoor>(true);
            if (door == null) return false;
            if (!door.IsOpen) door.Open();
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
            MovePlayerToCheckpoint(currentStageIndex);
            TelemetryManager.BeginCampaignStage(currentStageIndex + 1);
            RestoreColorForFinalStageIfNeeded();
        }

        public void ReloadCheckpoint()
        {
            if (!enabled) return;
            currentStageIndex = Mathf.Clamp(PlayerPrefs.GetInt(CheckpointKey, currentStageIndex), 0, stages.Count - 1);
            MovePlayerToCheckpoint(currentStageIndex);
            TelemetryManager.BeginCampaignStage(currentStageIndex + 1);
            RestoreColorForFinalStageIfNeeded();
        }

        public void ResetSavedProgress()
        {
            PlayerPrefs.DeleteKey(CheckpointKey);
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
            Renderer floor = FindNamedTransform(stages[stageIndex].transform, "Floor")?.GetComponent<Renderer>();
            if (floor == null) return;
            CharacterController controller = player.GetComponent<CharacterController>();
            if (controller != null) controller.enabled = false;
            Bounds bounds = floor.bounds;
            player.transform.SetPositionAndRotation(new Vector3(bounds.center.x, bounds.max.y, bounds.min.z) + checkpointSpawnOffset, Quaternion.identity);
            if (controller != null) controller.enabled = true;
        }

        private void RequestUpcomingStageLoad(int stageIndex)
        {
            // Streaming will be added after the stage content is established. Keeping this hook here
            // lets us add additive loading later without changing the progression/checkpoint flow.
            if (!loadUpcomingStageWhenPreviousIsCompleted) return;
            Debug.Log($"Stage streaming is reserved for a future pass (requested stage {stageIndex + 1}).", this);
        }

        private static Transform FindNamedTransform(Transform root, string objectName)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                if (child.name == objectName) return child;
            return null;
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
