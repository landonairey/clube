using System.Collections.Generic;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>A point on the terrain mesh and the surface's facing there.</summary>
    public readonly struct SurfacePoint
    {
        public SurfacePoint(Vector3 position, Vector3 normal)
        {
            Position = position;
            Normal = normal;
        }

        /// <summary>Relative to the world origin.</summary>
        public Vector3 Position { get; }

        /// <summary>Unit length, pointing out of the ground into the air.</summary>
        public Vector3 Normal { get; }
    }

    /// <summary>
    /// The mesh vertices a density sample controls (GL4): one on each of its edges that runs
    /// from it to an air sample, placed exactly as the mesher places them (V3). Every marching
    /// cubes edge joins two face-neighbouring samples, so these are the triangle corners that
    /// move when the sample's density changes, e.g. under a tool. A buried sample has none.
    /// </summary>
    /// <remarks>
    /// Normals come from the density gradient (central differences at both ends of the edge,
    /// blended like the vertex), the usual marching cubes normal, so a marker can lie flat on
    /// the surface without needing the mesh.
    /// </remarks>
    public static class SurfacePoints
    {
        private static readonly Vector3Int[] Faces =
        {
            Vector3Int.right, Vector3Int.left, Vector3Int.up, Vector3Int.down,
            new Vector3Int(0, 0, 1), new Vector3Int(0, 0, -1),
        };

        /// <summary>Adds the mesh vertices <paramref name="sample"/> controls to <paramref name="points"/>.</summary>
        public static void ForSample(World world, Vector3Int sample, ChunkMeshSettings settings, List<SurfacePoint> points)
        {
            float? density = world.GetDensity(sample);
            if (!density.HasValue || density.Value < settings.IsoLevel)
            {
                return;
            }

            IEdgeVertexPlacer placer = EdgeVertexPlacers.For(settings.EdgePlacement);
            Vector3 position = world.Grid.SampleToWorld(sample);
            Vector3 gradient = Gradient(world, sample, density.Value);
            foreach (Vector3Int face in Faces)
            {
                Vector3Int neighbour = sample + face;
                float? neighbourDensity = world.GetDensity(neighbour);
                if (!neighbourDensity.HasValue || neighbourDensity.Value >= settings.IsoLevel)
                {
                    continue;
                }

                Vector3 neighbourPosition = world.Grid.SampleToWorld(neighbour);
                Vector3 vertex = placer.Place(position, neighbourPosition, density.Value, neighbourDensity.Value, settings.IsoLevel);
                float t = Vector3.Distance(position, vertex) / Vector3.Distance(position, neighbourPosition);
                Vector3 blended = Vector3.Lerp(gradient, Gradient(world, neighbour, neighbourDensity.Value), t);
                // Density grows into the ground, so the surface faces down the gradient; an
                // edge with no gradient (all neighbours equal) faces along itself.
                Vector3 normal = blended.sqrMagnitude > 1e-12f ? -blended.normalized : (Vector3)face;
                points.Add(new SurfacePoint(vertex, normal));
            }
        }

        // Central differences; an unloaded neighbour counts as equal to the sample.
        private static Vector3 Gradient(World world, Vector3Int sample, float density)
        {
            return new Vector3(
                At(world, sample + Vector3Int.right, density) - At(world, sample + Vector3Int.left, density),
                At(world, sample + Vector3Int.up, density) - At(world, sample + Vector3Int.down, density),
                At(world, sample + new Vector3Int(0, 0, 1), density) - At(world, sample + new Vector3Int(0, 0, -1), density));
        }

        private static float At(World world, Vector3Int sample, float fallback)
        {
            return world.GetDensity(sample) ?? fallback;
        }
    }
}
