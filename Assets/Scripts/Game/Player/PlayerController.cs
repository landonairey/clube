using UnityEngine;
using UnityEngine.InputSystem;

namespace Clube.Game
{
    /// <summary>
    /// First-person walking (M6): Move walks, Sprint runs, Jump jumps (keeping its run-up:
    /// in the air Move only steers a little), Look turns the body left and right and tilts
    /// the head up and down. A <see cref="CharacterController"/>
    /// collides with the terrain's chunk colliders (<see cref="Clube.Core.ChunkCollider"/>);
    /// gravity is applied here. Locks the cursor while enabled; FreeCursor (Left Alt)
    /// frees it to use menus or the lab panel, pausing look until it is pressed again.
    /// </summary>
    /// <remarks>
    /// The transform is at the player's feet. <see cref="PlayerSpawn"/> keeps it disabled
    /// until there is ground to stand on.
    /// </remarks>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        public const float MinGravityMultiplier = 0.1f;
        public const float MaxGravityMultiplier = 4f;
        public const float MinJumpHeight = 0.2f;
        public const float MaxJumpHeight = 4f;

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

        [Tooltip("Frees the cursor and pauses looking; pressing it again locks the cursor and resumes.")]
        [SerializeField]
        private InputActionReference freeCursorAction;

        [Tooltip("Walking speed in metres per second.")]
        [SerializeField, Min(0f)]
        private float walkSpeed = 4f;

        [Tooltip("Speed multiplier while Sprint is held.")]
        [SerializeField, Min(1f)]
        private float sprintMultiplier = 1.75f;

        [Tooltip("Height of a jump in metres.")]
        [SerializeField, Range(MinJumpHeight, MaxJumpHeight)]
        private float jumpHeight = 1.2f;

        [Tooltip("Downward acceleration in metres per second squared.")]
        [SerializeField, Min(0.1f)]
        private float gravity = 20f;

        [Tooltip("How fast Move can change the velocity while in the air, in metres per second squared. Low, so a jump keeps its run-up: at 4, turning a sprint around takes about 3.5 s.")]
        [SerializeField, Min(0f)]
        private float airAcceleration = 4f;

        private CharacterController body;
        private float yaw;
        private float pitch;
        private float fallSpeed;
        private Vector3 velocity;
        private bool isCursorFree;

        // Lab tuning (M16); not saved, so the game always runs on the gravity set above.
        private float gravityMultiplier = 1f;

        /// <summary>The capsule the player collides with.</summary>
        public CharacterController Body => body != null ? body : body = GetComponent<CharacterController>();

        /// <summary>Where the player looks from.</summary>
        public Transform Head => head;

        public bool IsGrounded => Body.isGrounded;

        /// <summary>Height of a jump in metres; the same at any gravity.</summary>
        public float JumpHeight
        {
            get => jumpHeight;
            set => jumpHeight = Mathf.Clamp(value, MinJumpHeight, MaxJumpHeight);
        }

        /// <summary>
        /// Scales gravity for tuning how jumps and falls feel (M16), from the lab panel. Jumps keep
        /// their height, so a stronger pull makes them quicker and snappier. Resets each Play session.
        /// </summary>
        public float GravityMultiplier
        {
            get => gravityMultiplier;
            set => gravityMultiplier = Mathf.Clamp(value, MinGravityMultiplier, MaxGravityMultiplier);
        }

        /// <summary>The downward acceleration in use, in metres per second squared.</summary>
        public float Gravity => gravity * gravityMultiplier;

        /// <summary>Seconds a jump spends in the air, landing at the height it left from.</summary>
        public float JumpAirtime => 2f * Mathf.Sqrt(2f * jumpHeight / Gravity);

        /// <summary>
        /// True while the cursor is free for menus or the lab panel: looking pauses, and tools
        /// like <see cref="PlayerDigTool"/> ignore clicks. Walking still works.
        /// </summary>
        public bool IsCursorFree
        {
            get => isCursorFree;
            set
            {
                isCursorFree = value;
                if (isActiveAndEnabled)
                {
                    SetCursorLocked(!value);
                }
            }
        }

        /// <summary>Moves the player without colliding on the way, and stops any fall or momentum.</summary>
        public void Teleport(Vector3 position)
        {
            // A CharacterController overwrites a transform move made while it is enabled.
            Body.enabled = false;
            transform.position = position;
            Body.enabled = true;
            fallSpeed = 0f;
            velocity = Vector3.zero;
        }

        private void OnEnable()
        {
            yaw = transform.eulerAngles.y;
            pitch = head != null ? Mathf.DeltaAngle(0f, head.localEulerAngles.x) : 0f;
            fallSpeed = 0f;
            velocity = Vector3.zero;
            Enable(moveAction);
            Enable(lookAction);
            Enable(jumpAction);
            Enable(sprintAction);
            Enable(freeCursorAction);
            SetCursorLocked(!isCursorFree);
        }

        private void OnDisable()
        {
            SetCursorLocked(false);
        }

        private void Update()
        {
            if (freeCursorAction != null && freeCursorAction.action.WasPressedThisFrame())
            {
                IsCursorFree = !IsCursorFree;
            }
            if (!isCursorFree)
            {
                Look(Read<Vector2>(lookAction));
            }
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
                // On the ground the player goes where Move points, at once.
                velocity = walk;
                fallSpeed = GroundedFallSpeed;
                if (jumpAction != null && jumpAction.action.WasPressedThisFrame())
                {
                    fallSpeed = -Mathf.Sqrt(2f * Gravity * jumpHeight);
                }
            }
            else
            {
                // In the air the run-up carries on; Move only nudges it, so there's no
                // turning around on a dime. Letting go keeps the momentum.
                if (walk != Vector3.zero)
                {
                    velocity = Vector3.MoveTowards(velocity, walk, airAcceleration * Time.deltaTime);
                }
                fallSpeed += Gravity * Time.deltaTime;
            }

            CollisionFlags hits = Body.Move((velocity + Vector3.down * fallSpeed) * Time.deltaTime);

            // Running into a wall in the air stops the part of the momentum going into it.
            if (!Body.isGrounded && (hits & CollisionFlags.Sides) != 0)
            {
                Vector3 moved = Body.velocity;
                velocity = new Vector3(moved.x, 0f, moved.z);
            }

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
