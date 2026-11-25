using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// Climbing aboard from the water (backlog 20261124 #16: "When swimming up to a Boat press Space to grab onto the hull
    /// and begin climbing up"). While the player swims within <see cref="Reach"/> of a <see cref="Rideable"/>'s deck box
    /// (a vessel's hull collider) whose top is no more than <see cref="HighestClimb"/> above the feet, it shows
    /// "Space  Climb aboard"; Space then holds the player (<see cref="PlayerController.Held"/>) and moves it up the side
    /// and over onto the deck in <see cref="ClimbSeconds"/>. The path is kept in the deck's own frame, so the climber
    /// hangs on the side as the vessel moves, rocks and turns; on top, <see cref="PlatformRider"/> carries it.
    /// </summary>
    [DefaultExecutionOrder(-10)]
    [RequireComponent(typeof(PlayerController))]
    public sealed class PlayerBoarding : MonoBehaviour, IGuiDrawer
    {
        /// <summary>Metres from the body's side to a hull it can grab.</summary>
        public const float Reach = 1f;

        /// <summary>The highest deck, in metres above the feet, it climbs onto.</summary>
        public const float HighestClimb = 4f;

        /// <summary>How long the climb takes, in seconds.</summary>
        public const float ClimbSeconds = 1.1f;

        // How far inside the deck's edge the climb sets the feet down, and the share of the climb spent going up the side.
        private const float Inset = 0.45f;
        private const float RiseShare = 0.6f;
        private const float LipClearance = 0.12f;

        private readonly Collider[] nearby = new Collider[16];
        private PlayerController player;
        private GUIStyle promptStyle;

        // The climb, in the deck's own frame: feet from the water, over the lip, onto the deck.
        private Transform deck;
        private Vector3 startFeet, lipFeet, endFeet;
        private float progress;
        private float lastYaw;

        /// <summary>The hull the player could climb now; null when there is none in reach or it isn't swimming.</summary>
        public BoxCollider Hull { get; private set; }

        /// <summary>True while climbing aboard.</summary>
        public bool IsClimbing => deck != null;

        private void Awake()
        {
            player = GetComponent<PlayerController>();
        }

        private void OnEnable()
        {
            GuiHost.Add(this);
        }

        private void OnDisable()
        {
            GuiHost.Remove(this);
            if (deck != null)
            {
                End();
            }
            Hull = null;
        }

        private void Update()
        {
            if (IsClimbing)
            {
                return;
            }
            Hull = player.enabled && player.IsSwimming && !player.IsCursorFree ? FindHull() : null;
            if (Hull != null && player.JumpPressedThisFrame)
            {
                Climb(Hull);
            }
        }

        /// <summary>Starts climbing onto <paramref name="hull"/>'s top from where the player is (no reach check: callers and tests).</summary>
        public void Climb(BoxCollider hull)
        {
            if (hull == null || IsClimbing)
            {
                return;
            }
            deck = hull.transform;
            Vector3 center = hull.center, half = hull.size * 0.5f;
            float top = center.y + half.y;
            startFeet = deck.InverseTransformPoint(Feet());
            lipFeet = new Vector3(startFeet.x, top + LipClearance, startFeet.z);
            float insetX = Mathf.Max(half.x - Inset, 0f), insetZ = Mathf.Max(half.z - Inset, 0f);
            endFeet = new Vector3(
                Mathf.Clamp(startFeet.x, center.x - insetX, center.x + insetX),
                top,
                Mathf.Clamp(startFeet.z, center.z - insetZ, center.z + insetZ));
            progress = 0f;
            lastYaw = deck.eulerAngles.y;
            player.Held = true;
            Hull = null;
        }

        // After vessels have moved for the frame (they float in Update), and before PlatformRider.
        private void LateUpdate()
        {
            if (deck == null)
            {
                return;
            }
            if (!deck.gameObject.activeInHierarchy)
            {
                End();
                return;
            }
            progress = Mathf.Min(1f, progress + Time.deltaTime / ClimbSeconds);
            Vector3 feet = progress < RiseShare
                ? Vector3.Lerp(startFeet, lipFeet, Mathf.SmoothStep(0f, 1f, progress / RiseShare))
                : Vector3.Lerp(lipFeet, endFeet, Mathf.SmoothStep(0f, 1f, (progress - RiseShare) / (1f - RiseShare)));
            player.Teleport(deck.TransformPoint(feet) + Vector3.up * RootAboveFeet());
            float yaw = deck.eulerAngles.y;
            float turn = Mathf.DeltaAngle(lastYaw, yaw);
            if (Mathf.Abs(turn) > 1e-4f && Mathf.Abs(turn) < 45f)
            {
                player.TurnBy(turn);
            }
            lastYaw = yaw;
            if (progress >= 1f)
            {
                End();
            }
        }

        private void End()
        {
            deck = null;
            player.Held = false;
        }

        // The nearest rideable deck box in reach, low enough to climb.
        private BoxCollider FindHull()
        {
            CharacterController body = player.Body;
            Vector3 middle = transform.TransformPoint(body.center);
            Vector3 feet = Feet();
            int count = Physics.OverlapSphereNonAlloc(middle, body.radius + Reach, nearby, ~0, QueryTriggerInteraction.Ignore);
            BoxCollider best = null;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                // The deck box sits on the same object as its Rideable (a vessel's hull); crates and crew aboard don't count.
                if (!(nearby[i] is BoxCollider box) || !box.TryGetComponent(out Rideable _))
                {
                    continue;
                }
                float rise = box.transform.TransformPoint(box.center + Vector3.up * box.size.y * 0.5f).y - feet.y;
                if (rise <= 0f || rise > HighestClimb)
                {
                    continue;
                }
                float distance = (box.ClosestPoint(middle) - middle).sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = box;
                }
            }
            return best;
        }

        private Vector3 Feet()
        {
            return transform.position - Vector3.up * RootAboveFeet();
        }

        // The player's root above the bottom of its capsule.
        private float RootAboveFeet()
        {
            CharacterController body = player.Body;
            return body.height * 0.5f - body.center.y;
        }

        int IGuiDrawer.GuiDepth => 0;

        void IGuiDrawer.DrawGui()
        {
            if (Hull == null || IsClimbing || !player.enabled || player.IsCursorFree)
            {
                return;
            }
            GameUi.Apply();
            promptStyle ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = GameUi.Font(16) };
            GameUi.OutlinedLabel(new Rect(0f, Screen.height * 0.5f + 52f, Screen.width, 28f), "Space  Climb aboard", promptStyle);
        }
    }
}
