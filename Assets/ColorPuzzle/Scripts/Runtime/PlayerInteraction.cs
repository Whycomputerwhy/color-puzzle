using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using ColorPuzzle.Rendering;

namespace ColorPuzzle
{
    public sealed class PlayerInteraction : MonoBehaviour
    {
        public enum InteractionCueMode
        {
            BackGlow,
            CenterText
        }

        private enum DropPresentation
        {
            Immediate,
            FocusDepthAnimation,
            NearViewWallAnimation
        }

        private const string ColorSearchInstructionText =
            "WASD  •  SPACE jump  •  RMB magnifier  •  LMB pick up  •  Q drop/place  •  R reset";
        private const string ValueSearchInstructionText =
            "WASD  •  SPACE jump  •  LMB pick up  •  Q drop/place  •  R reset";

        [Header("References")]
        [SerializeField] private Camera viewCamera;
        [SerializeField] private Text statusText;

        [Header("Control HUD Styling")]
        [SerializeField] private bool applyStatusTextOutline = true;
        [SerializeField] private Color statusTextOutlineColor = new Color(1f, 1f, 1f, 0.92f);
        [SerializeField] private Vector2 statusTextOutlineDistance = new Vector2(1.5f, -1.5f);

        [Header("Controls")]
        [SerializeField] private Key dropOrPlaceKey = Key.Q;
        [SerializeField] private Key resetKey = Key.R;

        [Header("Interaction Feel")]
        [SerializeField, Min(0.1f)] private float interactionDistance = 4f;
        [SerializeField, Min(0f)] private float pickupAimRadius = 0.16f;
        [SerializeField, Min(0f)] private float pickupBelowProbeRadius = 0.3f;
        [SerializeField, Min(0f)] private float pickupBelowProbeStartHeight = 0.2f;
        [SerializeField, Min(0.1f)] private float pickupBelowProbeDistance = 0.8f;
        [SerializeField, Min(0f)] private float dropDistance = 1.4f;
        [SerializeField, Min(0f)] private float dropGroundProbeHeight = 1.5f;
        [SerializeField, Min(0.1f)] private float dropGroundProbeDistance = 20f;
        [SerializeField, Min(0f)] private float dropSurfaceClearance = 0.02f;
        [SerializeField, Min(0f)] private float dropObstacleClearance = 0.04f;
        [SerializeField, Min(0f)] private float dropCollisionSkin = 0.01f;
        [SerializeField, Range(1, 8)] private int dropCollisionResolutionIterations = 4;
        [SerializeField, Range(0f, 1f)] private float dropWallNormalMaximumY = 0.65f;
        [SerializeField, Min(0f)] private float dropMovementLeadTime = 0.12f;
        [SerializeField, Min(0f)] private float dropMovementMaximumOffset = 0.65f;
        [SerializeField, Min(0f)] private float dropMovementMinimumSpeed = 0.15f;
        [SerializeField] private LayerMask interactionLayers = ~0;

        [Header("Drop Animation")]
        [SerializeField, Min(0f)] private float dropAnimationDuration = 0.22f;
        [SerializeField, Range(1f, 4f)] private float dropAnimationFallExponent = 2.2f;
        [SerializeField, Min(0.05f)] private float wallDropAnimationStartDistance = 0.35f;

        [Header("Placement Assist")]
        [SerializeField, Min(0.1f)] private float placementDistance = 5.5f;
        [SerializeField, Min(0f)] private float placementCastRadius = 0.75f;
        [SerializeField, Min(0f)] private float topPlacementHorizontalPadding = 0.35f;
        [SerializeField, Min(0.1f)] private float topPlacementMaxCameraHeight = 3f;
        [SerializeField, Range(0f, 1f)] private float topPlacementMinimumDownwardLook = 0.1f;

        // CENTRALIZED INTERACTION CUE PARAMETERS
        [Header("Interaction Cues")]
        [SerializeField] private InteractionCueMode interactionCueMode = InteractionCueMode.BackGlow;
        [SerializeField, Min(0f)] private float interactionCueDwellDuration = 0.75f;
        [SerializeField, Min(0f)] private float interactionCueGlowFadeInDuration = 0.25f;
        [SerializeField, Min(8)] private int interactionCueFontSize = 14;
        [SerializeField] private Vector2 interactionCueTextSize = new Vector2(220f, 28f);
        [SerializeField] private Vector2 interactionCueTextOffset = new Vector2(0f, -26f);
        [SerializeField] private Color interactionCueGreyscaleTextColor = Color.black;
        [SerializeField] private Color interactionCueColorTextColor = Color.white;
        [SerializeField] private Color interactionCueTextOutlineColor = new Color(1f, 1f, 1f, 0.8f);
        [SerializeField, Range(2f, 60f)] private float interactionCueGlowWidthPixels = 18f;
        [SerializeField, Range(0f, 1f)] private float interactionCueGlowSoftness = 0.9f;
        [SerializeField] private Color interactionCueGreyscaleGlowColor = new Color(0f, 0f, 0f, 0.85f);
        [SerializeField] private Color interactionCueColorGlowColor = new Color(1f, 1f, 1f, 0.86f);
        [SerializeField, Range(0f, 1f)] private float interactionCueColorViewScale = 0.5f;

