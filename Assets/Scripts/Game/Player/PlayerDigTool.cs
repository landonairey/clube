using Clube.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Clube.Game
{
    /// <summary>
    /// Digging and placing for the player (M8): Attack digs and Place adds a sphere of
    /// terrain where the view's centre meets the surface, within reach, repeating while
    /// held. While a tool is held (<see cref="PlayerToolUser"/>), Attack hits with the
    /// tool instead and only Place uses the brush. Previous and Next shrink and grow the
    /// brush, whose radius is a player setting (A3). Edits go through
    /// <see cref="WorldView.ApplyBrush"/>, the world's single edit path (A7, M5). Draws a
    /// crosshair.
    /// </summary>
    /// <remarks>
    /// Placing never fills the player's own capsule, so the player can't bury itself.
    /// Only works while the <see cref="PlayerController"/> is enabled (not before spawning)
    /// and its cursor is locked, so clicks on a menu or the lab panel never dig.
    /// </remarks>
    [RequireComponent(typeof(PlayerController))]
    public class PlayerDigTool : MonoBehaviour
    {
        private const float RadiusStep = 0.5f;
        private const float CrosshairSize = 8f;

        [SerializeField]
        private PlayerSettings settings;

        [SerializeField]
        private WorldView worldView;

        [SerializeField]
        private InputActionReference digAction;

        [SerializeField]
        private InputActionReference placeAction;

        [SerializeField]
        private InputActionReference smallerAction;

        [SerializeField]
        private InputActionReference largerAction;

        [Tooltip("Material that placing puts down (M10), e.g. dirt.")]
        [SerializeField]
        private VoxelMaterial placeMaterial;

        [Tooltip("How far from the head the player can dig or place, in metres.")]
        [SerializeField, Min(0f)]
        private float reach = 6f;

        [Tooltip("Applications per second while the button is held.")]
        [SerializeField, Range(1f, 30f)]
        private float repeatRate = 5f;

        private PlayerController player;
        private BoulderBashing bashing;
        private ToolCursor cursor;
        private Texture2D dot;
        private PlayerParcels parcels;
        private PlayerToolUser tools;
        private float nextApplyTime;

        /// <summary>Brush radius in metres (the player setting).</summary>
        public float Radius => settings != null ? settings.BrushRadius : 1.5f;

        /// <summary>True when the view's centre meets the surface within reach this frame.</summary>
        public bool HasTarget { get; private set; }

        /// <summary>Where the brush would be applied, in world space; valid while <see cref="HasTarget"/>.</summary>
        public Vector3 Target { get; private set; }

        /// <summary>True when a place at <see cref="Target"/> would reach into the player, so it is refused.</summary>
        public bool PlaceBlocked => HasTarget && Overlaps(player.Body, Target, Radius);

        /// <summary>The operation held down this frame, if any: what a preview should show.</summary>
        public BrushOperation? HeldOperation { get; private set; }

        /// <summary>True while the player has a <see cref="PlayerToolUser"/> (the hand counts as a tool): Attack then hits with the tool, and only placing uses the brush.</summary>
        public bool ToolHeld => tools != null && tools.isActiveAndEnabled;

        private void Awake()
        {
            player = GetComponent<PlayerController>();
            bashing = GetComponent<BoulderBashing>();
            cursor = GetComponent<ToolCursor>();
            parcels = GetComponent<PlayerParcels>();
            tools = GetComponent<PlayerToolUser>();
        }

        private void OnEnable()
        {
            digAction?.action.Enable();
            placeAction?.action.Enable();
            smallerAction?.action.Enable();
            largerAction?.action.Enable();
        }

        private void Update()
        {
            HasTarget = false;
            HeldOperation = null;
            if (!player.enabled || player.IsCursorFree || worldView == null || !worldView.IsReady)
            {
                return;
            }

            if (settings != null)
            {
                if (WasPressed(smallerAction))
                {
                    settings.BrushRadius -= RadiusStep;
                }
                if (WasPressed(largerAction))
                {
                    settings.BrushRadius += RadiusStep;
                }
            }

            FindTarget();
            bool digging = !ToolHeld;
            // Place bashes a stone or ore on a boulder instead, when it can (GL146, GL148).
            bool placing = bashing == null || !bashing.CanBash;
            HeldOperation = digging && IsHeld(digAction) ? BrushOperation.Remove : placing && IsHeld(placeAction) ? BrushOperation.Add : (BrushOperation?)null;

            if (digging && ShouldApply(digAction))
            {
                Apply(BrushOperation.Remove);
            }
            else if (placing && ShouldApply(placeAction))
            {
                Apply(BrushOperation.Add);
            }
        }

        // On the press, then repeatedly while held.
        private bool ShouldApply(InputActionReference reference)
        {
            if (reference == null)
            {
                return false;
            }
            InputAction action = reference.action;
            if (action.WasPressedThisFrame() || (action.IsPressed() && Time.time >= nextApplyTime))
            {
                nextApplyTime = Time.time + 1f / repeatRate;
                return true;
            }
            return false;
        }

        private void FindTarget()
        {
            Transform head = player.Head != null ? player.Head : transform;
            HasTarget = worldView.Raycast(new Ray(head.position, head.forward), out Vector3 point)
                && (point - head.position).sqrMagnitude <= reach * reach;
            Target = point;
        }

        private void Apply(BrushOperation operation)
        {
            if (!HasTarget || (operation == BrushOperation.Add && PlaceBlocked) || (parcels != null && !parcels.CanEdit(Target)))
            {
                return;
            }
            byte material = placeMaterial != null ? placeMaterial.Id : (byte)0;
            worldView.ApplyBrush(Target, new BrushSettings(Radius, material: material), operation);
        }

        private static bool IsHeld(InputActionReference reference)
        {
            return reference != null && reference.action.IsPressed();
        }

        /// <summary>Whether a sphere reaches into the capsule.</summary>
        private static bool Overlaps(CharacterController body, Vector3 centre, float radius)
        {
            Transform t = body.transform;
            float half = Mathf.Max(0f, body.height * 0.5f - body.radius);
            Vector3 middle = t.TransformPoint(body.center);
            Vector3 bottom = middle - t.up * half;
            Vector3 top = middle + t.up * half;
            Vector3 segment = top - bottom;
            float along = Mathf.Clamp01(Vector3.Dot(centre - bottom, segment) / Mathf.Max(segment.sqrMagnitude, 1e-6f));
            Vector3 closest = bottom + segment * along;
            float reachOut = radius + body.radius + body.skinWidth;
            return (centre - closest).sqrMagnitude < reachOut * reachOut;
        }

        // An 8-pixel round dot: white, with a soft dark rim so it reads on bright ground.
        private static Texture2D MakeDot()
        {
            const int size = 8;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, name = "Dot cursor" };
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = new Vector2(x + 0.5f - size * 0.5f, y + 0.5f - size * 0.5f).magnitude;
                    Color color = distance < 2.2f ? Color.white : new Color(0f, 0f, 0f, 0.6f);
                    color.a *= Mathf.Clamp01(size * 0.5f - distance);
                    texture.SetPixel(x, y, color);
                }
            }
            texture.Apply();
            return texture;
        }

        private static bool WasPressed(InputActionReference reference)
        {
            return reference != null && reference.action.WasPressedThisFrame();
        }

        private void OnGUI()
        {
            if (!player.enabled || player.IsCursorFree)
            {
                return;
            }
            var centre = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            if (cursor != null && cursor.WantsDot)
            {
                // The collecting test's dot cursor (GL95): a small round dot with a dark rim.
                dot ??= MakeDot();
                GUI.DrawTexture(new Rect(centre.x - 4f, centre.y - 4f, 8f, 8f), dot);
            }
            else
            {
                GUI.DrawTexture(new Rect(centre.x - CrosshairSize, centre.y - 1f, CrosshairSize * 2f, 2f), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(centre.x - 1f, centre.y - CrosshairSize, 2f, CrosshairSize * 2f), Texture2D.whiteTexture);
            }
            if (!ToolHeld)
            {
                GUI.Label(new Rect(centre.x + 12f, centre.y + 12f, 160f, 20f), $"Brush {Radius:0.0} m");
            }
        }
    }
}
