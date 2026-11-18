using System.Collections.Generic;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// Which chunk columns around the focus the voxel world draws in full (P7): every layer of the
    /// column loaded and meshed, and drawn (within the detail distance, or edited, P18). Kept as a
    /// small single-channel texture, one texel per column (255 = drawn), so the far terrain's shader
    /// can leave out exactly those columns and fill in everywhere the chunks haven't streamed in yet
    /// or aren't drawn. Follows <see cref="WorldView.ChunkMeshed"/>, <see cref="WorldView.ChunkUnloaded"/>
    /// and <see cref="WorldView.DrawVersion"/>.
    /// </summary>
    public sealed class ChunkCoverageMask : System.IDisposable
    {
        private static readonly Dictionary<WorldView, ChunkCoverageMask> SharedMasks = new Dictionary<WorldView, ChunkCoverageMask>();

        private readonly WorldView view;
        private readonly System.Func<Vector2Int, bool> alsoReady;
        private readonly int size;
        private readonly HashSet<Vector3Int> meshed = new HashSet<Vector3Int>();
        private readonly Dictionary<Vector2Int, int> layersMeshed = new Dictionary<Vector2Int, int>();
        // The columns with every layer meshed: all a refill needs to look at (GL770).
        private readonly HashSet<Vector2Int> fullColumns = new HashSet<Vector2Int>();
        private int layers;
        private readonly byte[] texels;
        private int users = 1;
        private bool isShared;
        private Vector2Int origin;
        private bool hasOrigin;
        private bool dirty;
        private int drawVersion;

        /// <param name="radius">Columns covered each way from the focus column (the largest render distance).</param>
        /// <param name="alsoReady">A further condition for a column to count as covered (GL654: its water is meshed); call <see cref="Recheck"/> when it changes. Null for none.</param>
        public ChunkCoverageMask(WorldView view, int radius, System.Func<Vector2Int, bool> alsoReady = null)
        {
            this.view = view;
            this.alsoReady = alsoReady;
            layers = view.Config.WorldHeightInChunks;
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

        /// <summary>
        /// The mask every view of this world without a further condition uses (GL770): the far terrain, coarse terrain,
        /// distant trees and distant buildings each kept and refilled their own copy of the same texture. Each user calls
        /// <see cref="Dispose"/> once.
        /// </summary>
        public static ChunkCoverageMask SharedFor(WorldView view)
        {
            if (SharedMasks.TryGetValue(view, out ChunkCoverageMask mask))
            {
                mask.users++;
                return mask;
            }
            mask = new ChunkCoverageMask(view, WorldView.MaxRenderDistance) { isShared = true };
            SharedMasks.Add(view, mask);
            return mask;
        }

        public Texture2D Texture { get; }

        public int Size => size;

        /// <summary>Changes whenever the texture does (after <see cref="Update"/>).</summary>
        public int Version { get; private set; }

        /// <summary>
        /// Whether the texture marks the column covered, as the shaders read it: false outside the window (GL560).
        /// </summary>
        public bool Covers(Vector2Int column)
        {
            Vector2Int texel = column - origin;
            return hasOrigin && texel.x >= 0 && texel.y >= 0 && texel.x < size && texel.y < size && texels[texel.x + texel.y * size] != 0;
        }

        /// <summary>The column of texel (0, 0).</summary>
        public Vector2Int Origin => origin;

        /// <summary>Moves the window onto the focus column and uploads any change. Call once a frame.</summary>
        public void Update(Vector2Int focusColumn)
        {
            Vector2Int wantedOrigin = focusColumn - new Vector2Int(size / 2, size / 2);
            bool newHeight = view.Config.WorldHeightInChunks != layers;
            if (newHeight)
            {
                // The labs can change the world's height while it runs.
                layers = view.Config.WorldHeightInChunks;
                fullColumns.Clear();
                foreach (KeyValuePair<Vector2Int, int> column in layersMeshed)
                {
                    if (column.Value >= layers)
                    {
                        fullColumns.Add(column.Key);
                    }
                }
            }
            if (newHeight || !hasOrigin || wantedOrigin != origin || drawVersion != view.DrawVersion)
            {
                origin = wantedOrigin;
                hasOrigin = true;
                drawVersion = view.DrawVersion;
                Refill();
            }
            if (dirty)
            {
                Texture.SetPixelData(texels, 0);
                Texture.Apply(false, false);
                Version++;
                dirty = false;
            }
        }

        public void Dispose()
        {
            if (--users > 0)
            {
                return;
            }
            if (isShared)
            {
                SharedMasks.Remove(view);
            }
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
                if (count + 1 >= layers)
                {
                    fullColumns.Add(column);
                }
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
                if (count < layers)
                {
                    fullColumns.Remove(column);
                }
                SetTexel(column);
            }
        }

        /// <summary>Looks at a column again, after the further condition given to the constructor changed for it.</summary>
        public void Recheck(Vector2Int column)
        {
            SetTexel(column);
        }

        private bool IsCovered(Vector2Int column)
        {
            return fullColumns.Contains(column) && view.IsColumnDrawn(column) && (alsoReady == null || alsoReady(column));
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

        // Only the fully meshed columns can be covered (GL770): asking about all 17 000 texels each time the focus crossed a
        // column cost a millisecond or more per mask.
        private void Refill()
        {
            System.Array.Clear(texels, 0, texels.Length);
            foreach (Vector2Int column in fullColumns)
            {
                Vector2Int texel = column - origin;
                if (texel.x >= 0 && texel.y >= 0 && texel.x < size && texel.y < size && IsCovered(column))
                {
                    texels[texel.x + texel.y * size] = 255;
                }
            }
            dirty = true;
        }
    }
}
