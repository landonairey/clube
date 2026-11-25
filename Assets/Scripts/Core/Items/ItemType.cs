namespace Clube.Core
{
    /// <summary>
    /// What kind of thing an item is, as its description says ("Type: Earth"). A first list (list inventory prototype);
    /// more types come as items need them. Append new ones at the end: assets store the number.
    /// </summary>
    public enum ItemType
    {
        /// <summary>Dug or mined: stone, dirt, clay, sand, ore, coal, and what's made straight from them (brick).</summary>
        Earth,
        /// <summary>Water and other liquids.</summary>
        Liquid,
        /// <summary>From plants and animals: wood, boards, leaves, fibre, hides, wool.</summary>
        Organism,
        /// <summary>Tools, torches, buckets and stations.</summary>
        Tool,
        /// <summary>Weapons (a spear).</summary>
        Weapon,
        /// <summary>Armor.</summary>
        Armor,
        /// <summary>Clothing.</summary>
        Clothing,
        /// <summary>Jewelry and gems (pearls).</summary>
        Jewelry,
        /// <summary>Smelted metal: buns and ingots.</summary>
        Smelt,
        /// <summary>Eaten or drunk for an effect: food, coffee.</summary>
        Consumable,
    }
}
