using System;
using Clube.Core;

namespace Clube.Debug
{
    /// <summary>
    /// Human-readable names and descriptions for <see cref="BaseConfiguration"/>,
    /// shared by the Inspector readout now and on-screen lab UI later (V19).
    /// </summary>
    public static class BaseConfigurationText
    {
        public static string Name(BaseConfiguration configuration)
        {
            switch (configuration)
            {
                case BaseConfiguration.Empty: return "Empty";
                case BaseConfiguration.SingleCorner: return "Single corner";
                case BaseConfiguration.Edge: return "Edge";
                case BaseConfiguration.FaceDiagonal: return "Face diagonal";
                case BaseConfiguration.BodyDiagonal: return "Body diagonal";
                case BaseConfiguration.LOnFace: return "L on a face";
                case BaseConfiguration.EdgeAndFarCorner: return "Edge + far corner";
                case BaseConfiguration.ThreeFaceDiagonals: return "Three face diagonals";
                case BaseConfiguration.Face: return "Face";
                case BaseConfiguration.Star: return "Star";
                case BaseConfiguration.OppositeEdges: return "Opposite edges";
                case BaseConfiguration.Zigzag: return "Zigzag";
                case BaseConfiguration.LAndFarCorner: return "L + far corner";
                case BaseConfiguration.Alternating: return "Alternating";
                case BaseConfiguration.ZigzagMirrored: return "Zigzag (mirrored)";
                default: throw new ArgumentOutOfRangeException(nameof(configuration), configuration, null);
            }
        }

        /// <summary>Describes the solid corners, counting whichever side has four or fewer.</summary>
        public static string Description(BaseConfiguration configuration)
        {
            switch (configuration)
            {
                case BaseConfiguration.Empty: return "No solid corners (or all eight): no surface.";
                case BaseConfiguration.SingleCorner: return "One corner.";
                case BaseConfiguration.Edge: return "Two corners joined by an edge.";
                case BaseConfiguration.FaceDiagonal: return "Two corners diagonally across one face.";
                case BaseConfiguration.BodyDiagonal: return "Two corners diagonally across the whole cube.";
                case BaseConfiguration.LOnFace: return "Three corners of one face, in an L.";
                case BaseConfiguration.EdgeAndFarCorner: return "Two corners joined by an edge, plus one corner touching neither.";
                case BaseConfiguration.ThreeFaceDiagonals: return "Three corners, each pair diagonal across a face.";
                case BaseConfiguration.Face: return "All four corners of one face.";
                case BaseConfiguration.Star: return "One corner and its three neighbours.";
                case BaseConfiguration.OppositeEdges: return "Two parallel edges on opposite sides of the cube.";
                case BaseConfiguration.Zigzag: return "Four corners in a twisted path along three edges.";
                case BaseConfiguration.LAndFarCorner: return "Three corners in an L, plus one corner touching none of them.";
                case BaseConfiguration.Alternating: return "Four corners, no two joined by an edge.";
                case BaseConfiguration.ZigzagMirrored: return "Mirror image of Zigzag; no rotation turns one into the other.";
                default: throw new ArgumentOutOfRangeException(nameof(configuration), configuration, null);
            }
        }
    }
}
