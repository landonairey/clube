using System;
using System.Collections.Generic;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// Something that still has to write into chunk columns before they are first shown (GL653): a road's bed, a
    /// settlement's terraces. While a hold says a column is held, the streamer loads its chunks but doesn't mesh them, so
    /// what stands in for unloaded ground (the coarse terrain) stays, and the column first appears with the writing in
    /// it. A hold must let go of every column it can write, and only hold where it will: a column held for good is
    /// never shown.
    /// </summary>
    public interface IColumnHold
    {
        /// <summary>True while the chunk column (x, z) is still to be written.</summary>
        bool Holds(Vector2Int column);
    }

    /// <summary>
    /// The usual hold: a count of pieces still to write per column (<see cref="Add"/> a piece's ground when it is
    /// planned, <see cref="Remove"/> it once written), and wider rules for what is not planned in pieces yet
    /// (<see cref="Rule"/>: "every column until the map is planned", "the box round a road not worked out").
    /// </summary>
    public sealed class ColumnHoldSet : IColumnHold
    {
        private readonly Dictionary<Vector2Int, int> pieces = new Dictionary<Vector2Int, int>();

        /// <summary>A wider rule asked as well as the counts; null for none.</summary>
        public Func<Vector2Int, bool> Rule { get; set; }

        /// <summary>Columns with a piece still to write.</summary>
        public int Count => pieces.Count;

        /// <summary>Off, it holds nothing (its owner has stopped writing) but keeps its counts.</summary>
        public bool Enabled { get; set; } = true;

        public bool Holds(Vector2Int column)
        {
            return Enabled && (pieces.ContainsKey(column) || (Rule != null && Rule(column)));
        }

        /// <summary>Holds the columns under a rectangle of ground (x, z in metres) until <see cref="Remove"/> is called with the same one.</summary>
        public void Add(WorldGrid grid, Rect ground)
        {
            Change(grid, ground, 1);
        }

        public void Remove(WorldGrid grid, Rect ground)
        {
            Change(grid, ground, -1);
        }

        public void Clear()
        {
            pieces.Clear();
        }

        /// <summary>The columns a rectangle of ground lies in: lowest and highest, both included.</summary>
        public static void Columns(WorldGrid grid, Rect ground, out Vector2Int min, out Vector2Int max)
        {
            Vector3Int low = grid.WorldToChunk(new Vector3(ground.xMin, 0f, ground.yMin));
            Vector3Int high = grid.WorldToChunk(new Vector3(ground.xMax, 0f, ground.yMax));
            min = new Vector2Int(low.x, low.z);
            max = new Vector2Int(high.x, high.z);
        }

        private void Change(WorldGrid grid, Rect ground, int by)
        {
            Columns(grid, ground, out Vector2Int min, out Vector2Int max);
            for (int z = min.y; z <= max.y; z++)
            {
                for (int x = min.x; x <= max.x; x++)
                {
                    var column = new Vector2Int(x, z);
                    pieces.TryGetValue(column, out int count);
                    count += by;
                    if (count > 0)
                    {
                        pieces[column] = count;
                    }
                    else
                    {
                        pieces.Remove(column);
                    }
                }
            }
        }
    }
}
