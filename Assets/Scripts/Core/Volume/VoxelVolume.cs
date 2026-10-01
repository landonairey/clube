using System;
using System.Collections.Generic;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// How much of a single voxel (unit cube, volume 1) is solid. Lab tools compare
    /// the three measures (V11, V12, V14); mining will need them later (I5), which
    /// is why this lives in the core.
    /// </summary>
    public static class VoxelVolume
    {
        private const float PointTolerance = 1e-5f;

        // Corner offsets of a cube face in counter-clockwise order, seen from outside,
        // in that face's (u, v) coordinates. See FacePoint.
        private static readonly Vector2Int[] FaceCornersCcw =
        {
            new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(1, 1), new Vector2Int(0, 1),
        };

        /// <summary>
        /// V11: the mean corner density, clamped to 0-1. Cheap, ignores the iso level
        /// and the surface shape, so it is only an estimate.
        /// </summary>
        public static float Approximate(IReadOnlyList<float> cornerValues)
        {
            float sum = 0f;
            for (int corner = 0; corner < MarchingCubes.CornerCount; corner++)
            {
                sum += Mathf.Clamp01(cornerValues[corner]);
            }
            return sum / MarchingCubes.CornerCount;
        }

        /// <summary>
        /// V12: the exact volume (fraction of the unit cube) of the solid that Marching
        /// Cubes builds for these corners: the sum of <see cref="Tetrahedralise"/>.
        /// </summary>
        public static float Exact(IReadOnlyList<float> cornerValues, float isoLevel, IEdgeVertexPlacer placer)
        {
            var tetrahedra = new List<Tetrahedron>();
            Tetrahedralise(cornerValues, isoLevel, placer, tetrahedra);

            float volume = 0f;
            foreach (Tetrahedron tetrahedron in tetrahedra)
            {
                volume += tetrahedron.SignedVolume;
            }
            return volume;
        }

        /// <summary>
        /// Splits the solid into tetrahedra whose signed volumes sum to its volume.
        /// </summary>
        /// <remarks>
        /// The Marching Cubes surface alone is open, so the solid is closed with the
        /// parts of the cube faces that lie inside it. Every boundary triangle is then
        /// joined to a reference point, corner c0 (the origin), to form a tetrahedron.
        /// With c0 as the apex, the three faces through c0 (x = 0, y = 0, z = 0) give
        /// flat, zero-volume tetrahedra, so only the far faces (x = 1, y = 1, z = 1)
        /// need their inside parts added.
        /// </remarks>
        public static void Tetrahedralise(
            IReadOnlyList<float> cornerValues,
            float isoLevel,
            IEdgeVertexPlacer placer,
            List<Tetrahedron> output)
        {
            output.Clear();

            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            MarchingCubes.Polygonise(
                cornerValues, isoLevel, Vector3.zero, 1f, placer, new FlatVertexWriter(vertices), triangles);

            for (int i = 0; i < triangles.Count; i += 3)
            {
                output.Add(new Tetrahedron(
                    Vector3.zero, vertices[triangles[i]], vertices[triangles[i + 1]], vertices[triangles[i + 2]],
                    TetrahedronSource.SurfaceTriangle));
            }

            for (int axis = 0; axis < 3; axis++)
            {
                AddFarFaceTetrahedra(axis, cornerValues, isoLevel, placer, vertices, triangles, output);
            }
        }

        /// <summary>
        /// V14 reference: the fraction of random sample points where the trilinear
        /// interpolation of the corner densities is at or above the iso level. That is
        /// the smooth field Marching Cubes approximates with flat triangles. Seeded,
        /// so the same inputs always give the same estimate.
        /// </summary>
        public static float SampleTrilinear(IReadOnlyList<float> cornerValues, float isoLevel, int sampleCount, int seed = 12345)
        {
            if (sampleCount <= 0)
            {
                return 0f;
            }

            var random = new System.Random(seed);
            int inside = 0;
            for (int i = 0; i < sampleCount; i++)
            {
                var point = new Vector3((float)random.NextDouble(), (float)random.NextDouble(), (float)random.NextDouble());
                if (Trilinear(cornerValues, point) >= isoLevel)
                {
                    inside++;
                }
            }
            return (float)inside / sampleCount;
        }

        /// <summary>The density at a point in the unit cube, blended from the 8 corners.</summary>
        public static float Trilinear(IReadOnlyList<float> cornerValues, Vector3 point)
        {
            float density = 0f;
            for (int corner = 0; corner < MarchingCubes.CornerCount; corner++)
            {
                Vector3Int offset = MarchingCubes.CornerOffset(corner);
                float weight =
                    (offset.x == 1 ? point.x : 1f - point.x) *
                    (offset.y == 1 ? point.y : 1f - point.y) *
                    (offset.z == 1 ? point.z : 1f - point.z);
                density += weight * cornerValues[corner];
            }
            return density;
        }

        // Adds tetrahedra for the inside part of the face where coordinate `axis` is 1.
        // Its outline is built from oriented segments, then walked into loops:
        // - along the face's perimeter, counter-clockwise seen from outside, the stretches
        //   between solid corners and the surface crossings;
        // - every surface triangle edge lying in the face, reversed (on a closed solid,
        //   neighbouring faces run along a shared edge in opposite directions).
        // This follows whatever the triangle table did on ambiguous faces, so the closed
        // solid always matches the mesh.
        private static void AddFarFaceTetrahedra(
            int axis,
            IReadOnlyList<float> cornerValues,
            float isoLevel,
            IEdgeVertexPlacer placer,
            List<Vector3> vertices,
            List<int> triangles,
            List<Tetrahedron> output)
        {
            var segments = new List<(Vector3 From, Vector3 To)>();

            for (int i = 0; i < FaceCornersCcw.Length; i++)
            {
                Vector3 p = FacePoint(axis, FaceCornersCcw[i]);
                Vector3 q = FacePoint(axis, FaceCornersCcw[(i + 1) % FaceCornersCcw.Length]);
                float valueP = cornerValues[CornerAt(p)];
                float valueQ = cornerValues[CornerAt(q)];
                bool solidP = valueP >= isoLevel;
                bool solidQ = valueQ >= isoLevel;

                if (solidP && solidQ)
                {
                    AddSegment(segments, p, q);
                }
                else if (solidP != solidQ)
                {
                    Vector3 crossing = placer.Place(p, q, valueP, valueQ, isoLevel);
                    AddSegment(segments, solidP ? p : crossing, solidP ? crossing : q);
                }
            }

            for (int i = 0; i < triangles.Count; i += 3)
            {
                for (int k = 0; k < 3; k++)
                {
                    Vector3 from = vertices[triangles[i + k]];
                    Vector3 to = vertices[triangles[i + (k + 1) % 3]];
                    if (OnFarFace(from, axis) && OnFarFace(to, axis))
                    {
                        AddSegment(segments, to, from);
                    }
                }
            }

            foreach (List<Vector3> loop in WalkLoops(segments))
            {
                // Face regions inside a cube are convex, so a fan from the first point works.
                for (int i = 1; i + 1 < loop.Count; i++)
                {
                    output.Add(new Tetrahedron(Vector3.zero, loop[0], loop[i], loop[i + 1], TetrahedronSource.CubeFace));
                }
            }
        }

        // Face `axis` = 1, with (u, v) along the next two axes in cyclic order, so
        // u x v points out of the cube and counter-clockwise in (u, v) faces outwards.
        private static Vector3 FacePoint(int axis, Vector2Int uv)
        {
            var point = new Vector3();
            point[axis] = 1f;
            point[(axis + 1) % 3] = uv.x;
            point[(axis + 2) % 3] = uv.y;
            return point;
        }

        private static int CornerAt(Vector3 position)
        {
            var offset = Vector3Int.RoundToInt(position);
            for (int corner = 0; corner < MarchingCubes.CornerCount; corner++)
            {
                if (MarchingCubes.CornerOffset(corner) == offset)
                {
                    return corner;
                }
            }
            throw new ArgumentOutOfRangeException(nameof(position), position, "Not a cube corner.");
        }

        private static bool OnFarFace(Vector3 point, int axis)
        {
            return Mathf.Abs(point[axis] - 1f) < PointTolerance;
        }

        // Skips zero-length segments, and cancels a segment against its exact reverse
        // (a triangle edge shared by two triangles lying in the face plane).
        private static void AddSegment(List<(Vector3 From, Vector3 To)> segments, Vector3 from, Vector3 to)
        {
            if (Same(from, to))
            {
                return;
            }

            int reverse = segments.FindIndex(s => Same(s.From, to) && Same(s.To, from));
            if (reverse >= 0)
            {
                segments.RemoveAt(reverse);
                return;
            }

            segments.Add((from, to));
        }

        private static IEnumerable<List<Vector3>> WalkLoops(List<(Vector3 From, Vector3 To)> segments)
        {
            var remaining = new List<(Vector3 From, Vector3 To)>(segments);
            while (remaining.Count > 0)
            {
                var loop = new List<Vector3> { remaining[0].From };
                Vector3 current = remaining[0].To;
                remaining.RemoveAt(0);

                while (!Same(current, loop[0]))
                {
                    int next = remaining.FindIndex(s => Same(s.From, current));
                    if (next < 0)
                    {
                        throw new InvalidOperationException("Face outline does not close; the surface and cube faces disagree.");
                    }

                    loop.Add(current);
                    current = remaining[next].To;
                    remaining.RemoveAt(next);
                }

                yield return loop;
            }
        }

        private static bool Same(Vector3 a, Vector3 b)
        {
            return (a - b).sqrMagnitude < PointTolerance * PointTolerance;
        }
    }
}
