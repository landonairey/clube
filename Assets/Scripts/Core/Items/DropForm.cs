namespace Clube.Core
{
    /// <summary>What a dropped item turns into (GL610, GL611); set on its <see cref="ItemDefinition"/>.</summary>
    public enum DropForm
    {
        /// <summary>As its kind of item does: see <see cref="ItemDefinition.DropsAs"/>.</summary>
        ByKind,

        /// <summary>Into the ground as its material, piling up (stone, ore, dirt).</summary>
        Pile,

        /// <summary>Its own 3D model, lying on the ground with physics (tools).</summary>
        Model,

        /// <summary>A brown canvas bag holding it: large equipment (a campfire, a furnace) and anything very small.</summary>
        Bag,
    }
}
