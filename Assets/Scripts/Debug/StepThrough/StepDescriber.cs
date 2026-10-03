using System;
using System.Collections.Generic;
using System.Linq;
using Clube.Core;
using UnityEngine;

namespace Clube.Debug
{
    /// <summary>
    /// Plain-language text for the step-through info line (V18): what the current
    /// step does, and a running summary of the voxel so far, e.g.
    /// "Voxel (0,0,0): c0, c3, c5 solid → case 41 → edges e0, e2, e4, …"
    /// Reads only the recorded log.
    /// </summary>
    public static class StepDescriber
    {
        /// <summary>What the step at <paramref name="stepIndex"/> does.</summary>
        public static string Describe(MeshingRecorder recording, int stepIndex)
        {
            MeshingStep step = recording.Steps[stepIndex];
            float iso = recording.Settings.IsoLevel;
            if (step.Type == MeshingStepType.DensityField)
            {
                Vector3Int count = recording.SampleCount;
                return $"Density field: the terrain data is a 3D grid of density samples ({count.x} × {count.y} × {count.z} here), " +
                       $"shaded black (0) to white (1). The surface will pass wherever the field crosses the iso level, {iso:0.00}.";
            }
            if (step.Type == MeshingStepType.Normals)
            {
                return DescribeNormals(recording.Settings.Shading);
            }

            RecordedVoxel voxel = recording.Voxels[step.VoxelIndex];
            switch (step.Type)
            {
                case MeshingStepType.SampleCorners:
                    return $"Sample the cube's 8 corners from the field and compare each with iso {iso:0.00}: " +
                           $"at or above → solid (1, orange), below → empty (0, blue). Solid: {CornerList(SolidCorners(voxel.CaseIndex))}.";

                case MeshingStepType.CaseIndex:
                    IEnumerable<int> solid = SolidCorners(voxel.CaseIndex);
                    string bits = solid.Any() ? string.Join(" + ", solid.Select(corner => 1 << corner)) : "0";
                    return $"Combine the 8 solid/empty bits into one number, corner c setting bit c: {bits} = {voxel.CaseIndex} " +
                           $"({Binary(voxel.CaseIndex)}). This case index is the row to look up in the marching cubes tables.";

                case MeshingStepType.EdgeTable:
                    return voxel.CrossedEdgeMask == 0
                        ? $"Edge table, row {voxel.CaseIndex}: no edges crossed (every corner is on the same side), so no surface in this cube."
                        : $"Edge table, row {voxel.CaseIndex}: the surface crosses {EdgeList(Edges(voxel.CrossedEdgeMask))}. " +
                          "Each joins a solid and an empty corner, so each gets one vertex.";

                case MeshingStepType.Interpolate:
                    return DescribeInterpolate(recording, voxel, step.Edge, iso);

                case MeshingStepType.Triangle:
                    (int number, int total) = TrianglePosition(recording, stepIndex);
                    EdgeTriangle triangle = step.Triangle;
                    return $"Triangle table, row {voxel.CaseIndex}: triangle {number} of {total} joins the vertices on " +
                           $"e{triangle.A} → e{triangle.B} → e{triangle.C} (clockwise seen from outside the solid).";

                default:
                    throw new ArgumentOutOfRangeException(nameof(step.Type), step.Type, null);
            }
        }

        /// <summary>What is known so far once this step has run.</summary>
        public static string Summarise(MeshingRecorder recording, int stepIndex)
        {
            MeshingStep step = recording.Steps[stepIndex];
            if (step.Type == MeshingStepType.DensityField)
            {
                Vector3Int count = recording.SampleCount;
                return $"Density field: {count.x * count.y * count.z} samples, iso level {recording.Settings.IsoLevel:0.00}.";
            }
            if (step.Type == MeshingStepType.Normals)
            {
                int triangles = recording.Steps.Count(other => other.Type == MeshingStepType.Triangle);
                return $"Mesh finished: {recording.Voxels.Count} voxels → {triangles} triangles, " +
                       $"{recording.Settings.Shading.ToString().ToLowerInvariant()} shading.";
            }

            RecordedVoxel voxel = recording.Voxels[step.VoxelIndex];
            string text = $"Voxel ({voxel.Voxel.x},{voxel.Voxel.y},{voxel.Voxel.z})";
            if (step.Type >= MeshingStepType.SampleCorners)
            {
                text += $": {CornerList(SolidCorners(voxel.CaseIndex))} solid";
            }
            if (step.Type >= MeshingStepType.CaseIndex)
            {
                text += $" → case {voxel.CaseIndex}";
            }
            if (step.Type >= MeshingStepType.EdgeTable)
            {
                text += $" → edges {EdgeList(Edges(voxel.CrossedEdgeMask))}";
            }
            return text + ".";
        }

