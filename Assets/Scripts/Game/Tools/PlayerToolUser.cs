using System;
using System.Collections.Generic;
using Clube.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Clube.Game
{
    /// <summary>
    /// The player working the ground with a tool (GL1, GL2, GL5): aims at the sample under the
    /// view's centre, finds the samples the held tool reaches, and hits them on Use, at the
    /// tool's rate while held. Rock cracks, loosens and is collected (<see cref="ToolStrike"/>);
    /// each collected item is reported through <see cref="Collected"/>.
    /// </summary>
    /// <remarks>
    /// The tool is whatever the <see cref="Hotbar"/>'s selected slot holds (GL7), or the bare
    /// <see cref="hand"/> when that isn't a tool. Only works while the
    /// <see cref="PlayerController"/> is enabled and its cursor is locked.
    /// </remarks>
    [RequireComponent(typeof(PlayerController))]
    public class PlayerToolUser : MonoBehaviour
    {
        [SerializeField]
        private WorldView worldView;

        [Tooltip("Hits with the held tool (Attack).")]
        [SerializeField]
        private InputActionReference useAction;

        [Tooltip("Picks the held item; without one, the hand is always held.")]
        [SerializeField]
        private Hotbar hotbar;

        [Tooltip("Used when the selected hotbar slot holds no tool: bare hands.")]
        [SerializeField]
        private ToolDefinition hand;

        [Tooltip("How far from the head a tool reaches, in metres.")]
        [SerializeField, Min(0f)]
        private float reach = 4f;

        private readonly List<Vector3Int> targets = new List<Vector3Int>();
        private readonly List<ItemDefinition> collected = new List<ItemDefinition>();
        private PlayerController player;
        private float nextHitTime;

        /// <summary>Raised for each item a hit collects.</summary>
        public event Action<ItemDefinition> Collected;

        /// <summary>Raised when a hit lands, with what it did.</summary>
        public event Action<StrikeResult> Struck;

        /// <summary>The tool in hand: the selected hotbar slot's, or the hand. Null only with neither.</summary>
        public ToolDefinition Current => hotbar != null && hotbar.SelectedStack.Item is ToolDefinition tool ? tool : hand;

        /// <summary>The samples the held tool would hit this frame, as global samples of <see cref="World"/>.</summary>
        public IReadOnlyList<Vector3Int> Targets => targets;

        /// <summary>Damage carried by half-broken samples.</summary>
        public StrikeDamage Damage { get; } = new StrikeDamage();

        /// <summary>The world being worked on, or null before it exists.</summary>
        public WorldView WorldView => worldView;

        private void Awake()
        {
            player = GetComponent<PlayerController>();
        }

        private void OnEnable()
        {
            useAction?.action.Enable();
        }

        private void OnDisable()
        {
            targets.Clear();
        }

        private void Update()
        {
            targets.Clear();
            if (!player.enabled || player.IsCursorFree || worldView == null || !worldView.IsReady)
            {
                return;
            }

            ToolDefinition tool = Current;
            if (tool == null)
            {
                return;
            }

            FindTargets(tool);
            if (ShouldHit(tool))
            {
                Hit(tool);
            }
        }

        private void FindTargets(ToolDefinition tool)
        {
            Transform head = player.Head != null ? player.Head : transform;
            if (!worldView.Raycast(new Ray(head.position, head.forward), out WorldHit hit)
                || (worldView.transform.TransformPoint(hit.Point) - head.position).sqrMagnitude > reach * reach)
            {
                return;
            }

            float iso = worldView.Config.IsoLevel;
            if (ToolStrike.TryAim(worldView.World, hit, iso, out Vector3Int aimed))
            {
                ToolStrike.FindTargets(worldView.World, aimed, tool.Impact, iso, targets);
            }
        }

        // On the press, then at the tool's rate while held.
        private bool ShouldHit(ToolDefinition tool)
        {
            if (useAction == null || targets.Count == 0)
            {
                return false;
            }
            InputAction action = useAction.action;
            if (action.WasPressedThisFrame() || (action.IsPressed() && Time.time >= nextHitTime))
            {
                nextHitTime = Time.time + 1f / tool.HitsPerSecond;
                return true;
            }
            return false;
        }

        private void Hit(ToolDefinition tool)
        {
            collected.Clear();
            StrikeResult result = ToolStrike.Hit(worldView.World, targets, tool, worldView.Config.Materials, Damage, collected);
            Struck?.Invoke(result);
            foreach (ItemDefinition item in collected)
            {
                Collected?.Invoke(item);
            }
        }

        private void OnGUI()
        {
            ToolDefinition tool = Current;
            if (tool == null || !player.enabled || player.IsCursorFree)
            {
                return;
            }
            var centre = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            GUI.Label(new Rect(centre.x + 12f, centre.y + 12f, 200f, 20f), tool.DisplayName);
        }
    }
}
