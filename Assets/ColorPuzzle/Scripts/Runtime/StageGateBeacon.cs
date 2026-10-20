using UnityEngine;

namespace ColorPuzzle
{
    /// <summary>Simple temporary interaction object used to unlock the next stage portal.</summary>
    public sealed class StageGateBeacon : MonoBehaviour
    {
        private OceanCampaignManager campaign;
        private int stageIndex;
        private StageDoor door;
        private bool activated;

        public void Configure(OceanCampaignManager owner, int index, StageDoor stageDoor)
        {
            campaign = owner;
            stageIndex = index;
            door = stageDoor;
        }

        public bool TryActivate(Transform player, float range)
        {
            if (activated || campaign == null || player == null) return false;
            if (Vector3.Distance(player.position, transform.position) > range) return false;
            activated = true;
            campaign.UnlockDoor(stageIndex);
            door?.Open();
            return true;
        }
    }
}
