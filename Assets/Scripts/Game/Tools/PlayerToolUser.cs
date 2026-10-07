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
    /// Cycle steps through the tools and then to none, which hands Attack back to the
    /// <see cref="PlayerDigTool"/>'s sphere brush. Until the hotbar (GL7) exists, the tools
    /// are a fixed list here. Only works while the <see cref="PlayerController"/> is enabled
    /// and its cursor is locked.
    /// </remarks>
    [RequireComponent(typeof(PlayerController))]
    public class PlayerToolUser : MonoBehaviour
    {
        [SerializeField]
        private WorldView worldView;

        [Tooltip("Hits with the held tool (Attack).")]
        [SerializeField]
        private InputActionReference useAction;

        [Tooltip("Steps to the next tool, then to none (the brush).")]
        [SerializeField]
        private InputActionReference cycleAction;

        [Tooltip("The tools the player carries, in cycling order.")]
        [SerializeField]
        private List<ToolDefinition> tools = new List<ToolDefinition>();

        [Tooltip("Which tool is held at the start; -1 for none.")]
        [SerializeField]
        private int startTool;

        [Tooltip("How far from the head a tool reaches, in metres.")]
        [SerializeField, Min(0f)]
        private float reach = 4f;

        private readonly List<Vector3Int> targets = new List<Vector3Int>();
        private readonly List<ItemDefinition> collected = new List<ItemDefinition>();
        private PlayerController player;
        private int toolIndex;
        private float nextHitTime;

        /// <summary>Raised for each item a hit collects.</summary>
        public event Action<ItemDefinition> Collected;

        /// <summary>Raised when a hit lands, with what it did.</summary>
        public event Action<StrikeResult> Struck;

        /// <summary>The tool in hand, or null when none is (the brush digs instead).</summary>
        public ToolDefinition Current => toolIndex >= 0 && toolIndex < tools.Count ? tools[toolIndex] : null;

        /// <summary>The samples the held tool would hit this frame, as global samples of <see cref="World"/>.</summary>
        public IReadOnlyList<Vector3Int> Targets => targets;

        /// <summary>Damage carried by half-broken samples.</summary>
        public StrikeDamage Damage { get; } = new StrikeDamage();

        /// <summary>The world being worked on, or null before it exists.</summary>
        public WorldView WorldView => worldView;

        private void Awake()
        {
            player = GetComponent<PlayerController>();
            toolIndex = startTool;
        }

        private void OnEnable()
        {
            useAction?.action.Enable();
            cycleAction?.action.Enable();
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

            if (cycleAction != null && cycleAction.action.WasPressedThisFrame())
            {
                // Past the last tool comes "none" (index -1 wraps there too).
                toolIndex = toolIndex + 1 >= tools.Count ? -1 : toolIndex + 1;
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
            GUI.Label(new Rect(centre.x + 12f, centre.y + 12f, 200f, 20f), $"{tool.DisplayName} (Q to switch)");
        }
    }
}
