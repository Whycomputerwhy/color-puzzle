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

        [Header("Look")]
        [SerializeField] private Transform cameraPivot;
        [SerializeField, Min(0f)] private float mouseSensitivity = 0.13f;
        [SerializeField, Range(1f, 89f)] private float verticalLookLimit = 85f;
        [SerializeField] private bool lockCursorOnStart = true;

        private CharacterController controller;
        private float verticalVelocity;
        private float pitch;
        private bool movementLocked;
        private bool lookLocked;

        public Transform CameraPivot => cameraPivot;
        public bool IsGrounded => controller != null && controller.isGrounded;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            if (cameraPivot == null && Camera.main != null) cameraPivot = Camera.main.transform;
        }

        private void Start()
        {
            if (lockCursorOnStart) SetCursorLocked(true);
        }

        private void Update()
        {
            if (Keyboard.current == null || Mouse.current == null) return;

            if (Keyboard.current.escapeKey.wasPressedThisFrame) SetCursorLocked(false);
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

            float control = controller.isGrounded ? 1f : airControl;
            Vector3 planar = (transform.right * input.x + transform.forward * input.y) * (moveSpeed * control);

            if (controller.isGrounded)
            {
                verticalVelocity = groundedForce;
                if (Keyboard.current.spaceKey.wasPressedThisFrame)
                    verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            }
            else
            {
                verticalVelocity += gravity * Time.deltaTime;
            }

            controller.Move((planar + Vector3.up * verticalVelocity) * Time.deltaTime);
        }

        private void ApplyGravityOnly()
        {
            if (controller.isGrounded) verticalVelocity = groundedForce;
            else verticalVelocity += gravity * Time.deltaTime;
            controller.Move(Vector3.up * verticalVelocity * Time.deltaTime);
        }

        private static void SetCursorLocked(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}
