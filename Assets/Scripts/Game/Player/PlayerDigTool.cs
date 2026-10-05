using Clube.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Clube.Game
{
    /// <summary>
    /// Digging and placing for the player (M8): Attack digs and Place adds a sphere of
    /// terrain where the view's centre meets the surface, within reach, repeating while
    /// held. Previous and Next shrink and grow the brush, whose radius is a player setting
    /// (A3). Edits go through <see cref="WorldView.ApplyBrush"/>, the world's single edit
    /// path (A7, M5). Draws a crosshair.
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

        [Tooltip("How far from the head the player can dig or place, in metres.")]
        [SerializeField, Min(0f)]
        private float reach = 6f;

        [Tooltip("Applications per second while the button is held.")]
        [SerializeField, Range(1f, 30f)]
        private float repeatRate = 5f;

        private PlayerController player;
        private float nextApplyTime;

        private float Radius => settings != null ? settings.BrushRadius : 1.5f;

        private void Awake()
        {
            player = GetComponent<PlayerController>();
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

            if (ShouldApply(digAction))
            {
                Apply(BrushOperation.Remove);
            }
            else if (ShouldApply(placeAction))
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

        private void Apply(BrushOperation operation)
        {
            Transform head = player.Head != null ? player.Head : transform;
            if (!worldView.Raycast(new Ray(head.position, head.forward), out Vector3 point)
                || (point - head.position).sqrMagnitude > reach * reach)
            {
                return;
            }
            if (operation == BrushOperation.Add && Overlaps(player.Body, point, Radius))
            {
                return;
            }
            worldView.ApplyBrush(point, new BrushSettings(Radius), operation);
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
            GUI.DrawTexture(new Rect(centre.x - CrosshairSize, centre.y - 1f, CrosshairSize * 2f, 2f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(centre.x - 1f, centre.y - CrosshairSize, 2f, CrosshairSize * 2f), Texture2D.whiteTexture);
            GUI.Label(new Rect(centre.x + 12f, centre.y + 12f, 160f, 20f), $"Brush {Radius:0.0} m");
        }
    }
}
