using System;
using System.Collections.Generic;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// One biome (GL591, PK5), as data: the elevation, temperature and moisture it covers and what it sets, the surface
    /// materials, how thick the trees grow and of which kind, and how good the land is for grazing and game (GL592;
    /// the animals of GL605 key off the biome too). A map's biomes are a list, first match wins, so a new biome is a
    /// new entry.
    /// </summary>
    [Serializable]
    public class BiomeDefinition
    {
        /// <summary>A range end meaning "no limit".</summary>
        public const float Any = 100000f;

        [SerializeField]
        private string name = "Grassland";

        [Tooltip("Metres above sea level it covers, from and to.")]
        [SerializeField]
        private Vector2 elevation = new Vector2(-Any, Any);

        [Tooltip("Degrees it covers, from and to.")]
        [SerializeField]
        private Vector2 temperature = new Vector2(-Any, Any);

        [Tooltip("Moisture it covers (0 dry to 1 wet), from and to.")]
        [SerializeField]
        private Vector2 moisture = new Vector2(-Any, Any);

        [Tooltip("Surface material; none keeps the terrain layers' top.")]
        [SerializeField]
        private VoxelMaterial top;

        [Tooltip("Material under the surface; none keeps the terrain layers' band.")]
        [SerializeField]
        private VoxelMaterial band;

        [Tooltip("Multiplies the trees' chance of growing: 0 for none, 1 as the tree settings give.")]
        [SerializeField, Min(0f)]
        private float treeDensity = 1f;

        [Tooltip("Share of the trees that are maple; below 0 keeps the tree settings' share.")]
        [SerializeField, Range(-1f, 1f)]
        private float mapleShare = -1f;

        [Tooltip("How good the land is for grazing, 0 to 1.")]
        [SerializeField, Range(0f, 1f)]
        private float grazing;

        [Tooltip("How much game lives here, 0 to 1.")]
        [SerializeField, Range(0f, 1f)]
        private float game;

        [Tooltip("Colour on maps and previews.")]
        [SerializeField]
        private Color mapColor = new Color(0.45f, 0.65f, 0.3f);

        public BiomeDefinition()
        {
        }

        public BiomeDefinition(
            string name, Vector2 elevation, Vector2 temperature, Vector2 moisture, VoxelMaterial top, VoxelMaterial band,
            float treeDensity, float mapleShare, float grazing, float game, Color mapColor)
        {
            this.name = name;
            this.elevation = elevation;
            this.temperature = temperature;
            this.moisture = moisture;
            this.top = top;
            this.band = band;
            this.treeDensity = treeDensity;
            this.mapleShare = mapleShare;
            this.grazing = grazing;
            this.game = game;
            this.mapColor = mapColor;
        }

        public string Name => name;
        public Vector2 Elevation => elevation;
        public Vector2 Temperature => temperature;
        public Vector2 Moisture => moisture;
        public VoxelMaterial Top => top;
        public VoxelMaterial Band => band;
        public float TreeDensity => Mathf.Max(0f, treeDensity);

        /// <summary>Share of maples among the trees, or below 0 for the tree settings' own.</summary>
        public float MapleShare => mapleShare;

        public float Grazing => Mathf.Clamp01(grazing);
        public float Game => Mathf.Clamp01(game);
        public Color MapColor => mapColor;

        public const string OceanName = "Ocean";
        public const string BeachName = "Beach";
        public const string AlpineName = "Alpine";
        public const string DesertName = "Desert";
        public const string SteppeName = "Steppe";
        public const string HighlandName = "Highland";
        public const string ForestName = "Forest";
        public const string GrasslandName = "Grassland";

        /// <summary>
        /// The built-in table, from the materials a terrain already has: sea floor and beaches of sand, bare rock where
        /// it's cold (the peaks), sand desert where it's hot and dry, steppe of bare earth where it's dry, thin birch
        /// highlands, thick mixed forest where it's wet, and grassland everywhere else.
        /// </summary>
        public static List<BiomeDefinition> Defaults(VoxelMaterial sand, VoxelMaterial dirt, VoxelMaterial stone)
        {
            var all = new Vector2(-Any, Any);
            return new List<BiomeDefinition>
            {
                new BiomeDefinition(OceanName, new Vector2(-Any, -0.5f), all, all, sand, sand, 0f, -1f, 0f, 0f, new Color(0.16f, 0.36f, 0.6f)),
                new BiomeDefinition(BeachName, new Vector2(-0.5f, 1.5f), all, all, sand, sand, 0f, -1f, 0f, 0f, new Color(0.87f, 0.8f, 0.58f)),
                new BiomeDefinition(AlpineName, all, new Vector2(-Any, 1f), all, stone, stone, 0f, -1f, 0f, 0.1f, new Color(0.62f, 0.62f, 0.64f)),
                new BiomeDefinition(DesertName, all, new Vector2(15f, Any), new Vector2(-Any, 0.22f), sand, sand, 0f, -1f, 0f, 0f, new Color(0.9f, 0.74f, 0.42f)),
                new BiomeDefinition(SteppeName, all, all, new Vector2(-Any, 0.36f), dirt, dirt, 0.1f, 0f, 0.5f, 0.3f, new Color(0.66f, 0.62f, 0.36f)),
                new BiomeDefinition(HighlandName, new Vector2(90f, Any), all, all, null, null, 0.5f, 0f, 0.4f, 0.6f, new Color(0.42f, 0.55f, 0.4f)),
                new BiomeDefinition(ForestName, all, all, new Vector2(0.6f, Any), null, null, 1.6f, 0.6f, 0.2f, 1f, new Color(0.16f, 0.42f, 0.2f)),
                new BiomeDefinition(GrasslandName, all, all, all, null, null, 0.35f, -1f, 1f, 0.5f, new Color(0.5f, 0.72f, 0.34f)),
            };
        }
    }

    /// <summary>What the trees ask of the land's biomes (GL591); none set means the tree settings alone decide.</summary>
    public interface ITreeClimate
    {
        /// <summary>What the trees' chance is multiplied by at (x, z).</summary>
        float TreeDensityAt(float x, float z);

        /// <summary>Share of maples among the trees at (x, z); <paramref name="fallback"/> where the biome doesn't say.</summary>
        float MapleShareAt(float x, float z, float fallback);
    }
}
