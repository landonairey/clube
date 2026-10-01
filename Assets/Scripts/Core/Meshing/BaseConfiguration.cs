namespace Clube.Core
{
    /// <summary>
    /// The 15 shapes all 256 Marching Cubes cases reduce to, once rotations of
    /// the cube and swapping solid with empty are treated as the same case.
    /// Numbered by solid-corner count, then shape; figures in other sources may
    /// number them differently. Descriptions count solid corners on whichever
    /// side (solid or empty) has four or fewer.
    /// </summary>
    public enum BaseConfiguration
    {
        /// <summary>No solid corners (or all eight): no surface.</summary>
        Empty = 0,

        /// <summary>One corner.</summary>
        SingleCorner = 1,

        /// <summary>Two corners joined by an edge.</summary>
        Edge = 2,

        /// <summary>Two corners diagonally across one face.</summary>
        FaceDiagonal = 3,

        /// <summary>Two corners diagonally across the whole cube.</summary>
        BodyDiagonal = 4,

        /// <summary>Three corners of one face, in an L.</summary>
        LOnFace = 5,

        /// <summary>Two corners joined by an edge, plus one corner touching neither.</summary>
        EdgeAndFarCorner = 6,

        /// <summary>Three corners, each pair diagonal across a face.</summary>
        ThreeFaceDiagonals = 7,

        /// <summary>All four corners of one face.</summary>
        Face = 8,

        /// <summary>One corner and its three neighbours.</summary>
        Star = 9,

        /// <summary>Two parallel edges on opposite sides of the cube.</summary>
        OppositeEdges = 10,

        /// <summary>Four corners in a twisted path along three edges.</summary>
        Zigzag = 11,

        /// <summary>Three corners in an L, plus one corner touching none of them.</summary>
        LAndFarCorner = 12,

        /// <summary>Four corners, no two joined by an edge.</summary>
        Alternating = 13,

        /// <summary>The mirror image of <see cref="Zigzag"/>; no rotation turns one into the other.</summary>
        ZigzagMirrored = 14,
    }
}
