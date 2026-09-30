namespace Clube.Core
{
    /// <summary>Everything <see cref="ChunkMesher"/> needs besides the chunk itself.</summary>
    public readonly struct ChunkMeshSettings
    {
        public ChunkMeshSettings(
            float isoLevel,
            float voxelSize,
            EdgePlacement edgePlacement = EdgePlacement.Interpolated,
            Shading shading = Shading.Flat)
        {
            IsoLevel = isoLevel;
            VoxelSize = voxelSize;
            EdgePlacement = edgePlacement;
            Shading = shading;
        }

        public float IsoLevel { get; }

        public float VoxelSize { get; }

        public EdgePlacement EdgePlacement { get; }

        public Shading Shading { get; }
    }
}
