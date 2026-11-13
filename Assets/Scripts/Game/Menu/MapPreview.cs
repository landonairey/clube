using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// The new-game page's picture of a seed's map (GL636): elevation as contour lines and nothing else
    /// (<see cref="MapContours"/>), from the map's land on a coarse grid, so a new seed redraws in a moment. It waits
    /// for the seed to hold still a little, so typing one doesn't redraw on every key.
    /// </summary>
    public sealed class MapPreview
    {
        private const float SettleSeconds = 0.2f;
        // The share of the map's side shown beyond each edge, so what bounds the map (sea, ridge, desert) shows.
        private const float Margin = 0.08f;

        private readonly WorldConfig config;
        private readonly int resolution;
        private Texture2D picture;
        private int drawnSeed;
        private bool drawn;
        private int wantedSeed;
        private float wantedAt;

        /// <param name="mapConfig">A world config whose terrain is the bounded map; copied, so the asset's seed is never changed.</param>
        public MapPreview(WorldConfig mapConfig, int resolution = 256)
        {
            config = Object.Instantiate(mapConfig);
            config.hideFlags = HideFlags.HideAndDontSave;
            config.Terrain.Generator = TerrainGeneratorType.BoundedMap;
            this.resolution = resolution;
        }

        /// <summary>Milliseconds the last picture took to work out and draw.</summary>
        public float LastMilliseconds { get; private set; }

        /// <summary>The seed's contour picture; the one before (null the first time) while the seed is still changing.</summary>
        public Texture2D For(int seed)
        {
            float now = Time.realtimeSinceStartup;
            if (!drawn)
            {
                Draw(seed);
            }
            else if (seed != drawnSeed)
            {
                if (seed != wantedSeed)
                {
                    wantedSeed = seed;
                    wantedAt = now;
                }
                else if (now - wantedAt >= SettleSeconds)
                {
                    Draw(seed);
                }
            }
            wantedSeed = seed;
            return picture;
        }

        private void Draw(int seed)
        {
            float start = Time.realtimeSinceStartup;
            config.Terrain.Seed = seed;
            if (picture == null)
            {
                picture = new Texture2D(resolution, resolution, TextureFormat.RGB24, false)
                {
                    name = "Map preview", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear,
                    hideFlags = HideFlags.HideAndDontSave,
                };
            }
            picture.SetPixels32(MapContours.Render(BoundedMapGenerator.CreateDry(config.Terrain), resolution, margin: Margin));
            picture.Apply(false);
            drawnSeed = seed;
            drawn = true;
            LastMilliseconds = (Time.realtimeSinceStartup - start) * 1000f;
        }

        public void Dispose()
        {
            if (picture != null)
            {
                Object.Destroy(picture);
            }
            if (config != null)
            {
                Object.Destroy(config);
            }
        }
    }
}
