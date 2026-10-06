using System.Collections.Generic;

namespace Clube.Core
{
    /// <summary>
    /// Reuses the byte arrays chunks keep their samples in (single-byte densities, M12, and
    /// material ids, M10), so a streamed world that loads and unloads chunks as the player moves
    /// stops allocating once it has warmed up (K35, P2). Each new array is garbage for the
    /// collector to find later; a collection over the Editor's large heap can stall a frame for
    /// several milliseconds.
    /// </summary>
    /// <remarks>
    /// Main thread only, like the chunks themselves. Arrays come back through
    /// <see cref="Chunk.Release"/>, which the <see cref="World"/> calls when it drops a chunk; a
    /// chunk that is never released just leaves its arrays to the garbage collector.
    /// </remarks>
    public static class VoxelArrayPool
    {
        // Enough spare arrays of one size for a large render distance's worth of churn.
        private const int MaxFreePerLength = 2048;

        private static readonly Dictionary<int, Stack<byte[]>> Free = new Dictionary<int, Stack<byte[]>>();

        /// <summary>Spare arrays waiting to be reused, of every length.</summary>
        public static int FreeCount
        {
            get
            {
                int count = 0;
                foreach (Stack<byte[]> stack in Free.Values)
                {
                    count += stack.Count;
                }
                return count;
            }
        }

        /// <summary>An array of exactly <paramref name="length"/> bytes, all zero: a spare one if there is one.</summary>
        public static byte[] Rent(int length)
        {
            if (Free.TryGetValue(length, out Stack<byte[]> stack) && stack.Count > 0)
            {
                byte[] array = stack.Pop();
                System.Array.Clear(array, 0, array.Length);
                return array;
            }
            return new byte[length];
        }

        /// <summary>Gives an array back for reuse. The caller must not touch it afterwards.</summary>
        public static void Return(byte[] array)
        {
            if (array == null)
            {
                return;
            }
            if (!Free.TryGetValue(array.Length, out Stack<byte[]> stack))
            {
                stack = new Stack<byte[]>();
                Free.Add(array.Length, stack);
            }
            if (stack.Count < MaxFreePerLength)
            {
                stack.Push(array);
            }
        }

        /// <summary>Drops every spare array (e.g. after the chunk size changes, when the old size won't come back).</summary>
        public static void Clear()
        {
            Free.Clear();
        }
    }
}
