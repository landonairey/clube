using System;
using System.Collections.Generic;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// The dirty chunks waiting for a mesh (GL774), taken from the world as they become dirty (<see cref="World.TakeDirty"/>)
    /// rather than collected every frame, with those of held columns parked out of the way: at a long render distance a
    /// hundred thousand chunks wait for roads and settlements to be written, and looking at each of them every frame, twice,
    /// was most of what starting meshes cost. A slice of the parked columns is looked at again each frame; a column no longer
    /// held sends its chunks back to wait.
    /// </summary>
    public sealed class DirtyChunks
    {
        // Every parked column is looked at again within this many frames, however many there are, and at least this many a frame.
        private const int ParkedPasses = 16, LeastColumnsLooked = 256;

        private readonly HashSet<Vector3Int> waiting = new HashSet<Vector3Int>();
        private readonly HashSet<Vector3Int> parked = new HashSet<Vector3Int>();
        private readonly Dictionary<Vector2Int, List<Vector3Int>> parkedByColumn = new Dictionary<Vector2Int, List<Vector3Int>>();
        private readonly List<Vector2Int> parkedColumns = new List<Vector2Int>();
        private readonly Stack<List<Vector3Int>> spareLists = new Stack<List<Vector3Int>>();
        private readonly List<Vector3Int> taken = new List<Vector3Int>();
        private readonly List<Vector3Int> gone = new List<Vector3Int>();
        private int nextParked;

        /// <summary>Chunks waiting, some of them perhaps cleaned or unloaded since (they are dropped when next looked at).</summary>
        public int WaitingCount => waiting.Count;

        /// <summary>Chunks parked in held columns.</summary>
        public int ParkedCount => parked.Count;

        /// <summary>The chunks waiting, as a set; read only.</summary>
        public IReadOnlyCollection<Vector3Int> Waiting => waiting;

        /// <summary>Takes the chunks the world noted dirty since the last take. A parked chunk stays parked.</summary>
        public void Take(World world)
        {
            taken.Clear();
            world.TakeDirty(taken);
            foreach (Vector3Int coord in taken)
            {
                if (!parked.Contains(coord))
                {
                    waiting.Add(coord);
                }
            }
        }

        /// <summary>
        /// Fills <paramref name="into"/> (cleared first) with the waiting chunks that are still loaded and dirty, and forgets
        /// the others: a chunk is noted again when it next becomes dirty.
        /// </summary>
        public void CollectWaiting(World world, List<Vector3Int> into)
        {
            into.Clear();
            gone.Clear();
            foreach (Vector3Int coord in waiting)
            {
                if (IsDirty(world, coord))
                {
                    into.Add(coord);
                }
                else
                {
                    gone.Add(coord);
                }
            }
            foreach (Vector3Int coord in gone)
            {
                waiting.Remove(coord);
            }
        }

        /// <summary>Sets a waiting chunk aside until its column is no longer held (<see cref="LookAtParked"/>).</summary>
        public void Park(Vector3Int coord)
        {
            waiting.Remove(coord);
            if (!parked.Add(coord))
            {
                return;
            }
            var column = new Vector2Int(coord.x, coord.z);
            if (!parkedByColumn.TryGetValue(column, out List<Vector3Int> chunks))
            {
                chunks = spareLists.Count > 0 ? spareLists.Pop() : new List<Vector3Int>();
                parkedByColumn.Add(column, chunks);
                parkedColumns.Add(column);
            }
            chunks.Add(coord);
        }

        /// <summary>
        /// Looks again at the next slice of parked columns: those <paramref name="isHeld"/> no longer holds send their chunks
        /// back to wait; those unloaded meanwhile are forgotten.
        /// </summary>
        public void LookAtParked(World world, Func<Vector2Int, bool> isHeld)
        {
            int count = Mathf.Min(parkedColumns.Count, Mathf.Max(LeastColumnsLooked, parkedColumns.Count / ParkedPasses));
            for (int looked = 0; looked < count && parkedColumns.Count > 0; looked++)
            {
                if (nextParked >= parkedColumns.Count)
                {
                    nextParked = 0;
                }
                Vector2Int column = parkedColumns[nextParked];
                List<Vector3Int> chunks = parkedByColumn[column];
                // A parked chunk is never meshed, and a column unloads whole: while the column is held and loaded, its
                // chunks needn't be looked at one by one.
                bool released = !isHeld(column);
                if (released || world.LoadedLayersIn(column) == 0)
                {
                    foreach (Vector3Int coord in chunks)
                    {
                        parked.Remove(coord);
                        if (released && IsDirty(world, coord))
                        {
                            waiting.Add(coord);
                        }
                    }
                    chunks.Clear();
                }
                if (chunks.Count == 0)
                {
                    parkedByColumn.Remove(column);
                    spareLists.Push(chunks);
                    parkedColumns[nextParked] = parkedColumns[parkedColumns.Count - 1];
                    parkedColumns.RemoveAt(parkedColumns.Count - 1);
                }
                else
                {
                    nextParked++;
                }
            }
        }

        /// <summary>Forgets every chunk (the world was emptied).</summary>
        public void Clear()
        {
            waiting.Clear();
            parked.Clear();
            foreach (List<Vector3Int> chunks in parkedByColumn.Values)
            {
                chunks.Clear();
                spareLists.Push(chunks);
            }
            parkedByColumn.Clear();
            parkedColumns.Clear();
            nextParked = 0;
        }

        private static bool IsDirty(World world, Vector3Int coord)
        {
            return world.TryPeekChunk(coord, out Chunk chunk) && chunk.IsDirty;
        }
    }
}
