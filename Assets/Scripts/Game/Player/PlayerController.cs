using UnityEngine;
using UnityEngine.InputSystem;

namespace Clube.Game
{
    /// <summary>
    /// First-person walking (M6): Move walks, Sprint runs, Crouch (C) crouches, lowering the head and slowing the
    /// walk, and pressed while sprinting slides on while held, up to a set distance (GL219, GL247); Jump jumps (keeping its run-up:
    /// in the air Move only steers a little; held against a steep face it climbs, GL467), Look turns the body left and right and tilts
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
        [Tooltip("Swimming speed (GL316), metres per second; Sprint swims faster.")]
        [SerializeField, Min(0.5f)]
        private float swimSpeed = 2.2f;

        [Tooltip("How fast Jump swims up and Crouch dives while swimming, metres per second.")]
        [SerializeField, Min(0.5f)]
        private float swimRise = 2.4f;

        [Tooltip("How quickly the swimmer's speed follows the keys: water drags.")]
        [SerializeField, Min(0.5f)]
        private float swimAcceleration = 6f;

        [Tooltip("The upward push for climbing out onto a bank: Jump held against it at the surface.")]
        [SerializeField, Min(0f)]
        private float climbOutSpeed = 5f;

        [Tooltip("Climbing speed (GL467) as a share of the walk: Jump held against a steep face (a trunk, a pit wall) climbs it.")]
        [SerializeField, Range(0.05f, 1f)]
        private float climbSpeedShare = 0.25f;

        // How far in front of the capsule a face still counts for climbing, and how hard a climber presses into it so the
        // capsule keeps touching (metres, metres a second).
        private const float ClimbReach = 0.3f;
        private const float ClimbGrip = 0.6f;
        // The hop up and forward over the top of a climb (GL467): rise in metres, forward speed as a share of the walk.
        private const float ClimbTopHop = 0.4f;
        private const float ClimbTopPush = 0.6f;

        // Swimming starts with this much of the body under water and ends below the lower share, so it doesn't flicker.
        private const float SwimStart = 0.55f;
        private const float SwimEnd = 0.35f;

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

        // A knockback (GL356: the campfire) carries the player until this time, whatever Move says.
        private float knockbackUntil;
        private bool knockbackLiftPending;
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

        private float submersion;

        /// <summary>
        /// How much of the body is under water, 0-1 (GL316), set each frame by <see cref="PlayerSwimming"/>. Swimming starts
        /// once over half is under and stops once less than a third is.
        /// </summary>
        public float Submersion
        {
            get => submersion;
            set
            {
                submersion = Mathf.Clamp01(value);
                if (submersion >= SwimStart)
                {
                    IsSwimming = true;
                }
                else if (submersion < SwimEnd)
                {
                    IsSwimming = false;
                }
            }
        }

        /// <summary>True while swimming (GL316).</summary>
        public bool IsSwimming { get; private set; }

        /// <summary>True while walking, jumping, sprinting and crouching are off, so typing in a box can't move the player (GL519).</summary>
        public bool InputLocked { get; set; }

        /// <summary>True where holding Jump against a steep face doesn't climb it (GL529: the tutorial).</summary>
        public bool ClimbingDisabled { get; set; }

        /// <summary>True while climbing a steep face (GL467).</summary>
        public bool IsClimbing { get; private set; }

        /// <summary>Climbing speed in metres a second (GL467): a share of the walk.</summary>
        public float ClimbSpeed => walkSpeed * climbSpeedShare;

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
        /// <summary>The player's sideways speed and heading, in world space.</summary>
        public Vector3 HorizontalVelocity => velocity;

        /// <summary>
        /// Throws the player back (GL356: touching the campfire): <paramref name="push"/> sideways for <paramref name="seconds"/>,
        /// whatever Move says, with a hop of <paramref name="lift"/> metres a second upward. A slide ends.
        /// </summary>
        public void Knockback(Vector3 push, float lift, float seconds)
        {
            velocity = new Vector3(push.x, 0f, push.z);
            fallSpeed = -Mathf.Abs(lift);
            slideSpeed = 0f;
            knockbackUntil = Time.time + seconds;
            // Still on the ground this frame: keep the hop instead of settling it (GL375).
            knockbackLiftPending = lift > 0f;
        }

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

