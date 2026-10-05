namespace Clube.Core
{
    /// <summary>Everything <see cref="ChunkMesher"/> needs besides the chunk itself.</summary>
    public readonly struct ChunkMeshSettings
    {
        public ChunkMeshSettings(
            float isoLevel,
            float voxelSize,
            EdgePlacement edgePlacement = EdgePlacement.Interpolated,
            Shading shading = Shading.Flat,
            MesherBackend backend = MesherBackend.Managed)
        {
            IsoLevel = isoLevel;
            VoxelSize = voxelSize;
            EdgePlacement = edgePlacement;
            Shading = shading;
            Backend = backend;
        }

        public float IsoLevel { get; }

        public float VoxelSize { get; }

        public EdgePlacement EdgePlacement { get; }

        public Shading Shading { get; }

        /// <summary>Which mesher builds the mesh (K12); <see cref="ChunkMesher"/> itself is always the managed one.</summary>
        public MesherBackend Backend { get; }
    }
}
