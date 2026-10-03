namespace Clube.Core
{
    /// <summary>How a <see cref="TerrainBrush"/>'s effect fades from its centre (K16).</summary>
    public enum BrushFalloff
    {
        /// <summary>Full effect out to the radius: at full strength it carves or builds an exact sphere.</summary>
        Hard,

        /// <summary>Fades smoothly to nothing at the radius, for gradual sculpting.</summary>
        Smooth,
    }
}
