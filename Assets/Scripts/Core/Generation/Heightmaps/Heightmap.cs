using System;
using Unity.Collections;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// A grid of heights from 0 (black) to 1 (white), read from a grayscale image
    /// or a RAW file (K9), and sampled with bilinear interpolation between pixels.
    /// Pixel (0, 0) is at the lower-left; positions past the edges clamp to the edge.
    /// </summary>
    public sealed class Heightmap
    {
        private readonly float[] values;

        public Heightmap(int width, int height, float[] values)
        {
            if (width < 1 || height < 1 || values == null || values.Length != width * height)
            {
                throw new ArgumentException("A heightmap needs width x height values.");
            }

            Width = width;
            Height = height;
            this.values = values;
        }

        public int Width { get; }

        public int Height { get; }

        /// <summary>The pixel at (x, y), clamped to the image.</summary>
        public float this[int x, int y] =>
            values[Mathf.Clamp(x, 0, Width - 1) + Width * Mathf.Clamp(y, 0, Height - 1)];

        /// <summary>
        /// Reads a texture's red channel. The texture must be readable (Read/Write in its
        /// import settings) and uncompressed, or compression will distort the heights.
        /// </summary>
        public static Heightmap FromTexture(Texture2D texture)
        {
            if (!texture.isReadable)
            {
                throw new InvalidOperationException(
                    $"Heightmap texture '{texture.name}' isn't readable: enable Read/Write in its import settings.");
            }

            Color[] pixels = texture.GetPixels();
            var heights = new float[pixels.Length];
            for (int i = 0; i < pixels.Length; i++)
            {
                heights[i] = pixels[i].r;
            }
            return new Heightmap(texture.width, texture.height, heights);
        }

        /// <summary>
        /// Reads a square RAW heightmap: one byte per pixel, or two (little-endian) for
        /// 16-bit, as terrain tools export. Rows run from the bottom up.
        /// </summary>
        public static Heightmap FromRaw(byte[] data, bool sixteenBit)
        {
            int bytesPerPixel = sixteenBit ? 2 : 1;
            int pixels = data.Length / bytesPerPixel;
            int side = Mathf.RoundToInt(Mathf.Sqrt(pixels));
            if (side * side * bytesPerPixel != data.Length)
            {
                throw new ArgumentException(
                    $"RAW heightmap of {data.Length} bytes isn't square at {(sixteenBit ? 16 : 8)} bits per pixel.");
            }

            var heights = new float[pixels];
            for (int i = 0; i < pixels; i++)
            {
                heights[i] = sixteenBit
                    ? (data[i * 2] | (data[i * 2 + 1] << 8)) / 65535f
                    : data[i] / 255f;
            }
            return new Heightmap(side, side, heights);
        }

        /// <summary>Copies every pixel, row by row from the bottom, into a native array of <see cref="Width"/> × <see cref="Height"/>.</summary>
        public void CopyTo(NativeArray<float> pixels)
        {
            pixels.CopyFrom(values);
        }

        /// <summary>The height at a position measured in pixels, blended from the four nearest pixels.</summary>
        /// <remarks><see cref="HeightmapHeight"/> blends the same way inside generation jobs; keep the two in step.</remarks>
        public float Sample(float x, float y)
        {
            int x0 = Mathf.FloorToInt(x);
            int y0 = Mathf.FloorToInt(y);
            float tx = x - x0;
            float ty = y - y0;

            float bottom = Mathf.Lerp(this[x0, y0], this[x0 + 1, y0], tx);
            float top = Mathf.Lerp(this[x0, y0 + 1], this[x0 + 1, y0 + 1], tx);
            return Mathf.Lerp(bottom, top, ty);
        }
    }
}