        [Header("Inventory HUD")]
        [SerializeField] private bool showInventoryHud = true;
        [SerializeField] private Vector2 inventoryHudSize = new Vector2(150f, 42f);
        [SerializeField] private Vector2 inventoryHudOffset = new Vector2(0f, 92f);
        [SerializeField, Min(0f)] private float inventoryHudGap = 8f;
        [SerializeField, Min(8)] private int inventoryBallFontSize = 28;
        [SerializeField, Min(8)] private int inventoryFontSize = 18;
        [SerializeField] private Color inventoryFillColor = new Color(0.12f, 0.12f, 0.12f, 1f);
        [SerializeField] private Color inventoryOutlineColor = Color.white;
        [SerializeField] private Vector2 inventoryOutlineDistance = new Vector2(1f, -1f);

        private DiscoverableObject carried;
        private MagnifierView magnifier;
        private CharacterController playerController;
        private GameObject inventoryHudRoot;
        private Text inventoryLabel;
        private GameObject interactionCueTextRoot;
        private Text interactionCueText;
        private Object currentCueTarget;
        private string currentCueLabel;
        private float interactionCueDwellElapsed;
        private float interactionCueGlowFadeElapsed;
        private bool valueSearchMode;

        public InteractionCueMode CurrentInteractionCueMode => interactionCueMode;

        private void Awake()
        {
            magnifier = GetComponent<MagnifierView>();
            playerController = GetComponent<CharacterController>();
            ConfigureStatusTextOutline();
            CreateInventoryHud();
            CreateInteractionCueText();
            ClearInteractionCueVisuals();
            RefreshInventoryHud();
        }

        private void ConfigureStatusTextOutline()
        {
            if (!applyStatusTextOutline || statusText == null) return;

            Outline outline = statusText.GetComponent<Outline>();
            if (outline == null)
                outline = statusText.gameObject.AddComponent<Outline>();
            outline.effectColor = statusTextOutlineColor;
            outline.effectDistance = statusTextOutlineDistance;
            outline.useGraphicAlpha = true;
        }

        private void Update()
        {
            if (Mouse.current == null || Keyboard.current == null) return;
            if (PauseMenuController.IsPaused) return;
            if (Keyboard.current[resetKey].wasPressedThisFrame)
            {
                SceneManager.LoadScene(SceneManager.GetActiveScene().name);
                return;
            }

            UpdateInteractionCue();
            if (magnifier != null && magnifier.IsActive) { UpdateStatus(); return; }

            if (Mouse.current.leftButton.wasPressedThisFrame && carried == null) TryPickUp();
            if (Keyboard.current[dropOrPlaceKey].wasPressedThisFrame && carried != null) TryDropOrPlace();
            UpdateStatus();
        }

        private void TryPickUp()
        {
            DiscoverableObject item = FindPickupObjectInAim();
            if (item == null)
                item = FindPickupObjectBelowPlayer();
            if (item == null) return;
            carried = item;
            carried.PickUp();
            ResetInteractionCue();
            RefreshInventoryHud();
        }

        private DiscoverableObject FindPickupObjectInAim()
        {
            if (TryFindNearestPickupHit(0f, out RaycastHit centerHit))
            {
                DiscoverableObject centeredItem =
                    centerHit.collider.GetComponentInParent<DiscoverableObject>();
                if (centeredItem != null) return centeredItem;
            }

            if (pickupAimRadius <= 0f ||
                !TryFindNearestPickupHit(pickupAimRadius, out RaycastHit assistedHit))
                return null;
            return assistedHit.collider.GetComponentInParent<DiscoverableObject>();
        }

