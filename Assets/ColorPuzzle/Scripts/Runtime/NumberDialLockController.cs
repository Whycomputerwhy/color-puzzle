using UnityEngine;

namespace ColorPuzzle
{
    /// <summary>Completes Puzzle 3 when its four dial values match their authored target.</summary>
    public sealed class NumberDialLockController : MonoBehaviour
    {
        [SerializeField] private NumberDial[] dials;
        [SerializeField, Min(0)] private int campaignStageIndex = 2;
        [SerializeField] private bool lockWhenSolved = true;

        private bool solved;

        public bool CanRotate(NumberDial dial) =>
            !solved && dial != null && System.Array.IndexOf(dials, dial) >= 0;

        public bool TryRotate(NumberDial dial)
        {
            if (!CanRotate(dial)) return false;
            dial.Advance();

            foreach (NumberDial candidate in dials)
                if (candidate == null || candidate.CurrentValue != candidate.TargetValue)
                    return true;

            solved = true;
            if (lockWhenSolved)
                foreach (NumberDial candidate in dials)
                    candidate.enabled = false;
            OceanCampaignManager.Active?.CompleteStage(campaignStageIndex);
            return true;
        }
    }
}
