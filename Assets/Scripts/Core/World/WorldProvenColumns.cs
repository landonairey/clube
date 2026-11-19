using System;
using System.Collections.Generic;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// Sparse chunks (GL773): most of a tall world's chunks are sky or deep rock. A column's layers proven uniform
    /// (<see cref="UniformLayers"/>) count as loaded and answer reads, but hold no <see cref="Chunk"/> until something
    /// needs one: a write that changes them, or <see cref="TryGetChunk"/>. Then the chunk is made on the spot, as the
    /// generator would have made it (<see cref="ChunkMaterialised"/>).
    /// </summary>
    public sealed partial class World
    {
        private struct ProvenColumn
        {
            public UniformLayers Layers;

            // Proven layers that have a real chunk at the moment: it was asked for, and nothing has changed it yet.
            public int Materialised;
        }

        private readonly Dictionary<Vector2Int, ProvenColumn> proven = new Dictionary<Vector2Int, ProvenColumn>();

        /// <summary>A proven layer was given a real chunk. Raised during the edit that needed it: only note it down.</summary>
        public event Action<Vector3Int> ChunkMaterialised;

        /// <summary>How many of a column's layers are proven changed (<see cref="ProvenLayersIn"/>).</summary>
        public event Action<Vector2Int> ProvenLayersChanged;

        /// <summary>The columns whose layers have been proven.</summary>
        public IEnumerable<Vector2Int> ProvenColumns => proven.Keys;

        /// <summary>True once the column's layers have been looked at, whatever was proven.</summary>
        public bool IsColumnProven(Vector2Int column)
        {
            return proven.ContainsKey(column);
        }

        /// <summary>True for a chunk proven all air or all rock that nothing has changed.</summary>
        public bool IsProvenUniform(Vector3Int coord)
        {
            return proven.TryGetValue(new Vector2Int(coord.x, coord.z), out ProvenColumn column) && column.Layers.IsUniform(coord.y);
        }

        /// <summary>The column's proven layers that hold no real chunk: loaded, with nothing to mesh.</summary>
        public int ProvenLayersIn(Vector2Int column)
        {
            return proven.TryGetValue(column, out ProvenColumn found) ? found.Layers.Count - found.Materialised : 0;
        }

        /// <summary>Every proven layer holding no real chunk, over all columns: for readouts.</summary>
        public int CountProvenLayers()
        {
            int count = 0;
            foreach (ProvenColumn column in proven.Values)
            {
                count += column.Layers.Count - column.Materialised;
            }
            return count;
        }

        /// <summary>
        /// Records what was proven of a column. A layer with a chunk already loaded stays that chunk, and a layer that is
        /// edited or touches an edited chunk is left out: its border samples can differ from the proof, so it has to load
        /// the usual way and take them from its neighbour. Returns how many layers now count as loaded because of this.
        /// </summary>
        public int ProveColumn(Vector2Int column, UniformLayers layers, int heightInChunks)
        {
            if (proven.ContainsKey(column))
            {
                return 0;
            }
            bool editsNear = false;
            for (int dz = -1; dz <= 1 && !editsNear; dz++)
            {
                for (int dx = -1; dx <= 1 && !editsNear; dx++)
                {
                    editsNear = editedColumns.Contains(new Vector2Int(column.x + dx, column.y + dz));
                }
            }
            for (int layer = 0; layer < UniformLayers.MaxLayers; layer++)
            {
                if (!layers.IsUniform(layer))
                {
                    continue;
                }
                var coord = new Vector3Int(column.x, layer, column.y);
                if (layer >= heightInChunks || loaded.ContainsKey(coord) || (editsNear && TouchesEdit(coord)))
                {
                    layers.Clear(layer);
                }
            }
            proven.Add(column, new ProvenColumn { Layers = layers });
            int added = layers.Count;
            loadedColumns[column] = LoadedLayersIn(column) + added;
            ProvenLayersChanged?.Invoke(column);
            return added;
        }

        /// <summary>Drops a column's proof (it left the loaded area): its proven layers no longer count as loaded.</summary>
        public void ForgetColumnProof(Vector2Int column)
        {
            if (!proven.TryGetValue(column, out ProvenColumn found))
            {
                return;
            }
            proven.Remove(column);
            int layers = LoadedLayersIn(column) - (found.Layers.Count - found.Materialised);
            if (layers > 0)
            {
                loadedColumns[column] = layers;
            }
            else
            {
                loadedColumns.Remove(column);
            }
            ProvenLayersChanged?.Invoke(column);
        }

        /// <summary>Drops every column's proof.</summary>
        public void ForgetAllProofs()
        {
            foreach (Vector2Int column in new List<Vector2Int>(proven.Keys))
            {
                ForgetColumnProof(column);
            }
        }

        /// <summary>The loaded chunk, without making one for a proven layer: for readers to whom such a layer means "nothing here".</summary>
        public bool TryPeekChunk(Vector3Int coord, out Chunk chunk)
        {
            return loaded.TryGetValue(coord, out chunk);
        }

        /// <summary>Like <see cref="TryGetOwner"/>, without making a chunk for a proven layer.</summary>
        public bool TryPeekOwner(Vector3Int sample, out Chunk chunk, out Vector3Int local)
        {
            Vector3Int coord = OwnerOf(sample, out local);
            return loaded.TryGetValue(coord, out chunk);
        }

        // What every sample of a proven chunk holds.
        private bool TryProvenSample(Vector3Int coord, out float density, out byte material)
        {
            density = 0f;
            material = 0;
            if (!proven.TryGetValue(new Vector2Int(coord.x, coord.z), out ProvenColumn column) || !column.Layers.IsUniform(coord.y))
            {
                return false;
            }
            bool rock = column.Layers.IsRock(coord.y);
            density = rock ? 1f : 0f;
            material = rock ? column.Layers.RockMaterial : column.Layers.AirMaterial;
            return true;
        }

        // Makes the real chunk of a proven layer, dirty like any new chunk, so it goes through the usual meshing.
        private bool TryMaterialise(Vector3Int coord, out Chunk chunk)
        {
            chunk = null;
            if (loaded.ContainsKey(coord) || !TryProvenSample(coord, out float density, out byte material))
            {
                return false;
            }
            var key = new Vector2Int(coord.x, coord.z);
            ProvenColumn column = proven[key];
            column.Materialised++;
            proven[key] = column;

            chunk = Chunk.Uniform(Grid.ChunkSize + Vector3Int.one, density, material, createStorage);
            loaded.Add(coord, chunk);
            NoteLoadedBox(coord);
            chunk.BecameDirty = _ => dirty.Add(coord);
            dirty.Add(coord);
            ChunkMaterialised?.Invoke(coord);
            return true;
        }

        // Takes back a layer's proof (it was edited, or a real chunk replaces it); true when there was one.
        private bool TakeProof(Vector3Int coord)
        {
            var key = new Vector2Int(coord.x, coord.z);
            if (!proven.TryGetValue(key, out ProvenColumn column) || !column.Layers.IsUniform(coord.y))
            {
                return false;
            }
            column.Layers.Clear(coord.y);
            if (loaded.ContainsKey(coord))
            {
                column.Materialised--;
            }
            proven[key] = column;
            return true;
        }

        // An edit made this chunk differ from its proof: from now on it is an ordinary edited chunk.
        private void DropProofOfEdited(Vector3Int coord)
        {
            bool hadChunk = loaded.ContainsKey(coord);
            if (!TakeProof(coord) || hadChunk)
            {
                return;
            }

            // Kept from a save with no chunk in the world yet: the layer no longer counts as loaded.
            var column = new Vector2Int(coord.x, coord.z);
            int layers = LoadedLayersIn(column) - 1;
            if (layers > 0)
            {
                loadedColumns[column] = layers;
            }
            else
            {
                loadedColumns.Remove(column);
            }
            ProvenLayersChanged?.Invoke(column);
        }

        private bool TouchesEdit(Vector3Int coord)
        {
            for (int dz = -1; dz <= 1; dz++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (edited.Contains(coord + new Vector3Int(dx, dy, dz)))
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }
    }
}
