using System;

namespace Clube.Core
{
    /// <summary>Maps an <see cref="EdgePlacement"/> setting to its placer strategy (V3).</summary>
    public static class EdgeVertexPlacers
    {
        public static IEdgeVertexPlacer For(EdgePlacement placement)
        {
            switch (placement)
            {
                case EdgePlacement.Interpolated:
                    return InterpolatedEdgePlacer.Instance;
                case EdgePlacement.Midpoint:
                    return MidpointEdgePlacer.Instance;
                default:
                    throw new ArgumentOutOfRangeException(nameof(placement), placement, null);
            }
        }
    }
}
