namespace Clube.Core
{
    /// <summary>
    /// How an ore's journal sketch is filled in (GL627, GL628), so two dark ores don't read as the same drawing. Data on
    /// the <see cref="VoxelMaterial"/>; the drawing itself is the game's (<c>OreSketch</c>).
    /// </summary>
    public enum OreSketchStyle
    {
        /// <summary>Specks of the ore's colour in a pale grey rock: most ores.</summary>
        Speckled,

        /// <summary>Angular crystals of the ore's colour, each with a bright facet: magnetite's octahedra.</summary>
        Crystals,

        /// <summary>Filled in nearly solid with the ore's colour: coal, black right through like its seam.</summary>
        Solid,
    }
}
