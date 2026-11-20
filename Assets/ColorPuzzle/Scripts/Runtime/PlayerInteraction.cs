using System.Collections;
using System.Collections.Generic;
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
            "WASD  •  SPACE jump  •  RMB magnifier  •  LMB interact / pick up / drop  •  E activate  •  R reset";
        private const string ValueSearchInstructionText =
            "WASD  •  SPACE jump  •  LMB interact / pick up / drop  •  E activate  •  R reset";

        // Reused to avoid allocating while the player keeps their focus on a pickup.
        private readonly List<Renderer> interactionCueRenderers = new List<Renderer>();

        [Header("References")]
        [SerializeField] private Camera viewCamera;
        [SerializeField] private Text statusText;

        [Header("Control HUD Styling")]
        [SerializeField] private bool applyStatusTextOutline = true;
        [SerializeField] private Color statusTextOutlineColor = new Color(1f, 1f, 1f, 0.92f);
        [SerializeField] private Vector2 statusTextOutlineDistance = new Vector2(1.5f, -1.5f);

        [SerializeField] private Key interactKey = Key.E;
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

        // DIAL HOLD INPUT — captures a held LMB only when a number dial is aimed.
        [Header("Dial Hold Input")]
        [SerializeField, Min(0f)] private float dialHoldInitialDelay = 0.32f;
        [SerializeField, Min(0.02f)] private float dialHoldRepeatInterval = 0.12f;

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
        [Tooltip("Camera-relative position for the held object, in the lower-right of the player's view.")]
        [SerializeField] private Vector3 inventoryViewModelPosition = new Vector3(0.72f, -0.5f, 1.15f);
        [SerializeField, Min(0.1f)] private float inventoryPreviewModelSize = 0.55f;
        [SerializeField] private Vector3 inventoryPreviewRotation = new Vector3(14f, -20f, -8f);
        [Tooltip("Used by flat patterned stones so their marked face reads clearly in the player's hand.")]
        [SerializeField] private Vector3 inventoryPreviewStoneRotation = new Vector3(-90f, 75f, 0f);
        [SerializeField, Min(0f)] private float inventoryPreviewStoneVerticalOffset = 0.12f;

        [Header("Opening Tutorial")]
        [SerializeField] private bool showOpeningTutorial = true;
        [SerializeField, Min(0f)] private float tutorialFadeDuration = 0.25f;
        [SerializeField, Min(0f), Tooltip("Minimum time each tutorial cue remains on screen. It will then wait for the player to try one of its shown actions before fading.")]
        private float tutorialVisibleDuration = 3.8f;

        [Header("Contextual Hints")]
        [SerializeField, Min(0f)] private float contextHintVisibleDuration = 4f;
        [SerializeField, Min(0f)] private float contextHintVerticalOffset = 140f;

        private DiscoverableObject carried;
        private ShellPuzzlePiece carriedShellKey;
        private MagnifierView magnifier;
        private CharacterController playerController;
        private GameObject inventoryPreviewRoot;
        private GameObject inventoryPreviewVisual;
        private GameObject inventoryPreviewLight;
        private int originalViewCameraCullingMask;
        private bool inventoryPreviewCullingConfigured;
        private GameObject tutorialHudRoot;
        private CanvasGroup tutorialCanvasGroup;
        private Text tutorialText;
        private Coroutine tutorialRoutine;
        private GameObject contextHintHudRoot;
        private CanvasGroup contextHintCanvasGroup;
        private Text contextHintText;
        private Coroutine contextHintRoutine;
        private GameObject interactionCueTextRoot;
        private Text interactionCueText;
        private Object currentCueTarget;
        private string currentCueLabel;
        private float interactionCueDwellElapsed;
        private float interactionCueGlowFadeElapsed;
        private bool valueSearchMode;
        private bool openingTutorialActive;
        private bool showedPickupTutorial;
        private bool showedMagnifierTutorial;
        private bool showedDialTutorial;
        private TutorialAction activeTutorialAction;
        private bool tutorialActionAttempted;

        private enum TutorialAction
        {
            None,
            MoveOrJump,
            PickUpOrDrop,
            Magnifier,
            RotateDial
        }
        private NumberDial heldDial;
        private NumberDialLockController heldDialLock;
        private float nextDialHoldStepTime;

        public InteractionCueMode CurrentInteractionCueMode => interactionCueMode;

        private void Awake()
        {
            magnifier = GetComponent<MagnifierView>();
            playerController = GetComponent<CharacterController>();
            ConfigureStatusTextOutline();
            CreateInventoryHud();
            CreateTutorialHud();
            CreateContextHintHud();
            CreateInteractionCueText();
            ClearInteractionCueVisuals();
            RefreshInventoryHud();
        }

        private void Start()
        {
            openingTutorialActive = showOpeningTutorial && OceanCampaignManager.Active != null &&
                                    OceanCampaignManager.Active.CurrentStageNumber == 1;
            if (openingTutorialActive)
                ShowTutorial("WASD move  •  SPACE jump", TutorialAction.MoveOrJump);
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
                if (OceanCampaignManager.Active != null)
                {
                    OceanCampaignManager.Active.ReloadCheckpoint();
                    return;
                }
                SceneManager.LoadScene(SceneManager.GetActiveScene().name);
                return;
            }

            TrackTutorialActionAttempt();

            UpdateInteractionCue();
            if (magnifier != null && magnifier.IsActive) { UpdateStatus(); return; }

            if (Keyboard.current[interactKey].wasPressedThisFrame) TryActivateStageGate();
            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                if (carriedShellKey != null) TryDropOrPlaceShellKey();
                else if (carried != null) TryDropOrPlace();
                else if (!TryBeginNumberDialHold()) TryPickUp();
            }
            UpdateNumberDialHold();
            UpdateStatus();
        }

        private void TryActivateStageGate()
        {
            if (viewCamera == null) return;
            Ray ray = new Ray(viewCamera.transform.position, viewCamera.transform.forward);
            if (Physics.Raycast(ray, out RaycastHit hit, interactionDistance, interactionLayers,
                    QueryTriggerInteraction.Collide))
            {
                StageGateBeacon aimedGate = hit.collider.GetComponentInParent<StageGateBeacon>();
                if (aimedGate != null && aimedGate.TryActivate(transform, interactionDistance)) return;
            }

            StageGateBeacon[] gates = FindObjectsByType<StageGateBeacon>(FindObjectsSortMode.None);
            foreach (StageGateBeacon gate in gates)
                if (gate.TryActivate(transform, interactionDistance)) return;
        }

        private bool TryBeginNumberDialHold()
        {
            // UpdateInteractionCue runs immediately before input. Reusing its
            // candidate means the visible dial cue and LMB targeting share one
            // answer, instead of two slightly different raycast decisions.
            if (currentCueTarget is NumberDial cuedDial && TryCaptureNumberDial(cuedDial))
                return true;

            if (viewCamera == null) return false;
            Ray ray = new Ray(viewCamera.transform.position, viewCamera.transform.forward);
            if (!Physics.Raycast(ray, out RaycastHit hit, interactionDistance, interactionLayers,
                    QueryTriggerInteraction.Collide)) return false;

            // Puzzle 4 reserves the dial centre for key placement. Only its outer
            // ring/backplate may begin a number rotation.
            if (hit.collider.GetComponentInParent<FinalDialKeySlot>() != null) return false;
            NumberDial dial = hit.collider.GetComponentInParent<NumberDial>();
            return TryCaptureNumberDial(dial);
        }

        private bool TryCaptureNumberDial(NumberDial dial)
        {
            NumberDialLockController dialLock = dial != null
                ? dial.GetComponentInParent<NumberDialLockController>()
                : null;
            if (dialLock == null || !dialLock.TryRotate(dial)) return false;

            // The press advances once. Holding repeats after a short delay, while
            // releasing LMB always stops the captured dial immediately.
            if (dialLock.CanRotate(dial))
            {
                heldDial = dial;
                heldDialLock = dialLock;
                nextDialHoldStepTime = Time.unscaledTime + dialHoldInitialDelay;
            }
            return true;
        }

        private void UpdateNumberDialHold()
        {
            if (heldDial == null || heldDialLock == null) return;
            if (!Mouse.current.leftButton.isPressed)
            {
                // Resolve the complete combination only after the player has
                // stopped changing this dial. This also makes every later hold
                // a fresh answer check after an earlier failure.
                heldDialLock.EvaluateDialInput();
                heldDial = null;
                heldDialLock = null;
                return;
            }
            if (Time.unscaledTime < nextDialHoldStepTime) return;

            if (!heldDialLock.TryRotate(heldDial))
            {
                heldDial = null;
                heldDialLock = null;
                return;
            }
            nextDialHoldStepTime = Time.unscaledTime + dialHoldRepeatInterval;
        }

        private void TryPickUp()
        {
            if (TryFindNearestPickupHit(0f, out RaycastHit specialHit))
            {
                MagnifierPickup magnifierPickup = specialHit.collider.GetComponentInParent<MagnifierPickup>();
                if (magnifierPickup != null && magnifierPickup.TryAcquire(transform, interactionDistance))
                {
                    ShowMagnifierTutorial();
                    ResetInteractionCue();
                    return;
                }
            }

            if (TryFindNearestPickupHit(0f, out RaycastHit shellHit))
            {
                ShellPuzzlePiece shell = shellHit.collider.GetComponentInParent<ShellPuzzlePiece>();
                if (shell != null && shell.Kind == ShellPuzzlePiece.PieceKind.Keyhole)
                    shell = shell.PlacedKey;
                if (shell != null && shell.PickUp())
                {
                    carriedShellKey = shell;
                    TelemetryManager.RecordObjectPickup();
                    ResetInteractionCue();
                    RefreshInventoryHud();
                    return;
                }
            }

            // Puzzle 4 keys rest inside a dial recess. Its collider sits in front
            // of the stone, so use a raycast-all fallback that can reach the key
            // only when the crosshair is actually on the key's own collider.
            if (TryFindPlacedFinalDialKeyForPickup(out ShellPuzzlePiece dialKey) &&
                dialKey.PickUp())
            {
                carriedShellKey = dialKey;
                TelemetryManager.RecordObjectPickup();
                ResetInteractionCue();
                RefreshInventoryHud();
                return;
            }

            DiscoverableObject item = FindPickupObjectInAim();
            if (item == null)
                item = FindPickupObjectBelowPlayer();
            if (item == null) return;
            carried = item;
            carried.PickUp();
            TelemetryManager.RecordObjectPickup();
            ResetInteractionCue();
            RefreshInventoryHud();
        }

        private void TryDropOrPlaceShellKey()
        {
            if (TryFindFinalDialKeySlot(out FinalDialKeySlot dialSlot))
            {
                if (dialSlot.TryPlace(carriedShellKey, out ShellPuzzlePiece swappedOutKey))
                {
                    // Replacing an occupied slot is a direct hand-to-slot swap:
                    // the outgoing key becomes the player's carried item.
                    carriedShellKey = swappedOutKey;
                    TelemetryManager.RecordObjectDrop();
                    if (swappedOutKey != null) TelemetryManager.RecordObjectPickup();
                    ResetInteractionCue();
                    RefreshInventoryHud();
                }
                return;
            }
            if (TryFindShellKeyhole(out ShellPuzzlePiece keyhole))
            {
                ShellPuzzleController puzzle = keyhole.GetComponentInParent<ShellPuzzleController>();
                if (puzzle != null && puzzle.TryPlace(carriedShellKey, keyhole,
                        out ShellPuzzlePiece swappedOutKey))
                {
                    carriedShellKey = swappedOutKey;
                    ResetInteractionCue();
                    RefreshInventoryHud();
                }
                return;
            }

            Vector3 candidate = viewCamera.transform.position + viewCamera.transform.forward * dropDistance;
            RaycastHit[] surfaces = Physics.RaycastAll(
                candidate + Vector3.up * dropGroundProbeHeight,
                Vector3.down,
                dropGroundProbeHeight + dropGroundProbeDistance,
                interactionLayers,
                QueryTriggerInteraction.Ignore);
            float nearestDistance = float.PositiveInfinity;
            bool found = false;
            Vector3 floorPoint = default;
            foreach (RaycastHit surface in surfaces)
            {
                if (IsPlayerCollider(surface.collider) || surface.normal.y < 0.6f ||
                    surface.distance >= nearestDistance) continue;
                nearestDistance = surface.distance;
                floorPoint = surface.point;
                found = true;
            }
            if (!found) return;
            Vector3 visualCenter = viewCamera.transform.position +
                                   viewCamera.transform.forward * dropDistance;
            Vector3 animationStart = visualCenter - carriedShellKey.GetVisualCenterOffset();
            carriedShellKey.DropAnimated(animationStart, floorPoint,
                dropAnimationDuration, dropAnimationFallExponent, playerController);
            TelemetryManager.RecordObjectDrop();
            carriedShellKey = null;
            ResetInteractionCue();
            RefreshInventoryHud();
        }

        private bool TryFindShellKeyhole(out ShellPuzzlePiece selected)
        {
            selected = null;
            if (carriedShellKey == null || viewCamera == null) return false;
            Vector3 origin = viewCamera.transform.position;
            Vector3 forward = viewCamera.transform.forward;
            float bestScore = float.PositiveInfinity;
            foreach (ShellPuzzlePiece candidate in FindObjectsByType<ShellPuzzlePiece>(FindObjectsSortMode.None))
            {
                if (candidate.Kind != ShellPuzzlePiece.PieceKind.Keyhole) continue;
                Vector3 slot = candidate.IsOccupied
                    ? candidate.PlacedKey.transform.position
                    : candidate.SlotPosition;
                Vector3 toSlot = slot - origin;
                float forwardDistance = Vector3.Dot(toSlot, forward);
                if (forwardDistance < 0f || forwardDistance > placementDistance) continue;
                float aimError = Vector3.Distance(slot, origin + forward * forwardDistance);
                float targetRadius = candidate.IsOccupied
                    ? Mathf.Max(placementCastRadius, 0.9f)
                    : Mathf.Max(placementCastRadius, candidate.FitDiameter * 0.5f + 0.25f);
                if (aimError > targetRadius ||
                    aimError >= bestScore) continue;
                if (!HasShellPlacementLineOfSight(candidate, origin, toSlot)) continue;
                selected = candidate;
                bestScore = aimError;
            }
            return selected != null;
        }

        private bool TryFindFinalDialKeySlot(out FinalDialKeySlot selected)
        {
            selected = null;
            if (carriedShellKey == null || viewCamera == null) return false;

            if (TryFindAimedOccupiedFinalDialKeySlot(out FinalDialKeySlot occupiedSlot))
            {
                selected = occupiedSlot;
                return true;
            }

            // An occupied slot is selected by aiming at its actual placed key, not
            // its hidden center anchor. This makes swapping feel like a direct
            // replacement interaction and avoids requiring an awkward precision aim.
            if (TryFindFocusedPlacedDialKey(0f, out ShellPuzzlePiece focusedKey) ||
                TryFindFocusedPlacedDialKey(placementCastRadius, out focusedKey))
            {
                FinalDialKeySlot focusedSlot = focusedKey.CurrentDialSlot;
                if (focusedSlot != null && focusedSlot.PlacedKey == focusedKey && focusedKey.CanPickUp)
                {
                    selected = focusedSlot;
                    return true;
                }
            }

            // The recessed slot lives inside a dial. A simple raycast usually hits the
            // dial's outer rim first, which used to make Q fall through to a normal
            // ground drop. Select the authored anchor instead, using a forgiving
            // screen-space-style aim radius just like the earlier keyhole placement.
            Vector3 origin = viewCamera.transform.position;
            Vector3 forward = viewCamera.transform.forward;
            float bestScore = float.PositiveInfinity;
            foreach (FinalDialKeySlot candidate in FindObjectsByType<FinalDialKeySlot>(FindObjectsSortMode.None))
            {
                if (candidate == null || candidate.PlacedKey != null) continue;

                Vector3 target = candidate.KeyPosition;
                Vector3 toTarget = target - origin;
                float forwardDistance = Vector3.Dot(toTarget, forward);
                if (forwardDistance < 0f || forwardDistance > placementDistance) continue;

                float aimError = Vector3.Distance(target, origin + forward * forwardDistance);
                if (aimError > candidate.PlacementAimRadius || aimError >= bestScore) continue;
                if (!HasFinalDialSlotLineOfSight(candidate, origin, toTarget)) continue;

                selected = candidate;
                bestScore = aimError;
            }

            return selected != null;
        }

        private bool TryFindAimedOccupiedFinalDialKeySlot(out FinalDialKeySlot selected)
        {
            selected = null;
            Vector3 origin = viewCamera.transform.position;
            Vector3 forward = viewCamera.transform.forward;
            float bestScore = float.PositiveInfinity;

            foreach (FinalDialKeySlot candidate in FindObjectsByType<FinalDialKeySlot>(FindObjectsSortMode.None))
            {
                ShellPuzzlePiece placedKey = candidate != null ? candidate.PlacedKey : null;
                if (placedKey == null) continue;

                // Use the placed model's authored transform as the target. This
                // avoids relying on individual imported stone collider shapes.
                Vector3 target = placedKey.transform.position;
                Vector3 toTarget = target - origin;
                float forwardDistance = Vector3.Dot(toTarget, forward);
                if (forwardDistance < 0f || forwardDistance > placementDistance) continue;

                float aimError = Vector3.Distance(target, origin + forward * forwardDistance);
                float generousKeyRadius = Mathf.Max(candidate.PlacementAimRadius, 0.9f);
                if (aimError > generousKeyRadius || aimError >= bestScore) continue;
                if (!HasFinalDialSlotLineOfSight(candidate, origin, toTarget)) continue;

                selected = candidate;
                bestScore = aimError;
            }

            return selected != null;
        }

        private bool TryFindFocusedPlacedDialKey(float radius, out ShellPuzzlePiece selectedKey)
        {
            selectedKey = null;
            RaycastHit[] hits = radius <= 0f
                ? Physics.RaycastAll(viewCamera.transform.position, viewCamera.transform.forward,
                    placementDistance, interactionLayers, QueryTriggerInteraction.Ignore)
                : Physics.SphereCastAll(viewCamera.transform.position, radius, viewCamera.transform.forward,
                    placementDistance, interactionLayers, QueryTriggerInteraction.Ignore);

            float nearestDistance = float.PositiveInfinity;
            foreach (RaycastHit hit in hits)
            {
                if (IsPlayerCollider(hit.collider) || hit.distance >= nearestDistance) continue;
                ShellPuzzlePiece key = hit.collider.GetComponentInParent<ShellPuzzlePiece>();
                if (key == null || key.CurrentDialSlot == null || !key.CanPickUp) continue;
                selectedKey = key;
                nearestDistance = hit.distance;
            }
            return selectedKey != null;
        }

        /// <summary>
        /// Finds a removable key seated in a Puzzle 4 dial only when the aiming
        /// ray actually crosses that key's collider. The dial itself must remain
        /// free to receive rotation input around its rim.
        /// </summary>
        private bool TryFindPlacedFinalDialKeyForPickup(out ShellPuzzlePiece selectedKey)
        {
            selectedKey = null;
            if (viewCamera == null) return false;

            // RaycastAll sees through the dial recess collider to the stone, but
            // unlike a spherecast or center-anchor check it cannot activate from
            // the dial rim or an empty part of the slot.
            return TryFindFocusedPlacedDialKey(0f, out selectedKey);
        }

        private bool HasFinalDialSlotLineOfSight(FinalDialKeySlot slot, Vector3 origin, Vector3 toSlot)
        {
            float distance = toSlot.magnitude;
            if (distance < 0.001f) return true;

            NumberDialLockController slotDial = slot.GetComponentInParent<NumberDialLockController>();
            foreach (RaycastHit hit in Physics.RaycastAll(origin, toSlot / distance, distance,
                         interactionLayers, QueryTriggerInteraction.Ignore))
            {
                if (IsPlayerCollider(hit.collider)) continue;

                // The dial rim and its center recess are intentionally in front of the
                // anchor. They are part of the target, not an obstruction.
                if (hit.collider.GetComponentInParent<FinalDialKeySlot>() == slot ||
                    hit.collider.GetComponentInParent<ShellPuzzlePiece>() == slot.PlacedKey ||
                    (slotDial != null && hit.collider.GetComponentInParent<NumberDialLockController>() == slotDial))
                    continue;

                return false;
            }

            return true;
        }

        private bool HasShellPlacementLineOfSight(ShellPuzzlePiece keyhole, Vector3 origin, Vector3 toSlot)
        {
            float distance = toSlot.magnitude;
            if (distance < 0.001f) return true;
            foreach (RaycastHit hit in Physics.RaycastAll(origin, toSlot / distance, distance, interactionLayers,
                         QueryTriggerInteraction.Ignore))
            {
                if (IsPlayerCollider(hit.collider) ||
                    hit.collider.GetComponentInParent<ShellPuzzlePiece>() == keyhole ||
                    hit.collider.GetComponentInParent<ShellPuzzlePiece>() == keyhole.PlacedKey ||
                    IsInsideNamedObject(hit.collider.transform, "Clam Casing")) continue;
                return false;
            }
            return true;
        }

        private static bool IsInsideNamedObject(Transform child, string name)
        {
            for (Transform current = child; current != null; current = current.parent)
                if (current.name == name) return true;
            return false;
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
                    TelemetryManager.RecordObjectDrop();
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
            TelemetryManager.RecordObjectDrop();
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

            // The magnifier is collected silently; its control tutorial appears after
            // acquisition instead of showing a separate pickup hint while aiming at it.
            if (cueTarget is MagnifierPickup)
            {
                ResetInteractionCue();
                return;
            }

            if (!showedPickupTutorial && (cueTarget is ShellPuzzlePiece || cueTarget is DiscoverableObject))
            {
                showedPickupTutorial = true;
                ShowTutorial("LMB pick up / drop", TutorialAction.PickUpOrDrop);
            }
            if (!showedDialTutorial && cueTarget is NumberDial)
            {
                showedDialTutorial = true;
                ShowTutorial("HOLD LMB rotate", TutorialAction.RotateDial);
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

            if (cueTarget is NumberDial)
            {
                // The dial uses the one-time bottom tutorial, never the centre cue.
                return;
            }
            if (cueTarget is MagnifierPickup || interactionCueMode == InteractionCueMode.CenterText)
                ShowInteractionCueText(cueLabel);
            else if (supportsGlow)
            {
                interactionCueGlowFadeElapsed += Time.unscaledDeltaTime;
                float fadeProgress = interactionCueGlowFadeInDuration <= 0f
                    ? 1f
                    : Mathf.Clamp01(interactionCueGlowFadeElapsed / interactionCueGlowFadeInDuration);
                float easedOpacity = fadeProgress * fadeProgress * (3f - 2f * fadeProgress);
                ShowInteractionCueGlow(cueTarget, cueRenderer, cueColorRenderer, easedOpacity);
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

            if (carriedShellKey != null && TryFindShellKeyhole(out ShellPuzzlePiece shellGoal))
            {
                cueTarget = shellGoal;
                cueLabel = "LMB PLACE";
                return true;
            }

            if (carriedShellKey != null && TryFindFinalDialKeySlot(out FinalDialKeySlot dialGoal))
            {
                cueTarget = dialGoal;
                cueLabel = "LMB PLACE";
                return true;
            }

            if (carried != null && TryFindPlacementGoal(out GoalPedestal goal))
            {
                cueTarget = goal;
                cueBounds = goal.PlacementBounds;
                cueLabel = "LMB PLACE";
                // Goal bases keep the explicit text cue but never receive object glow.
                supportsGlow = false;
                return true;
            }

            // Carrying has only one contextual cue: a valid matching placement.
            // Ordinary left-click drops remain available but are never suggested.
            if (carried != null || carriedShellKey != null) return false;

            // A Puzzle 4 dial recess sits in front of its seated slab. Show the
            // pickup cue only when the ray reaches the slab's own collider, so
            // aiming at the dial rim remains exclusively a rotate interaction.
            if (TryFindPlacedFinalDialKeyForPickup(out ShellPuzzlePiece seatedDialKey))
            {
                cueTarget = seatedDialKey;
                cueLabel = "LMB PICK UP";
                cueRenderer = seatedDialKey.CueRenderer;
                cueColorRenderer = cueRenderer;
                supportsGlow = cueRenderer != null;
                return true;
            }

            if (!TryFindNearestPickupHit(0f, out RaycastHit centerHit)) return false;
            MagnifierPickup ability = centerHit.collider.GetComponentInParent<MagnifierPickup>();
            if (ability != null && ability.IsAvailable)
            {
                cueTarget = ability;
                cueLabel = "LMB TAKE MAGNIFIER";
                return true;
            }
            NumberDial dial = centerHit.collider.GetComponentInParent<NumberDial>();
            if (centerHit.collider.GetComponentInParent<FinalDialKeySlot>() != null)
                return false;
            NumberDialLockController dialLock = dial != null
                ? dial.GetComponentInParent<NumberDialLockController>()
                : null;
            if (dialLock != null && dialLock.CanRotate(dial))
            {
                cueTarget = dial;
                cueLabel = "HOLD LMB ROTATE";
                return true;
            }
            ShellPuzzlePiece shell = centerHit.collider.GetComponentInParent<ShellPuzzlePiece>();
            if (shell != null && shell.CanPickUp)
            {
                cueTarget = shell;
                cueLabel = "LMB PICK UP";
                cueRenderer = shell.CueRenderer;
                cueColorRenderer = cueRenderer;
                supportsGlow = cueRenderer != null;
                return true;
            }
            DiscoverableObject item = centerHit.collider.GetComponentInParent<DiscoverableObject>();
            if (item == null) return false;
            cueTarget = item;
            cueBounds = item.InteractionCueBounds;
            cueLabel = "LMB PICK UP";
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
            Object cueTarget,
            Renderer sourceRenderer,
            Renderer colorSourceRenderer,
            float opacity)
        {
            if (interactionCueTextRoot != null) interactionCueTextRoot.SetActive(false);
            if (sourceRenderer == null) return;
            CollectCueRenderers(cueTarget, sourceRenderer, interactionCueRenderers);
            InteractionOutlineRuntime.Show(
                interactionCueRenderers,
                interactionCueRenderers,
                opacity,
                interactionCueGlowWidthPixels,
                interactionCueGlowSoftness,
                interactionCueGreyscaleGlowColor,
                interactionCueColorGlowColor,
                interactionCueColorViewScale);
        }

        private static void CollectCueRenderers(
            Object cueTarget,
            Renderer fallbackRenderer,
            List<Renderer> destination)
        {
            destination.Clear();
            Component component = cueTarget as Component;
            Transform root = component != null ? component.transform : fallbackRenderer.transform;
            Renderer[] hierarchyRenderers = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < hierarchyRenderers.Length; i++)
            {
                Renderer renderer = hierarchyRenderers[i];
                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                destination.Add(renderer);
            }

            if (destination.Count == 0 && fallbackRenderer != null)
                destination.Add(fallbackRenderer);
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
            if (!showInventoryHud || viewCamera == null) return;
            CreateInventoryPreviewCamera();
        }

        private void CreateInventoryPreviewCamera()
        {
            const int previewLayer = 11;
            inventoryPreviewRoot = new GameObject("CarriedObjectViewModel");
            inventoryPreviewRoot.transform.SetParent(viewCamera.transform, false);
            inventoryPreviewRoot.transform.localPosition = inventoryViewModelPosition;
            inventoryPreviewRoot.layer = previewLayer;
            originalViewCameraCullingMask = viewCamera.cullingMask;
            viewCamera.cullingMask |= 1 << previewLayer;
            inventoryPreviewCullingConfigured = true;

            inventoryPreviewLight = new GameObject("InventoryModelLight", typeof(Light));
            inventoryPreviewLight.transform.SetParent(viewCamera.transform, false);
            inventoryPreviewLight.transform.localRotation = Quaternion.Euler(16f, -10f, 0f);
            Light previewLight = inventoryPreviewLight.GetComponent<Light>();
            previewLight.type = LightType.Directional;
            previewLight.intensity = 3f;
            previewLight.color = Color.white;
            previewLight.shadows = LightShadows.None;
            previewLight.renderingLayerMask = -1;
            previewLight.cullingMask = 1 << previewLayer;
        }

        private void CreateTutorialHud()
        {
            if (statusText == null) return;
            Canvas canvas = statusText.GetComponentInParent<Canvas>();
            if (canvas == null) return;

            tutorialHudRoot = new GameObject("OpeningTutorialHUD", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(Text), typeof(CanvasGroup));
            RectTransform rect = tutorialHudRoot.GetComponent<RectTransform>();
            rect.SetParent(canvas.transform, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 92f);
            rect.sizeDelta = new Vector2(300f, 42f);
            rect.SetAsLastSibling();

            tutorialText = tutorialHudRoot.GetComponent<Text>();
            tutorialText.font = statusText.font != null
                ? statusText.font
                : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            tutorialText.fontSize = 18;
            tutorialText.alignment = TextAnchor.MiddleCenter;
            tutorialText.color = new Color(0.12f, 0.12f, 0.12f, 1f);
            tutorialText.horizontalOverflow = HorizontalWrapMode.Overflow;
            tutorialText.verticalOverflow = VerticalWrapMode.Overflow;
            tutorialText.raycastTarget = false;
            tutorialCanvasGroup = tutorialHudRoot.GetComponent<CanvasGroup>();
            tutorialCanvasGroup.alpha = 0f;
            tutorialHudRoot.SetActive(false);
        }

        private void CreateContextHintHud()
        {
            if (statusText == null) return;
            Canvas canvas = statusText.GetComponentInParent<Canvas>();
            if (canvas == null) return;

            contextHintHudRoot = new GameObject("ContextHintHUD", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(Text), typeof(CanvasGroup));
            RectTransform rect = contextHintHudRoot.GetComponent<RectTransform>();
            rect.SetParent(canvas.transform, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, contextHintVerticalOffset);
            rect.sizeDelta = new Vector2(520f, 58f);
            rect.SetAsLastSibling();

            contextHintText = contextHintHudRoot.GetComponent<Text>();
            contextHintText.font = statusText.font != null
                ? statusText.font
                : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            contextHintText.fontSize = 18;
            contextHintText.alignment = TextAnchor.MiddleCenter;
            contextHintText.color = new Color(0.12f, 0.12f, 0.12f, 1f);
            contextHintText.horizontalOverflow = HorizontalWrapMode.Wrap;
            contextHintText.verticalOverflow = VerticalWrapMode.Overflow;
            contextHintText.raycastTarget = false;
            contextHintCanvasGroup = contextHintHudRoot.GetComponent<CanvasGroup>();
            contextHintCanvasGroup.alpha = 0f;
            contextHintHudRoot.SetActive(false);
        }

        /// <summary>Shows a short, non-blocking state hint above the tutorial area.</summary>
        public void ShowContextHint(string message)
        {
            if (contextHintHudRoot == null || contextHintText == null || string.IsNullOrWhiteSpace(message))
                return;
            if (contextHintRoutine != null) StopCoroutine(contextHintRoutine);
            contextHintRoutine = StartCoroutine(ShowContextHintRoutine(message));
        }

        private IEnumerator ShowContextHintRoutine(string message)
        {
            contextHintText.text = message;
            contextHintHudRoot.SetActive(true);
            yield return FadeCanvasGroup(contextHintCanvasGroup,
                contextHintCanvasGroup != null ? contextHintCanvasGroup.alpha : 0f, 1f);
            if (contextHintVisibleDuration > 0f)
                yield return new WaitForSecondsRealtime(contextHintVisibleDuration);
            yield return FadeCanvasGroup(contextHintCanvasGroup, 1f, 0f);
            contextHintHudRoot.SetActive(false);
            contextHintRoutine = null;
        }

        private void ShowMagnifierTutorial()
        {
            if (showedMagnifierTutorial) return;
            showedMagnifierTutorial = true;
            ShowTutorial("RMB look through magnifier", TutorialAction.Magnifier);
        }

        private void ShowTutorial(string message, TutorialAction requiredAction = TutorialAction.None)
        {
            if (tutorialHudRoot == null || tutorialText == null) return;
            if (tutorialRoutine != null) StopCoroutine(tutorialRoutine);
            activeTutorialAction = requiredAction;
            tutorialActionAttempted = requiredAction == TutorialAction.None;
            tutorialRoutine = StartCoroutine(ShowTutorialRoutine(message));
        }

        private void TrackTutorialActionAttempt()
        {
            if (tutorialActionAttempted || activeTutorialAction == TutorialAction.None ||
                tutorialHudRoot == null || !tutorialHudRoot.activeSelf)
                return;

            bool attempted = activeTutorialAction switch
            {
                TutorialAction.MoveOrJump =>
                    Keyboard.current.wKey.wasPressedThisFrame || Keyboard.current.aKey.wasPressedThisFrame ||
                    Keyboard.current.sKey.wasPressedThisFrame || Keyboard.current.dKey.wasPressedThisFrame ||
                    Keyboard.current.spaceKey.wasPressedThisFrame,
                TutorialAction.PickUpOrDrop =>
                    Mouse.current.leftButton.wasPressedThisFrame,
                TutorialAction.Magnifier => Mouse.current.rightButton.wasPressedThisFrame,
                TutorialAction.RotateDial => Mouse.current.leftButton.wasPressedThisFrame,
                _ => false
            };

            if (attempted) tutorialActionAttempted = true;
        }

        private IEnumerator ShowTutorialRoutine(string message)
        {
            tutorialText.text = message;
            tutorialHudRoot.SetActive(true);
            RefreshInventoryHud();
            yield return FadeTutorial(tutorialCanvasGroup != null ? tutorialCanvasGroup.alpha : 0f, 1f);
            if (tutorialVisibleDuration > 0f)
                yield return new WaitForSecondsRealtime(tutorialVisibleDuration);
            while (!tutorialActionAttempted)
                yield return null;
            yield return FadeTutorial(1f, 0f);
            tutorialHudRoot.SetActive(false);
            RefreshInventoryHud();
            activeTutorialAction = TutorialAction.None;
            tutorialRoutine = null;
        }

        private IEnumerator FadeTutorial(float from, float to)
        {
            yield return FadeCanvasGroup(tutorialCanvasGroup, from, to);
        }

        private IEnumerator FadeCanvasGroup(CanvasGroup group, float from, float to)
        {
            if (group == null) yield break;
            if (tutorialFadeDuration <= 0f)
            {
                group.alpha = to;
                yield break;
            }
            float elapsed = 0f;
            while (elapsed < tutorialFadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                group.alpha = Mathf.Lerp(from, to, elapsed / tutorialFadeDuration);
                yield return null;
            }
            group.alpha = to;
        }

        private void RefreshInventoryHud()
        {
            if (inventoryPreviewRoot == null) return;
            bool hasObject = carried != null || carriedShellKey != null;
            // The tutorial occupies the lower centre, while the carried object
            // reads as a right-hand view model. Keeping both visible makes the
            // first pickup immediately legible instead of hiding the model.
            inventoryPreviewRoot.SetActive(hasObject);
            if (!hasObject) return;
            RebuildInventoryPreview(carriedShellKey != null
                ? carriedShellKey.transform
                : carried.transform);
        }

        private void RebuildInventoryPreview(Transform source)
        {
            if (inventoryPreviewRoot == null || source == null) return;
            if (inventoryPreviewVisual != null) Destroy(inventoryPreviewVisual);

            inventoryPreviewVisual = new GameObject("Carried Object Visual");
            inventoryPreviewVisual.transform.SetParent(inventoryPreviewRoot.transform, false);
            ShellPuzzlePiece sourcePiece = source.GetComponent<ShellPuzzlePiece>();
            bool isPatternStone = sourcePiece != null && sourcePiece.PearlRenderer == null;
            inventoryPreviewVisual.transform.localRotation = Quaternion.Euler(
                isPatternStone ? inventoryPreviewStoneRotation : inventoryPreviewRotation);
            CopyVisualHierarchy(source, inventoryPreviewVisual.transform);

            Bounds bounds = default;
            bool hasBounds = false;
            foreach (Renderer renderer in inventoryPreviewVisual.GetComponentsInChildren<Renderer>(true))
            {
                if (!renderer.enabled) continue;
                if (!hasBounds) { bounds = renderer.bounds; hasBounds = true; }
                else bounds.Encapsulate(renderer.bounds);
            }
            if (!hasBounds) return;

            inventoryPreviewVisual.transform.position += inventoryPreviewRoot.transform.position - bounds.center;
            float largestDimension = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
            float scale = largestDimension > 0.0001f ? inventoryPreviewModelSize / largestDimension : 1f;
            inventoryPreviewVisual.transform.localScale = Vector3.one * scale;

            // Scaling around an imported model's root can shift its mesh. Centre
            // it again after scaling so every carried model sits consistently in
            // the lower-right preview.
            Bounds scaledBounds = default;
            bool hasScaledBounds = false;
            foreach (Renderer renderer in inventoryPreviewVisual.GetComponentsInChildren<Renderer>(true))
            {
                if (!renderer.enabled) continue;
                if (!hasScaledBounds) { scaledBounds = renderer.bounds; hasScaledBounds = true; }
                else scaledBounds.Encapsulate(renderer.bounds);
            }
            if (hasScaledBounds)
                inventoryPreviewVisual.transform.position += inventoryPreviewRoot.transform.position - scaledBounds.center;
            if (isPatternStone)
                inventoryPreviewVisual.transform.localPosition +=
                    Vector3.up * inventoryPreviewStoneVerticalOffset;
        }

        private void CopyVisualHierarchy(Transform source, Transform parent)
        {
            // DualViewMaterial creates a hidden runtime-only mesh called
            // ColorSphereRevealProxy. The normal game cameras toggle it for the
            // world-reveal effect, but a hand-held copy must never duplicate it:
            // rendering that proxy over the real mesh causes a dark, outline-like
            // slab. Inactive legacy colour proxies are likewise not presentation
            // geometry for the normal view model.
            if (source == null || !source.gameObject.activeSelf ||
                source.name == "ColorSphereRevealProxy")
                return;

            GameObject copy = new GameObject(source.name);
            copy.layer = 11;
            copy.transform.SetParent(parent, false);
            copy.transform.localPosition = source.localPosition;
            copy.transform.localRotation = source.localRotation;
            copy.transform.localScale = source.localScale;

            MeshFilter sourceFilter = source.GetComponent<MeshFilter>();
            MeshRenderer sourceRenderer = source.GetComponent<MeshRenderer>();
            if (sourceFilter != null && sourceRenderer != null)
            {
                MeshFilter filter = copy.AddComponent<MeshFilter>();
                filter.sharedMesh = sourceFilter.sharedMesh;
                MeshRenderer renderer = copy.AddComponent<MeshRenderer>();
                renderer.sharedMaterials = sourceRenderer.sharedMaterials;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                MaterialPropertyBlock properties = new MaterialPropertyBlock();
                sourceRenderer.GetPropertyBlock(properties);
                if (sourceRenderer.sharedMaterial != null &&
                    sourceRenderer.sharedMaterial.HasProperty("_LowIntensity"))
                {
                    properties.SetFloat("_LowIntensity", 1f);
                    properties.SetColor("_LowColor", new Color(0.68f, 0.79f, 0.88f, 1f));
                }
                renderer.SetPropertyBlock(properties);
            }

            foreach (Transform child in source)
                CopyVisualHierarchy(child, copy.transform);
        }

        private void OnDisable()
        {
            ClearInteractionCueVisuals();
        }

        private void OnDestroy()
        {
            if (interactionCueTextRoot != null) Destroy(interactionCueTextRoot);
            if (tutorialHudRoot != null) Destroy(tutorialHudRoot);
            if (contextHintHudRoot != null) Destroy(contextHintHudRoot);
            if (inventoryPreviewVisual != null) Destroy(inventoryPreviewVisual);
            if (inventoryPreviewRoot != null) Destroy(inventoryPreviewRoot);
            if (inventoryPreviewLight != null) Destroy(inventoryPreviewLight);
            if (viewCamera != null && inventoryPreviewCullingConfigured)
                viewCamera.cullingMask = originalViewCameraCullingMask;
            InteractionOutlineRuntime.Hide();
        }
    }
}
