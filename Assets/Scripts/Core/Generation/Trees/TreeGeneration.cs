using System;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// Where trees grow and what they look like (GL30, first pass of PK7): at most one tree per
    /// square cell of the world, on ground flat enough, shaped by <see cref="TreeShape"/> from the
    /// seed. Part of <see cref="TerrainSettings"/>; trees need a wood and a leaves material.
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

        [Tooltip("Steepest ground a tree grows on, in degrees.")]
        [SerializeField, Range(0f, 45f)]
        private float maxSlope = 12f;

        [Tooltip("Lowest and highest tree, in metres from the ground to the top of the trunk.")]
        [SerializeField]
        private Vector2 height = new Vector2(7f, 10f);

        [Tooltip("Trunk radius at the ground, in metres.")]
        [SerializeField, Range(0.15f, 1.5f)]
        private float trunkRadius = 0.4f;

        [Tooltip("Fewest and most large branches on the trunk.")]
        [SerializeField]
        private Vector2Int branches = new Vector2Int(3, 5);

        [Tooltip("Thinnest branch radius, in metres; under about a voxel, thin branches break into floating bits.")]
        [SerializeField, Range(0.05f, 0.5f)]
        private float minBranchRadius = 0.22f;

        [Tooltip("Radius of one ball in a leaf cluster, in metres (smallest, largest).")]
        [SerializeField]
        private Vector2 leafRadius = new Vector2(0.55f, 0.9f);

        [SerializeField]
        private VoxelMaterial wood;

        [SerializeField]
        private VoxelMaterial leaves;

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

        public float MaxSlope
        {
            get => maxSlope;
            set => maxSlope = Mathf.Clamp(value, 0f, 45f);
        }

        /// <summary>Lowest and highest trunk, in metres.</summary>
        public Vector2 Height
        {
            get => new Vector2(Mathf.Max(1f, Mathf.Min(height.x, height.y)), Mathf.Max(1f, height.x, height.y));
            set => height = value;
        }

        public float TrunkRadius
        {
            get => trunkRadius;
            set => trunkRadius = Mathf.Clamp(value, 0.15f, 1.5f);
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

        /// <summary>True when trees can grow: some chance, and both materials set.</summary>
        public bool HasTrees => chance > 0f && wood != null && leaves != null;

        /// <summary>
        /// Furthest any part of a tree can reach sideways from its root, leaves included: a bound
        /// for gathering the trees near a chunk. Matches the limits <see cref="TreeShape"/> keeps to.
        /// </summary>
        public float MaxSpread => Height.y * TreeShape.MaxSpreadPerHeight + LeafRadius.y * 2f + TreeShape.ClusterScatter;
    }
}
