using UnityEngine;
using UnityEngine.InputSystem;

namespace Clube.Debug
{
    /// <summary>
    /// Debug free-fly camera for lab scenes. Hold the right mouse button to look,
    /// WASD to move, Q/E to move down/up, Shift to move faster, and scroll to
    /// change the base speed. Reads devices directly rather than through input
    /// actions, since it is a lab tool and never ships in the Game scene.
    /// </summary>
    public class FreeFlyCamera : MonoBehaviour
    {
        private const float MaxPitch = 89f;

        [Tooltip("Base movement speed in world units per second.")]
        [SerializeField, Min(0.01f)]
        private float moveSpeed = 3f;

        [Tooltip("Speed multiplier while Shift is held.")]
        [SerializeField, Min(1f)]
        private float fastMultiplier = 4f;

        [Tooltip("Degrees of rotation per pixel of mouse movement.")]
        [SerializeField, Min(0f)]
        private float lookSensitivity = 0.15f;

        [Tooltip("Fraction of the base speed added or removed per scroll notch.")]
        [SerializeField, Range(0f, 1f)]
        private float scrollSpeedStep = 0.1f;

        private float yaw;
        private float pitch;

        private void OnEnable()
        {
            Vector3 euler = transform.eulerAngles;
            yaw = euler.y;
            pitch = Mathf.DeltaAngle(0f, euler.x);
        }

        private void OnDisable()
        {
            SetCursorLocked(false);
        }

        private void Update()
        {
            Mouse mouse = Mouse.current;
            Keyboard keyboard = Keyboard.current;
            if (mouse == null || keyboard == null)
            {
                return;
            }

            bool isLooking = mouse.rightButton.isPressed;
            SetCursorLocked(isLooking);
            if (isLooking)
            {
                Look(mouse.delta.ReadValue());
            }

            AdjustSpeed(mouse.scroll.ReadValue().y);
            Move(ReadMoveInput(keyboard), keyboard.shiftKey.isPressed);
        }

        private void Look(Vector2 mouseDelta)
        {
            yaw += mouseDelta.x * lookSensitivity;
            pitch = Mathf.Clamp(pitch - mouseDelta.y * lookSensitivity, -MaxPitch, MaxPitch);
            transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
        }

        // Scroll magnitude per notch differs between platforms, so only the direction is used.
        private void AdjustSpeed(float scroll)
        {
            if (scroll != 0f)
            {
                moveSpeed = Mathf.Max(0.01f, moveSpeed * (1f + Mathf.Sign(scroll) * scrollSpeedStep));
            }
        }

        /// <summary>Returns local-space input: x = right, y = world up, z = forward.</summary>
        private static Vector3 ReadMoveInput(Keyboard keyboard)
        {
            return new Vector3(
                Axis(keyboard.dKey.isPressed, keyboard.aKey.isPressed),
                Axis(keyboard.eKey.isPressed, keyboard.qKey.isPressed),
                Axis(keyboard.wKey.isPressed, keyboard.sKey.isPressed));
        }

        private void Move(Vector3 input, bool isFast)
        {
            if (input == Vector3.zero)
            {
                return;
            }

            // Vertical movement follows world up so Q/E behave the same at any pitch.
            Vector3 direction = transform.right * input.x + Vector3.up * input.y + transform.forward * input.z;
            float speed = moveSpeed * (isFast ? fastMultiplier : 1f);

            // Unscaled time keeps the camera usable while the game is paused (timeScale = 0).
            transform.position += direction.normalized * (speed * Time.unscaledDeltaTime);
        }

        private static float Axis(bool positive, bool negative)
        {
            return (positive ? 1f : 0f) - (negative ? 1f : 0f);
        }

        private static void SetCursorLocked(bool isLocked)
        {
            CursorLockMode mode = isLocked ? CursorLockMode.Locked : CursorLockMode.None;
            if (Cursor.lockState != mode)
            {
                Cursor.lockState = mode;
                Cursor.visible = !isLocked;
            }
        }
    }
}