        private bool TryFindNearestPickupHit(float radius, out RaycastHit nearestHit)
        {
            nearestHit = default;
            RaycastHit[] hits = radius <= 0f
                ? Physics.RaycastAll(
                    viewCamera.transform.position,
                    viewCamera.transform.forward,
                    interactionDistance,
                    interactionLayers,
                    QueryTriggerInteraction.Ignore)
                : Physics.SphereCastAll(
                    viewCamera.transform.position,
                    radius,
                    viewCamera.transform.forward,
                    interactionDistance,
                    interactionLayers,
                    QueryTriggerInteraction.Ignore);

            bool foundHit = false;
            float nearestDistance = float.PositiveInfinity;
            for (int i = 0; i < hits.Length; i++)
            {
                RaycastHit hit = hits[i];
                if (IsPlayerCollider(hit.collider) || hit.distance >= nearestDistance) continue;
                nearestHit = hit;
                nearestDistance = hit.distance;
                foundHit = true;
            }
            return foundHit;
        }

        private DiscoverableObject FindPickupObjectBelowPlayer()
        {
            Vector3 origin = transform.position + Vector3.up * pickupBelowProbeStartHeight;
            RaycastHit[] hits = Physics.SphereCastAll(
                origin,
                pickupBelowProbeRadius,
                Vector3.down,
                pickupBelowProbeDistance,
                interactionLayers,
                QueryTriggerInteraction.Ignore);

            RaycastHit nearestHit = default;
            float nearestDistance = float.PositiveInfinity;
            bool foundHit = false;
            for (int i = 0; i < hits.Length; i++)
            {
                RaycastHit hit = hits[i];
                Transform hitTransform = hit.collider.transform;
                if (hitTransform == transform || hitTransform.IsChildOf(transform)) continue;
                if (hit.distance >= nearestDistance) continue;
                nearestHit = hit;
                nearestDistance = hit.distance;
                foundHit = true;
            }

            if (!foundHit) return null;
            return nearestHit.collider.GetComponentInParent<DiscoverableObject>();
        }

        private void TryDropOrPlace()
        {
            if (TryFindPlacementGoal(out GoalPedestal goal))
            {
                if (goal.TryPlace(carried))
                {
                    carried = null;
                    ResetInteractionCue();
                    RefreshInventoryHud();
                    return;
                }
            }

            if (!TryFindSafeDropPosition(
                    out Vector3 restingPosition,
                    out DropPresentation presentation)) return;

            Vector3 animationStart = Vector3.zero;
            if (presentation != DropPresentation.Immediate)
            {
                animationStart = GetDropAnimationStart(restingPosition, presentation);
                if (presentation == DropPresentation.FocusDepthAnimation &&
                    ShouldPlaceDirectlyOnResolvedSurface(animationStart, restingPosition))
                    presentation = DropPresentation.Immediate;
            }

            if (presentation != DropPresentation.Immediate)
            {
                carried.DropAnimated(
                    animationStart,
                    restingPosition,
                    dropAnimationDuration,
                    dropAnimationFallExponent,
                    playerController);
            }
            else
            {
                carried.Drop(restingPosition, playerController);
            }
            carried = null;
            ResetInteractionCue();
            RefreshInventoryHud();
        }

        private Vector3 GetDropAnimationStart(
            Vector3 restingPosition,
            DropPresentation presentation)
        {
            float startDistance = presentation == DropPresentation.NearViewWallAnimation
                ? Mathf.Min(wallDropAnimationStartDistance, dropDistance)
                : dropDistance;
            Vector3 focusCenter = viewCamera.transform.position +
                                  viewCamera.transform.forward * startDistance;
            Vector3 boundsCenterOffset = carried.GetDropBoundsCenter(restingPosition) - restingPosition;
            return focusCenter - boundsCenterOffset;
        }

        private bool ShouldPlaceDirectlyOnResolvedSurface(
            Vector3 animationStart,
            Vector3 restingPosition)
        {
            float startCenterY = carried.GetDropBoundsCenter(animationStart).y;
            float restingCenterY = carried.GetDropBoundsCenter(restingPosition).y;
            if (startCenterY <= restingCenterY + dropCollisionSkin) return true;
            return HasBlockingDropOverlap(animationStart);
        }

