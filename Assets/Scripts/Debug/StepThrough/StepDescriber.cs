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
    /// "Voxel (0,0,0): corners 0, 3, 5 inside → case 41 → edges 0, 3, 8."
    /// Reads only the recorded log.
    /// </summary>
    public static class StepDescriber
    {
        /// <summary>What the step at <paramref name="stepIndex"/> does.</summary>
        public static string Describe(MeshingRecorder recording, int stepIndex)
        {
            MeshingStep step = recording.Steps[stepIndex];
            RecordedVoxel voxel = recording.Voxels[step.VoxelIndex];
            float iso = recording.Settings.IsoLevel;

            switch (step.Type)
            {
                case MeshingStepType.ReadCorners:
                    string values = string.Join("  ", Enumerable.Range(0, MarchingCubes.CornerCount)
                        .Select(corner => $"c{corner} {voxel.CornerValues[corner]:0.00}"));
                    return $"Read the 8 corner densities: {values}";

                case MeshingStepType.Classify:
                    return $"Classify against iso {iso:0.00}: a corner at or above it is solid (inside). " +
                           $"Solid: {List(SolidCorners(voxel.CaseIndex))}.";

                case MeshingStepType.CaseIndex:
                    IEnumerable<int> solid = SolidCorners(voxel.CaseIndex);
                    string bits = solid.Any() ? string.Join(" + ", solid.Select(corner => 1 << corner)) : "0";
                    return $"Case index: each solid corner c sets bit c, so {bits} = {voxel.CaseIndex} " +
                           $"({Binary(voxel.CaseIndex)}).";

                case MeshingStepType.EdgeTable:
                    return voxel.CrossedEdgeMask == 0
                        ? $"Edge table: case {voxel.CaseIndex} crosses no edges (all corners on one side), so no surface here."
                        : $"Edge table: case {voxel.CaseIndex} → the surface crosses edges {List(Edges(voxel.CrossedEdgeMask))}, " +
                          "each joining a solid and an empty corner.";

                case MeshingStepType.Interpolate:
                    return DescribeInterpolate(recording, voxel, step.Edge, iso);

                case MeshingStepType.Triangle:
                    (int number, int total) = TrianglePosition(recording, stepIndex);
                    EdgeTriangle triangle = step.Triangle;
                    return $"Triangle table: triangle {number} of {total} joins the vertices on edges " +
                           $"{triangle.A} → {triangle.B} → {triangle.C} (clockwise seen from outside the solid).";

                default:
                    throw new ArgumentOutOfRangeException(nameof(step.Type), step.Type, null);
            }
        }

        /// <summary>What is known about the step's voxel once this step has run.</summary>
        public static string Summarise(MeshingRecorder recording, int stepIndex)
        {
            MeshingStep step = recording.Steps[stepIndex];
            RecordedVoxel voxel = recording.Voxels[step.VoxelIndex];

            string text = $"Voxel ({voxel.Voxel.x},{voxel.Voxel.y},{voxel.Voxel.z})";
            if (step.Type >= MeshingStepType.Classify)
            {
                text += $": corners {List(SolidCorners(voxel.CaseIndex))} inside";
            }
            if (step.Type >= MeshingStepType.CaseIndex)
            {
                text += $" → case {voxel.CaseIndex}";
            }
            if (step.Type >= MeshingStepType.EdgeTable)
            {
                text += $" → edges {List(Edges(voxel.CrossedEdgeMask))}";
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
            return $"Interpolate edge {edge} (c{cornerA} {valueA:0.00} → c{cornerB} {valueB:0.00}): " +
                   $"{how}, so the vertex sits {t:0.00} of the way along.";
        }

        // Which of the voxel's triangles this step is, counted from 1.
        private static (int Number, int Total) TrianglePosition(MeshingRecorder recording, int stepIndex)
        {
            int voxelIndex = recording.Steps[stepIndex].VoxelIndex;
            int number = 0;
            int total = 0;
            for (int i = 0; i < recording.Steps.Count; i++)
            {
                MeshingStep other = recording.Steps[i];
                if (other.VoxelIndex == voxelIndex && other.Type == MeshingStepType.Triangle)
                {
                    total++;
                    if (i <= stepIndex)
                    {
                        number++;
                    }
                }
            }
            return (number, total);
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

        private static string List(IEnumerable<int> values)
        {
            string text = string.Join(", ", values);
            return text.Length > 0 ? text : "none";
        }
    }
}
