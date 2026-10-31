using System;
using System.Diagnostics;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// A map of what grass looks like from afar round the focus (GL257): a small texture, a few metres a texel, of
    /// <see cref="GrassLawn"/> colours, for the terrain shaders (and the far tufts) to colour grass ground with, so meadows
    /// show straw and wheat fields wheat out past the 3D grass. Built a few rows a frame within a time budget, and built
    /// again round the focus once it has moved a quarter of the way to the map's edge; the finished map replaces the old
    /// one at once. Sets the shader globals <c>_GrassLawnMap</c>, <c>_GrassLawnMapRect</c> (x and z of its corner, 1 over
    /// its width, 1 when shown) and <c>_GrassLawnColor</c> (its average, for ground beyond it).
    /// </summary>
    public sealed class GrassLawnMap : IDisposable
    {
        private static readonly int MapId = Shader.PropertyToID("_GrassLawnMap");
        private static readonly int RectId = Shader.PropertyToID("_GrassLawnMapRect");
        private static readonly int ColorId = Shader.PropertyToID("_GrassLawnColor");

        private readonly int size;
        private readonly float texel;
        private readonly Color32[] building;
        private readonly Stopwatch clock = new Stopwatch();
        private Texture2D texture;
        private Vector2 shownCentre;
        private Vector2 buildCorner;
        private Vector2 buildCentre;
        private Color sum;
        private int nextRow = -1;

        /// <param name="size">Texels along each side.</param>
        /// <param name="texel">Metres a texel.</param>
        public GrassLawnMap(int size = 128, float texel = 4f)
        {
            this.size = Mathf.Max(8, size);
            this.texel = Mathf.Max(0.5f, texel);
            building = new Color32[this.size * this.size];
        }

        /// <summary>True once a map is shown.</summary>
        public bool IsShown { get; private set; }

        /// <summary>The map's width in metres.</summary>
        public float Width => size * texel;

        /// <summary>Starts again from nothing (the grass grows differently now); the old map stays until the new one is done.</summary>
        public void Rebuild()
        {
            nextRow = -1;
            IsShown = IsShown && texture != null;
            shownCentre = new Vector2(float.MaxValue, float.MaxValue);
        }

        /// <summary>
        /// Builds towards a map round <paramref name="focus"/> (world x, z) for up to <paramref name="milliseconds"/>,
        /// taking each texel's colour (linear) from <paramref name="colourAt"/> (world x, z).
        /// </summary>
        public void Update(Vector2 focus, Func<float, float, Color> colourAt, float milliseconds)
        {
            if (nextRow < 0)
            {
                // A new map is due when the focus has moved a quarter of the way to the shown one's edge.
                if (IsShown && (focus - shownCentre).sqrMagnitude < Width * Width / 16f)
                {
                    return;
                }
                float snap = texel * 8f;
                buildCentre = new Vector2(Mathf.Round(focus.x / snap) * snap, Mathf.Round(focus.y / snap) * snap);
                buildCorner = buildCentre - Vector2.one * (Width * 0.5f);
                sum = Color.clear;
                nextRow = 0;
            }

            clock.Restart();
            while (nextRow < size && clock.Elapsed.TotalMilliseconds < milliseconds)
            {
                float z = buildCorner.y + (nextRow + 0.5f) * texel;
                for (int column = 0; column < size; column++)
                {
                    Color colour = colourAt(buildCorner.x + (column + 0.5f) * texel, z);
                    sum += colour;
                    building[nextRow * size + column] = colour;
                }
                nextRow++;
            }
            if (nextRow >= size)
            {
                Show();
            }
        }

        /// <summary>Takes the map off the shaders.</summary>
        public void Hide()
        {
            IsShown = false;
            Shader.SetGlobalVector(RectId, Vector4.zero);
        }

        public void Dispose()
        {
            Hide();
            if (texture != null)
            {
                UnityEngine.Object.Destroy(texture);
                texture = null;
            }
        }

        private void Show()
        {
            nextRow = -1;
            if (texture == null)
            {
                // Linear: the colours are what the shaders use as they are.
                texture = new Texture2D(size, size, TextureFormat.RGBA32, false, true)
                {
                    name = "Grass lawn map",
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear,
                };
            }
            texture.SetPixels32(building);
            texture.Apply(false, false);
            shownCentre = buildCentre;
            IsShown = true;
            Color mean = sum / (size * size);
            Shader.SetGlobalTexture(MapId, texture);
            Shader.SetGlobalVector(RectId, new Vector4(buildCorner.x, buildCorner.y, 1f / Width, 1f));
            Shader.SetGlobalVector(ColorId, new Vector4(mean.r, mean.g, mean.b, 1f));
        }
    }
}