        private bool TryFindSafeDropPosition(
            out Vector3 restingPosition,
            out DropPresentation presentation)
        {
            restingPosition = Vector3.zero;
            presentation = DropPresentation.FocusDepthAnimation;
            Vector3 planarForward = Vector3.ProjectOnPlane(viewCamera.transform.forward, Vector3.up);
            if (planarForward.sqrMagnitude < 0.0001f)
                planarForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            planarForward.Normalize();

            bool usingCloseFocusedSurface = TryGetCloseFocusedSurfaceDropProbe(
                out Vector3 desiredPosition,
                out bool focusedSurfaceIsWall);
            if (!usingCloseFocusedSurface)
            {
                desiredPosition = viewCamera.transform.position +
                                  viewCamera.transform.forward * dropDistance;
                desiredPosition += GetDropMovementOffset();
            }
            else
            {
                presentation = focusedSurfaceIsWall
                    ? DropPresentation.NearViewWallAnimation
                    : DropPresentation.Immediate;
            }

            if (!TryFindDropSurfaceAt(desiredPosition, out RaycastHit surfaceHit) &&
                !TryFindDropSurfaceAt(viewCamera.transform.position, out surfaceHit))
                return false;

            restingPosition = carried.GetRestingPosition(
                surfaceHit.point,
                surfaceHit.normal,
                dropSurfaceClearance);
            if (!ClampDropToPlayerSideOfObstacles(planarForward, ref restingPosition)) return false;
            if (!ResolveDropOverlaps(ref restingPosition)) return false;
            return !HasBlockingDropOverlap(restingPosition);
        }

        private Vector3 GetDropMovementOffset()
        {
            if (playerController == null) return Vector3.zero;
            Vector3 planarVelocity = Vector3.ProjectOnPlane(playerController.velocity, Vector3.up);
            Vector3 playerForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            float forwardSpeed = Vector3.Dot(planarVelocity, playerForward);
            if (forwardSpeed < dropMovementMinimumSpeed) return Vector3.zero;
            float forwardOffset = Mathf.Min(
                forwardSpeed * dropMovementLeadTime,
                dropMovementMaximumOffset);
            return playerForward * forwardOffset;
        }

        private bool TryGetCloseFocusedSurfaceDropProbe(
            out Vector3 probePosition,
            out bool isWall)
        {
            probePosition = Vector3.zero;
            isWall = false;
            if (!TryFindNearestAimedDropHit(dropDistance, out RaycastHit nearestHit)) return false;

            if (nearestHit.normal.y > dropWallNormalMaximumY)
            {
                probePosition = nearestHit.point;
                return true;
            }
            if (Mathf.Abs(nearestHit.normal.y) > dropWallNormalMaximumY) return false;
            isWall = true;

            Vector3 horizontalNormal = Vector3.ProjectOnPlane(nearestHit.normal, Vector3.up);
            if (horizontalNormal.sqrMagnitude < 0.0001f) return false;
            horizontalNormal.Normalize();

            Vector3 playerSideCenter = nearestHit.point + horizontalNormal *
                (carried.HorizontalDropRadius + dropObstacleClearance);
            probePosition = new Vector3(
                playerSideCenter.x,
                viewCamera.transform.position.y,
                playerSideCenter.z);
            return true;
        }

        private bool TryFindNearestAimedDropHit(float maxDistance, out RaycastHit nearestHit)
        {
            nearestHit = default;
            RaycastHit[] hits = Physics.RaycastAll(
                viewCamera.transform.position,
                viewCamera.transform.forward,
                maxDistance,
                interactionLayers,
                QueryTriggerInteraction.Ignore);

            bool foundHit = false;
            float nearestDistance = float.PositiveInfinity;
            for (int i = 0; i < hits.Length; i++)
            {
                RaycastHit hit = hits[i];
                if (IsIgnoredDropCollider(hit.collider) || hit.distance >= nearestDistance) continue;
                nearestHit = hit;
                nearestDistance = hit.distance;
                foundHit = true;
            }
            return foundHit;
        }

        private bool TryFindDropSurfaceAt(Vector3 candidatePosition, out RaycastHit surfaceHit)
        {
            surfaceHit = default;
            float castDistance = dropGroundProbeHeight + dropGroundProbeDistance;
            Vector3 candidateOrigin = candidatePosition + Vector3.up * dropGroundProbeHeight;
            RaycastHit[] hits = Physics.RaycastAll(
                candidateOrigin,
                Vector3.down,
                castDistance,
                interactionLayers,
                QueryTriggerInteraction.Ignore);

            bool foundSurface = false;
            float nearestDistance = float.PositiveInfinity;
            for (int i = 0; i < hits.Length; i++)
            {
                RaycastHit hit = hits[i];
                if (IsIgnoredDropCollider(hit.collider) || hit.distance >= nearestDistance) continue;
                surfaceHit = hit;
                nearestDistance = hit.distance;
                foundSurface = true;
            }
            return foundSurface;
        }

