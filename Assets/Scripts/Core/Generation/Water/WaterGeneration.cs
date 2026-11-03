using System;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// Settings for rivers and lakes (GL307), part of <see cref="TerrainSettings"/>. <see cref="Hydrology"/> drains
    /// the heightfield over a square area round the origin: water runs down the valleys, gathers into
    /// rivers once enough land drains into it, and fills the hollows it reaches into lakes. Heightfield
    /// generators only (Landscape, the game's).
    /// </summary>
    [Serializable]
    public class WaterGeneration
    {
        [Tooltip("Rivers and lakes on (GL307).")]
        [SerializeField]
        private bool enabled = true;

        [Tooltip("Width of the square, round the origin, that has rivers and lakes, in metres. Outside it the land is dry.")]
        [SerializeField, Range(128f, 2048f)]
        private float areaSize = 640f;

        [Tooltip("Size of the cells water is routed over, in metres. Rivers are cut on a 1 m grid either way.")]
        [SerializeField, Range(2f, 8f)]
        private float flowCellSize = 4f;

        [Tooltip("Land that must drain through a place before it's a river, in square metres.")]
        [SerializeField, Range(1000f, 200000f)]
        private float riverArea = 12000f;

        [Tooltip("Width of a river where it starts and at its widest, in metres.")]
        [SerializeField]
        private Vector2 riverWidth = new Vector2(1.5f, 6f);

        [Tooltip("How deep a river cuts its bed where it starts and at its widest, in metres.")]
        [SerializeField]
        private Vector2 riverDepth = new Vector2(0.8f, 2f);

        [Tooltip("Share of a river's cut that is water.")]
        [SerializeField, Range(0.3f, 1f)]
        private float riverFill = 0.75f;

        [Tooltip("Hollows shallower than this, in metres, stay dry instead of holding a pond.")]
        [SerializeField, Range(0.1f, 3f)]
        private float lakeMinDepth = 0.6f;

        [Tooltip("Sand on beds, in metres (lake shores take three quarters of it).")]
        [SerializeField]
        private VoxelMaterial sand;

        [SerializeField, Range(0.25f, 3f)]
        private float sandDepth = 1.25f;

        [Tooltip("Clay along river banks and on deeper lake floors; its depth in metres. The material is the layers' clay.")]
        [SerializeField, Range(0.25f, 3f)]
        private float clayDepth = 1f;

        [Tooltip("Lake floors under at least this much water have clay patches, in metres.")]
        [SerializeField, Range(0.25f, 5f)]
        private float clayFloorDepth = 1f;

        [Tooltip("Metres of sandy shore round lakes.")]
        [SerializeField, Range(0f, 8f)]
        private float shoreSand = 2.5f;

        [Tooltip("Metres from a river that can be clay bank, and the share of them that is.")]
        [SerializeField, Range(0f, 8f)]
        private float bankClay = 3f;

        [SerializeField, Range(0f, 1f)]
        private float bankClayCover = 0.55f;

        public bool Enabled
        {
            get => enabled;
            set => enabled = value;
        }

        public float AreaSize
        {
            get => Mathf.Clamp(areaSize, 128f, 2048f);
            set => areaSize = Mathf.Clamp(value, 128f, 2048f);
        }

        public float FlowCellSize => Mathf.Clamp(flowCellSize, 2f, 8f);

        public float RiverArea
        {
            get => Mathf.Max(1000f, riverArea);
            set => riverArea = Mathf.Max(1000f, value);
        }

        public Vector2 RiverWidth => new Vector2(Mathf.Max(0.5f, riverWidth.x), Mathf.Max(riverWidth.x, riverWidth.y));

        public Vector2 RiverDepth => new Vector2(Mathf.Max(0.25f, riverDepth.x), Mathf.Max(riverDepth.x, riverDepth.y));

        public float RiverFill => Mathf.Clamp(riverFill, 0.3f, 1f);

        public float LakeMinDepth
        {
            get => Mathf.Max(0.1f, lakeMinDepth);
            set => lakeMinDepth = Mathf.Max(0.1f, value);
        }

        public VoxelMaterial Sand
        {
            get => sand;
            set => sand = value;
        }

        public float SandDepth => sandDepth;
        public float ClayDepth => clayDepth;
        public float ClayFloorDepth => clayFloorDepth;
        public float ShoreSand => shoreSand;
        public float BankClay => bankClay;
        public float BankClayCover => bankClayCover;

        /// <summary>A key that changes whenever anything that shapes the water does.</summary>
        public string Key => $"{areaSize}|{flowCellSize}|{riverArea}|{riverWidth}|{riverDepth}|{riverFill}|{lakeMinDepth}";
    }
}
