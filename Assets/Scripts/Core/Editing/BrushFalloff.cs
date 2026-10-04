namespace Clube.Core
{
    /// <summary>Which samples a <see cref="TerrainBrush"/> application changes (K16).</summary>
    public enum BrushFalloff
    {
        /// <summary>Every sample within the radius gains or loses the brush strength.</summary>
        Hard,

        /// <summary>
        /// Only the surface layer within the radius changes, so holding the brush digs
        /// down or piles up layer by layer: adding fills samples next to fully solid ones,
        /// removing empties samples next to fully empty ones.
        /// </summary>
        Soft,
    }
}