        private bool ClampDropToPlayerSideOfObstacles(Vector3 planarForward, ref Vector3 objectPosition)
        {
            Vector3 desiredCenter = carried.GetDropBoundsCenter(objectPosition);
            float probeRadius = Mathf.Max(0.01f, carried.HorizontalDropRadius - dropCollisionSkin);
            Vector3 castOrigin = new Vector3(
                viewCamera.transform.position.x,
                desiredCenter.y,
                viewCamera.transform.position.z) + planarForward * (probeRadius + dropObstacleClearance);
            Vector3 horizontalDelta = Vector3.ProjectOnPlane(desiredCenter - castOrigin, Vector3.up);
            float castDistance = horizontalDelta.magnitude;
            if (castDistance <= 0.0001f) return true;

            Vector3 castDirection = horizontalDelta / castDistance;
            RaycastHit[] hits = Physics.SphereCastAll(
                castOrigin,
                probeRadius,
                castDirection,
                castDistance,
                interactionLayers,
                QueryTriggerInteraction.Ignore);

            float allowedDistance = castDistance;
            for (int i = 0; i < hits.Length; i++)
            {
                RaycastHit hit = hits[i];
                if (IsIgnoredDropCollider(hit.collider)) continue;
                if (Mathf.Abs(hit.normal.y) > dropWallNormalMaximumY) continue;
                allowedDistance = Mathf.Min(
                    allowedDistance,
                    Mathf.Max(0f, hit.distance - dropObstacleClearance));
            }

            if (allowedDistance >= castDistance) return true;
            Vector3 clampedCenter = castOrigin + castDirection * allowedDistance;
            objectPosition.x += clampedCenter.x - desiredCenter.x;
            objectPosition.z += clampedCenter.z - desiredCenter.z;
            return SnapDropPositionToFloor(ref objectPosition);
        }

        private bool ResolveDropOverlaps(ref Vector3 objectPosition)
        {
            for (int iteration = 0; iteration < dropCollisionResolutionIterations; iteration++)
            {
                Collider[] overlaps = FindDropOverlaps(objectPosition);
                Vector3 horizontalPush = Vector3.zero;
                bool foundPenetration = false;

                for (int i = 0; i < overlaps.Length; i++)
                {
                    Collider obstacle = overlaps[i];
                    if (IsIgnoredDropCollider(obstacle)) continue;
                    if (!Physics.ComputePenetration(
                            carried.PhysicsCollider,
                            objectPosition,
                            carried.transform.rotation,
                            obstacle,
                            obstacle.transform.position,
                            obstacle.transform.rotation,
                            out Vector3 direction,
                            out float distance))
                        continue;

                    Vector3 planarDirection = Vector3.ProjectOnPlane(direction, Vector3.up);
                    if (planarDirection.sqrMagnitude < 0.0001f) continue;
                    horizontalPush += planarDirection.normalized * (distance + dropObstacleClearance);
                    foundPenetration = true;
                }

                if (!foundPenetration) return true;
                objectPosition += horizontalPush;
                if (!SnapDropPositionToFloor(ref objectPosition)) return false;
            }
            return true;
        }

        private bool SnapDropPositionToFloor(ref Vector3 objectPosition)
        {
            Vector3 boundsCenter = carried.GetDropBoundsCenter(objectPosition);
            if (!TryFindDropSurfaceAt(boundsCenter, out RaycastHit surfaceHit)) return false;
            objectPosition = carried.GetRestingPosition(
                surfaceHit.point,
                surfaceHit.normal,
                dropSurfaceClearance);
            return true;
        }

        private bool HasBlockingDropOverlap(Vector3 objectPosition)
        {
            Collider[] overlaps = FindDropOverlaps(objectPosition);
            for (int i = 0; i < overlaps.Length; i++)
            {
                Collider obstacle = overlaps[i];
                if (IsIgnoredDropCollider(obstacle)) continue;
                if (Physics.ComputePenetration(
                        carried.PhysicsCollider,
                        objectPosition,
                        carried.transform.rotation,
                        obstacle,
                        obstacle.transform.position,
                        obstacle.transform.rotation,
                        out _,
                        out float distance) &&
                    distance > dropCollisionSkin)
                    return true;
            }
            return false;
        }

        private Collider[] FindDropOverlaps(Vector3 objectPosition)
        {
            Vector3 center = carried.GetDropBoundsCenter(objectPosition);
            Vector3 halfExtents = carried.DropBoundsExtents + Vector3.one * dropObstacleClearance;
            return Physics.OverlapBox(
                center,
                halfExtents,
                Quaternion.identity,
                interactionLayers,
                QueryTriggerInteraction.Ignore);
        }

        private bool IsIgnoredDropCollider(Collider candidate)
        {
            if (candidate == null) return true;
            if (carried != null &&
                (candidate == carried.PhysicsCollider || candidate.transform.IsChildOf(carried.transform)))
                return true;
            return IsPlayerCollider(candidate);
        }

