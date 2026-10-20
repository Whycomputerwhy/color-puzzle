using System.Collections;
using UnityEngine;

namespace ColorPuzzle
{
    /// <summary>A solid door that rises from the real doorway once its beacon is activated.</summary>
    public sealed class StageDoor : MonoBehaviour
    {
        private OceanCampaignManager campaign;
        private int stageIndex;
        private float openDuration;
        private bool open;

        public bool IsOpen => open;

        public void Configure(OceanCampaignManager owner, int index, float duration)
        {
            campaign = owner;
            stageIndex = index;
            openDuration = duration;
        }

        public void Open()
        {
            if (open) return;
            open = true;
            StartCoroutine(Raise());
        }

        private IEnumerator Raise()
        {
            Vector3 closedPosition = transform.position;
            Vector3 openPosition = closedPosition + Vector3.up * (transform.lossyScale.y + .5f);
            float elapsed = 0f;
            while (elapsed < openDuration)
            {
                elapsed += Time.deltaTime;
                transform.position = Vector3.Lerp(closedPosition, openPosition, Mathf.SmoothStep(0f, 1f, elapsed / openDuration));
                yield return null;
            }
            transform.position = openPosition;
            Collider solidCollider = GetComponent<Collider>();
            if (solidCollider != null) solidCollider.enabled = false;
        }

        public void NotifyPlayerCrossed()
        {
            if (!open) return;
            campaign?.ReachNextStage(stageIndex);
        }
    }
}
