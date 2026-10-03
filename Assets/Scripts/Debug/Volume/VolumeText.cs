using UnityEngine;

namespace Clube.Debug
{
    /// <summary>
    /// Number formats for the voxel volume readout (V11, V12, V14), shared by the
    /// Inspector and the in-game lab panel.
    /// </summary>
    public static class VolumeText
    {
        /// <summary>e.g. "12.50%   (0.1250 u³)": a fraction of the voxel, and the volume itself.</summary>
        public static string Volume(float fraction, float cubeVolume)
        {
            return $"{fraction * 100f:0.00}%   ({fraction * cubeVolume:0.0000} u³)";
        }

        /// <summary>e.g. "+10.42 pts (+500.0%)": the gap in percentage points, then relative to the reference.</summary>
        public static string Error(float value, float reference)
        {
            float points = (value - reference) * 100f;
            string relative = Mathf.Abs(reference) > 1e-6f
                ? $"{(value - reference) / reference * 100f:+0.0;-0.0;0.0}%"
                : "n/a";
            return $"{points:+0.00;-0.00;0.00} pts   ({relative})";
        }
    }
}
