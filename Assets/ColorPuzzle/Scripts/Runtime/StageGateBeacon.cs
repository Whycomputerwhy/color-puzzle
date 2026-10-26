using UnityEngine;

namespace ColorPuzzle
{
    /// <summary>Temporary puzzle-completion interaction; stages have no physical gate.</summary>
    public sealed class StageGateBeacon : MonoBehaviour
    {
        private OceanCampaignManager campaign;
        private int stageIndex;
        private bool activated;

        public void Configure(OceanCampaignManager owner, int index)
        {
            campaign = owner;
            stageIndex = index;
        }

        public bool TryActivate(Transform player, float range)
        {
            if (activated || campaign == null || player == null) return false;
            if (Vector3.Distance(player.position, transform.position) > range) return false;
            if (!campaign.CompleteStage(stageIndex)) return false;
            activated = true;
            return true;
        }

        /// <summary>Marks a temporary beacon complete when an earlier stage is skipped in development.</summary>
        public void CompleteForDeveloperSkip() => activated = true;
    }
}
