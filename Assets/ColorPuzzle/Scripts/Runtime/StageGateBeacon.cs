using UnityEngine;

namespace ColorPuzzle
{
    /// <summary>Simple temporary interaction object used to unlock the next stage door.</summary>
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
            if (!campaign.UnlockDoor(stageIndex)) return false;
            activated = true;
            return true;
        }
    }
}
