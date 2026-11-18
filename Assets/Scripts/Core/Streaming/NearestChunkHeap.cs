using System.Collections.Generic;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// Takes chunks from a list nearest first without sorting all of it (GL759): a frame starts a few mesh jobs out of
    /// what can be tens of thousands of waiting chunks, so only the few it takes pay for their order. Builds a binary heap
    /// in the list itself, which is left in no useful order.
    /// </summary>
    public struct NearestChunkHeap
    {
        private readonly List<Vector3Int> items;
        private readonly Vector3Int centre;
        private int count;

        public NearestChunkHeap(List<Vector3Int> items, Vector3Int centre)
        {
            this.items = items;
            this.centre = centre;
            count = items.Count;
            for (int i = count / 2 - 1; i >= 0; i--)
            {
                SiftDown(i);
            }
        }

        /// <summary>Chunks not taken yet.</summary>
        public int Remaining => count;

        /// <summary>The nearest chunk not taken yet; false when none are left.</summary>
        public bool TryTake(out Vector3Int coord)
        {
            if (count == 0)
            {
                coord = default;
                return false;
            }
            coord = items[0];
            count--;
            items[0] = items[count];
            SiftDown(0);
            return true;
        }

        private void SiftDown(int at)
        {
            Vector3Int moving = items[at];
            int distance = DistanceSquared(moving);
            while (true)
            {
                int child = at * 2 + 1;
                if (child >= count)
                {
                    break;
                }
                int childDistance = DistanceSquared(items[child]);
                if (child + 1 < count)
                {
                    int otherDistance = DistanceSquared(items[child + 1]);
                    if (otherDistance < childDistance)
                    {
                        child++;
                        childDistance = otherDistance;
                    }
                }
                if (distance <= childDistance)
                {
                    break;
                }
                items[at] = items[child];
                at = child;
            }
            items[at] = moving;
        }

        private int DistanceSquared(Vector3Int coord)
        {
            Vector3Int d = coord - centre;
            return d.x * d.x + d.y * d.y + d.z * d.z;
        }
    }
}
