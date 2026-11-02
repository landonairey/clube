using UnityEngine;
using UnityEngine.InputSystem;

namespace Clube.Game
{
    /// <summary>
    /// First-person walking (M6): Move walks, Sprint runs, Crouch (C) crouches, lowering the head and slowing the
    /// walk, and pressed while sprinting slides on while held, up to a set distance (GL219, GL247); Jump jumps (keeping its run-up:
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

        // Seconds after leaving the ground that a jump still works.
        private const float CoyoteTime = 0.15f;

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

        [Tooltip("Crouches while held; pressed while sprinting, slides (GL219). Without one, the Crouch action in the same map as Sprint.")]
        [SerializeField]
        private InputActionReference crouchAction;

        [Tooltip("The capsule's height while crouched, as a share of standing.")]
        [SerializeField, Range(0.3f, 1f)]
        private float crouchHeight = 0.6f;

        [Tooltip("Speed multiplier while crouched.")]
        [SerializeField, Range(0.1f, 1f)]
        private float crouchSpeedMultiplier = 0.5f;

        [Tooltip("A slide starts this much faster than the sprint it came from.")]
        [SerializeField, Min(1f)]
        private float slideBoost = 1.2f;

        [Tooltip("How fast a slide slows on level ground, in metres per second squared (GL247). Downhill it slows less, or speeds up.")]
        [SerializeField, Min(0.5f)]
        private float slideDrag = 3f;

        [Tooltip("The longest a slide goes while Crouch is held, in metres (GL247).")]
        [SerializeField, Min(1f)]
        private float maxSlideDistance = 8f;

        [Tooltip("The steepest rise a slide starts up or keeps going up, in metres per metre (GL247): 0.2 is about 11 degrees.")]
        [SerializeField, Range(0f, 1f)]
        private float steepestSlideRise = 0.2f;

        // How long a slide carries on over a bump or a dip it leaves the ground on, in seconds (GL247).
        private const float SlideAirTime = 0.25f;

        // A slide never goes faster than this many times its start (downhill).
        private const float SlideTopSpeed = 1.6f;

        // How fast the capsule and head go down and up, in metres per second.
        private const float CrouchRate = 6f;

        private CharacterController body;
        private float standingHeight;
        private Vector3 standingCenter;
        private float headStandingY;
        private float currentHeight;
        private Vector3 slideDirection;
        private float slideSpeed;
        private float slideStartSpeed;
        private float slideDistance;
        private float yaw;
        private float pitch;
        private float fallSpeed;
        private float lastGroundedTime = float.NegativeInfinity;
        private Vector3 velocity;
        // Freed by FreeCursor; a menu frees it separately (isInMenu).
        private bool isCursorFree;
        private bool isInMenu;
        private int menuClosedFrame = -1;

        // Lab tuning (M16); not saved, so the game always runs on the gravity set above.
        private float gravityMultiplier = 1f;

        /// <summary>The capsule the player collides with.</summary>
        public CharacterController Body => body != null ? body : body = GetComponent<CharacterController>();

        /// <summary>Where the player looks from.</summary>
        public Transform Head => head;

        public bool IsGrounded => Body.isGrounded;

        /// <summary>True while the capsule is lower than standing: crouching or sliding (GL219).</summary>
        public bool IsCrouching => currentHeight < standingHeight - 0.01f;

        /// <summary>True while sliding out of a sprint (GL219).</summary>
        public bool IsSliding => slideSpeed > 0f;

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
        /// True while the cursor is free, for a menu (<see cref="IsInMenu"/>) or by FreeCursor
        /// for the lab panel: looking pauses, and tools like <see cref="PlayerDigTool"/> ignore
        /// clicks. Walking still works.
        /// </summary>
        public bool IsCursorFree => isInMenu || isCursorFree;

        /// <summary>
        /// True while one of the player's screens is open (inventory, a station, the merchant);
        /// they set it as they open and close. The cursor stays free until it's closed, whatever
        /// FreeCursor does.
        /// </summary>
        public bool IsInMenu
        {
            get => isInMenu;
            set
            {
                if (isInMenu && !value)
                {
                    menuClosedFrame = Time.frameCount;
                }
                isInMenu = value;
                if (isActiveAndEnabled)
                {
                    SetCursorLocked(!IsCursorFree);
                }
            }
        }

