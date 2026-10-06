using System;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// Which material generated ground is made of, by depth below the surface (M10): a top
    /// layer (grass), a band under it (dirt), then the base (stone) all the way down. Held
    /// in <see cref="TerrainSettings"/>; depths are in metres, like the terrain itself.
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
