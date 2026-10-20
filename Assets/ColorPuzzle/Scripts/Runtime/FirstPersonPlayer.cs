using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace ColorPuzzle
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class FirstPersonPlayer : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField, Min(0f)] private float moveSpeed = 5f;
        [SerializeField, Min(0f)] private float airControl = 0.55f;
        [SerializeField, Min(0f)] private float jumpHeight = 1.35f;
        [SerializeField] private float gravity = -22f;
        [SerializeField, Min(0f)] private float groundedForce = -2f;

        [Header("Footstep Audio")]
        [SerializeField] private AudioClip[] footstepClips;
        [SerializeField, Min(0.1f)] private float footstepDistance = 2.1f;
        [SerializeField, Range(0f, 1f)] private float footstepVolume = 0.12f;

        [Header("Jump Audio")]
        [SerializeField] private bool useFootstepForJump = true;
        [SerializeField, Range(0f, 1f)] private float jumpFootstepVolume = 0.1f;

        [Header("Look")]
        [SerializeField] private Transform cameraPivot;
        [SerializeField, Min(0f)] private float mouseSensitivity = 0.13f;
        [SerializeField, Range(1f, 89f)] private float verticalLookLimit = 85f;
        [SerializeField] private bool lockCursorOnStart = true;

        [Header("View Motion")]
        [SerializeField] private bool enableViewMotion = true;
        [SerializeField, Min(0f)] private float walkBobAmplitude = 0.025f;
        [SerializeField, Min(0f)] private float walkBobFrequency = 1.8f;
        [SerializeField, Min(0f)] private float walkBobMinimumSpeed = 0.15f;
        [SerializeField, Min(0f)] private float walkBobSmoothing = 14f;
        [SerializeField, Min(0f)] private float jumpSquatDepth = 0.055f;
        [SerializeField, Min(0f)] private float jumpSquatDuration = 0.07f;
        [SerializeField, Min(0f)] private float jumpSquatRecoveryDuration = 0.12f;

        private CharacterController controller;
        private Vector3 cameraPivotRestLocalPosition;
        private float verticalVelocity;
        private float pitch;
        private float walkBobPhase;
        private float smoothedWalkBobOffset;
        private float jumpSquatOffset;
        private float jumpSquatElapsed;
        private float jumpSquatRecoveryElapsed;
        private float jumpSquatRecoveryStartOffset;
        private bool movementLocked;
        private bool lookLocked;
        private bool jumpAnticipating;
        private bool jumpSquatRecovering;
        private AudioSource footstepAudioSource;
        private float distanceSinceFootstep;
        private int previousFootstepIndex = -1;

        public Transform CameraPivot => cameraPivot;
        public bool IsGrounded => controller != null && controller.isGrounded;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            if (cameraPivot == null && Camera.main != null) cameraPivot = Camera.main.transform;
            if (cameraPivot != null) cameraPivotRestLocalPosition = cameraPivot.localPosition;
            footstepAudioSource = GetComponent<AudioSource>();
            if (footstepAudioSource == null)
                footstepAudioSource = gameObject.AddComponent<AudioSource>();
            footstepAudioSource.playOnAwake = false;
            footstepAudioSource.spatialBlend = 0f;
        }

        private void Start()
        {
            if (lockCursorOnStart) SetCursorLocked(true);
        }

        private void Update()
        {
            if (Keyboard.current == null || Mouse.current == null) return;
            if (PauseMenuController.IsPaused) return;

            if (Mouse.current.leftButton.wasPressedThisFrame &&
                Cursor.lockState != CursorLockMode.Locked &&
                (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject()))
                SetCursorLocked(true);

            if (Cursor.lockState == CursorLockMode.Locked)
            {
                if (!lookLocked) UpdateLook();
                if (!movementLocked) UpdateMovement();
                else ApplyGravityOnly();
            }
            else
            {
                ApplyGravityOnly();
            }
        }

        private void LateUpdate()
        {
            if (PauseMenuController.IsPaused) return;
            UpdateViewMotion();
        }

        private void OnDisable()
        {
            if (cameraPivot != null) cameraPivot.localPosition = cameraPivotRestLocalPosition;
            jumpAnticipating = false;
            jumpSquatRecovering = false;
            jumpSquatOffset = 0f;
            smoothedWalkBobOffset = 0f;
        }

        public void SetControlsLocked(bool locked)
        {
            movementLocked = locked;
            lookLocked = locked;
        }

        public void SetMagnifierControls(bool magnifierActive, bool allowCameraLook)
        {
            movementLocked = magnifierActive;
            lookLocked = magnifierActive && !allowCameraLook;
        }

        private void UpdateLook()
        {
            Vector2 delta = Mouse.current.delta.ReadValue() * mouseSensitivity;
            transform.Rotate(Vector3.up, delta.x, Space.World);
            pitch = Mathf.Clamp(pitch - delta.y, -verticalLookLimit, verticalLookLimit);
            cameraPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        private void UpdateMovement()
        {
            Vector2 input = Vector2.zero;
            if (Keyboard.current.wKey.isPressed) input.y += 1f;
            if (Keyboard.current.sKey.isPressed) input.y -= 1f;
            if (Keyboard.current.dKey.isPressed) input.x += 1f;
            if (Keyboard.current.aKey.isPressed) input.x -= 1f;
            input = Vector2.ClampMagnitude(input, 1f);

            bool wasGrounded = controller.isGrounded;
            float control = wasGrounded ? 1f : airControl;
            Vector3 planar = (transform.right * input.x + transform.forward * input.y) * (moveSpeed * control);

            if (controller.isGrounded)
            {
                verticalVelocity = groundedForce;
                if (Keyboard.current.spaceKey.wasPressedThisFrame && !jumpAnticipating)
                {
                    if (enableViewMotion && jumpSquatDuration > 0f)
                    {
                        jumpAnticipating = true;
                        jumpSquatRecovering = false;
                        jumpSquatElapsed = 0f;
                    }
                    else
                    {
                        verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
                        PlayJumpFootstep();
                    }
                }

                if (jumpAnticipating)
                {
                    jumpSquatElapsed += Time.deltaTime;
                    if (jumpSquatDuration <= 0f || jumpSquatElapsed >= jumpSquatDuration)
                    {
                        jumpAnticipating = false;
                        BeginJumpSquatRecovery(-jumpSquatDepth);
                        verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
                        PlayJumpFootstep();
                    }
                }
            }
            else
            {
                if (jumpAnticipating) CancelJumpAnticipation();
                verticalVelocity += gravity * Time.deltaTime;
            }

            controller.Move((planar + Vector3.up * verticalVelocity) * Time.deltaTime);
            UpdateFootsteps(planar, wasGrounded);
        }

        private void ApplyGravityOnly()
        {
            if (jumpAnticipating) CancelJumpAnticipation();
            if (controller.isGrounded) verticalVelocity = groundedForce;
            else verticalVelocity += gravity * Time.deltaTime;
            controller.Move(Vector3.up * verticalVelocity * Time.deltaTime);
            distanceSinceFootstep = 0f;
        }

        private void UpdateFootsteps(Vector3 planarVelocity, bool wasGrounded)
        {
            if (!wasGrounded || planarVelocity.sqrMagnitude < 0.01f ||
                footstepClips == null || footstepClips.Length == 0)
            {
                distanceSinceFootstep = 0f;
                return;
            }

            distanceSinceFootstep += planarVelocity.magnitude * Time.deltaTime;
            if (distanceSinceFootstep < footstepDistance) return;
            distanceSinceFootstep -= footstepDistance;

            PlayFootstep(footstepVolume);
        }

        private void PlayJumpFootstep()
        {
            if (!useFootstepForJump) return;
            distanceSinceFootstep = 0f;
            PlayFootstep(jumpFootstepVolume);
        }

        private void PlayFootstep(float volume)
        {
            if (footstepClips == null || footstepClips.Length == 0) return;

            int index = Random.Range(0, footstepClips.Length);
            if (footstepClips.Length > 1 && index == previousFootstepIndex)
                index = (index + 1) % footstepClips.Length;
            previousFootstepIndex = index;
            if (footstepAudioSource != null && footstepClips[index] != null)
                footstepAudioSource.PlayOneShot(footstepClips[index], volume);
        }

        private void UpdateViewMotion()
        {
            if (cameraPivot == null) return;
            if (!enableViewMotion)
            {
                smoothedWalkBobOffset = 0f;
                jumpSquatOffset = 0f;
                cameraPivot.localPosition = cameraPivotRestLocalPosition;
                return;
            }
            if (movementLocked)
            {
                cameraPivot.localPosition = cameraPivotRestLocalPosition +
                                            Vector3.up * (smoothedWalkBobOffset + jumpSquatOffset);
                return;
            }

            float planarSpeed = controller == null
                ? 0f
                : Vector3.ProjectOnPlane(controller.velocity, Vector3.up).magnitude;
            bool isWalking = controller != null &&
                             controller.isGrounded &&
                             planarSpeed >= walkBobMinimumSpeed;

            float targetWalkBob = 0f;
            if (isWalking)
            {
                float speedScale = moveSpeed > 0.0001f ? planarSpeed / moveSpeed : 1f;
                walkBobPhase = Mathf.Repeat(
                    walkBobPhase + Time.deltaTime * walkBobFrequency * speedScale,
                    1f);
                targetWalkBob = Mathf.Sin(walkBobPhase * Mathf.PI * 2f) * walkBobAmplitude;
            }
            else
            {
                walkBobPhase = 0f;
            }

            float bobBlend = walkBobSmoothing <= 0f
                ? 1f
                : 1f - Mathf.Exp(-walkBobSmoothing * Time.deltaTime);
            smoothedWalkBobOffset = Mathf.Lerp(
                smoothedWalkBobOffset,
                targetWalkBob,
                bobBlend);

            UpdateJumpSquatViewOffset();
            cameraPivot.localPosition = cameraPivotRestLocalPosition +
                                        Vector3.up * (smoothedWalkBobOffset + jumpSquatOffset);
        }

        private void UpdateJumpSquatViewOffset()
        {
            if (jumpAnticipating)
            {
                float progress = jumpSquatDuration <= 0f
                    ? 1f
                    : Mathf.Clamp01(jumpSquatElapsed / jumpSquatDuration);
                jumpSquatOffset = -jumpSquatDepth * Mathf.SmoothStep(0f, 1f, progress);
                return;
            }

            if (!jumpSquatRecovering)
            {
                jumpSquatOffset = 0f;
                return;
            }

            jumpSquatRecoveryElapsed += Time.deltaTime;
            float recoveryProgress = jumpSquatRecoveryDuration <= 0f
                ? 1f
                : Mathf.Clamp01(jumpSquatRecoveryElapsed / jumpSquatRecoveryDuration);
            jumpSquatOffset = Mathf.Lerp(
                jumpSquatRecoveryStartOffset,
                0f,
                Mathf.SmoothStep(0f, 1f, recoveryProgress));
            if (recoveryProgress >= 1f) jumpSquatRecovering = false;
        }

        private void BeginJumpSquatRecovery(float startOffset)
        {
            jumpSquatRecoveryStartOffset = startOffset;
            jumpSquatRecoveryElapsed = 0f;
            jumpSquatRecovering = true;
        }

        private void CancelJumpAnticipation()
        {
            jumpAnticipating = false;
            BeginJumpSquatRecovery(jumpSquatOffset);
        }

        private static void SetCursorLocked(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}