        private bool IsPlayerCollider(Collider candidate)
        {
            if (candidate == null) return false;
            Transform candidateTransform = candidate.transform;
            if (candidateTransform == transform || candidateTransform.IsChildOf(transform)) return true;
            return false;
        }

        private bool TryFindPlacementGoal(out GoalPedestal selectedGoal)
        {
            selectedGoal = null;
            Ray aimRay = new Ray(viewCamera.transform.position, viewCamera.transform.forward);

            // Preserve exact aiming as the highest-priority placement path.
            if (Physics.Raycast(aimRay, out RaycastHit directHit, placementDistance, interactionLayers, QueryTriggerInteraction.Ignore))
            {
                GoalPedestal directGoal = directHit.collider.GetComponentInParent<GoalPedestal>();
                if (directGoal != null &&
                    directGoal.CanPlace(carried) &&
                    IsAimNearPlacementTarget(directGoal) &&
                    HasPlacementLineOfSight(directGoal))
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

            // A wider cast finds nearby goal geometry, but the final gate remains the
            // marked top-surface target rather than the pedestal's full collider.
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
                if (candidate == null || !candidate.CanPlace(carried)) continue;
                if (!IsAimNearPlacementTarget(candidate) || !HasPlacementLineOfSight(candidate)) continue;

                Vector3 toGoal = candidate.PlacementAimPosition - viewCamera.transform.position;
                float angle = Vector3.Angle(viewCamera.transform.forward, toGoal);
                float score = angle + toGoal.magnitude * 0.05f;
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
                if (candidate == null || !candidate.CanPlace(carried)) continue;
                Bounds bounds = candidate.PlacementBounds;
                float verticalGap = cameraPosition.y - bounds.max.y;
                if (verticalGap < 0f || verticalGap > topPlacementMaxCameraHeight) continue;

                bool insideX = cameraPosition.x >= bounds.min.x - topPlacementHorizontalPadding &&
                               cameraPosition.x <= bounds.max.x + topPlacementHorizontalPadding;
                bool insideZ = cameraPosition.z >= bounds.min.z - topPlacementHorizontalPadding &&
                               cameraPosition.z <= bounds.max.z + topPlacementHorizontalPadding;
                if (!insideX || !insideZ) continue;

                if (!IsAimNearPlacementTarget(candidate) || !HasPlacementLineOfSight(candidate)) continue;
                if (verticalGap >= bestVerticalGap) continue;
                bestVerticalGap = verticalGap;
                selectedGoal = candidate;
            }

            return selectedGoal != null;
        }

        private bool IsAimNearPlacementTarget(GoalPedestal goal)
        {
            Vector3 origin = viewCamera.transform.position;
            Vector3 forward = viewCamera.transform.forward;
            Vector3 toTarget = goal.PlacementAimPosition - origin;
            float forwardDistance = Vector3.Dot(toTarget, forward);
            if (forwardDistance < 0f || forwardDistance > placementDistance) return false;

            Vector3 closestAimPoint = origin + forward * forwardDistance;
            return (goal.PlacementAimPosition - closestAimPoint).sqrMagnitude <=
                   placementCastRadius * placementCastRadius;
        }

        private bool HasPlacementLineOfSight(GoalPedestal goal)
        {
            Vector3 origin = viewCamera.transform.position;
            Vector3 toTarget = goal.PlacementAimPosition - origin;
            float targetDistance = toTarget.magnitude;
            if (targetDistance <= Mathf.Epsilon) return true;

            RaycastHit[] hits = Physics.RaycastAll(
                origin,
                toTarget / targetDistance,
                targetDistance + dropCollisionSkin,
                interactionLayers,
                QueryTriggerInteraction.Ignore);
            RaycastHit nearestHit = default;
            float nearestDistance = float.PositiveInfinity;
            bool foundHit = false;
            for (int i = 0; i < hits.Length; i++)
            {
                RaycastHit hit = hits[i];
                if (IsPlayerCollider(hit.collider) || hit.distance >= nearestDistance) continue;
                nearestHit = hit;
                nearestDistance = hit.distance;
                foundHit = true;
            }

            return !foundHit || nearestHit.collider.GetComponentInParent<GoalPedestal>() == goal;
        }

        public void SetInteractionCueMode(InteractionCueMode mode)
        {
            interactionCueMode = mode;
            interactionCueGlowFadeElapsed = 0f;
            ClearInteractionCueVisuals();
        }

        public void SetValueSearchMode(bool enabled)
        {
            valueSearchMode = enabled;
            UpdateStatus();
        }

