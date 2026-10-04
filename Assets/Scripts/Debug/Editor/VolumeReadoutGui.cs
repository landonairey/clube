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
        /// <summary>
        /// Exact volume toggle and explode control, for panels other than the Volume
        /// Lab's own Inspector (which already shows them as fields).
        /// </summary>
        public static void DrawViewControls(VoxelVolumeLab lab)
        {
            EditorGUI.BeginChangeCheck();
            bool exact = EditorGUILayout.Toggle(
                new GUIContent(
                    "Exact volume",
                    "Decompose the solid into tetrahedra for the exact volume (V12) and draw them (V13). " +
                    "Off skips that work entirely."),
                lab.ExactVolume);
            float explode;
            using (new EditorGUI.DisabledScope(!exact))
            {
                explode = EditorGUILayout.Slider(
                    new GUIContent("Explode distance", "Pull the tetrahedra apart (also the scroll wheel in Play mode)."),
                    lab.ExplodeDistance, 0f, 1f);
            }

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(lab, "Change exact volume view");
                lab.ExactVolume = exact;
                lab.ExplodeDistance = explode;
                EditorUtility.SetDirty(lab);
                SceneView.RepaintAll();
            }
        }

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
            bool hasSmooth = report.MonteCarloSamples > 0;

            EditorGUILayout.LabelField("Volume (whole voxel = 100%)", EditorStyles.boldLabel);
            using (new EditorGUI.IndentLevelScope())
            {
                EditorGUILayout.LabelField(
                    new GUIContent("Approximate (V11)", "Mean of the 8 corner densities. Ignores the iso level and the surface shape."),
                    new GUIContent(VolumeText.Volume(report.Approximate, cubeVolume)));

                if (report.HasExact)
                {
                    DrawExact(report, cubeVolume);
                }

                if (hasSmooth)
                {
                    EditorGUILayout.LabelField(
                        new GUIContent("Smooth field (V14)",
                            $"Monte Carlo estimate ({report.MonteCarloSamples:N0} samples) of how much of the cube the trilinear " +
                            "density field fills. Marching Cubes' flat triangles approximate this field."),
                        new GUIContent(VolumeText.Volume(report.TrilinearEstimate, cubeVolume)));

                    if (report.HasExact)
                    {
                        EditorGUILayout.LabelField(
                            new GUIContent("Exact − smooth field", "How far Marching Cubes' solid is from the smooth field."),
                            new GUIContent(VolumeText.Error(report.Exact, report.TrilinearEstimate)));
                    }
                    else
                    {
                        EditorGUILayout.LabelField(
                            new GUIContent("Approximate − smooth field", "How far the corner average is from the smooth field."),
                            new GUIContent(VolumeText.Error(report.Approximate, report.TrilinearEstimate)));
                    }
                }
            }
        }

        private static void DrawExact(VolumeReport report, float cubeVolume)
        {
            EditorGUILayout.LabelField(
                new GUIContent("Exact (V12)", "The solid Marching Cubes builds, closed with the cube faces and summed as tetrahedra."),
                new GUIContent(VolumeText.Volume(report.Exact, cubeVolume)));

            EditorGUILayout.LabelField(" ", $"{report.PositiveTetrahedra} tetrahedra", EditorStyles.miniLabel);
            if (report.NegativeTetrahedra > 0)
            {
                EditorGUILayout.HelpBox(
                    $"{report.NegativeTetrahedra} inside-out tetrahedra (drawn red). The decomposition " +
                    "should never produce these; the volume is still right, but please report this case.",
                    MessageType.Warning);
            }

            EditorGUILayout.LabelField(
                new GUIContent("Approximate − exact", "Absolute difference in percentage points, and relative to the exact volume."),
                new GUIContent(VolumeText.Error(report.Approximate, report.Exact)));
        }
    }
}