        /// <summary>
        /// True on the frame a screen closed, so the key press that closed it (E) isn't also
        /// taken by something else, like using the station still under the crosshair.
        /// </summary>
        public bool MenuClosedThisFrame => menuClosedFrame == Time.frameCount;

        /// <summary>Moves the player without colliding on the way, and stops any fall or momentum.</summary>
        public void Teleport(Vector3 position)
        {
            // A CharacterController overwrites a transform move made while it is enabled.
            Body.enabled = false;
            transform.position = position;
            Body.enabled = true;
            fallSpeed = 0f;
            velocity = Vector3.zero;
            slideSpeed = 0f;
        }

        private void Awake()
        {
            standingHeight = Body.height;
            standingCenter = Body.center;
            currentHeight = standingHeight;
            headStandingY = head != null ? head.localPosition.y : 0f;
        }

        private void OnEnable()
        {
            slideSpeed = 0f;
            yaw = transform.eulerAngles.y;
            pitch = head != null ? Mathf.DeltaAngle(0f, head.localEulerAngles.x) : 0f;
            fallSpeed = 0f;
            velocity = Vector3.zero;
            Enable(moveAction);
            Enable(lookAction);
            Enable(jumpAction);
            Enable(sprintAction);
            Enable(freeCursorAction);
            SetCursorLocked(!IsCursorFree);
        }

        private void OnDisable()
        {
            SetCursorLocked(false);
        }

