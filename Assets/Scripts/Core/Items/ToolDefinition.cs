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
            bool soft = aimed != null && aimed.IsSoft;
            return wideOn == ReachOn.Anything || (wideOn == ReachOn.Soft) == soft;
        }

        /// <summary>Limits the whole reach to one kind of ground, for tools made in code (tests, tools).</summary>
        public ToolDefinition WithWideOn(ReachOn ground)
        {
            wideOn = ground;
            return this;
        }

        public float Power => power;

        public float HitsPerSecond => hitsPerSecond;

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
