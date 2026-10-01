using UnityEditor;
using UnityEngine;

namespace Clube.Debug.Editor
{
    /// <summary>
    /// Inspector readout comparing the voxel's volume measures (V11, V12, V14).
    /// Shown both on the Volume Lab object and in the voxel's corner panel, so the
    /// numbers stay visible while dragging corners.
    /// </summary>
    internal static class VolumeReadoutGui
    {
        public static void Draw(VoxelVolumeLab lab)
        {
            VoxelCornerEditor corners = lab.Corners;
            if (corners == null)
            {
                EditorGUILayout.HelpBox("Put the Volume Lab on a child of the voxel.", MessageType.Warning);
                return;
            }

            VolumeReport report = lab.Measure();
            float cubeVolume = Mathf.Pow(corners.VoxelSize, 3f);

            EditorGUILayout.LabelField("Volume (whole voxel = 100%)", EditorStyles.boldLabel);
            using (new EditorGUI.IndentLevelScope())
            {
                EditorGUILayout.LabelField(
                    new GUIContent("Approximate (V11)", "Mean of the 8 corner densities. Ignores the iso level and the surface shape."),
                    new GUIContent(FormatVolume(report.Approximate, cubeVolume)));

                EditorGUILayout.LabelField(
                    new GUIContent("Exact (V12)", "The solid Marching Cubes builds, closed with the cube faces and summed as tetrahedra fanned from c0."),
                    new GUIContent(FormatVolume(report.Exact, cubeVolume)));

                EditorGUILayout.LabelField(
                    " ",
                    $"{report.PositiveTetrahedra + report.NegativeTetrahedra} tetrahedra: " +
                    $"{report.PositiveTetrahedra} add, {report.NegativeTetrahedra} subtract (red)",
                    EditorStyles.miniLabel);

                EditorGUILayout.LabelField(
                    new GUIContent("Approximate − exact", "Absolute difference in percentage points, and relative to the exact volume."),
                    new GUIContent(FormatError(report.Approximate, report.Exact)));

                if (report.MonteCarloSamples > 0)
                {
                    EditorGUILayout.LabelField(
                        new GUIContent("Smooth field (V14)",
                            $"Monte Carlo estimate ({report.MonteCarloSamples:N0} samples) of how much of the cube the trilinear " +
                            "density field fills. Marching Cubes' flat triangles approximate this field."),
                        new GUIContent(FormatVolume(report.TrilinearEstimate, cubeVolume)));

                    EditorGUILayout.LabelField(
                        new GUIContent("Exact − smooth field", "How far Marching Cubes' solid is from the smooth field."),
                        new GUIContent(FormatError(report.Exact, report.TrilinearEstimate)));
                }
            }
        }

        private static string FormatVolume(float fraction, float cubeVolume)
        {
            return $"{fraction * 100f:0.00}%   ({fraction * cubeVolume:0.0000} u³)";
        }

        // e.g. "+10.42 pts (+500.0%)": the gap in percentage points, then relative to the reference.
        private static string FormatError(float value, float reference)
        {
            float points = (value - reference) * 100f;
            string relative = Mathf.Abs(reference) > 1e-6f
                ? $"{(value - reference) / reference * 100f:+0.0;-0.0;0.0}%"
                : "n/a";
            return $"{points:+0.00;-0.00;0.00} pts   ({relative})";
        }
    }
}
