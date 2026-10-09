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

        [Tooltip("Share of the ore in what's mined from a node (GL32), rolled once per node between these: 0.2 is 20% ore, the rest rock.")]
        [SerializeField]
        private Vector2 grade = new Vector2(0.15f, 0.3f);

        [Tooltip("What the rest of mined ore is (GL32), e.g. stone. None: the first host.")]
        [SerializeField]
        private VoxelMaterial gangue;

        [Tooltip("Other contents of the ore (GL32), e.g. a trace of silver in copper, each rolled per node within its range.")]
        [SerializeField]
        private List<OreContentRange> extraContents = new List<OreContentRange>();

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

        /// <summary>Lowest and highest share of ore in a node (GL32), each 0-1.</summary>
        public Vector2 Grade
        {
            get => new Vector2(Mathf.Clamp01(Mathf.Min(grade.x, grade.y)), Mathf.Clamp01(Mathf.Max(grade.x, grade.y)));
            set => grade = value;
        }

        /// <summary>The rock the rest of the ore is (GL32): the gangue, or the first host, or null.</summary>
        public VoxelMaterial Gangue
        {
            get => gangue != null ? gangue : hosts.Count > 0 ? hosts[0] : null;
            set => gangue = value;
        }

        public List<OreContentRange> ExtraContents => extraContents;

        /// <summary>
        /// What ore from a node is made of (GL32), from rolls 0-1: the ore at
        /// <paramref name="gradeRoll"/> through <see cref="Grade"/>, each extra content at its roll
        /// through its range, and the <see cref="Gangue"/> for the rest.
        /// </summary>
        /// <param name="extraRoll">The roll for extra content <c>i</c>.</param>
        public Composition ContentsFor(float gradeRoll, Func<int, float> extraRoll)
        {
            var shares = new List<Content>();
            Vector2 range = Grade;
            float oreShare = Mathf.Lerp(range.x, range.y, gradeRoll);
            float left = 1f - oreShare;
            shares.Add(new Content(ore, oreShare));
            for (int i = 0; i < extraContents.Count; i++)
            {
                OreContentRange extra = extraContents[i];
                if (extra.Material == null)
                {
                    continue;
                }
                float share = Mathf.Min(left, Mathf.Lerp(extra.Min, extra.Max, extraRoll != null ? extraRoll(i) : 0.5f));
                shares.Add(new Content(extra.Material, share));
                left -= share;
            }
            shares.Add(new Content(Gangue, left));
            return Composition.From(shares);
        }

        /// <summary>A node's contents halfway through every range: for ore no node accounts for.</summary>
        public Composition TypicalContents => ContentsFor(0.5f, null);
    }

    /// <summary>Another material in an ore and its share range (GL32), e.g. 0-5% silver in copper ore.</summary>
    [Serializable]
    public struct OreContentRange
    {
        public VoxelMaterial Material;

        [Range(0f, 1f)]
        public float Min;

        [Range(0f, 1f)]
        public float Max;

        public OreContentRange(VoxelMaterial material, float min, float max)
        {
            Material = material;
            Min = min;
            Max = max;
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
