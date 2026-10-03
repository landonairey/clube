namespace Clube.Core
{
    /// <summary>A level surface (K9).</summary>
    public sealed class FlatGenerator : HeightfieldGenerator
    {
        private readonly float level;

        public FlatGenerator(float level)
        {
            this.level = level;
        }

        public override float Height(float x, float z)
        {
            return level;
        }
    }
}