        /// <summary>The case index in binary with only the first <paramref name="knownBits"/> bits (from corner 0) shown.</summary>
        public static string PartialBinary(int caseIndex, int knownBits)
        {
            var chars = new char[9];
            int position = 0;
            for (int bit = MarchingCubes.CornerCount - 1; bit >= 0; bit--)
            {
                if (bit == 3)
                {
                    chars[position++] = ' ';
                }
                chars[position++] = bit >= knownBits ? '·' : MarchingCubes.IsCornerSolid(caseIndex, bit) ? '1' : '0';
            }
            return new string(chars);
        }

        private static string DescribeInterpolate(MeshingRecorder recording, RecordedVoxel voxel, int edge, float iso)
        {
            int cornerA = MarchingCubesTables.EdgeCorners[edge, 0];
            int cornerB = MarchingCubesTables.EdgeCorners[edge, 1];
            float valueA = voxel.CornerValues[cornerA];
            float valueB = voxel.CornerValues[cornerB];

            // Read back from the recorded vertex rather than recomputing it (A11).
            float size = recording.Settings.VoxelSize;
            Vector3 from = voxel.Origin + (Vector3)MarchingCubes.CornerOffset(cornerA) * size;
            float t = Vector3.Distance(from, voxel.EdgeVertices[edge]) / size;

            string how = recording.Settings.EdgePlacement == EdgePlacement.Midpoint
                ? "midpoint placement puts it halfway"
                : $"t = ({iso:0.00} − {valueA:0.00}) / ({valueB:0.00} − {valueA:0.00})";
            return $"Place a vertex on e{edge} (c{cornerA} {valueA:0.00} → c{cornerB} {valueB:0.00}): " +
                   $"{how}, so the vertex sits {t:0.00} of the way along.";
        }

        private static string DescribeNormals(Shading shading)
        {
            string how = shading == Shading.Smooth
                ? "Smooth shading: neighbouring triangles share their vertices, so each vertex averages the face normals around it and the surface looks rounded."
                : "Flat shading: every triangle has its own three vertices, so each vertex just takes its face's normal and the facets show.";
            return "Normals: each triangle faces the way its winding says: the cross product of two of its edges, " +
                   "pointing out of the solid (shown from each triangle's centre). Lighting uses per-vertex normals built from these. " +
                   $"{how} The mesher doesn't do this; ChunkView calls Unity's RecalculateNormals on the finished mesh.";
        }

        // Which of the voxel's triangles this step is, counted from 1. A voxel's steps
        // are contiguous, so only its own run of steps is scanned.
        private static (int Number, int Total) TrianglePosition(MeshingRecorder recording, int stepIndex)
        {
            IReadOnlyList<MeshingStep> steps = recording.Steps;
            int voxelIndex = steps[stepIndex].VoxelIndex;
            int number = 0;
            for (int i = stepIndex; i >= 0 && steps[i].VoxelIndex == voxelIndex; i--)
            {
                if (steps[i].Type == MeshingStepType.Triangle)
                {
                    number++;
                }
            }

            int later = 0;
            for (int i = stepIndex + 1; i < steps.Count && steps[i].VoxelIndex == voxelIndex; i++)
            {
                if (steps[i].Type == MeshingStepType.Triangle)
                {
                    later++;
                }
            }
            return (number, number + later);
        }

        private static IEnumerable<int> SolidCorners(int caseIndex)
        {
            return Enumerable.Range(0, MarchingCubes.CornerCount)
                .Where(corner => MarchingCubes.IsCornerSolid(caseIndex, corner));
        }

        private static IEnumerable<int> Edges(int mask)
        {
            return Enumerable.Range(0, MarchingCubes.EdgeCount).Where(edge => (mask & (1 << edge)) != 0);
        }

        private static string Binary(int caseIndex)
        {
            return PartialBinary(caseIndex, MarchingCubes.CornerCount);
        }

        /// <summary>e.g. "c0, c3", or "none".</summary>
        private static string CornerList(IEnumerable<int> corners)
        {
            return PrefixedList("c", corners);
        }

        /// <summary>e.g. "e0, e3, e8", or "none".</summary>
        private static string EdgeList(IEnumerable<int> edges)
        {
            return PrefixedList("e", edges);
        }

        private static string PrefixedList(string prefix, IEnumerable<int> values)
        {
            string text = string.Join(", ", values.Select(value => prefix + value));
            return text.Length > 0 ? text : "none";
        }
    }
}
