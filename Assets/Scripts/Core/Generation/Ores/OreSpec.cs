using System;
using System.Collections.Generic;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// Where one ore generates in a world (O1): how many nodes per cell, how deep, how rich at
    /// the centre and how far it spreads, and which materials it may replace. The ore itself
    /// is a <see cref="VoxelMaterial"/> in the registry (M9); its spread through a world is
    /// part of that world's <see cref="TerrainSettings"/>, so labs can tune it per world and
    /// in Play mode without touching the material asset.
    /// </summary>
    [Serializable]
    public class OreSpec
    {
        [Tooltip("The ore material (category Ore) this places.")]
        [SerializeField]
        private VoxelMaterial ore;

        [Tooltip("Average nodes per ore cell; fractions give a node in that share of cells.")]
        [SerializeField, Min(0f)]
        private float nodesPerCell = 1f;

        [Tooltip("Shallowest a node centre may be, in metres below the surface.")]
        [SerializeField]
        private float minDepth = 4f;

        [Tooltip("Deepest a node centre may be, in metres below the surface.")]
        [SerializeField]
        private float maxDepth = 24f;

        [Tooltip("Chance a host voxel at the very centre becomes ore (O4).")]
        [SerializeField, Range(0f, 1f)]
        private float peakProbability = 0.6f;

        [Tooltip("Gaussian σ along x, y and z in metres: a flat y makes a seam, one long axis a vein.")]
        [SerializeField]
        private Vector3 spread = new Vector3(1.5f, 1f, 1.5f);

        [Tooltip("Materials this ore may replace, e.g. stone. Empty: any.")]
        [SerializeField]
        private List<VoxelMaterial> hosts = new List<VoxelMaterial>();

        [Tooltip("Where nodes of two ores overlap, the higher priority wins (O5); equal priorities go to the likelier one.")]
        [SerializeField]
        private int priority;

        public VoxelMaterial Ore
        {
            get => ore;
            set => ore = value;
        }

        public float NodesPerCell
        {
            get => nodesPerCell;
            set => nodesPerCell = Mathf.Max(0f, value);
        }

        public float MinDepth
        {
            get => minDepth;
            set => minDepth = value;
        }

        public float MaxDepth
        {
            get => maxDepth;
            set => maxDepth = value;
        }

        public float PeakProbability
        {
            get => peakProbability;
            set => peakProbability = Mathf.Clamp01(value);
        }

        /// <summary>σ per axis in metres; each at least a hundredth of a metre.</summary>
        public Vector3 Spread
        {
            get => Vector3.Max(spread, Vector3.one * 0.01f);
            set => spread = value;
        }

        public List<VoxelMaterial> Hosts => hosts;

        public int Priority
        {
            get => priority;
            set => priority = value;
        }
    }

    /// <summary>
    /// A world's ore generation (3D): the cell size nodes are placed in (O2) and one
    /// <see cref="OreSpec"/> per ore. Held in <see cref="TerrainSettings"/>.
    /// </summary>
    [Serializable]
    public class OreGeneration
    {
        [Tooltip("Size of the cells ore nodes are placed in, in metres (O2). Node counts are per cell.")]
        [SerializeField, Min(1f)]
        private float cellSize = 16f;

        [SerializeField]
        private List<OreSpec> ores = new List<OreSpec>();

        public float CellSize
        {
            get => Mathf.Max(1f, cellSize);
            set => cellSize = Mathf.Max(1f, value);
        }

        public List<OreSpec> Ores => ores;

        /// <summary>True when at least one spec has an ore that can generate.</summary>
        public bool HasOres
        {
            get
            {
                foreach (OreSpec spec in ores)
                {
                    if (spec != null && spec.Ore != null && spec.NodesPerCell > 0f && spec.PeakProbability > 0f)
                    {
                        return true;
                    }
                }
                return false;
            }
        }
    }
}
