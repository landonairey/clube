using Unity.Mathematics;

namespace Clube.Core
{
    /// <summary>
    /// The shape of a heightfield terrain (K9): how tall the ground is at each horizontal
    /// position. Implemented by small structs that hold everything they need inline (or in
    /// read-only native arrays), so the same code runs in managed lab code and inside Burst
    /// generation jobs (K35). One struct per generator; the managed generator class wraps it.
    /// </summary>
    /// <remarks>
    /// A heightfield only needs one height per column, so generation samples it once per
    /// (x, z) and derives every sample's depth from that (<see cref="ColumnHeightsJob{THeight}"/>).
    /// </remarks>
    public interface IHeightField
    {
        /// <summary>Ground height in world units at a horizontal position.</summary>
        float Height(float x, float z);
    }

    /// <summary>
    /// The shape of a fully 3D terrain (K9): how far below the surface any position is.
    /// For terrain that folds back on itself (overhangs, caves), where one height per column
    /// isn't enough. Like <see cref="IHeightField"/>, a Burst-compatible struct.
    /// </summary>
    public interface IVolumeField
    {
        /// <summary>World units below the surface: positive underground, negative above.</summary>
        float Depth(float3 position);
    }

    /// <summary>A level surface (K9).</summary>
    public struct FlatHeight : IHeightField
    {
        public float Level;

        public FlatHeight(float level)
        {
            Level = level;
        }

        public float Height(float x, float z)
        {
            return Level;
        }
    }

    /// <summary>Rolling waves: a sine along x times a sine along z (K9).</summary>
    public struct SineHeight : IHeightField
    {
        private float level;
        private float amplitude;
        private float angularFrequency;

        public SineHeight(float level, float amplitude, float frequency)
        {
            this.level = level;
            this.amplitude = amplitude;
            angularFrequency = frequency * 2f * math.PI;
        }

        public float Height(float x, float z)
        {
            return level + amplitude * math.sin(x * angularFrequency) * math.sin(z * angularFrequency);
        }
    }
}
