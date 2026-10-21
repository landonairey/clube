namespace Clube.Game
{
    /// <summary>How the tool cursor shows what a hit reaches (GL95), switchable in play to compare them.</summary>
    public enum ToolCursorStyle
    {
        /// <summary>A small dot in the middle of the screen, nothing on the ground.</summary>
        Dot,

        /// <summary>The dot, and a black outline of the reached voxels.</summary>
        DotOutline,

        /// <summary>The surface's triangles inside the reached voxels, shaded and traced (GL24).</summary>
        Faces,

        /// <summary>Soft glows on the reached voxels' solid corners, seen through the ground.</summary>
        GlowCorners,

        /// <summary>A translucent sphere over the area of impact.</summary>
        ImpactSphere,
    }
}
