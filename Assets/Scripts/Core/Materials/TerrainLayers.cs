using System;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// Which material generated ground is made of, by depth below the surface (M10): a top
    /// layer (grass), a band under it (dirt), then the base (stone) all the way down; steep
    /// mountain slopes show the base instead (GL21), and small rocks lie scattered on the top
    /// layer (GL22). Held in <see cref="TerrainSettings"/>; depths are in metres, like the
    /// terrain itself.
    /// </summary>
    [Serializable]
    public class TerrainLayers
    {
        [Tooltip("The surface layer, e.g. grass.")]
        [SerializeField]
        private VoxelMaterial top;

        [Tooltip("Depth of the top layer below the surface, in metres. Keep it at least one voxel, or no surface vertex reaches it.")]
        [SerializeField, Min(0f)]
        private float topDepth = 1f;

        [Tooltip("The band under the top layer, e.g. dirt.")]
        [SerializeField]
        private VoxelMaterial band;

        [Tooltip("Depth below the surface where the band ends and the base starts, in metres.")]
        [SerializeField, Min(0f)]
        private float bandDepth = 4f;

        [Tooltip("Everything deeper, e.g. stone.")]
        [SerializeField]
        private VoxelMaterial baseMaterial;

        [Tooltip("Slopes at least this steep (degrees) show the base material instead of the top layers (GL21). 90 turns it off.")]
        [SerializeField, Range(10f, 90f)]
        private float steepAngle = 90f;

        [Tooltip("Height (metres) above which steep slopes turn to the base material, so only mountains do.")]
        [SerializeField]
        private float steepMinHeight = 24f;

        [Tooltip("Small rocks scattered on the top layer (GL22): clusters of 1-3 samples just above the surface. None for no rocks.")]
        [SerializeField]
        private VoxelMaterial rock;

        [Tooltip("Chance that a surface column starts a rock cluster.")]
        [SerializeField, Range(0f, 0.05f)]
        private float rockChance = 0.004f;

        [Tooltip("Clay deposits at the surface (GL34): flush with the ground, reaching a little way down, deepest at the middle. None for no clay.")]
        [SerializeField]
        private VoxelMaterial clay;

        [Tooltip("Size of the cells clay deposits are placed in, in metres: at most one deposit per cell.")]
        [SerializeField, Min(2f)]
        private float clayCellSize = 24f;

        [Tooltip("Chance a cell has a clay deposit.")]
        [SerializeField, Range(0f, 1f)]
        private float clayChance = 0.3f;

        [Tooltip("Largest radius of a deposit, in metres; each is between half this and this.")]
        [SerializeField, Min(0.5f)]
        private float clayRadius = 3f;

        [Tooltip("How far down a deposit reaches at its middle, in metres.")]
        [SerializeField, Min(0f)]
        private float clayDepth = 0.75f;

        public VoxelMaterial Top
        {
            get => top;
            set => top = value;
        }

        public float TopDepth
        {
            get => topDepth;
            set => topDepth = Mathf.Max(0f, value);
        }

        public VoxelMaterial Band
        {
            get => band;
            set => band = value;
        }

        public float BandDepth
        {
            get => bandDepth;
            set => bandDepth = Mathf.Max(0f, value);
        }

        public VoxelMaterial Base
        {
            get => baseMaterial;
            set => baseMaterial = value;
        }

        /// <summary>Slopes at least this steep, in degrees, show the base material above <see cref="SteepMinHeight"/> (GL21); 90 is off.</summary>
        public float SteepAngle
        {
            get => steepAngle;
            set => steepAngle = Mathf.Clamp(value, 10f, 90f);
        }

        public float SteepMinHeight
        {
            get => steepMinHeight;
            set => steepMinHeight = value;
        }

        /// <summary>The material of the small surface rocks (GL22), or null for none.</summary>
        public VoxelMaterial Rock
        {
            get => rock;
            set => rock = value;
        }

        /// <summary>Chance that a surface column starts a rock cluster.</summary>
        public float RockChance
        {
            get => rockChance;
            set => rockChance = Mathf.Clamp(value, 0f, 0.05f);
        }

        /// <summary>The material of the surface clay deposits (GL34), or null for none.</summary>
        public VoxelMaterial Clay
        {
            get => clay;
            set => clay = value;
        }

        public float ClayCellSize
        {
            get => Mathf.Max(2f, clayCellSize);
            set => clayCellSize = Mathf.Max(2f, value);
        }

        /// <summary>Chance a cell has a clay deposit.</summary>
        public float ClayChance
        {
            get => clayChance;
            set => clayChance = Mathf.Clamp01(value);
        }

        public float ClayRadius
        {
            get => Mathf.Max(0.5f, clayRadius);
            set => clayRadius = Mathf.Max(0.5f, value);
        }

        public float ClayDepth
        {
            get => clayDepth;
            set => clayDepth = Mathf.Max(0f, value);
        }

        /// <summary>True when no layer has a material, so generation can skip materials.</summary>
        public bool IsEmpty => top == null && band == null && baseMaterial == null;

        /// <summary>The material id at a depth below the surface (above it, the top layer's).</summary>
        /// <remarks>Generation reads the same rule as a <see cref="LayerTable"/>.</remarks>
        public byte MaterialAt(float depth)
        {
            return LayerTable.From(this).MaterialAt(depth);
        }
    }
}
