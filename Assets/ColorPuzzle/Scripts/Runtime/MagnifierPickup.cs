using UnityEngine;

namespace ColorPuzzle
{
    /// <summary>One-time ability pickup. Its display model never enters the object inventory.</summary>
    public sealed class MagnifierPickup : MonoBehaviour
    {
        [SerializeField] private GameObject visualRoot;
        [SerializeField] private Collider pickupCollider;
        [SerializeField] private AudioClip pickupClip;
        [SerializeField, Range(0f, 1f)] private float pickupVolume = 0.18f;

        public bool IsAvailable => pickupCollider != null && pickupCollider.enabled;

        public bool TryAcquire(Transform player, float maximumDistance)
        {
            if (!IsAvailable || player == null ||
                Vector3.Distance(player.position, transform.position) > maximumDistance)
                return false;

            OceanCampaignManager campaign = OceanCampaignManager.Active;
            if (campaign == null || !campaign.AcquireMagnifier()) return false;

            if (pickupClip != null)
                AudioSource.PlayClipAtPoint(pickupClip, transform.position, pickupVolume);
            SetAvailable(false);
            return true;
        }

        public void SetAvailable(bool available)
        {
            if (visualRoot != null) visualRoot.SetActive(available);
            if (pickupCollider != null) pickupCollider.enabled = available;
        }
    }
}
