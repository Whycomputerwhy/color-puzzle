using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ColorPuzzle
{
    public sealed class PlayerInteraction : MonoBehaviour
    {
        private enum DropPresentation
        {
            Immediate,
            FocusDepthAnimation,
            NearViewWallAnimation
        }

        private const string InstructionText = "WASD  •  SPACE jump  •  RMB magnifier  •  LMB pick up  •  Q drop/place  •  R reset";

        [Header("References")]
        [SerializeField] private Camera viewCamera;
        [SerializeField] private Text statusText;

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

        [Header("Inventory HUD")]
        [SerializeField] private bool showInventoryHud = true;
        [SerializeField] private Vector2 inventoryHudSize = new Vector2(260f, 52f);
        [SerializeField] private Vector2 inventoryHudOffset = new Vector2(0f, 92f);
        [SerializeField, Min(0f)] private float inventoryHudPadding = 10f;
        [SerializeField, Min(8f)] private float inventorySwatchSize = 28f;
        [SerializeField, Min(8)] private int inventoryFontSize = 18;
        [SerializeField] private Color inventoryBackgroundColor = new Color(0f, 0f, 0f, 0.72f);
        [SerializeField] private Color inventoryTextColor = Color.white;

        private DiscoverableObject carried;
        private MagnifierView magnifier;
        private CharacterController playerController;
        private GameObject inventoryHudRoot;
        private Image inventorySwatch;
        private Text inventoryLabel;

        private void Awake()
        {
            magnifier = GetComponent<MagnifierView>();
            playerController = GetComponent<CharacterController>();
            CreateInventoryHud();
            RefreshInventoryHud();
        }

        private void Update()
        {
            if (Mouse.current == null || Keyboard.current == null) return;
            if (Keyboard.current[resetKey].wasPressedThisFrame)
            {
                SceneManager.LoadScene(SceneManager.GetActiveScene().name);
                return;
            }

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
                    directGoal.CanAccept(carried) &&
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
                if (candidate == null || !candidate.CanAccept(carried)) continue;
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
                if (candidate == null || !candidate.CanAccept(carried)) continue;
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

        private void UpdateStatus()
        {
            if (statusText == null) return;
            statusText.text = InstructionText;
        }

        private void CreateInventoryHud()
        {
            if (!showInventoryHud || statusText == null) return;
            Canvas canvas = statusText.GetComponentInParent<Canvas>();
            if (canvas == null) return;

            inventoryHudRoot = new GameObject("InventoryHUD", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform panelRect = inventoryHudRoot.GetComponent<RectTransform>();
            panelRect.SetParent(canvas.transform, false);
            panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0f);
            panelRect.pivot = new Vector2(0.5f, 0f);
            panelRect.anchoredPosition = inventoryHudOffset;
            panelRect.sizeDelta = inventoryHudSize;
            Image panelImage = inventoryHudRoot.GetComponent<Image>();
            panelImage.color = inventoryBackgroundColor;
            panelImage.raycastTarget = false;
            panelRect.SetAsLastSibling();

            GameObject swatchObject = new GameObject("ObjectColor", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform swatchRect = swatchObject.GetComponent<RectTransform>();
            swatchRect.SetParent(panelRect, false);
            swatchRect.anchorMin = swatchRect.anchorMax = new Vector2(0f, 0.5f);
            swatchRect.pivot = new Vector2(0f, 0.5f);
            swatchRect.anchoredPosition = new Vector2(inventoryHudPadding, 0f);
            swatchRect.sizeDelta = Vector2.one * inventorySwatchSize;
            inventorySwatch = swatchObject.GetComponent<Image>();
            inventorySwatch.raycastTarget = false;

            GameObject labelObject = new GameObject("ObjectLabel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            RectTransform labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.SetParent(panelRect, false);
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(inventoryHudPadding * 2f + inventorySwatchSize, 0f);
            labelRect.offsetMax = new Vector2(-inventoryHudPadding, 0f);
            inventoryLabel = labelObject.GetComponent<Text>();
            inventoryLabel.font = statusText.font != null ? statusText.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            inventoryLabel.fontSize = inventoryFontSize;
            inventoryLabel.alignment = TextAnchor.MiddleLeft;
            inventoryLabel.color = inventoryTextColor;
            inventoryLabel.raycastTarget = false;
        }

        private void RefreshInventoryHud()
        {
            if (inventoryHudRoot == null) return;
            bool hasObject = carried != null;
            inventoryHudRoot.SetActive(hasObject);
            if (!hasObject) return;
            inventorySwatch.color = carried.InventoryColor;
            inventoryLabel.text = $"CARRYING: {carried.DisplayName}";
        }
    }
}
