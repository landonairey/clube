namespace Clube.Core
{
    /// <summary>Rolling waves: a sine along x times a sine along z (K9).</summary>
    public sealed class SineGenerator : HeightfieldGenerator<SineHeight>
    {
        public SineGenerator(float level, float amplitude, float frequency)
            : base(new SineHeight(level, amplitude, frequency))
        {
        }
    }
}
