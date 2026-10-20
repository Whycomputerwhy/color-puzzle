using UnityEngine;

namespace ColorPuzzle
{
    /// <summary>Stays in the doorway while the physical door raises out of the way.</summary>
    [RequireComponent(typeof(Collider))]
    public sealed class StageDoorwayTrigger : MonoBehaviour
    {
        private StageDoor door;

        public void Configure(StageDoor stageDoor)
        {
            door = stageDoor;
            Collider trigger = GetComponent<Collider>();
            trigger.isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (door == null || !door.IsOpen || other.GetComponentInParent<FirstPersonPlayer>() == null) return;
            door.NotifyPlayerCrossed();
        }
    }
}
