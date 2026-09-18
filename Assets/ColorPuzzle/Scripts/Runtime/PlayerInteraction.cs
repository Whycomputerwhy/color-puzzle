using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ColorPuzzle
{
    public sealed class PlayerInteraction : MonoBehaviour
    {
        private const string InstructionText = "WASD move  •  SPACE jump  •  HOLD Q magnifier  •  LEFT CLICK pick up  •  RIGHT CLICK drop/place";

        [Header("References")]
        [SerializeField] private Camera viewCamera;
        [SerializeField] private Transform holdAnchor;
        [SerializeField] private Text statusText;

        [Header("Interaction Feel")]
        [SerializeField, Min(0.1f)] private float interactionDistance = 4f;
        [SerializeField, Min(0f)] private float dropDistance = 1.4f;
        [SerializeField, Min(0f)] private float dropImpulse = 1.5f;
        [SerializeField] private LayerMask interactionLayers = ~0;

        [Header("Placement Assist")]
        [SerializeField, Min(0.1f)] private float placementDistance = 5.5f;
        [SerializeField, Min(0f)] private float placementCastRadius = 0.75f;
        [SerializeField, Min(0f)] private float topPlacementHorizontalPadding = 0.35f;
        [SerializeField, Min(0.1f)] private float topPlacementMaxCameraHeight = 3f;
        [SerializeField, Range(0f, 1f)] private float topPlacementMinimumDownwardLook = 0.1f;

        private DiscoverableObject carried;
        private void Update()
        {
            if (Mouse.current == null || Keyboard.current == null) return;
            if (Keyboard.current.qKey.isPressed) { UpdateStatus(); return; }

            if (Mouse.current.leftButton.wasPressedThisFrame && carried == null) TryPickUp();
            if (Mouse.current.rightButton.wasPressedThisFrame && carried != null) TryDropOrPlace();
            UpdateStatus();
        }

        private void TryPickUp()
        {
            if (!TryRaycast(out RaycastHit hit)) return;
            DiscoverableObject item = hit.collider.GetComponentInParent<DiscoverableObject>();
            if (item == null) return;
            carried = item;
            carried.PickUp(holdAnchor);
        }

        private void TryDropOrPlace()
        {
            if (TryFindPlacementGoal(out GoalPedestal goal))
            {
                if (goal.TryPlace(carried))
                {
                    carried = null;
                    return;
                }
            }

            Vector3 position = viewCamera.transform.position + viewCamera.transform.forward * dropDistance;
            carried.Drop(position, viewCamera.transform.forward * dropImpulse);
            carried = null;
        }

        private bool TryFindPlacementGoal(out GoalPedestal selectedGoal)
        {
            selectedGoal = null;
            Ray aimRay = new Ray(viewCamera.transform.position, viewCamera.transform.forward);

            // Preserve exact aiming as the highest-priority placement path.
            if (Physics.Raycast(aimRay, out RaycastHit directHit, placementDistance, interactionLayers, QueryTriggerInteraction.Ignore))
            {
                GoalPedestal directGoal = directHit.collider.GetComponentInParent<GoalPedestal>();
                if (directGoal != null && directGoal.CanAccept(carried))
                {
                    selectedGoal = directGoal;
                    return true;
                }
            }

            // When the player is standing on the pedestal, a forward sphere cast can begin
            // above or inside its footprint. Detect this intentional top-down placement directly.
            if (TryFindGoalBelowPlayer(out GoalPedestal goalBelow))
            {
                selectedGoal = goalBelow;
                return true;
            }

            // A wider cast makes aiming at the pedestal forgiving without placing through walls.
            RaycastHit[] assistedHits = Physics.SphereCastAll(
                aimRay,
                placementCastRadius,
                placementDistance,
                interactionLayers,
                QueryTriggerInteraction.Ignore);

            float bestScore = float.PositiveInfinity;
            for (int i = 0; i < assistedHits.Length; i++)
            {
                GoalPedestal candidate = assistedHits[i].collider.GetComponentInParent<GoalPedestal>();
                if (candidate == null || !candidate.CanAccept(carried)) continue;
                if (!HasPlacementLineOfSight(candidate, assistedHits[i].collider)) continue;

                Vector3 toGoal = assistedHits[i].collider.bounds.center - viewCamera.transform.position;
                float angle = Vector3.Angle(viewCamera.transform.forward, toGoal);
                float score = angle + assistedHits[i].distance * 0.05f;
                if (score >= bestScore) continue;
                bestScore = score;
                selectedGoal = candidate;
            }

            return selectedGoal != null;
        }

        private bool TryFindGoalBelowPlayer(out GoalPedestal selectedGoal)
        {
            selectedGoal = null;
            if (viewCamera.transform.forward.y > -topPlacementMinimumDownwardLook) return false;

            Vector3 cameraPosition = viewCamera.transform.position;
            float bestVerticalGap = float.PositiveInfinity;
            foreach (GoalPedestal candidate in GoalPedestal.ActiveGoals)
            {
                if (candidate == null || !candidate.CanAccept(carried)) continue;
                Bounds bounds = candidate.PlacementBounds;
                float verticalGap = cameraPosition.y - bounds.max.y;
                if (verticalGap < 0f || verticalGap > topPlacementMaxCameraHeight) continue;

                bool insideX = cameraPosition.x >= bounds.min.x - topPlacementHorizontalPadding &&
                               cameraPosition.x <= bounds.max.x + topPlacementHorizontalPadding;
                bool insideZ = cameraPosition.z >= bounds.min.z - topPlacementHorizontalPadding &&
                               cameraPosition.z <= bounds.max.z + topPlacementHorizontalPadding;
                if (!insideX || !insideZ) continue;

                Collider goalCollider = candidate.GetComponent<Collider>();
                if (goalCollider == null) goalCollider = candidate.GetComponentInChildren<Collider>();
                if (goalCollider == null || !HasPlacementLineOfSight(candidate, goalCollider)) continue;
                if (verticalGap >= bestVerticalGap) continue;
                bestVerticalGap = verticalGap;
                selectedGoal = candidate;
            }

            return selectedGoal != null;
        }

        private bool HasPlacementLineOfSight(GoalPedestal goal, Collider goalCollider)
        {
            Vector3 origin = viewCamera.transform.position;
            Vector3 toGoal = goalCollider.bounds.center - origin;
            if (!Physics.Raycast(origin, toGoal.normalized, out RaycastHit hit, toGoal.magnitude + 0.1f, interactionLayers, QueryTriggerInteraction.Ignore))
                return false;
            return hit.collider.GetComponentInParent<GoalPedestal>() == goal;
        }

        private bool TryRaycast(out RaycastHit hit)
        {
            Ray ray = new Ray(viewCamera.transform.position, viewCamera.transform.forward);
            return Physics.Raycast(ray, out hit, interactionDistance, interactionLayers, QueryTriggerInteraction.Ignore);
        }

        private void UpdateStatus()
        {
            if (statusText == null) return;
            statusText.text = InstructionText;
        }
    }
}
