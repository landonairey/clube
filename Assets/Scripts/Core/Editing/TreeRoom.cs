using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// The room a tree can take up round its root (GL652): how far any of it reaches sideways and how high. Whoever cuts
    /// the trees standing on a piece of ground (a road's piece, a settlement's tile) waits until the chunks within this
    /// room of the piece are loaded. A tree is stamped into every chunk it reaches, so cut while one of them was still
    /// to load, the part in that chunk arrived afterwards and hung in the air.
    /// </summary>
    public readonly struct TreeRoom
    {
        public TreeRoom(float spread, float height)
        {
            Spread = Mathf.Max(0f, spread);
            Height = Mathf.Max(0f, height);
        }

        /// <summary>Metres any part of a tree reaches sideways from its root.</summary>
        public readonly float Spread;

        /// <summary>Metres a tree stands over its root, crown and all.</summary>
        public readonly float Height;

        /// <summary>Room for a tree where the world's own trees are not known: a tall one.</summary>
        public static TreeRoom Default => new TreeRoom(10f, 20f);

        /// <summary>No trees: nothing to wait for.</summary>
        public static TreeRoom None => new TreeRoom(0f, 0f);

        /// <summary>The room the tallest tree of a world's tree settings takes; none where no trees grow.</summary>
        public static TreeRoom Of(TreeGeneration trees)
        {
            if (trees == null || !trees.HasTrees)
            {
                return None;
            }
            // A crown stands a leaf cluster over the trunk's top.
            return new TreeRoom(trees.MaxSpread, trees.TallestHeight + trees.MaxSpread * 0.5f);
        }
    }
}
