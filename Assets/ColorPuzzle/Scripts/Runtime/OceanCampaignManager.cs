using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ColorPuzzle
{
    /// <summary>Builds five contiguous, same-altitude rooms from the authored room in this scene.</summary>
    public sealed class OceanCampaignManager : MonoBehaviour
    {
        private const string CheckpointKey = "ColorPuzzle.OceanCampaign.Checkpoint";

        [Header("Campaign Structure")]
        [SerializeField] private FirstPersonPlayer player;
        [SerializeField] private GameObject authoredRoomSource;
        [SerializeField, Min(1)] private int stageCount = 5;
        [SerializeField, Min(1f)] private float roomSpacing = 20.67f;
        [SerializeField] private Vector3 checkpointSpawnOffset = new Vector3(0f, 2f, 2f);

        [Header("Door Openings")]
        [SerializeField, Min(1f)] private float doorwayWidth = 5f;
        [SerializeField, Min(1f)] private float doorwayHeight = 5f;
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
            if (authoredRoomSource == null) authoredRoomSource = GameObject.Find("UnderwaterLevel");
            if (authoredRoomSource == null)
            {
                Debug.LogError("Ocean Campaign needs the authored UnderwaterLevel room in this scene.", this);
                enabled = false;
                return;
            }

            currentStageIndex = Mathf.Clamp(resumeFromSavedCheckpoint ? PlayerPrefs.GetInt(CheckpointKey, 0) : 0, 0, stageCount - 1);
            BuildLinearRooms();
            StartCoroutine(PlacePlayerAfterSetup());
        }

        private IEnumerator PlacePlayerAfterSetup()
        {
            yield return null;
            MovePlayerToCheckpoint(currentStageIndex);
            TelemetryManager.BeginCampaignStage(currentStageIndex + 1);
            RestoreColorForFinalStageIfNeeded();
        }

        private void BuildLinearRooms()
        {
            stages.Add(authoredRoomSource);
            Vector3 sourcePosition = authoredRoomSource.transform.position;
            for (int index = 1; index < stageCount; index++)
            {
                GameObject room = Instantiate(authoredRoomSource);
                room.name = $"Ocean Stage {index + 1}";
                room.transform.position = sourcePosition + Vector3.forward * (roomSpacing * index);
                stages.Add(room);
            }

            for (int index = 0; index < stages.Count - 1; index++) CreateConnectingDoor(index);
        }

        private void CreateConnectingDoor(int stageIndex)
        {
            GameObject room = stages[stageIndex];
            Transform exitWall = FindNamedTransform(room.transform, "BackWall");
            Transform entryWall = FindNamedTransform(stages[stageIndex + 1].transform, "FrontWall");
            Renderer floor = FindNamedTransform(room.transform, "Floor")?.GetComponent<Renderer>();
            if (exitWall == null || entryWall == null || floor == null) return;

            Renderer wallRenderer = exitWall.GetComponent<Renderer>();
            Bounds wall = wallRenderer.bounds;
            float floorTop = floor.bounds.max.y;
            exitWall.gameObject.SetActive(false);
            entryWall.gameObject.SetActive(false);
            CreateDoorFrame(room.transform, wall, floorTop, wallRenderer.sharedMaterial);

            GameObject door = CreateBlock($"Stage {stageIndex + 1} Door", room.transform,
                new Vector3(wall.center.x, floorTop + doorwayHeight * .5f, wall.center.z),
                new Vector3(doorwayWidth, doorwayHeight, wall.size.z * 1.15f), wallRenderer.sharedMaterial);
            StageDoor stageDoor = door.AddComponent<StageDoor>();
            stageDoor.Configure(this, stageIndex, doorOpenDuration);
            CreateDoorwayTrigger(room.transform, door.transform.position, wall.size.z, stageDoor);

            GameObject gate = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            gate.name = $"Stage {stageIndex + 1} Gate Beacon (E)";
            gate.transform.SetParent(room.transform, true);
            gate.transform.position = new Vector3(wall.center.x, floorTop + .65f, wall.center.z - 3f);
            gate.transform.localScale = new Vector3(1.25f, 1.25f, 1.25f);
            gate.GetComponent<Renderer>().sharedMaterial = wallRenderer.sharedMaterial;
            StageGateBeacon beacon = gate.AddComponent<StageGateBeacon>();
            beacon.Configure(this, stageIndex, stageDoor);
        }

        private void CreateDoorFrame(Transform parent, Bounds wall, float floorTop, Material material)
        {
            float sideWidth = Mathf.Max(.1f, (wall.size.x - doorwayWidth) * .5f);
            float lintelHeight = Mathf.Max(.1f, wall.max.y - (floorTop + doorwayHeight));
            CreateBlock("Doorway Left", parent, new Vector3(wall.center.x - doorwayWidth * .5f - sideWidth * .5f, wall.center.y, wall.center.z),
                new Vector3(sideWidth, wall.size.y, wall.size.z), material);
            CreateBlock("Doorway Right", parent, new Vector3(wall.center.x + doorwayWidth * .5f + sideWidth * .5f, wall.center.y, wall.center.z),
                new Vector3(sideWidth, wall.size.y, wall.size.z), material);
            CreateBlock("Doorway Lintel", parent, new Vector3(wall.center.x, floorTop + doorwayHeight + lintelHeight * .5f, wall.center.z),
                new Vector3(doorwayWidth, lintelHeight, wall.size.z), material);
        }

        private void CreateDoorwayTrigger(Transform parent, Vector3 doorwayCenter, float wallDepth, StageDoor door)
        {
            GameObject triggerObject = new GameObject("Doorway Crossing Trigger");
            triggerObject.transform.SetParent(parent, true);
            triggerObject.transform.position = doorwayCenter;
            BoxCollider trigger = triggerObject.AddComponent<BoxCollider>();
            trigger.size = new Vector3(doorwayWidth, doorwayHeight, Mathf.Max(2f, wallDepth * 3f));
            triggerObject.AddComponent<StageDoorwayTrigger>().Configure(door);
        }

        private static GameObject CreateBlock(string name, Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = name;
            block.transform.SetParent(parent, true);
            block.transform.position = position;
            block.transform.localScale = scale;
            block.GetComponent<Renderer>().sharedMaterial = material;
            return block;
        }

        public void UnlockDoor(int stageIndex)
        {
            if (stageIndex != currentStageIndex || stageIndex >= stages.Count - 1) return;
            StageDoor door = stages[stageIndex].GetComponentInChildren<StageDoor>(true);
            if (door != null && !door.IsOpen)
            {
                door.Open();
            }
        }

        public void ReachNextStage(int doorStageIndex)
        {
            if (doorStageIndex != currentStageIndex) return;
            TelemetryManager.CompleteCampaignStage(doorStageIndex + 1);
            currentStageIndex = Mathf.Min(doorStageIndex + 1, stageCount - 1);
            PlayerPrefs.SetInt(CheckpointKey, currentStageIndex);
            PlayerPrefs.Save();
            TelemetryManager.BeginCampaignStage(currentStageIndex + 1);
            RestoreColorForFinalStageIfNeeded();
        }

        /// <summary>Developer-panel shortcut. It moves within the continuous level; it does not load a scene.</summary>
        public void TeleportToStage(int stageNumber)
        {
            currentStageIndex = Mathf.Clamp(stageNumber - 1, 0, stageCount - 1);
            MovePlayerToCheckpoint(currentStageIndex);
            TelemetryManager.BeginCampaignStage(currentStageIndex + 1);
            RestoreColorForFinalStageIfNeeded();
        }

        public void ReloadCheckpoint()
        {
            if (!enabled) return;
            currentStageIndex = Mathf.Clamp(PlayerPrefs.GetInt(CheckpointKey, currentStageIndex), 0, stageCount - 1);
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
            if (currentStageIndex < stageCount - 1) return;
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
