using UnityEngine;

namespace ColorPuzzle
{
    /// <summary>Reports the first player crossing after a completion portal becomes active.</summary>
    public sealed class TelemetryPortalTrigger : MonoBehaviour
    {
        private bool hasRecordedEntry;

        private void OnEnable()
        {
            hasRecordedEntry = false;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (hasRecordedEntry || other.GetComponentInParent<FirstPersonPlayer>() == null) return;
            hasRecordedEntry = true;
            TelemetryManager.RecordPortalEntered();
        }
    }
}
