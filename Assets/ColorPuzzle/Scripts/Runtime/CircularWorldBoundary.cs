using UnityEngine;

namespace ColorPuzzle
{
    /// <summary>
    /// Builds a smooth, invisible movement boundary that follows a circular floor.
    /// The colliders are runtime-only, keeping the shared-room prefab tidy while
    /// making the playable edge substantially closer to a true circle.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CircularWorldBoundary : MonoBehaviour
    {
        [Header("Circular boundary")]
        [SerializeField] private Vector3 localCenter = new Vector3(85.07297f, 610.0127f, 285.47827f);
        [SerializeField, Min(3)] private int segments = 32;
        [Tooltip("Distance from the centre to the playable-side face of the boundary.")]
        [SerializeField, Min(1f)] private float playableRadius = 23.35f;
        [SerializeField, Min(0.05f)] private float wallThickness = 0.4f;
        [SerializeField, Min(0.1f)] private float wallHeight = 12f;
        [SerializeField, Min(0f)] private float segmentOverlap = 0.08f;

        private Transform runtimeBoundaryRoot;

        private void Awake()
        {
            BuildBoundary();
        }

        private void OnDestroy()
        {
            if (runtimeBoundaryRoot != null)
                Destroy(runtimeBoundaryRoot.gameObject);
        }

        private void BuildBoundary()
        {
            if (runtimeBoundaryRoot != null) return;

            GameObject root = new("Runtime Circular Boundaries")
            {
                hideFlags = HideFlags.DontSave
            };
            runtimeBoundaryRoot = root.transform;
            runtimeBoundaryRoot.SetParent(transform, false);

            int safeSegments = Mathf.Max(3, segments);
            float wallCentreRadius = playableRadius + wallThickness * 0.5f;
            float wallLength = 2f * wallCentreRadius * Mathf.Tan(Mathf.PI / safeSegments) + segmentOverlap;

            for (int index = 0; index < safeSegments; index++)
            {
                float angle = index * 360f / safeSegments;
                float radians = angle * Mathf.Deg2Rad;
                GameObject wall = new($"Circular Boundary {index + 1:00}")
                {
                    hideFlags = HideFlags.DontSave
                };
                Transform wallTransform = wall.transform;
                wallTransform.SetParent(runtimeBoundaryRoot, false);
                wallTransform.localPosition = localCenter + new Vector3(
                    Mathf.Sin(radians) * wallCentreRadius,
                    0f,
                    Mathf.Cos(radians) * wallCentreRadius);
                wallTransform.localRotation = Quaternion.Euler(0f, angle, 0f);

                BoxCollider collider = wall.AddComponent<BoxCollider>();
                collider.size = new Vector3(wallLength, wallHeight, wallThickness);
            }

            Physics.SyncTransforms();
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.15f, 0.75f, 1f, 0.8f);
            Gizmos.DrawWireSphere(transform.TransformPoint(localCenter), playableRadius);
        }
    }
}
