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

        // Tetrahedra smaller than this are flat (their base lies in a face through
        // the apex) and are dropped.
        private const float DegenerateVolume = 1e-7f;

        // Corner offsets of a cube face in counter-clockwise order in that face's
        // (u, v) coordinates. See FacePoint.
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
        /// Splits the solid into tetrahedra that fill it exactly, so their volumes sum
        /// to the solid's volume.
        /// </summary>
        /// <remarks>
        /// <list type="number">
        /// <item>The Marching Cubes surface alone is open, so the solid is closed with
        /// the parts of the six cube faces that lie inside it.</item>
        /// <item>The closed boundary is split into separate pieces (e.g. two
        /// disconnected corners on an ambiguous face).</item>
        /// <item>Each piece is fanned from one of its own solid corners: every boundary
        /// triangle plus that corner makes a tetrahedron. Triangles in a face through
        /// the corner give flat tetrahedra and are dropped, so a single solid corner is
        /// exactly one tetrahedron and an edge (a prism) is three. The corner chosen is
        /// the one giving no inside-out (negative) tetrahedra and the fewest pieces.</item>
        /// </list>
        /// </remarks>
        public static void Tetrahedralise(
            IReadOnlyList<float> cornerValues,
            float isoLevel,
            IEdgeVertexPlacer placer,
            List<Tetrahedron> output)
        {
            output.Clear();

            List<BoundaryTriangle> boundary = BuildClosedBoundary(cornerValues, isoLevel, placer);
            foreach (List<BoundaryTriangle> piece in SplitIntoPieces(boundary))
            {
                Vector3 apex = ChooseApex(piece, cornerValues, isoLevel);
                foreach (BoundaryTriangle triangle in piece)
                {
                    var tetrahedron = new Tetrahedron(apex, triangle.A, triangle.B, triangle.C, triangle.Source);
                    if (Mathf.Abs(tetrahedron.SignedVolume) > DegenerateVolume)
                    {
                        output.Add(tetrahedron);
                    }
                }
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

        private readonly struct BoundaryTriangle
        {
            public BoundaryTriangle(Vector3 a, Vector3 b, Vector3 c, TetrahedronSource source)
            {
                A = a;
                B = b;
                C = c;
                Source = source;
            }

            public Vector3 A { get; }

            public Vector3 B { get; }

            public Vector3 C { get; }

            public TetrahedronSource Source { get; }
        }

        // Surface triangles plus the inside parts of all six cube faces, every
        // triangle wound so its normal (b - a) x (c - a) points out of the solid.
        private static List<BoundaryTriangle> BuildClosedBoundary(
            IReadOnlyList<float> cornerValues, float isoLevel, IEdgeVertexPlacer placer)
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            MarchingCubes.Polygonise(
                cornerValues, isoLevel, Vector3.zero, 1f, placer, new FlatVertexWriter(vertices), triangles);

            var boundary = new List<BoundaryTriangle>();
            for (int i = 0; i < triangles.Count; i += 3)
            {
                boundary.Add(new BoundaryTriangle(
                    vertices[triangles[i]], vertices[triangles[i + 1]], vertices[triangles[i + 2]],
                    TetrahedronSource.SurfaceTriangle));
            }

            for (int axis = 0; axis < 3; axis++)
            {
                for (int side = 0; side <= 1; side++)
                {
                    AddFacePatch(axis, side, cornerValues, isoLevel, placer, vertices, triangles, boundary);
                }
            }
            return boundary;
        }

        // Adds the inside part of the face where coordinate `axis` equals `side`.
        // Its outline is built from oriented segments, then walked into loops:
        // - along the face's perimeter, counter-clockwise seen from outside, the
        //   stretches between solid corners and the surface crossings;
        // - every surface triangle edge lying in the face, reversed (on a closed
        //   solid, neighbouring faces run along a shared edge in opposite directions).
        // This follows whatever the triangle table did on ambiguous faces, so the
        // closed solid always matches the mesh.
        private static void AddFacePatch(
            int axis,
            int side,
            IReadOnlyList<float> cornerValues,
            float isoLevel,
            IEdgeVertexPlacer placer,
            List<Vector3> vertices,
            List<int> triangles,
            List<BoundaryTriangle> boundary)
        {
            var segments = new List<(Vector3 From, Vector3 To)>();

            for (int i = 0; i < FaceCornersCcw.Length; i++)
            {
                // Counter-clockwise in (u, v) faces +axis; the side = 0 face looks the
                // other way, so its perimeter is walked in reverse.
                int from = side == 1 ? i : FaceCornersCcw.Length - 1 - i;
                int to = side == 1 ? (i + 1) % FaceCornersCcw.Length : (from + FaceCornersCcw.Length - 1) % FaceCornersCcw.Length;

                Vector3 p = FacePoint(axis, side, FaceCornersCcw[from]);
                Vector3 q = FacePoint(axis, side, FaceCornersCcw[to]);
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
                    if (OnFace(from, axis, side) && OnFace(to, axis, side))
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
                    boundary.Add(new BoundaryTriangle(loop[0], loop[i], loop[i + 1], TetrahedronSource.CubeFace));
                }
            }
        }

        // Groups triangles that share vertices: each group bounds one separate piece of solid.
        private static List<List<BoundaryTriangle>> SplitIntoPieces(List<BoundaryTriangle> boundary)
        {
            var points = new List<Vector3>();
            var parent = new List<int>();

            int PointId(Vector3 point)
            {
                int existing = points.FindIndex(p => Same(p, point));
                if (existing >= 0)
                {
                    return existing;
                }

                points.Add(point);
                parent.Add(parent.Count);
                return points.Count - 1;
            }

            int Root(int id)
            {
                while (parent[id] != id)
                {
                    id = parent[id];
                }
                return id;
            }

            var firstPointOf = new int[boundary.Count];
            for (int i = 0; i < boundary.Count; i++)
            {
                int a = PointId(boundary[i].A);
                int b = PointId(boundary[i].B);
                int c = PointId(boundary[i].C);
                parent[Root(b)] = Root(a);
                parent[Root(c)] = Root(a);
                firstPointOf[i] = a;
            }

            var pieces = new Dictionary<int, List<BoundaryTriangle>>();
            for (int i = 0; i < boundary.Count; i++)
            {
                int root = Root(firstPointOf[i]);
                if (!pieces.TryGetValue(root, out List<BoundaryTriangle> piece))
                {
                    piece = new List<BoundaryTriangle>();
                    pieces.Add(root, piece);
                }
                piece.Add(boundary[i]);
            }
            return new List<List<BoundaryTriangle>>(pieces.Values);
        }

        // Picks the solid corner of this piece to fan from: no negative tetrahedra
        // first, then the fewest tetrahedra. Falls back to the piece's vertex average
        // if no corner works (not expected for single-cube Marching Cubes solids).
        private static Vector3 ChooseApex(List<BoundaryTriangle> piece, IReadOnlyList<float> cornerValues, float isoLevel)
        {
            // Solid corners first (they give the fewest pieces), then every other vertex
            // of the piece, then its vertex average.
            var candidates = new List<Vector3>();
            for (int corner = 0; corner < MarchingCubes.CornerCount; corner++)
            {
                Vector3 position = MarchingCubes.CornerPosition(corner);
                if (cornerValues[corner] >= isoLevel && piece.Exists(t => Same(t.A, position) || Same(t.B, position) || Same(t.C, position)))
                {
                    candidates.Add(position);
                }
            }

            foreach (BoundaryTriangle triangle in piece)
            {
                foreach (Vector3 vertex in new[] { triangle.A, triangle.B, triangle.C })
                {
                    if (!candidates.Exists(c => Same(c, vertex)))
                    {
                        candidates.Add(vertex);
                    }
                }
            }
            candidates.Add(VertexAverage(piece));

            Vector3 best = candidates[0];
            int bestNegatives = int.MaxValue;
            int bestCount = int.MaxValue;
            foreach (Vector3 apex in candidates)
            {
                int negatives = 0;
                int count = 0;
                foreach (BoundaryTriangle triangle in piece)
                {
                    float volume = new Tetrahedron(apex, triangle.A, triangle.B, triangle.C, triangle.Source).SignedVolume;
                    if (volume > DegenerateVolume)
                    {
                        count++;
                    }
                    else if (volume < -DegenerateVolume)
                    {
                        count++;
                        negatives++;
                    }
                }

                if (negatives < bestNegatives || (negatives == bestNegatives && count < bestCount))
                {
                    best = apex;
                    bestNegatives = negatives;
                    bestCount = count;
                }
            }
            return best;
        }

        private static Vector3 VertexAverage(List<BoundaryTriangle> piece)
        {
            Vector3 sum = Vector3.zero;
            foreach (BoundaryTriangle triangle in piece)
            {
                sum += triangle.A + triangle.B + triangle.C;
            }
            return sum / (piece.Count * 3);
        }

        // Point on the face `axis` = `side`, with (u, v) along the next two axes in
        // cyclic order, so u x v points along +axis.
        private static Vector3 FacePoint(int axis, int side, Vector2Int uv)
        {
            var point = new Vector3();
            point[axis] = side;
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

        private static bool OnFace(Vector3 point, int axis, int side)
        {
            return Mathf.Abs(point[axis] - side) < PointTolerance;
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
