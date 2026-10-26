using UnityEngine;

namespace ColorPuzzle
{
    /// <summary>Lightweight decorative fish movement contained around its authored starting point.</summary>
    public sealed class FishSwimmer : MonoBehaviour
    {
        [Header("Roaming")]
        [SerializeField, Min(0.05f)] private float swimSpeed = 0.45f;
        [SerializeField, Min(0.1f)] private float roamRadius = 3f;
        [SerializeField, Min(0.1f)] private float roamHeight = 1.2f;
        [SerializeField, Min(0.1f)] private float destinationChangeInterval = 4f;
        [SerializeField, Range(0f, 20f)] private float turnSpeed = 3f;

        private Vector3 homePosition;
        private Vector3 destination;
        private float nextDestinationTime;

        public void Configure(float speed, float radius, float height, float interval)
        {
            swimSpeed = Mathf.Max(0.05f, speed);
            roamRadius = Mathf.Max(0.1f, radius);
            roamHeight = Mathf.Max(0.1f, height);
            destinationChangeInterval = Mathf.Max(0.1f, interval);
        }

        private void Start()
        {
            homePosition = transform.localPosition;
            ChooseDestination();
        }

        private void Update()
        {
            if (Time.time >= nextDestinationTime ||
                Vector3.SqrMagnitude(transform.localPosition - destination) < 0.01f)
            {
                ChooseDestination();
            }

            Vector3 movement = destination - transform.localPosition;
            transform.localPosition = Vector3.MoveTowards(
                transform.localPosition, destination, swimSpeed * Time.deltaTime);

            Vector3 flatMovement = Vector3.ProjectOnPlane(movement, Vector3.up);
            if (flatMovement.sqrMagnitude > 0.001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(flatMovement.normalized, Vector3.up);
                transform.localRotation = Quaternion.Slerp(
                    transform.localRotation, targetRotation, turnSpeed * Time.deltaTime);
            }
        }

        private void ChooseDestination()
        {
            Vector2 horizontal = Random.insideUnitCircle * roamRadius;
            destination = homePosition + new Vector3(
                horizontal.x,
                Random.Range(-roamHeight, roamHeight),
                horizontal.y);
            nextDestinationTime = Time.time + Random.Range(
                destinationChangeInterval * 0.65f, destinationChangeInterval * 1.35f);
        }
    }
}
