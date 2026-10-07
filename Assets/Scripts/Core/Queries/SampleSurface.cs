using System.Collections.Generic;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// Where a solid sample shows on the mesh: the surface vertices it gives, one on each of
    /// its six edges that runs to an air sample, placed as the mesher places them (V3). Every
    /// marching cubes edge joins two face-neighbouring samples, so these are exactly the
    /// polygon corners that move when the sample changes. A buried sample gives none.
    /// </summary>
    public static class SampleSurface
    {
        private static readonly Vector3Int[] Faces =
        {
            Vector3Int.right, Vector3Int.left, Vector3Int.up, Vector3Int.down,
            new Vector3Int(0, 0, 1), new Vector3Int(0, 0, -1),
        };

        /// <summary>Adds the sample's surface vertices to <paramref name="vertices"/>, relative to the world origin.</summary>
        public static void Vertices(World world, Vector3Int sample, ChunkMeshSettings settings, List<Vector3> vertices)
        {
            float? density = world.GetDensity(sample);
            if (!density.HasValue || density.Value < settings.IsoLevel)
            {
                return;
            }

            IEdgeVertexPlacer placer = EdgeVertexPlacers.For(settings.EdgePlacement);
            Vector3 position = world.Grid.SampleToWorld(sample);
            foreach (Vector3Int face in Faces)
            {
                Vector3Int neighbour = sample + face;
                float? neighbourDensity = world.GetDensity(neighbour);
                if (neighbourDensity.HasValue && neighbourDensity.Value < settings.IsoLevel)
                {
                    vertices.Add(placer.Place(
                        position, world.Grid.SampleToWorld(neighbour), density.Value, neighbourDensity.Value, settings.IsoLevel));
                }
            }
        }
    }
}