        private void Update()
        {
            // While a menu is open the cursor stays free: FreeCursor can't lock it under the menu.
            if (!isInMenu && freeCursorAction != null && freeCursorAction.action.WasPressedThisFrame())
            {
                isCursorFree = !isCursorFree;
                SetCursorLocked(!isCursorFree);
            }
            if (!IsCursorFree)
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
            InputAction crouch = Crouch;
            bool crouchPressed = crouch != null && !isInMenu && crouch.WasPressedThisFrame();
            bool crouchHeld = crouch != null && !isInMenu && crouch.IsPressed();
            float speed = walkSpeed * (IsCrouching ? crouchSpeedMultiplier : isSprinting ? sprintMultiplier : 1f);
            Vector3 walk = (transform.right * input.x + transform.forward * input.y) * speed;
            if (walk.sqrMagnitude > speed * speed)
            {
                walk = walk.normalized * speed;
            }

            // Crouch pressed while sprinting slides on, faster at first (GL219), for as long as it's held up to
            // maxSlideDistance (GL247). Running downhill the controller is often a frame off the ground, so a press just
            // after leaving it counts; level ground, downhill and a gentle rise slide, a steeper rise doesn't.
            bool nearGround = Body.isGrounded || Time.time - lastGroundedTime <= CoyoteTime;
            if (crouchPressed && isSprinting && !IsSliding && nearGround && walk.sqrMagnitude > walkSpeed * walkSpeed
                && RiseAlong(walk.normalized) <= steepestSlideRise)
            {
                slideDirection = walk.normalized;
                slideSpeed = walk.magnitude * slideBoost;
                slideStartSpeed = slideSpeed;
                slideDistance = 0f;
            }
            if (IsSliding && !crouchHeld)
            {
                // Letting go of Crouch ends the slide.
                slideSpeed = 0f;
            }
            UpdateCrouch(crouchHeld || IsSliding);

            bool jumpPressed = jumpAction != null && jumpAction.action.WasPressedThisFrame();
            // Holding Jump jumps again at every landing.
            bool jumpHeld = jumpAction != null && jumpAction.action.IsPressed();
            if (Body.isGrounded)
            {
                // On the ground the player goes where Move points, at once; a slide goes on where it started.
                velocity = walk;
                if (IsSliding)
                {
                    SlideOn();
                }
                fallSpeed = GroundedFallSpeed;
                lastGroundedTime = Time.time;
                if (jumpHeld)
                {
                    // A jump out of a slide keeps its speed.
                    slideSpeed = 0f;
                    Jump();
                }
            }
            else
            {
                // Over a bump or a dip the slide carries on a moment (GL247); off a ledge it ends, its speed carrying on as a run-up.
                if (IsSliding && Time.time - lastGroundedTime <= SlideAirTime)
                {
                    velocity = slideDirection * slideSpeed;
                    slideDistance += slideSpeed * Time.deltaTime;
                }
                else
                {
                    slideSpeed = 0f;
                }
                // Running downhill the controller is often a frame off the ground; a jump
                // pressed just after leaving it still counts (and only on the way down or level).
                if (jumpPressed && fallSpeed >= 0f && Time.time - lastGroundedTime <= CoyoteTime)
                {
                    Jump();
                }
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

        // One frame of a slide on the ground (GL247): drag slows it, the slope speeds it downhill and slows it uphill; it
        // ends when it's down to a crouch, has gone its full distance, or meets a rise too steep.
        private void SlideOn()
        {
            float rise = RiseAlong(slideDirection);
            float slowing = slideDrag + Gravity * rise;
            slideSpeed = Mathf.Min(slideStartSpeed * SlideTopSpeed, slideSpeed - slowing * Time.deltaTime);
            velocity = slideDirection * Mathf.Max(0f, slideSpeed);
            slideDistance += Mathf.Max(0f, slideSpeed) * Time.deltaTime;
            if (slideSpeed <= walkSpeed * crouchSpeedMultiplier || slideDistance >= maxSlideDistance || rise > steepestSlideRise)
            {
                slideSpeed = 0f;
            }
        }

        // How steeply the ground under the feet rises going this way, in metres per metre: negative downhill, 0 with no
        // ground found.
        private float RiseAlong(Vector3 direction)
        {
            Vector3 from = transform.position + Vector3.up * (Body.radius + 0.05f);
            if (!Physics.SphereCast(from, Body.radius * 0.9f, Vector3.down, out RaycastHit hit, Body.radius + 0.6f, ~0, QueryTriggerInteraction.Ignore)
                || hit.normal.y < 0.2f)
            {
                return 0f;
            }
            Vector3 n = hit.normal;
            return -(n.x * direction.x + n.z * direction.z) / n.y;
        }

        // The Crouch action: the one set, or the one beside Sprint in the input asset.
        private InputAction Crouch
        {
            get
            {
                if (crouchAction != null)
                {
                    return crouchAction.action;
                }
                InputAction found = sprintAction != null && sprintAction.action.actionMap != null ? sprintAction.action.actionMap.FindAction("Crouch") : null;
                found?.Enable();
                return found;
            }
        }

        // Lowers the capsule and the head towards crouching, or raises them back when there's room overhead (GL219).
        // The feet stay where they are.
        private void UpdateCrouch(bool wantsCrouch)
        {
            float target = wantsCrouch ? standingHeight * crouchHeight : standingHeight;
            if (target > currentHeight && !RoomToStand(target))
            {
                return;
            }
            float height = Mathf.MoveTowards(currentHeight, target, CrouchRate * Time.deltaTime);
            if (Mathf.Approximately(height, currentHeight))
            {
                return;
            }
            currentHeight = height;
            float drop = standingHeight - currentHeight;
            Body.height = currentHeight;
            Body.center = new Vector3(standingCenter.x, standingCenter.y - drop * 0.5f, standingCenter.z);
            if (head != null)
            {
                Vector3 local = head.localPosition;
                head.localPosition = new Vector3(local.x, headStandingY - drop, local.z);
            }
        }

        // Whether the capsule can grow to a height without its top running into anything.
        private bool RoomToStand(float height)
        {
            float radius = Body.radius * 0.95f;
            Vector3 top = transform.position + Vector3.up * (currentHeight - Body.radius);
            return !Physics.SphereCast(top, radius, Vector3.up, out _, height - currentHeight + Body.skinWidth, ~0, QueryTriggerInteraction.Ignore);
        }

        private void Jump()
        {
            fallSpeed = -Mathf.Sqrt(2f * Gravity * jumpHeight);
            lastGroundedTime = float.NegativeInfinity;
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
