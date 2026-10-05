using UnityEngine;
using UnityEngine.InputSystem;

namespace Clube.Game
{
    /// <summary>
    /// First-person walking (M6): Move walks, Sprint runs, Jump jumps, Look turns the body
    /// left and right and tilts the head up and down. A <see cref="CharacterController"/>
    /// collides with the terrain's chunk colliders (<see cref="Clube.Core.ChunkCollider"/>);
    /// gravity is applied here. Locks the cursor while enabled.
    /// </summary>
    /// <remarks>
    /// The transform is at the player's feet. <see cref="PlayerSpawn"/> keeps it disabled
    /// until there is ground to stand on.
    /// </remarks>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        private const float MaxPitch = 89f;

        // Keeps the controller pressed onto the ground, so it stays grounded walking downhill.
        private const float GroundedFallSpeed = 2f;

        [SerializeField]
        private PlayerSettings settings;

        [Tooltip("The head: tilts up and down, and carries the camera.")]
        [SerializeField]
        private Transform head;

        [SerializeField]
        private InputActionReference moveAction;

        [SerializeField]
        private InputActionReference lookAction;

        [SerializeField]
        private InputActionReference jumpAction;

        [SerializeField]
        private InputActionReference sprintAction;

        [Tooltip("Walking speed in metres per second.")]
        [SerializeField, Min(0f)]
        private float walkSpeed = 4f;

        [Tooltip("Speed multiplier while Sprint is held.")]
        [SerializeField, Min(1f)]
        private float sprintMultiplier = 1.75f;

        [Tooltip("Height of a jump in metres.")]
        [SerializeField, Min(0f)]
        private float jumpHeight = 1.2f;

        [Tooltip("Downward acceleration in metres per second squared.")]
        [SerializeField, Min(0f)]
        private float gravity = 20f;

        private CharacterController body;
        private float yaw;
        private float pitch;
        private float fallSpeed;

        /// <summary>The capsule the player collides with.</summary>
        public CharacterController Body => body != null ? body : body = GetComponent<CharacterController>();

        /// <summary>Where the player looks from.</summary>
        public Transform Head => head;

        public bool IsGrounded => Body.isGrounded;

        /// <summary>Moves the player without colliding on the way, and stops any fall.</summary>
        public void Teleport(Vector3 position)
        {
            // A CharacterController overwrites a transform move made while it is enabled.
            Body.enabled = false;
            transform.position = position;
            Body.enabled = true;
            fallSpeed = 0f;
        }

        private void OnEnable()
        {
            yaw = transform.eulerAngles.y;
            pitch = head != null ? Mathf.DeltaAngle(0f, head.localEulerAngles.x) : 0f;
            fallSpeed = 0f;
            Enable(moveAction);
            Enable(lookAction);
            Enable(jumpAction);
            Enable(sprintAction);
            SetCursorLocked(true);
        }

        private void OnDisable()
        {
            SetCursorLocked(false);
        }

        private void Update()
        {
            Look(Read<Vector2>(lookAction));
            Move(Read<Vector2>(moveAction));
        }

        private void Look(Vector2 delta)
        {
            float sensitivity = settings != null ? settings.LookSensitivity : 0.1f;
            yaw += delta.x * sensitivity;
            pitch = Mathf.Clamp(pitch - delta.y * sensitivity, -MaxPitch, MaxPitch);
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            if (head != null)
            {
                head.localRotation = Quaternion.Euler(pitch, 0f, 0f);
            }
        }

        private void Move(Vector2 input)
        {
            bool isSprinting = sprintAction != null && sprintAction.action.IsPressed();
            float speed = walkSpeed * (isSprinting ? sprintMultiplier : 1f);
            Vector3 walk = (transform.right * input.x + transform.forward * input.y) * speed;
            if (walk.sqrMagnitude > speed * speed)
            {
                walk = walk.normalized * speed;
            }

            if (Body.isGrounded)
            {
                fallSpeed = GroundedFallSpeed;
                if (jumpAction != null && jumpAction.action.WasPressedThisFrame())
                {
                    fallSpeed = -Mathf.Sqrt(2f * gravity * jumpHeight);
                }
            }
            else
            {
                fallSpeed += gravity * Time.deltaTime;
            }

            CollisionFlags hits = Body.Move((walk + Vector3.down * fallSpeed) * Time.deltaTime);

            // Bumping a ceiling ends the rise.
            if ((hits & CollisionFlags.Above) != 0 && fallSpeed < 0f)
            {
                fallSpeed = 0f;
            }
        }

        private static T Read<T>(InputActionReference reference) where T : struct
        {
            return reference != null ? reference.action.ReadValue<T>() : default;
        }

        private static void Enable(InputActionReference reference)
        {
            reference?.action.Enable();
        }

        private static void SetCursorLocked(bool isLocked)
        {
            Cursor.lockState = isLocked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !isLocked;
        }
    }
}
