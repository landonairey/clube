using System.Collections.Generic;
using Clube.Core;

namespace Clube.Game
{
    /// <summary>
    /// What each crate of a cart or vessel holds (GL726): the load of one trip, or nothing. A crate is filled only when
    /// the trip it should hold changes, so goods taken out by the player stay taken until the next load, and a crate
    /// filled while loading at a stop is the same one the vehicle leaves with.
    /// </summary>
    public sealed class CrateCargo
    {
        private const int Empty = int.MinValue, Unset = int.MinValue + 1;

        private readonly IReadOnlyList<StorageChest> crates;
        private readonly int[] holds;

        public CrateCargo(IReadOnlyList<StorageChest> crates)
        {
            this.crates = crates;
            holds = new int[crates.Count];
            for (int i = 0; i < holds.Length; i++)
            {
                holds[i] = Unset;
            }
        }

        public int Count => crates.Count;

        /// <summary>
        /// Crates from <paramref name="first"/> up to (not with) <paramref name="last"/> hold the load of
        /// <paramref name="trip"/> (a few kinds of <paramref name="goods"/>, drawn from <paramref name="seed"/>); the rest are empty.
        /// </summary>
        public void Show(int first, int last, int trip, IReadOnlyList<ItemDefinition> goods, int seed)
        {
            for (int i = 0; i < crates.Count; i++)
            {
                StorageChest crate = crates[i];
                if (crate == null || crate.Storage == null)
                {
                    continue;
                }
                bool full = i >= first && i < last;
                int wanted = full ? trip : Empty;
                if (holds[i] == wanted)
                {
                    continue;
                }
                holds[i] = wanted;
                StorageText.Fill(crate.Storage, "");
                if (full)
                {
                    CargoLoad.FillOne(crate, goods, new System.Random(unchecked(seed * 31 + trip * 131 + i * 7)));
                }
            }
        }
    }
}
