using System;
using System.Collections.Generic;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>The shape of a tool's reach inside its cube (GL24).</summary>
    public enum ImpactShape
    {
        /// <summary>Every voxel of the cube.</summary>
        Cube,

        /// <summary>The cube without its edges and corners.</summary>
        Ball,
    }

    /// <summary>Which ground a tool reaches its whole cube on (GL39); on the rest it hits one voxel, like the hand.</summary>
    public enum ReachOn
    {
        Anything,

        /// <summary>Soft ground: grass, dirt, clay (the shovel).</summary>
        Soft,

        /// <summary>Hard ground: stone, ore (the pickaxe).</summary>
        Hard,

        /// <summary>Plants: wood and leaves (the hatchet, ML1).</summary>
        Wood,
    }

    /// <summary>Exact hits a tool takes on a material (or a whole category of them); 0: can't break it.</summary>
    [Serializable]
    public struct MaterialHits
    {
        public VoxelMaterial Material;

        [Tooltip("Every material of the Material's category (e.g. all ore), not just its break chain.")]
        public bool WholeCategory;

        [Min(0)]
        public int Hits;
    }

    /// <summary>
    /// An item the player works the ground with (TL1, GL1): the hand, the pickaxe, later the
    /// shovel, trowel, rake and the rest of TL2. A tool reaches a cube of
    /// <see cref="ImpactSize"/> voxels around the one it's aimed at, whole or rounded into a ball
    /// (<see cref="ImpactShape"/>, GL24), and each hit adds its
    /// <see cref="Power"/> to the damage of every solid corner of those voxels; a corner
    /// breaks once that reaches its material's hardness (<see cref="ToolStrike"/>).
    /// </summary>
    /// <remarks>
    /// The reach is in voxels, not metres, so it follows the voxel size (M25). First pass:
    /// one power for every material. How well each tool suits each kind of material (the
    /// rake only on soft ground, the pickaxe best on rock) is TL4.
    /// </remarks>
    [CreateAssetMenu(fileName = "Tool", menuName = "Clube/Tool")]
    public class ToolDefinition : ItemDefinition
    {
        [Tooltip("Voxels per side of the cube it reaches, around the aimed voxel: 1 for the hand, 4 for the pickaxe.")]
        [SerializeField, Range(1, 5)]
        private int impactSize = 1;

        [Tooltip("Cube: every voxel of the cube. Ball: all but the cube's edges and corners (a 4-voxel cube keeps 32).")]
        [SerializeField]
        private ImpactShape impactShape = ImpactShape.Cube;

        [Tooltip("The ground its whole reach applies to (GL39). On other ground it hits only the voxel aimed at, like the hand.")]
        [SerializeField]
        private ReachOn wideOn = ReachOn.Anything;

        [Tooltip("Damage each hit adds to every corner it reaches, in hardness units (dirt = 1).")]
        [SerializeField, Min(0.01f)]
        private float power = 1f;

        [Tooltip("Hits it takes before it breaks (GL37), each swing that lands counting one. 0: never wears out.")]
        [SerializeField, Min(0)]
        private int maxHits;

        [Tooltip("Hits per second while the use button is held.")]
        [SerializeField, Range(0.5f, 10f)]
        private float hitsPerSecond = 2f;

        [Tooltip("Exact hits it takes to remove a material whole (ML1 tuning, 2026-10-13), overriding power against hardness. " +
                 "An entry covers its material's break chain (stone, cracked, loose), or with Whole Category every material of that category (e.g. all ore). 0 hits: can't break it.")]
        [SerializeField]
        private List<MaterialHits> hitsOn = new List<MaterialHits>();

        [Tooltip("A chisel (GL87): its hits square off the voxels it reaches, and cut away ones already square, instead of breaking ground.")]
        [SerializeField]
        private bool chisels;

        [Tooltip("Hits it takes to remove any material no entry above covers. 0: power against hardness, as before. Worktops (stumps) are never covered by this.")]
        [SerializeField, Min(0)]
        private int hitsOnAnything;

        [Tooltip("Its tier (GL145): what it can work. 0 bare hands, 1 hand axe, 2 stone tools, 3 bronze. A material needing a higher tier gives it nothing.")]
        [SerializeField, Range(0, 5)]
        private int tier = 1;

        [Tooltip("How much of a full hit it takes, 0-1 (GL146: a hand axe takes everything, slowly). Scales what the gradual sphere takes.")]
        [SerializeField, Range(0.05f, 1f)]
        private float efficiency = 1f;

        [Tooltip("Sphere modes: each hit takes only from the ground sample under the dot, like the bare hand (GL155: the hand axe), not a sphere.")]
        [SerializeField]
        private bool aimedOnly;

        /// <summary>Makes a tool in code (tests); the game's tools are assets.</summary>
        public static ToolDefinition Create(
            string id, string displayName, int impactSize, float power, float hitsPerSecond = 2f, ImpactShape shape = ImpactShape.Cube)
        {
            ToolDefinition tool = Create<ToolDefinition>(id, displayName, 1);
            tool.impactSize = Mathf.Clamp(impactSize, 1, 5);
            tool.impactShape = shape;
            tool.power = Mathf.Max(0.01f, power);
            tool.hitsPerSecond = hitsPerSecond;
            return tool;
        }

        /// <summary>Voxels per side of the cube it reaches.</summary>
        public int ImpactSize => impactSize;

        public ImpactShape ImpactShape => impactShape;

        /// <summary>The ground its whole reach applies to.</summary>
        public ReachOn WideOn => wideOn;

        /// <summary>True when a hit on this material (null: unknown, counted as hard) uses the tool's whole reach (GL39).</summary>
        public bool ReachesWideOn(VoxelMaterial aimed)
        {
            switch (wideOn)
            {
                case ReachOn.Soft:
                    return aimed != null && aimed.IsSoft;
                case ReachOn.Hard:
                    return aimed == null || (!aimed.IsSoft && aimed.Category != VoxelMaterialCategory.Plant);
                case ReachOn.Wood:
                    return aimed != null && aimed.Category == VoxelMaterialCategory.Plant;
                default:
                    return true;
            }
        }

        /// <summary>Limits the whole reach to one kind of ground, for tools made in code (tests, tools).</summary>
        /// <summary>Its tier (GL145): a material needing a higher one gives it nothing.</summary>
        public int Tier => tier;

        /// <summary>How much of a full hit it takes, 0-1 (GL146).</summary>
        public float Efficiency => efficiency;

        /// <summary>True when a sphere-mode hit takes only the ground sample under the dot (GL155).</summary>
        public bool AimedOnly => aimedOnly;

        /// <summary>True when it can work the material at all (GL145): its tier is high enough.</summary>
        public bool CanWork(VoxelMaterial material)
        {
            return material == null || material.ToolTier <= tier;
        }

        /// <summary>Sets its tier and efficiency (GL145, GL146), for tools made in code (tests).</summary>
        public ToolDefinition WithTier(int value, float hitEfficiency = 1f)
        {
            tier = Mathf.Clamp(value, 0, 5);
            efficiency = Mathf.Clamp(hitEfficiency, 0.05f, 1f);
            return this;
        }

        public ToolDefinition WithWideOn(ReachOn ground)
        {
            wideOn = ground;
            return this;
        }

        public float Power => power;

        public float HitsPerSecond => hitsPerSecond;

        /// <summary>True for a chisel (GL87): hits go through <see cref="ChiselStrike"/> rather than <see cref="ToolStrike"/>.</summary>
        public bool Chisels => chisels;

        /// <summary>
        /// The exact hits this tool takes to remove <paramref name="material"/> whole, when a rule sets
        /// them (an entry for it or its break chain, then one for its category, then
        /// <c>hitsOnAnything</c>): true, with 0 hits meaning it can't break it. False: no rule, so
        /// power against hardness decides, a stage at a time.
        /// </summary>
        public bool HitsFor(VoxelMaterial material, out int hits)
        {
            hits = 0;
            if (material == null)
            {
                return false;
            }
            foreach (MaterialHits rule in hitsOn)
            {
                if (!rule.WholeCategory && rule.Material != null && SameChain(rule.Material, material))
                {
                    hits = rule.Hits;
                    return true;
                }
            }
            foreach (MaterialHits rule in hitsOn)
            {
                if (rule.WholeCategory && rule.Material != null && rule.Material.Category == material.Category)
                {
                    hits = rule.Hits;
                    return true;
                }
            }
            if (hitsOnAnything > 0 && !material.IsWorktop)
            {
                hits = hitsOnAnything;
                return true;
            }
            return false;
        }

        /// <summary>Sets exact hits for a material (and its break chain), for tools made in code (tests).</summary>
        public ToolDefinition WithHits(VoxelMaterial material, int hits, bool wholeCategory = false)
        {
            hitsOn.Add(new MaterialHits { Material = material, Hits = Mathf.Max(0, hits), WholeCategory = wholeCategory });
            return this;
        }

        /// <summary>Sets the hits for every material no rule covers, for tools made in code (tests).</summary>
        public ToolDefinition WithHitsOnAnything(int hits)
        {
            hitsOnAnything = Mathf.Max(0, hits);
            return this;
        }

        // True when one material is the other or follows it in its break chain (stone → cracked → loose).
        private static bool SameChain(VoxelMaterial rule, VoxelMaterial material)
        {
            for (VoxelMaterial stage = rule; stage != null; stage = stage.BreaksInto)
            {
                if (stage == material)
                {
                    return true;
                }
                if (stage.BreaksInto == rule)
                {
                    break;
                }
            }
            return false;
        }

        /// <summary>Hits it takes before it breaks, or 0 when it never wears out.</summary>
        public int MaxHits => maxHits;

        /// <summary>Sets how many hits it lasts, for tools made in code (tests, tools).</summary>
        public ToolDefinition WithDurability(int hits)
        {
            maxHits = Mathf.Max(0, hits);
            return this;
        }

        /// <summary>The voxels it reaches when aimed at <paramref name="voxel"/>.</summary>
        public VoxelReach ImpactAround(Vector3Int voxel)
        {
            return VoxelReach.Around(voxel, impactSize, impactShape == ImpactShape.Ball);
        }
    }
}
