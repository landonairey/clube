using System;
using System.Collections.Generic;
using Clube.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Clube.Game
{
    /// <summary>
    /// The player working the ground with a tool (GL1, GL2, GL5): aims at the voxel under the
    /// view's centre, finds the voxels the held tool reaches and their solid corners, and hits
    /// those on Use, at the tool's rate while held. Rock cracks, loosens and is collected (<see cref="ToolStrike"/>);
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

        [Tooltip("Hard materials break in stages, solid → cracked → loose (GL3). Off: each comes off whole when broken (the 2026-10-13 hit-count playtest).")]
        [SerializeField]
        private bool breakInStages;

        [Tooltip("How far from the head a tool reaches, in metres.")]
        [SerializeField, Min(0f)]
        private float reach = 4f;

        [Tooltip("The collecting test (GL96): how a hit takes the ground, and the sphere modes' sizes and amounts.")]
        [SerializeField]
        private CollectingSettings collecting = new CollectingSettings();

        private readonly List<Vector3Int> targets = new List<Vector3Int>();
        private readonly Dictionary<VoxelMaterial, float> owed = new Dictionary<VoxelMaterial, float>();
        private readonly List<ItemDefinition> collected = new List<ItemDefinition>();
        private readonly List<MinedSample> minedFrom = new List<MinedSample>();
        private PlayerController player;
        private PlayerInteractor interactor;
        private PlayerParcels parcels;
        private OreKnowledge knowledge;
        private float nextHitTime;
        private string brokenName;
        private float brokenUntil;

        /// <summary>Raised for each item a hit collects, one at a time, with its contents (mixed ore, GL32).</summary>
        public event Action<ItemStack> Collected;

        /// <summary>Raised for each sample a hit breaks and collects, with its material (GL47: experience).</summary>
        public event Action<VoxelMaterial> Mined;

        /// <summary>
        /// Raised for each sample a hit breaks and collects, with its material, where it was (world
        /// position) and what it held (GL73: the mining journal's depth and grade).
        /// </summary>
        public event Action<VoxelMaterial, Vector3, Composition> Unearthed;

        /// <summary>Raised when a hit lands, with what it did.</summary>
        public event Action<StrikeResult> Struck;

        /// <summary>Raised after a chisel hit (GL87), with the voxels it reached and what it did.</summary>
        public event Action<VoxelReach, ChiselResult> Chiselled;

        /// <summary>
        /// The tool in hand: the selected hotbar slot's, or the hand. Null with neither, and while
        /// a placeable item is selected (the hands are carrying it, GL23).
        /// </summary>
        public ToolDefinition Current => hotbar == null ? hand
            : hotbar.SelectedStack.Item is ToolDefinition tool ? tool
            : hotbar.SelectedStack.Item is PlaceableDefinition ? null
            : hand;

        /// <summary>How hits take the ground (GL96); the lab panel switches it in play.</summary>
        public CollectingSettings Collecting => collecting;

        /// <summary>True when this frame's reach is an area-of-impact sphere (GL96) rather than voxels.</summary>
        public bool ReachIsSphere { get; private set; }

        /// <summary>The sphere's centre (world view space) and radius in metres; valid while <see cref="ReachIsSphere"/>.</summary>
        public Vector3 ImpactCentre { get; private set; }

        public float ImpactRadius { get; private set; }

        /// <summary>Whether a voxel is in this frame's reach: inside the voxel reach, or its middle inside the sphere.</summary>
        public bool Reaches(Vector3Int voxel)
        {
            if (!ReachIsSphere)
            {
                return Reach.Contains(voxel);
            }
            float size = worldView.World.Grid.VoxelSize;
            return (((Vector3)voxel + Vector3.one * 0.5f) * size - ImpactCentre).sqrMagnitude <= ImpactRadius * ImpactRadius;
        }

        /// <summary>True when the held tool is aimed at ground within reach this frame.</summary>
        public bool HasReach { get; private set; }

        /// <summary>The voxels the held tool reaches this frame (global voxel indices); valid while <see cref="HasReach"/>.</summary>
        public VoxelReach Reach { get; private set; }

        /// <summary>The samples the held tool would hit this frame, as global samples of <see cref="World"/>.</summary>
        public IReadOnlyList<Vector3Int> Targets => targets;

        /// <summary>Damage carried by half-broken samples.</summary>
        public StrikeDamage Damage { get; } = new StrikeDamage();

        /// <summary>The world being worked on, or null before it exists.</summary>
        public WorldView WorldView => worldView;

        private void Awake()
        {
            player = GetComponent<PlayerController>();
            interactor = GetComponent<PlayerInteractor>();
            parcels = GetComponent<PlayerParcels>();
            knowledge = GetComponent<OreKnowledge>();
        }

        private void OnEnable()
        {
            useAction?.action.Enable();
        }

        private void OnDisable()
        {
            targets.Clear();
            HasReach = false;
            ReachIsSphere = false;
        }

        private void Update()
        {
            targets.Clear();
            HasReach = false;
            ReachIsSphere = false;
            if (!player.enabled || player.IsCursorFree || worldView == null || !worldView.IsReady)
            {
                return;
            }

            // Looking at a station or another usable thing: Interact is for it, and the
            // ground behind it is left alone.
            if (interactor != null && interactor.Target != null)
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

            if (tool.Chisels)
            {
                AimChisel(hit);
                return;
            }
            if (collecting.UsesSphere)
            {
                AimSphere(tool, hit);
                return;
            }

            // A tool made for one kind of ground reaches wide only on it; elsewhere it hits the aimed voxel alone.
            Vector3Int aimed = ToolStrike.AimedVoxel(worldView.World, hit);
            VoxelMaterial ground = tool.WideOn == ReachOn.Anything
                ? null
                : ToolStrike.AimedMaterial(worldView.World, aimed, worldView.Config.IsoLevel, worldView.Config.Materials);
            Reach = tool.ReachesWideOn(ground) ? tool.ImpactAround(aimed) : VoxelReach.Around(aimed, 1, false);
            HasReach = true;
            ToolStrike.FindTargets(worldView.World, Reach, worldView.Config.IsoLevel, targets);
            // Locked land (GL42) is left alone: the tool digs right up to the border.
            if (parcels != null)
            {
                targets.RemoveAll(sample => !parcels.CanEditSample(sample));
            }
        }

        // The chisel reaches the one voxel on the grid the player looks into (GL92): the one just past the
        // hit point along the view, so a sharp wall picks the voxel behind it. Its targets are its solid
        // corners, for the cursor and so a hit needs ground to work on.
        private void AimChisel(WorldHit hit)
        {
            World world = worldView.World;
            Transform head = player.Head != null ? player.Head : transform;
            Vector3 along = worldView.transform.InverseTransformDirection(head.forward).normalized;
            Vector3Int voxel = world.Grid.WorldToVoxel(hit.Point + along * (world.Grid.VoxelSize * 0.05f));
            float iso = worldView.Config.IsoLevel;
            if (!ChiselStrike.CanCarve(world, voxel, iso))
            {
                voxel = ToolStrike.AimedVoxel(world, hit);
                if (!ChiselStrike.CanCarve(world, voxel, iso))
                {
                    return;
                }
            }
            Reach = VoxelReach.Around(voxel, 1, false);
            HasReach = true;
            ToolStrike.FindTargets(world, Reach, iso, targets);
            if (parcels != null)
            {
                targets.RemoveAll(sample => !parcels.CanEditSample(sample));
            }
        }

        // The collecting test's sphere (GL96): every sample with ground inside a sphere round the hit point,
        // the hand's smaller than a tool's. Reach is the box of voxels round it, for the cursor.
        private void AimSphere(ToolDefinition tool, WorldHit hit)
        {
            World world = worldView.World;
            float radius = tool == hand ? collecting.HandRadius : collecting.ToolRadius;
            ImpactCentre = hit.Point;
            ImpactRadius = radius;
            ReachIsSphere = true;
            Vector3Int min = world.Grid.WorldToVoxel(hit.Point - Vector3.one * radius);
            Vector3Int max = world.Grid.WorldToVoxel(hit.Point + Vector3.one * radius);
            Reach = new VoxelReach(new VoxelBox(min, max), false);
            HasReach = true;
            SphereStrike.FindTargets(world, hit.Point, radius, targets);
            if (parcels != null)
            {
                targets.RemoveAll(sample => !parcels.CanEditSample(sample));
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
            minedFrom.Clear();
            if (tool.Chisels)
            {
                // A chisel carves the aimed voxel out (GL87, GL92); the Flat Rock may form (GL88).
                ChiselResult chiselled = ChiselStrike.Hit(
                    worldView.World, Reach, worldView.Config.Materials, collected, worldView.Config.IsoLevel, minedFrom,
                    parcels != null ? parcels.CanEditSample : null);
                Chiselled?.Invoke(Reach, chiselled);
                Struck?.Invoke(new StrikeResult(targets.Count, chiselled.Carved, chiselled.Carved));
            }
            else if (collecting.Mode == CollectMode.SphereGradual)
            {
                // Each sample in the sphere gives up the impact amount; whole units become items (GL96).
                float amount = tool == hand ? collecting.HandAmount : collecting.ToolAmount;
                int lowered = SphereStrike.Lower(worldView.World, targets, amount, worldView.Config.Materials, owed, collected, minedFrom);
                Struck?.Invoke(new StrikeResult(targets.Count, lowered, collected.Count));
            }
            else if (collecting.Mode == CollectMode.SphereWhole)
            {
                int taken = SphereStrike.Take(worldView.World, targets, worldView.Config.Materials, collected, worldView.Config.IsoLevel, minedFrom);
                Struck?.Invoke(new StrikeResult(targets.Count, taken, taken));
            }
            else
            {
                StrikeResult result = ToolStrike.Hit(
                    worldView.World, targets, tool, worldView.Config.Materials, Damage, collected, worldView.Config.IsoLevel, minedFrom, breakInStages);
                Struck?.Invoke(result);
            }
            WearTool(tool);
            for (int i = 0; i < collected.Count; i++)
            {
                Mined?.Invoke(minedFrom[i].Material);
                Composition contents = ContentsOf(minedFrom[i]);
                Unearthed?.Invoke(minedFrom[i].Material, worldView.transform.TransformPoint(worldView.World.Grid.SampleToWorld(minedFrom[i].Sample)), contents);
                var stack = new ItemStack(collected[i], 1, contents);
                // Ore the player can't tell yet comes out as "? ore" (GL54).
                Collected?.Invoke(knowledge != null ? knowledge.Disguise(stack, minedFrom[i].Material) : stack);
            }
        }

        // Each swing wears the held tool (GL37); the hand never breaks.
        private void WearTool(ToolDefinition tool)
        {
            if (hotbar == null || tool.MaxHits <= 0 || hotbar.SelectedStack.Item != tool)
            {
                return;
            }
            if (hotbar.WearSelected(tool.MaxHits))
            {
                brokenName = tool.DisplayName;
                brokenUntil = Time.time + 3f;
            }
        }

        // What a mined sample is made of: ore from the node it came from (GL32), else nothing.
        private Composition ContentsOf(MinedSample mined)
        {
            OreField ores = worldView.Ores;
            if (ores == null || mined.Material == null)
            {
                return null;
            }
            return ores.ContentsAt(mined.Material, worldView.World.Grid.SampleToWorld(mined.Sample));
        }

        private void OnGUI()
        {
            if (Time.time < brokenUntil)
            {
                GUI.Label(new Rect(Screen.width * 0.5f - 150f, Screen.height * 0.5f + 48f, 300f, 24f), $"Your {brokenName.ToLowerInvariant()} broke!");
            }
            ToolDefinition tool = Current;
            if (tool == null || !player.enabled || player.IsCursorFree)
            {
                return;
            }
            var centre = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            string wear = tool.MaxHits > 0 && hotbar != null && hotbar.SelectedStack.Item == tool
                ? $" ({tool.MaxHits - hotbar.SelectedStack.Wear}/{tool.MaxHits})"
                : "";
            GUI.Label(new Rect(centre.x + 12f, centre.y + 12f, 220f, 20f), tool.DisplayName + wear);
        }
    }
}
