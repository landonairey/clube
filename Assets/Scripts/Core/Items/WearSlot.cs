namespace Clube.Core
{
    /// <summary>
    /// Where on the body an item is worn (list inventory prototype, items 18 and 19): the equipment panel's body slots.
    /// None for anything that isn't worn. Append new ones at the end: assets store the number.
    /// </summary>
    public enum WearSlot
    {
        None,
        Head,
        Chest,
        Arms,
        Hands,
        Legs,
        Feet,
    }
}