        private void UpdateInteractionCue()
        {
            if (viewCamera == null || (magnifier != null && magnifier.IsDeveloperPanelOpen))
            {
                ResetInteractionCue();
                return;
            }

            if (!TryGetInteractionCueCandidate(
                    out Object cueTarget,
                    out Bounds cueBounds,
                    out string cueLabel,
                    out bool supportsGlow,
                    out Renderer cueRenderer,
                    out Renderer cueColorRenderer))
            {
                ResetInteractionCue();
                return;
            }

            bool sameCue = currentCueTarget == cueTarget && currentCueLabel == cueLabel;
            if (!sameCue)
            {
                currentCueTarget = cueTarget;
                currentCueLabel = cueLabel;
                interactionCueDwellElapsed = 0f;
                interactionCueGlowFadeElapsed = 0f;
                ClearInteractionCueVisuals();
            }

            interactionCueDwellElapsed += Time.unscaledDeltaTime;
            if (interactionCueDwellElapsed < interactionCueDwellDuration) return;

            if (interactionCueMode == InteractionCueMode.CenterText)
                ShowInteractionCueText(cueLabel);
            else if (supportsGlow)
            {
                interactionCueGlowFadeElapsed += Time.unscaledDeltaTime;
                float fadeProgress = interactionCueGlowFadeInDuration <= 0f
                    ? 1f
                    : Mathf.Clamp01(interactionCueGlowFadeElapsed / interactionCueGlowFadeInDuration);
                float easedOpacity = fadeProgress * fadeProgress * (3f - 2f * fadeProgress);
                ShowInteractionCueGlow(cueRenderer, cueColorRenderer, easedOpacity);
            }
        }

        private bool TryGetInteractionCueCandidate(
            out Object cueTarget,
            out Bounds cueBounds,
            out string cueLabel,
            out bool supportsGlow,
            out Renderer cueRenderer,
            out Renderer cueColorRenderer)
        {
            cueTarget = null;
            cueBounds = default;
            cueLabel = string.Empty;
            supportsGlow = false;
            cueRenderer = null;
            cueColorRenderer = null;

            if (carried != null && TryFindPlacementGoal(out GoalPedestal goal))
            {
                cueTarget = goal;
                cueBounds = goal.PlacementBounds;
                cueLabel = "Q PLACE";
                // Goal bases keep the explicit text cue but never receive object glow.
                supportsGlow = false;
                return true;
            }

            // Carrying has only one contextual cue: a valid matching placement.
            // Ordinary drops remain available through Q but are never suggested.
            if (carried != null) return false;

            if (!TryFindNearestPickupHit(0f, out RaycastHit centerHit)) return false;
            DiscoverableObject item = centerHit.collider.GetComponentInParent<DiscoverableObject>();
            if (item == null) return false;
            cueTarget = item;
            cueBounds = item.InteractionCueBounds;
            cueLabel = "LC PICKUP";
            supportsGlow = true;
            cueRenderer = item.InteractionCueRenderer;
            cueColorRenderer = item.InteractionCueColorRenderer;
            return true;
        }

        private void ShowInteractionCueText(string cueLabel)
        {
            InteractionOutlineRuntime.Hide();
            if (interactionCueTextRoot == null || interactionCueText == null) return;
            interactionCueText.text = cueLabel;
            interactionCueText.color = magnifier != null && magnifier.IsFullColorView
                ? interactionCueColorTextColor
                : interactionCueGreyscaleTextColor;
            interactionCueTextRoot.SetActive(true);
        }

        private void ShowInteractionCueGlow(
            Renderer sourceRenderer,
            Renderer colorSourceRenderer,
            float opacity)
        {
            if (interactionCueTextRoot != null) interactionCueTextRoot.SetActive(false);
            if (sourceRenderer == null) return;
            InteractionOutlineRuntime.Show(
                sourceRenderer,
                colorSourceRenderer,
                opacity,
                interactionCueGlowWidthPixels,
                interactionCueGlowSoftness,
                interactionCueGreyscaleGlowColor,
                interactionCueColorGlowColor,
                interactionCueColorViewScale);
        }

        private void ResetInteractionCue()
        {
            currentCueTarget = null;
            currentCueLabel = string.Empty;
            interactionCueDwellElapsed = 0f;
            interactionCueGlowFadeElapsed = 0f;
            ClearInteractionCueVisuals();
        }

        private void ClearInteractionCueVisuals()
        {
            if (interactionCueTextRoot != null) interactionCueTextRoot.SetActive(false);
            InteractionOutlineRuntime.Hide();
        }

