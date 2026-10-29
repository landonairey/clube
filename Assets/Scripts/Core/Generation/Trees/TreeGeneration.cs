using System;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// Where trees grow and what they look like (GL30, first pass of PK7): at most one tree per
    /// square cell of the world, on ground flat enough, shaped by <see cref="TreeShape"/> from the
    /// seed. Part of <see cref="TerrainSettings"/>; trees need a wood and a leaves material. Two kinds
    /// grow (GL150): birch (the wood and leaves) and maple (its own), picked per tree.
    /// </summary>
    [Serializable]
    public class TreeGeneration
    {
        [Tooltip("Size of the square cells trees are placed in, in metres; at most one tree per cell.")]
        [SerializeField, Min(2f)]
        private float cellSize = 10f;

        [Tooltip("Chance that a cell has a tree (before the ground check).")]
        [SerializeField, Range(0f, 1f)]
        private float chance;

        [Tooltip("Share of the land that is forest (GL164); the rest is open field with few trees. 1: forest everywhere, every cell at Chance.")]
        [SerializeField, Range(0f, 1f)]
        private float forestCover = 1f;

        [Tooltip("Typical width of a forest or a field, in metres.")]
        [SerializeField, Range(50f, 2000f)]
        private float forestScale = 300f;

        [Tooltip("Chance that a cell out in the open field has a tree.")]
        [SerializeField, Range(0f, 1f)]
        private float fieldChance = 0.04f;

        [Tooltip("The youngest trees' size against a full-grown one (GL165): every tree is scaled by its age. 1: every tree full-grown.")]
        [SerializeField, Range(0.2f, 1f)]
        private float youngestSize = 1f;

        [Tooltip("Steepest ground a tree grows on, in degrees.")]
        [SerializeField, Range(0f, 45f)]
        private float maxSlope = 12f;

        [Tooltip("Lowest and highest full-grown birch (the first kind), in metres from the ground to the top of the trunk; age scales it down (YoungestSize).")]
        [SerializeField]
        private Vector2 height = new Vector2(7f, 10f);

        [Tooltip("A full-grown birch's trunk radius at the ground, in metres.")]
        [SerializeField, Range(0.15f, 1.5f)]
        private float trunkRadius = 0.4f;

        [Tooltip("Lowest and highest full-grown maple (GL166), in metres from the ground to the top of the trunk.")]
        [SerializeField]
        private Vector2 mapleHeight = new Vector2(7f, 10f);

        [Tooltip("A full-grown maple's trunk radius at the ground, in metres.")]
        [SerializeField, Range(0.15f, 1.5f)]
        private float mapleTrunkRadius = 0.4f;

        [Tooltip("Fewest and most large branches on the trunk.")]
        [SerializeField]
        private Vector2Int branches = new Vector2Int(3, 5);

        [Tooltip("Thinnest branch radius, in metres; under about a voxel, thin branches break into floating bits.")]
        [SerializeField, Range(0.05f, 0.5f)]
        private float minBranchRadius = 0.22f;

        [Tooltip("Radius of one ball in a leaf cluster, in metres (smallest, largest), on a tree 8.5 m tall; it grows with the tree's height.")]
        [SerializeField]
        private Vector2 leafRadius = new Vector2(0.55f, 0.9f);

        [SerializeField]
        private VoxelMaterial wood;

        [SerializeField]
        private VoxelMaterial leaves;

        [Tooltip("The second kind of tree (GL150): maple, with its own wood and leaves. None: every tree is the first kind (birch).")]
        [SerializeField]
        private VoxelMaterial mapleWood;

        [Tooltip("Maple's leaves. None: maples have the first kind's leaves.")]
        [SerializeField]
        private VoxelMaterial mapleLeaves;

        [Tooltip("Share of trees that are maple (GL150), picked per tree from the seed.")]
        [SerializeField, Range(0f, 1f)]
        private float mapleShare = 0.35f;

        [Tooltip("The tree's base, left standing when the trunk is cut: much harder than wood, and a worktop (GL36). None: the base is wood.")]
        [SerializeField]
        private VoxelMaterial stump;

        [Tooltip("A maple's base (GL160): a stump of maple wood. None: maples have the first kind's stump.")]
        [SerializeField]
        private VoxelMaterial mapleStump;

        [Tooltip("How far the stump stands above the ground, in metres: 0.75 is 3 voxels at 4 per metre.")]
        [SerializeField, Range(0.25f, 2f)]
        private float stumpHeight = 0.75f;

        public float CellSize
        {
            get => Mathf.Max(2f, cellSize);
            set => cellSize = Mathf.Max(2f, value);
        }

        public float Chance
        {
            get => chance;
            set => chance = Mathf.Clamp01(value);
        }

        /// <summary>Share of the land that is forest, 0-1 (<see cref="TreeForests"/>).</summary>
        public float ForestCover
        {
            get => forestCover;
            set => forestCover = Mathf.Clamp01(value);
        }

        /// <summary>Typical width of a forest or a field, in metres.</summary>
        public float ForestScale
        {
            get => Mathf.Max(50f, forestScale);
            set => forestScale = Mathf.Clamp(value, 50f, 2000f);
        }

        /// <summary>Chance of a tree in an open-field cell.</summary>
        public float FieldChance
        {
            get => fieldChance;
            set => fieldChance = Mathf.Clamp01(value);
        }

        /// <summary>The youngest trees' size against a full-grown one, 0.2-1.</summary>
        public float YoungestSize
        {
            get => Mathf.Clamp(youngestSize, 0.2f, 1f);
            set => youngestSize = Mathf.Clamp(value, 0.2f, 1f);
        }

        public float MaxSlope
        {
            get => maxSlope;
            set => maxSlope = Mathf.Clamp(value, 0f, 45f);
        }

        /// <summary>Lowest and highest full-grown birch trunk, in metres.</summary>
        public Vector2 Height
        {
            get => Ordered(height);
            set => height = value;
        }

        /// <summary>A full-grown birch's trunk radius at the ground.</summary>
        public float TrunkRadius
        {
            get => trunkRadius;
            set => trunkRadius = Mathf.Clamp(value, 0.15f, 1.5f);
        }

        /// <summary>Lowest and highest full-grown maple trunk, in metres.</summary>
        public Vector2 MapleHeight
        {
            get => Ordered(mapleHeight);
            set => mapleHeight = value;
        }

        /// <summary>A full-grown maple's trunk radius at the ground.</summary>
        public float MapleTrunkRadius
        {
            get => mapleTrunkRadius;
            set => mapleTrunkRadius = Mathf.Clamp(value, 0.15f, 1.5f);
        }

        /// <summary>The full-grown height range of a maple or a birch (GL166).</summary>
        public Vector2 HeightFor(bool maple)
        {
            return maple ? MapleHeight : Height;
        }

        /// <summary>The full-grown trunk radius of a maple or a birch.</summary>
        public float TrunkRadiusFor(bool maple)
        {
            return maple ? mapleTrunkRadius : trunkRadius;
        }

        /// <summary>The tallest any tree can grow, either kind.</summary>
        public float TallestHeight => MapleShare > 0f ? Mathf.Max(Height.y, MapleHeight.y) : Height.y;

        private static Vector2 Ordered(Vector2 range)
        {
            return new Vector2(Mathf.Max(1f, Mathf.Min(range.x, range.y)), Mathf.Max(1f, range.x, range.y));
        }

        /// <summary>Fewest and most large branches.</summary>
        public Vector2Int Branches
        {
            get => new Vector2Int(Mathf.Max(0, Mathf.Min(branches.x, branches.y)), Mathf.Max(0, branches.x, branches.y));
            set => branches = value;
        }

        public float MinBranchRadius
        {
            get => minBranchRadius;
            set => minBranchRadius = Mathf.Clamp(value, 0.05f, 0.5f);
        }

        /// <summary>Smallest and largest leaf ball radius, in metres.</summary>
        public Vector2 LeafRadius
        {
            get => new Vector2(Mathf.Max(0.1f, Mathf.Min(leafRadius.x, leafRadius.y)), Mathf.Max(0.1f, leafRadius.x, leafRadius.y));
            set => leafRadius = value;
        }

        public VoxelMaterial Wood
        {
            get => wood;
            set => wood = value;
        }

        public VoxelMaterial Leaves
        {
            get => leaves;
            set => leaves = value;
        }

        /// <summary>Maple's wood (GL150), or null when every tree is birch.</summary>
        public VoxelMaterial MapleWood
        {
            get => mapleWood;
            set => mapleWood = value;
        }

        /// <summary>Maple's leaves: its own, or the first kind's.</summary>
        public VoxelMaterial MapleLeaves
        {
            get => mapleLeaves != null ? mapleLeaves : leaves;
            set => mapleLeaves = value;
        }

        /// <summary>Share of trees that are maple, 0-1.</summary>
        public float MapleShare
        {
            get => mapleWood != null ? mapleShare : 0f;
            set => mapleShare = Mathf.Clamp01(value);
        }

        public float StumpHeight
        {
            get => stumpHeight;
            set => stumpHeight = Mathf.Clamp(value, 0.25f, 2f);
        }

        /// <summary>What a tree's base is made of: the stump material, or wood when none is set.</summary>
        public VoxelMaterial Stump
        {
            get => stump != null ? stump : wood;
            set => stump = value;
        }

        /// <summary>What a maple's base is made of: its own stump, or the first kind's.</summary>
        public VoxelMaterial MapleStump
        {
            get => mapleStump != null ? mapleStump : Stump;
            set => mapleStump = value;
        }

        /// <summary>True when trees can grow: some chance, and both materials set.</summary>
        public bool HasTrees => chance > 0f && wood != null && leaves != null;

        /// <summary>
        /// Furthest any part of a tree can reach sideways from its root, leaves included: a bound
        /// for gathering the trees near a chunk. Matches the limits <see cref="TreeShape"/> keeps to.
        /// </summary>
        public float MaxSpread => TallestHeight * TreeShape.MaxSpreadPerHeight
                                 + (LeafRadius.y * 2f + TreeShape.ClusterScatter) * Mathf.Max(1f, TallestHeight / TreeShape.LeafReferenceHeight);
    }
}
