using UnityEngine;

namespace ColorPuzzle
{
    /// <summary>Authored puzzle content and checkpoint for one area of the shared ocean room.</summary>
    public sealed class OceanCampaignStage : MonoBehaviour
    {
        [SerializeField] private Transform checkpointSpawn;
        [SerializeField] private StageGateBeacon completionBeacon;

        public Transform CheckpointSpawn => checkpointSpawn;
        public StageGateBeacon CompletionBeacon => completionBeacon;
        public ShellPuzzleController ShellPuzzle => GetComponent<ShellPuzzleController>();
        public bool HasCompletionInteraction => completionBeacon != null || ShellPuzzle != null;
    }
}
