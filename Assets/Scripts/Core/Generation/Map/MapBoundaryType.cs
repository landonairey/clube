using System;
using System.Collections.Generic;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>The four sides of a bounded map (GL589), in the order their boundaries are stored.</summary>
    public enum MapSide
    {
        /// <summary>The -x side.</summary>
        West,

        /// <summary>The +x side.</summary>
        East,

        /// <summary>The -z side.</summary>
        South,

        /// <summary>The +z side.</summary>
        North,
    }

    /// <summary>
    /// One kind of map edge (GL590), as data: how the land changes as it nears the edge and what lies past it forever.
    /// A new kind is a new entry in <see cref="MapDefinition.BoundaryTypes"/>; nothing switches on the name.
    /// </summary>
    [Serializable]
    public class MapBoundaryType
    {
        [SerializeField]
        private string name = "Ocean";

        [Tooltip("The land past this edge is sea: the water stands at sea level and goes on forever.")]
        [SerializeField]
        private bool ocean;

        [Tooltip("Height the land is taken to at the edge and beyond, in metres against sea level.")]
        [SerializeField]
        private float edgeHeight = -30f;

        [Tooltip("1: the land is replaced by the edge height (a sea floor, a desert plain). 0: the edge height is added to the land (a ridge).")]
        [SerializeField, Range(0f, 1f)]
        private float flatten = 1f;

        [Tooltip("Metres inside the edge over which the land changes.")]
        [SerializeField, Min(1f)]
        private float blendWidth = 700f;

        [Tooltip("Above 1 the change gathers at the edge (a wall); below 1 it starts early.")]
        [SerializeField, Range(0.25f, 4f)]
        private float sharpness = 1f;

        [Tooltip("Metres the edge line wanders in and out.")]
        [SerializeField, Min(0f)]
        private float wobble = 200f;

        [Tooltip("Metres of rough ground added on the changed land (dunes, crags).")]
        [SerializeField, Min(0f)]
        private float roughness;

        [Tooltip("Degrees the climate warms near this edge (GL591's biomes).")]
        [SerializeField]
        private float temperatureShift;

        [Tooltip("How much wetter (or, below 0, drier) the climate is near this edge, on moisture's 0-1 scale.")]
        [SerializeField, Range(-2f, 2f)]
        private float moistureShift;

        public MapBoundaryType()
        {
        }

        public MapBoundaryType(
            string name, bool ocean, float edgeHeight, float flatten, float blendWidth, float sharpness, float wobble, float roughness,
            float temperatureShift = 0f, float moistureShift = 0f)
        {
            this.name = name;
            this.ocean = ocean;
            this.edgeHeight = edgeHeight;
            this.flatten = flatten;
            this.blendWidth = blendWidth;
            this.sharpness = sharpness;
            this.wobble = wobble;
            this.roughness = roughness;
            this.temperatureShift = temperatureShift;
            this.moistureShift = moistureShift;
        }

        public string Name => name;

        /// <summary>True when the sea lies past this edge. Every map has at least one such side (GL589).</summary>
        public bool IsOcean => ocean;

        public float EdgeHeight => edgeHeight;
        public float BlendWidth => Mathf.Max(1f, blendWidth);

        /// <summary>The entry as plain values, for the height field and its jobs.</summary>
        public BoundaryProfile ToProfile()
        {
            return new BoundaryProfile
            {
                Ocean = ocean,
                EdgeHeight = edgeHeight,
                Flatten = Mathf.Clamp01(flatten),
                BlendWidth = BlendWidth,
                Sharpness = Mathf.Clamp(sharpness, 0.25f, 4f),
                Wobble = Mathf.Max(0f, wobble),
                Roughness = Mathf.Max(0f, roughness),
                TemperatureShift = temperatureShift,
                MoistureShift = moistureShift,
            };
        }

        public const string OceanName = "Ocean";
        public const string DesertName = "Desert";
        public const string VoidCliffName = "Void cliff";
        public const string MountainRidgeName = "Mountain ridge";

        /// <summary>
        /// The four kinds the game starts with (GL590): an ocean that goes on forever, an endless sprawling desert, a
        /// cliff that drops off into a void, and a mountain ridge too tall to climb.
        /// </summary>
        public static List<MapBoundaryType> Defaults()
        {
            return new List<MapBoundaryType>
            {
                new MapBoundaryType(OceanName, true, -30f, 1f, 500f, 1f, 220f, 0f, 0f, 0.15f),
                new MapBoundaryType(DesertName, false, 6f, 1f, 800f, 1f, 150f, 5f, 16f, -1.6f),
                new MapBoundaryType(VoidCliffName, false, -600f, 1f, 30f, 1f, 45f, 0f),
                new MapBoundaryType(MountainRidgeName, false, 650f, 0f, 550f, 1.6f, 90f, 90f),
            };
        }
    }

    /// <summary>A <see cref="MapBoundaryType"/> as plain values (K35).</summary>
    public struct BoundaryProfile
    {
        public bool Ocean;
        public float EdgeHeight;
        public float Flatten;
        public float BlendWidth;
        public float Sharpness;
        public float Wobble;
        public float Roughness;
        public float TemperatureShift;
        public float MoistureShift;
    }
}
