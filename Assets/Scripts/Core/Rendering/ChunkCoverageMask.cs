using System.Collections.Generic;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// Which chunk columns around the focus the voxel world draws in full (P7): every layer of the
    /// column loaded and meshed. Kept as a small single-channel texture, one texel per column
    /// (255 = drawn), so the far terrain's shader can leave out exactly those columns and fill in
    /// everywhere the chunks haven't streamed in yet. Follows <see cref="WorldView.ChunkMeshed"/>
    /// and <see cref="WorldView.ChunkUnloaded"/>.
    /// </summary>
    public sealed class ChunkCoverageMask : System.IDisposable
    {
        private readonly WorldView view;
        private readonly int size;
        private readonly HashSet<Vector3Int> meshed = new HashSet<Vector3Int>();
        private readonly Dictionary<Vector2Int, int> layersMeshed = new Dictionary<Vector2Int, int>();
        private readonly byte[] texels;
        private Vector2Int origin;
        private bool hasOrigin;
        private bool dirty;

        /// <param name="radius">Columns covered each way from the focus column (the largest render distance).</param>
        public ChunkCoverageMask(WorldView view, int radius)
        {
            this.view = view;
            size = radius * 2 + 3;
            texels = new byte[size * size];
            Texture = new Texture2D(size, size, TextureFormat.R8, false, true)
            {
                name = "Chunk coverage",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };

            // Chunks meshed before this started (it can be switched on mid-play): clean ones count.
            if (view.World != null)
            {
                foreach (KeyValuePair<Vector3Int, Chunk> loaded in view.World.Chunks)
                {
                    if (!loaded.Value.IsDirty)
                    {
                        OnMeshed(loaded.Key, null);
                    }
                }
            }
            view.ChunkMeshed += OnMeshed;
            view.ChunkUnloaded += OnUnloaded;
        }

        public Texture2D Texture { get; }

        public int Size => size;

        /// <summary>The column of texel (0, 0).</summary>
        public Vector2Int Origin => origin;

        /// <summary>Moves the window onto the focus column and uploads any change. Call once a frame.</summary>
        public void Update(Vector2Int focusColumn)
        {
            Vector2Int wantedOrigin = focusColumn - new Vector2Int(size / 2, size / 2);
            if (!hasOrigin || wantedOrigin != origin)
            {
                origin = wantedOrigin;
                hasOrigin = true;
                Refill();
            }
            if (dirty)
            {
                Texture.SetPixelData(texels, 0);
                Texture.Apply(false, false);
                dirty = false;
            }
        }

        public void Dispose()
        {
            view.ChunkMeshed -= OnMeshed;
            view.ChunkUnloaded -= OnUnloaded;
            Object.Destroy(Texture);
        }

        private void OnMeshed(Vector3Int coord, ChunkRenderer _)
        {
            if (meshed.Add(coord))
            {
                var column = new Vector2Int(coord.x, coord.z);
                layersMeshed.TryGetValue(column, out int count);
                layersMeshed[column] = count + 1;
                SetTexel(column);
            }
        }

        private void OnUnloaded(Vector3Int coord)
        {
            if (meshed.Remove(coord))
            {
                var column = new Vector2Int(coord.x, coord.z);
                int count = layersMeshed[column] - 1;
                if (count <= 0)
                {
                    layersMeshed.Remove(column);
                }
                else
                {
                    layersMeshed[column] = count;
                }
                SetTexel(column);
            }
        }

        private bool IsCovered(Vector2Int column)
        {
            return layersMeshed.TryGetValue(column, out int count) && count >= view.Config.WorldHeightInChunks;
        }

        private void SetTexel(Vector2Int column)
        {
            if (!hasOrigin)
            {
                return;
            }
            Vector2Int texel = column - origin;
            if (texel.x < 0 || texel.y < 0 || texel.x >= size || texel.y >= size)
            {
                return;
            }
            byte value = IsCovered(column) ? (byte)255 : (byte)0;
            int index = texel.x + texel.y * size;
            if (texels[index] != value)
            {
                texels[index] = value;
                dirty = true;
            }
        }

        private void Refill()
        {
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    texels[x + y * size] = IsCovered(origin + new Vector2Int(x, y)) ? (byte)255 : (byte)0;
                }
            }
            dirty = true;
        }
    }
}
