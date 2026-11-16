namespace Clube.Core
{
    /// <summary>
    /// What an item's inventory icon is a picture of (GL612): icons are rendered from low-poly models, and this says
    /// which model. <see cref="Auto"/> picks by the item's kind (<see cref="ItemIconShapes.Resolve"/>), so a new item
    /// gets an icon without anyone drawing one; the rest are set on the item's asset where the kind isn't enough.
    /// </summary>
    public enum ItemIconShape
    {
        /// <summary>By kind: a tool is its model, a placeable its prefab, a material's drop a chunk or mound of it.</summary>
        Auto = 0,

        /// <summary>The item's own model: a tool, torch or bucket as held, a placeable's prefab.</summary>
        Model = 1,

        /// <summary>A faceted lump, as the loose stones on the ground: stone, ores, clay, coal.</summary>
        Chunk = 2,

        /// <summary>A low heap of loose stuff: dirt, sand.</summary>
        Mound = 3,

        /// <summary>A few small lumps together: crushed ore and stone.</summary>
        Gravel = 4,

        /// <summary>A short length of trunk, bark round it and rings on its ends.</summary>
        Log = 5,

        /// <summary>A sprig of a few leaves.</summary>
        Leaves = 6,

        /// <summary>A small stack of sawn planks.</summary>
        Boards = 7,

        /// <summary>A cast bar of metal.</summary>
        Ingot = 8,

        /// <summary>A rough dome of metal, as a smelt leaves it.</summary>
        Bun = 9,

        /// <summary>A fired brick.</summary>
        Brick = 10,

        /// <summary>A flat hide, legs and all.</summary>
        Hide = 11,

        /// <summary>A soft tuft of wool or fur.</summary>
        Tuft = 12,

        /// <summary>A thin stick.</summary>
        Stick = 13,

        /// <summary>No model: the icon stays the picture already on the asset (hand-painted).</summary>
        Painted = 14,
    }
}
