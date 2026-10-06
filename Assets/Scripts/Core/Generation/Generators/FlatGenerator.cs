namespace Clube.Core
{
    /// <summary>A level surface (K9).</summary>
    public sealed class FlatGenerator : HeightfieldGenerator<FlatHeight>
    {
        public FlatGenerator(float level)
            : base(new FlatHeight(level))
        {
        }
    }
}
