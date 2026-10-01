using System;
using System.Collections.Generic;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// Read-only facts about the 256 case indices: which base configuration each
    /// belongs to (V7) and whether it has an ambiguous face (V10). Everything is
    /// derived from the corner layout at startup, not typed in, and checked by
    /// tests against the known size of each configuration.
    /// </summary>
    public static class MarchingCubesCases
    {
        public const int RotationCount = 24;

        public const int BaseConfigurationCount = 15;

        // rotations[r][corner] = where corner ends up under rotation r.
        private static readonly int[][] Rotations = BuildRotations();

        private static readonly BaseConfiguration[] ConfigurationByCase = BuildConfigurationTable();

        private static readonly int[] RepresentativeByConfiguration = BuildRepresentatives();

        public static BaseConfiguration GetBaseConfiguration(int caseIndex)
        {
            return ConfigurationByCase[caseIndex];
        }

        /// <summary>The lowest case index with this configuration, e.g. for preset buttons.</summary>
        public static int GetRepresentativeCase(BaseConfiguration configuration)
        {
            return RepresentativeByConfiguration[(int)configuration];
        }

        /// <summary>The case index after rotating the cube by rotation <paramref name="rotation"/> (0-23).</summary>
        public static int Rotate(int caseIndex, int rotation)
        {
            int[] permutation = Rotations[rotation];
            int rotated = 0;
            for (int corner = 0; corner < MarchingCubes.CornerCount; corner++)
            {
                if (MarchingCubes.IsCornerSolid(caseIndex, corner))
                {
                    rotated |= 1 << permutation[corner];
                }
            }
            return rotated;
        }

        /// <summary>
        /// True when some face has exactly two solid corners, diagonally opposite.
        /// The surface could then connect across that face two ways, and Marching
        /// Cubes just picks one, which can leave holes between neighbouring cubes.
        /// </summary>
        public static bool HasAmbiguousFace(int caseIndex)
        {
            for (int axis = 0; axis < 3; axis++)
            {
                for (int side = 0; side <= 1; side++)
                {
                    var solidOnFace = new List<Vector3Int>(4);
                    for (int corner = 0; corner < MarchingCubes.CornerCount; corner++)
                    {
                        Vector3Int offset = MarchingCubes.CornerOffset(corner);
                        if (offset[axis] == side && MarchingCubes.IsCornerSolid(caseIndex, corner))
                        {
                            solidOnFace.Add(offset);
                        }
                    }

                    if (solidOnFace.Count == 2 && Distance(solidOnFace[0], solidOnFace[1]) == 2)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private static int[][] BuildRotations()
        {
            // Quarter turns about X and Y generate all 24 rotations of the cube.
            int[] turnX = PermutationOf(p => new Vector3Int(p.x, 1 - p.z, p.y));
            int[] turnY = PermutationOf(p => new Vector3Int(p.z, p.y, 1 - p.x));

            var found = new List<int[]> { new[] { 0, 1, 2, 3, 4, 5, 6, 7 } };
            for (int i = 0; i < found.Count; i++)
            {
                foreach (int[] turn in new[] { turnX, turnY })
                {
                    int[] next = Compose(found[i], turn);
                    if (!found.Exists(existing => SameSequence(existing, next)))
                    {
                        found.Add(next);
                    }
                }
            }

            if (found.Count != RotationCount)
            {
                throw new InvalidOperationException($"Expected {RotationCount} cube rotations, found {found.Count}.");
            }
            return found.ToArray();
        }

        private static BaseConfiguration[] BuildConfigurationTable()
        {
            var table = new BaseConfiguration[MarchingCubes.CaseCount];
            for (int caseIndex = 0; caseIndex < MarchingCubes.CaseCount; caseIndex++)
            {
                // Complement symmetry: classify whichever side has four or fewer solid corners.
                int side = SolidCount(caseIndex) <= 4 ? caseIndex : ~caseIndex & 0xFF;
                table[caseIndex] = Classify(side);
            }
            return table;
        }

        private static int[] BuildRepresentatives()
        {
            var representatives = new int[BaseConfigurationCount];
            for (int i = 0; i < representatives.Length; i++)
            {
                representatives[i] = -1;
            }

            for (int caseIndex = 0; caseIndex < MarchingCubes.CaseCount; caseIndex++)
            {
                int configuration = (int)ConfigurationByCase[caseIndex];
                if (representatives[configuration] < 0)
                {
                    representatives[configuration] = caseIndex;
                }
            }
            return representatives;
        }

        // Classifies a set of at most four solid corners by how they connect.
        // All the measures used (counts, adjacency, distances, handedness) are
        // unchanged by rotation, so every rotation of a case lands in the same class.
        private static BaseConfiguration Classify(int solidCorners)
        {
            var corners = new List<int>();
            for (int corner = 0; corner < MarchingCubes.CornerCount; corner++)
            {
                if (MarchingCubes.IsCornerSolid(solidCorners, corner))
                {
                    corners.Add(corner);
                }
            }

            var edges = new List<(int A, int B)>();
            for (int i = 0; i < corners.Count; i++)
            {
                for (int j = i + 1; j < corners.Count; j++)
                {
                    if (Distance(corners[i], corners[j]) == 1)
                    {
                        edges.Add((corners[i], corners[j]));
                    }
                }
            }

            switch (corners.Count)
            {
                case 0:
                    return BaseConfiguration.Empty;
                case 1:
                    return BaseConfiguration.SingleCorner;
                case 2:
                    if (edges.Count == 1)
                    {
                        return BaseConfiguration.Edge;
                    }
                    return Distance(corners[0], corners[1]) == 2
                        ? BaseConfiguration.FaceDiagonal
                        : BaseConfiguration.BodyDiagonal;
                case 3:
                    switch (edges.Count)
                    {
                        case 2: return BaseConfiguration.LOnFace;
                        case 1: return BaseConfiguration.EdgeAndFarCorner;
                        case 0: return BaseConfiguration.ThreeFaceDiagonals;
                    }
                    break;
                case 4:
                    switch (edges.Count)
                    {
                        case 4:
                            return BaseConfiguration.Face;
                        case 0:
                            return BaseConfiguration.Alternating;
                        case 2:
                            return SharesCorner(edges[0], edges[1])
                                ? BaseConfiguration.LAndFarCorner
                                : BaseConfiguration.OppositeEdges;
                        case 3:
                            if (corners.Exists(corner => Degree(corner, edges) == 3))
                            {
                                return BaseConfiguration.Star;
                            }
                            return PathHandedness(corners, edges) > 0
                                ? BaseConfiguration.Zigzag
                                : BaseConfiguration.ZigzagMirrored;
                    }
                    break;
            }

            throw new InvalidOperationException($"Unclassified corner set {Convert.ToString(solidCorners, 2)}.");
        }

        // Sign of the triple product of the path's three edge directions. A rotation
        // keeps it, a mirror flips it, and walking the path backwards leaves it
        // unchanged, so it tells the two zigzags apart.
        private static int PathHandedness(List<int> corners, List<(int A, int B)> edges)
        {
            int current = corners.Find(corner => Degree(corner, edges) == 1);
            var path = new List<int> { current };
            while (path.Count < corners.Count)
            {
                foreach ((int a, int b) in edges)
                {
                    int next = a == current ? b : b == current ? a : -1;
                    if (next >= 0 && !path.Contains(next))
                    {
                        path.Add(next);
                        current = next;
                        break;
                    }
                }
            }

            Vector3Int u = MarchingCubes.CornerOffset(path[1]) - MarchingCubes.CornerOffset(path[0]);
            Vector3Int v = MarchingCubes.CornerOffset(path[2]) - MarchingCubes.CornerOffset(path[1]);
            Vector3Int w = MarchingCubes.CornerOffset(path[3]) - MarchingCubes.CornerOffset(path[2]);
            int determinant =
                u.x * (v.y * w.z - v.z * w.y) -
                u.y * (v.x * w.z - v.z * w.x) +
                u.z * (v.x * w.y - v.y * w.x);
            return Math.Sign(determinant);
        }

        private static int Degree(int corner, List<(int A, int B)> edges)
        {
            return edges.FindAll(edge => edge.A == corner || edge.B == corner).Count;
        }

        private static bool SharesCorner((int A, int B) first, (int A, int B) second)
        {
            return first.A == second.A || first.A == second.B || first.B == second.A || first.B == second.B;
        }

        /// <summary>Number of axes two corners differ on: 1 = edge, 2 = face diagonal, 3 = body diagonal.</summary>
        private static int Distance(int cornerA, int cornerB)
        {
            return Distance(MarchingCubes.CornerOffset(cornerA), MarchingCubes.CornerOffset(cornerB));
        }

        private static int Distance(Vector3Int a, Vector3Int b)
        {
            Vector3Int d = a - b;
            return Math.Abs(d.x) + Math.Abs(d.y) + Math.Abs(d.z);
        }

        private static int SolidCount(int caseIndex)
        {
            int count = 0;
            for (int corner = 0; corner < MarchingCubes.CornerCount; corner++)
            {
                if (MarchingCubes.IsCornerSolid(caseIndex, corner))
                {
                    count++;
                }
            }
            return count;
        }

        private static int[] PermutationOf(Func<Vector3Int, Vector3Int> transform)
        {
            var permutation = new int[MarchingCubes.CornerCount];
            for (int corner = 0; corner < MarchingCubes.CornerCount; corner++)
            {
                permutation[corner] = IndexOfCorner(transform(MarchingCubes.CornerOffset(corner)));
            }
            return permutation;
        }

        private static int IndexOfCorner(Vector3Int offset)
        {
            for (int corner = 0; corner < MarchingCubes.CornerCount; corner++)
            {
                if (MarchingCubes.CornerOffset(corner) == offset)
                {
                    return corner;
                }
            }
            throw new ArgumentOutOfRangeException(nameof(offset), offset, "Not a cube corner.");
        }

        // Apply first, then second.
        private static int[] Compose(int[] first, int[] second)
        {
            var result = new int[first.Length];
            for (int i = 0; i < first.Length; i++)
            {
                result[i] = second[first[i]];
            }
            return result;
        }

        private static bool SameSequence(int[] a, int[] b)
        {
            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] != b[i])
                {
                    return false;
                }
            }
            return true;
        }
    }
}