        private void CreateInteractionCueText()
        {
            if (statusText == null) return;
            Canvas canvas = statusText.GetComponentInParent<Canvas>();
            if (canvas == null) return;

            interactionCueTextRoot = new GameObject(
                "InteractionCueText",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Text),
                typeof(Outline));
            RectTransform cueRect = interactionCueTextRoot.GetComponent<RectTransform>();
            cueRect.SetParent(canvas.transform, false);
            cueRect.anchorMin = cueRect.anchorMax = new Vector2(0.5f, 0.5f);
            cueRect.pivot = new Vector2(0.5f, 0.5f);
            cueRect.anchoredPosition = interactionCueTextOffset;
            cueRect.sizeDelta = interactionCueTextSize;
            cueRect.SetAsLastSibling();

            interactionCueText = interactionCueTextRoot.GetComponent<Text>();
            interactionCueText.font = statusText.font != null
                ? statusText.font
                : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            interactionCueText.fontSize = interactionCueFontSize;
            interactionCueText.fontStyle = FontStyle.Bold;
            interactionCueText.alignment = TextAnchor.MiddleCenter;
            interactionCueText.raycastTarget = false;

            Outline outline = interactionCueTextRoot.GetComponent<Outline>();
            outline.effectColor = interactionCueTextOutlineColor;
            outline.effectDistance = Vector2.one;
            outline.useGraphicAlpha = true;
        }

        private void UpdateStatus()
        {
            if (statusText == null) return;
            statusText.text = valueSearchMode
                ? ValueSearchInstructionText
                : ColorSearchInstructionText;
        }

        private void CreateInventoryHud()
        {
            if (!showInventoryHud || statusText == null) return;
            Canvas canvas = statusText.GetComponentInParent<Canvas>();
            if (canvas == null) return;

            inventoryHudRoot = new GameObject("InventoryHUD", typeof(RectTransform));
            RectTransform panelRect = inventoryHudRoot.GetComponent<RectTransform>();
            panelRect.SetParent(canvas.transform, false);
            panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0f);
            panelRect.pivot = new Vector2(0.5f, 0f);
            panelRect.anchoredPosition = inventoryHudOffset;
            panelRect.sizeDelta = inventoryHudSize;
            panelRect.SetAsLastSibling();

            Font hudFont = statusText.font != null
                ? statusText.font
                : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            float ballSlotWidth = Mathf.Max(inventoryBallFontSize, inventoryHudSize.y);
            GameObject ballObject = new GameObject(
                "BallShape",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Text),
                typeof(Outline));
            RectTransform ballRect = ballObject.GetComponent<RectTransform>();
            ballRect.SetParent(panelRect, false);
            ballRect.anchorMin = new Vector2(0f, 0f);
            ballRect.anchorMax = new Vector2(0f, 1f);
            ballRect.pivot = new Vector2(0f, 0.5f);
            ballRect.anchoredPosition = Vector2.zero;
            ballRect.sizeDelta = new Vector2(ballSlotWidth, 0f);
            Text ballText = ballObject.GetComponent<Text>();
            ballText.font = hudFont;
            ballText.fontSize = inventoryBallFontSize;
            ballText.alignment = TextAnchor.MiddleCenter;
            ballText.color = inventoryFillColor;
            ballText.text = "●";
            ballText.raycastTarget = false;
            ConfigureInventoryOutline(ballObject.GetComponent<Outline>());

            GameObject labelObject = new GameObject(
                "ObjectLabel",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Text),
                typeof(Outline));
            RectTransform labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.SetParent(panelRect, false);
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(ballSlotWidth + inventoryHudGap, 0f);
            labelRect.offsetMax = Vector2.zero;
            inventoryLabel = labelObject.GetComponent<Text>();
            inventoryLabel.font = hudFont;
            inventoryLabel.fontSize = inventoryFontSize;
            inventoryLabel.alignment = TextAnchor.MiddleLeft;
            inventoryLabel.color = inventoryFillColor;
            inventoryLabel.raycastTarget = false;
            ConfigureInventoryOutline(labelObject.GetComponent<Outline>());
        }

        private void ConfigureInventoryOutline(Outline outline)
        {
            if (outline == null) return;
            outline.effectColor = inventoryOutlineColor;
            outline.effectDistance = inventoryOutlineDistance;
            outline.useGraphicAlpha = true;
        }

        private void RefreshInventoryHud()
        {
            if (inventoryHudRoot == null) return;
            bool hasObject = carried != null;
            inventoryHudRoot.SetActive(hasObject);
            if (!hasObject) return;
            inventoryLabel.text = carried.DisplayName;
        }

        private void OnDisable()
        {
            ClearInteractionCueVisuals();
        }

        private void OnDestroy()
        {
            if (interactionCueTextRoot != null) Destroy(interactionCueTextRoot);
            InteractionOutlineRuntime.Hide();
        }
    }
}
