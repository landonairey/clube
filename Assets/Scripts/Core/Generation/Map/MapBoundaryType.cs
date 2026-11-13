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
    /// One kind of map edge (GL590), as data: how the land changes as it nears the edge, what lies past it forever,
    /// where the player is turned back and what they are told there (GL640), and the colour of its horizon (GL641).
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

        [Tooltip("Height the land is taken to at the edge and beyond, in metres: against the sea for an ocean's floor, against the plains there otherwise.")]
        [SerializeField]
        private float edgeHeight = -30f;

        [Tooltip("1: the land is replaced by the edge height (a desert plain, the void). 0: the edge height is added to the land (a range).")]
        [SerializeField, Range(0f, 1f)]
        private float flatten = 1f;

        [Tooltip("Metres inside the edge over which the land changes. For an ocean: the nearest the coast comes to the edge.")]
        [SerializeField, Min(1f)]
        private float blendWidth = 700f;

        [Tooltip("Metres past the edge by which the change is complete; 0 completes it at the edge. A range's wall rises on past the edge, so the ground the player reaches stays low.")]
        [SerializeField, Min(0f)]
        private float blendPast;

        [Tooltip("Above 1 the change gathers at the edge (a wall); below 1 it starts early.")]
        [SerializeField, Range(0.25f, 4f)]
        private float sharpness = 1f;

        [Tooltip("Metres this kind's own line wanders in and out, on top of the edge's.")]
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

        [Tooltip("How often the seed picks this kind for a side that isn't sea, against the other kinds (GL639). 0: only set by hand.")]
        [SerializeField, Min(0f)]
        private float weight = 1f;

        [Tooltip("A mountain range crosses the map to this side and goes on past it into the larger system (GL639).")]
        [SerializeField]
        private bool range;

        [Tooltip("Metres past the edge where the player is stopped (GL640); below 0 stops them inside it (before a cliff's lip).")]
        [SerializeField]
        private float limitDistance = 150f;

        [Tooltip("Metres before the stop over which the player slows and the warning shows.")]
        [SerializeField, Min(1f)]
        private float limitSoftness = 120f;

        [Tooltip("What the player is told as the limit nears (GL640).")]
        [SerializeField]
        private string limitMessage = "";

        [Tooltip("Colour of the land far past this edge, as the far terrain draws it (GL641).")]
        [SerializeField]
        private Color horizonColor = new Color(0.45f, 0.45f, 0.24f);

        [Tooltip("How much of the far land past this edge takes the horizon colour (0: the far terrain's own grass and rock).")]
        [SerializeField, Range(0f, 1f)]
        private float horizonTint;

        public MapBoundaryType()
        {
        }

        public MapBoundaryType(
            string name, bool ocean, float edgeHeight, float flatten, float blendWidth, float sharpness, float wobble, float roughness,
            float temperatureShift = 0f, float moistureShift = 0f, float blendPast = 0f, float weight = 1f, bool range = false,
            float limitDistance = 150f, float limitSoftness = 120f, string limitMessage = "", Color horizonColor = default,
            float horizonTint = 0f)
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
            this.blendPast = blendPast;
            this.weight = weight;
            this.range = range;
            this.limitDistance = limitDistance;
            this.limitSoftness = limitSoftness;
            this.limitMessage = limitMessage;
            this.horizonColor = horizonColor;
            this.horizonTint = horizonTint;
        }

        public string Name => name;

        /// <summary>True when the sea lies past this edge. Every map has at least one such side (GL589).</summary>
        public bool IsOcean => ocean;

        public float EdgeHeight => edgeHeight;
        public float BlendWidth => Mathf.Max(1f, blendWidth);

        /// <summary>How often the seed picks this kind for a side that isn't sea.</summary>
        public float Weight => Mathf.Max(0f, weight);

        /// <summary>True when a mountain range crosses the map to this side and goes on past it.</summary>
        public bool HasRange => range;

        /// <summary>What the player is told as this edge's limit nears (GL640).</summary>
        public string LimitMessage => limitMessage;

        /// <summary>Colour of the land far past this edge (GL641).</summary>
        public Color HorizonColor => horizonColor;

        /// <summary>How much of the far land past this edge takes <see cref="HorizonColor"/>.</summary>
        public float HorizonTint => Mathf.Clamp01(horizonTint);

        /// <summary>The entry as plain values, for the height field and its jobs.</summary>
        public BoundaryProfile ToProfile()
        {
            return new BoundaryProfile
            {
                Ocean = ocean,
                EdgeHeight = edgeHeight,
                Flatten = Mathf.Clamp01(flatten),
                BlendWidth = BlendWidth,
                BlendPast = Mathf.Max(0f, blendPast),
                Sharpness = Mathf.Clamp(sharpness, 0.25f, 4f),
                Wobble = Mathf.Max(0f, wobble),
                Roughness = Mathf.Max(0f, roughness),
                TemperatureShift = temperatureShift,
                MoistureShift = moistureShift,
                LimitDistance = limitDistance,
                LimitSoftness = Mathf.Max(1f, limitSoftness),
                Empty = !ocean && flatten > 0.5f && edgeHeight < -50f,
            };
        }

        public const string OceanName = "Ocean";
        public const string DesertName = "Desert";
        public const string VoidCliffName = "Void cliff";
        public const string MountainRidgeName = "Mountain ridge";

        /// <summary>
        /// The four kinds the game starts with (GL590): an ocean that goes on forever, an endless sprawling desert, a
        /// cliff that drops off into a void, and a mountain range too tall to climb, the near side of a larger system.
        /// </summary>
        public static List<MapBoundaryType> Defaults()
        {
            return new List<MapBoundaryType>
            {
                new MapBoundaryType(OceanName, true, -26f, 1f, 260f, 1f, 0f, 0f, 0f, 0.12f,
                    limitDistance: 170f, limitSoftness: 140f,
                    limitMessage: "The open sea is too cold and its current too strong. Turn back."),
                new MapBoundaryType(DesertName, false, 6f, 1f, 800f, 1f, 150f, 9f, 16f, -1.6f,
                    weight: 1f, limitDistance: 150f, limitSoftness: 130f,
                    limitMessage: "The deep desert's heat is more than anyone can bear. Turn back.",
                    horizonColor: new Color(0.80f, 0.66f, 0.40f), horizonTint: 1f),
                new MapBoundaryType(VoidCliffName, false, -600f, 1f, 30f, 1f, 45f, 0f,
                    weight: 0.45f, limitDistance: -36f, limitSoftness: 5f,
                    limitMessage: "The world ends here.",
                    horizonColor: new Color(0.06f, 0.07f, 0.10f), horizonTint: 1f),
                new MapBoundaryType(MountainRidgeName, false, 650f, 0f, 520f, 1.6f, 90f, 60f,
                    blendPast: 700f, weight: 1.2f, range: true, limitDistance: 60f, limitSoftness: 70f,
                    limitMessage: "The air is too thin and the cold too bitter to climb on. Turn back.",
                    horizonColor: new Color(0.44f, 0.43f, 0.42f), horizonTint: 0.8f),
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
        public float BlendPast;
        public float Sharpness;
        public float Wobble;
        public float Roughness;
        public float TemperatureShift;
        public float MoistureShift;
        public float LimitDistance;
        public float LimitSoftness;

        /// <summary>True for an edge with nothing past it (the void): no sea stands there.</summary>
        public bool Empty;
    }
}
