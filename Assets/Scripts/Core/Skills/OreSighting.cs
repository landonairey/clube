using System.Collections.Generic;

namespace Clube.Core
{
    /// <summary>
    /// When an ore counts as discovered (GL796): the first time an inventory entry of that ore type is seen, mined as its
    /// own stack by a tool aimed at it or popped out as a separate item by refining (dirt picked over into cassiterite),
    /// or its crushed form. A stack of something else that only holds some of the ore (a shovel load of dirt) shows no ore:
    /// it's a stack of poorer dirt.
    /// </summary>
    public static class OreSighting
    {
        // An ore and what it breaks down into (cracked, loose), a few steps at most.
        private const int MaxStages = 4;

        /// <summary>The ore an inventory entry of <paramref name="item"/> shows, among <paramref name="ores"/>; null when none.</summary>
        public static VoxelMaterial OreShownBy(ItemDefinition item, IReadOnlyList<VoxelMaterial> ores)
        {
            if (item == null || ores == null)
            {
                return null;
            }
            foreach (VoxelMaterial ore in ores)
            {
                VoxelMaterial stage = ore;
                for (int step = 0; step < MaxStages && stage != null; step++)
                {
                    ItemDefinition drop = stage.Drop;
                    if (drop != null && (item == drop || item.PurityAs == drop))
                    {
                        return ore;
                    }
                    stage = stage.BreaksInto;
                }
            }
            return null;
        }
    }
}
