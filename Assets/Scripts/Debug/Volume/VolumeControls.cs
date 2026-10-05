using UnityEngine;

namespace Clube.Debug
{
    /// <summary>
    /// The voxel's volume for the lab panel (1C, M20): the three measures (V11, V12, V14)
    /// and the exact-volume view (V13), for the <see cref="VoxelVolumeLab"/>'s voxel in any lab.
    /// </summary>
    public static class VolumeControls
    {
        public static void Draw(LabPanelFrame frame, VoxelVolumeLab volumeLab)
        {
            VoxelCornerEditor corners = volumeLab.Corners;
            if (corners == null || !corners.HasVoxel)
            {
                GUILayout.Label("Click a voxel on the surface (Select tool) to measure it.", frame.Hint);
                return;
            }

            VolumeReport report = volumeLab.Measure();
            float cubeVolume = Mathf.Pow(corners.VoxelSize, 3f);
            string text = $"Approximate (corner mean)  {VolumeText.Volume(report.Approximate, cubeVolume)}";
            if (report.HasExact)
            {
                text += $"\nExact (tetrahedra)  {VolumeText.Volume(report.Exact, cubeVolume)}" +
                        $"\nApproximate − exact  {VolumeText.Error(report.Approximate, report.Exact)}";
            }
            if (report.MonteCarloSamples > 0)
            {
                text += $"\nSmooth field  {VolumeText.Volume(report.TrilinearEstimate, cubeVolume)}";
            }
            GUILayout.Label(text, frame.Label);

            bool exact = frame.ToggleField("Exact volume (draw the tetrahedra)", volumeLab.ExactVolume);
            if (exact != volumeLab.ExactVolume)
            {
                volumeLab.ExactVolume = exact;
            }
            if (exact)
            {
                volumeLab.ExplodeDistance = frame.Slider("Explode distance", volumeLab.ExplodeDistance, 0f, 1f);
            }
        }
    }
}