        /// <summary>
        /// Turns the view towards a point (world space) a little each call, at most <paramref name="degreesPerSecond"/>
        /// (GL567: the camera swings to the crew lead). Returns true once it faces it.
        /// </summary>
        public bool TurnTowards(Vector3 point, float degreesPerSecond)
        {
            Vector3 eye = head != null ? head.position : transform.position + Vector3.up * 1.6f;
            Vector3 to = point - eye;
            if (to.sqrMagnitude < 1e-6f)
            {
                return true;
            }
            Quaternion look = Quaternion.LookRotation(to);
            float step = degreesPerSecond * Time.deltaTime;
            yaw = Mathf.MoveTowardsAngle(yaw, look.eulerAngles.y, step);
            pitch = Mathf.MoveTowards(pitch, Mathf.Clamp(Mathf.DeltaAngle(0f, look.eulerAngles.x), -MaxPitch, MaxPitch), step);
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            if (head != null)
            {
                head.localRotation = Quaternion.Euler(pitch, 0f, 0f);
            }
            return Mathf.Abs(Mathf.DeltaAngle(yaw, look.eulerAngles.y)) < 0.5f;
        }

        /// <summary>Turns the player to face a point (world space), looking level.</summary>
        public void LookTowards(Vector3 point)
        {
            Vector3 flat = Vector3.ProjectOnPlane(point - transform.position, Vector3.up);
            if (flat.sqrMagnitude < 1e-6f)
            {
                return;
            }
            yaw = Quaternion.LookRotation(flat).eulerAngles.y;
            pitch = 0f;
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            if (head != null)
            {
                head.localRotation = Quaternion.identity;
            }
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
            Move(InputLocked ? Vector2.zero : Read<Vector2>(moveAction));
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
            bool isSprinting = !InputLocked && sprintAction != null && sprintAction.action.IsPressed();
            if (IsSwimming)
            {
                Swim(input, isSprinting);
                return;
            }
            InputAction crouch = Crouch;
            bool crouchPressed = crouch != null && !isInMenu && !InputLocked && crouch.WasPressedThisFrame();
            bool crouchHeld = crouch != null && !isInMenu && !InputLocked && crouch.IsPressed();
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

            bool jumpPressed = !InputLocked && jumpAction != null && jumpAction.action.WasPressedThisFrame();
            // Holding Jump jumps again at every landing.
            bool jumpHeld = !InputLocked && jumpAction != null && jumpAction.action.IsPressed();
            if (Climb(jumpHeld && !isInMenu && !ClimbingDisabled, input))
            {
                return;
            }
            if (Body.isGrounded)
            {
                // On the ground the player goes where Move points, at once; a slide goes on where it started.
                if (Time.time >= knockbackUntil)
                {
                    velocity = walk;
                }
                if (IsSliding)
                {
                    SlideOn();
                }
                if (knockbackLiftPending)
                {
                    knockbackLiftPending = false;
                }
                else
                {
                    fallSpeed = GroundedFallSpeed;
                }
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

        // One frame of climbing (GL467), when it applies: Jump held in the air, once a jump's rise is spent, with a steep face
        // just in front climbs it at ClimbSpeed, Move's left and right going along it. The capsule moves through the
        // CharacterController, so it never passes into terrain; an overhang stops the rise. Letting go of Jump drops off;
        // past the top (no face in front any more, down to the knees) the climber hops up and onto the ledge. True when
        // this frame climbed.
        private bool Climb(bool jumpHeld, Vector2 input)
        {
            bool canStart = !Body.isGrounded && fallSpeed >= 0f && Time.time >= knockbackUntil;
            if (!jumpHeld || !(IsClimbing || canStart))
            {
                IsClimbing = false;
                return false;
            }
            if (!FindClimbFace(out Vector3 normal))
            {
                if (IsClimbing)
                {
                    // Over the top: a small hop up and forward onto the ledge.
                    IsClimbing = false;
                    fallSpeed = -Mathf.Sqrt(2f * Gravity * ClimbTopHop);
                    velocity = transform.forward * walkSpeed * ClimbTopPush;
                }
                return false;
            }
            IsClimbing = true;
            slideSpeed = 0f;
            Vector3 into = new Vector3(-normal.x, 0f, -normal.z).normalized;
            Vector3 along = Vector3.Cross(Vector3.up, into).normalized;
            float speed = ClimbSpeed;
            Vector3 move = into * ClimbGrip + along * (Vector3.Dot(transform.right, along) * input.x * speed) + Vector3.up * speed;
            velocity = Vector3.zero;
            fallSpeed = 0f;
            Body.Move(move * Time.deltaTime);
            return true;
        }

        // A face steeper than the walkable slope just in front of the capsule, at chest or knee height (GL467).
        private bool FindClimbFace(out Vector3 normal)
        {
            CharacterController capsule = Body;
            float radius = capsule.radius * 0.9f;
            Vector3 feet = transform.position + capsule.center - Vector3.up * (capsule.height * 0.5f);
            float reach = capsule.radius - radius + capsule.skinWidth + ClimbReach;
            float steepest = Mathf.Cos(capsule.slopeLimit * Mathf.Deg2Rad);
            for (int i = 0; i < 2; i++)
            {
                float height = i == 0 ? capsule.height * 0.7f : capsule.stepOffset + radius;
                Vector3 from = feet + Vector3.up * Mathf.Max(height, radius);
                if (Physics.SphereCast(from, radius, transform.forward, out RaycastHit hit, reach, ~0, QueryTriggerInteraction.Ignore)
                    && hit.normal.y < steepest && hit.normal.y > -0.5f)
                {
                    normal = hit.normal;
                    return true;
                }
            }
            normal = Vector3.zero;
            return false;
        }

        // One frame of swimming (GL316): Move swims where the player looks (looking down and forward dives), Jump swims up and
        // Crouch down, and with neither the body floats with its head at the surface. Water drags, so the speed follows the
        // keys gently. Jump held against a bank at the surface pushes up and out.
        private void Swim(Vector2 input, bool isSprinting)
        {
            slideSpeed = 0f;
            IsClimbing = false;
            UpdateCrouch(false);
            Transform look = head != null ? head : transform;
            float speed = swimSpeed * (isSprinting ? 1.4f : 1f);
            Vector3 wish = (transform.right * input.x + look.forward * input.y) * speed;
            if (wish.sqrMagnitude > speed * speed)
            {
                wish = wish.normalized * speed;
            }
            InputAction crouch = Crouch;
            bool up = jumpAction != null && !isInMenu && jumpAction.action.IsPressed();
            bool down = crouch != null && !isInMenu && crouch.IsPressed();
            if (up)
            {
                wish.y = Mathf.Max(wish.y, 0f) + swimRise;
            }
            else if (down)
            {
                wish.y = Mathf.Min(wish.y, 0f) - swimRise;
            }
            else if (input.y == 0f)
            {
                // Floats: rises while the head is under, settles with the shoulders at the surface.
                wish.y = submersion > 0.8f ? 0.9f : submersion < 0.65f ? -0.5f : 0f;
            }

            Vector3 current = velocity + Vector3.down * fallSpeed;
            current = Vector3.MoveTowards(current, wish, swimAcceleration * Time.deltaTime);
            CollisionFlags hits = Body.Move(current * Time.deltaTime);
            if (up && (hits & CollisionFlags.Sides) != 0 && submersion < 0.9f)
            {
                current.y = Mathf.Max(current.y, climbOutSpeed);
            }
            velocity = new Vector3(current.x, 0f, current.z);
            fallSpeed = -current.y;
            if (Body.isGrounded)
            {
                lastGroundedTime = Time.time;
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
