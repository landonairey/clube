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
            MesherBackend backend = MesherBackend.Managed,
            MaterialDisplay materialDisplay = MaterialDisplay.None,
            MeshSeal seal = MeshSeal.None)
        {
            IsoLevel = isoLevel;
            VoxelSize = voxelSize;
            EdgePlacement = edgePlacement;
            Shading = shading;
            Backend = backend;
            MaterialDisplay = materialDisplay;
            Seal = seal;
        }

        public float IsoLevel { get; }

        public float VoxelSize { get; }

        public EdgePlacement EdgePlacement { get; }

        public Shading Shading { get; }

        /// <summary>Which mesher builds the mesh (K12); <see cref="ChunkMesher"/> itself is always the managed one.</summary>
        public MesherBackend Backend { get; }

        /// <summary>How materials show (M15); anything but None adds the material pass after meshing.</summary>
        public MaterialDisplay MaterialDisplay { get; }

        /// <summary>Faces of the chunk meshed as air, closing the surface with walls there (labs); none in the game.</summary>
        public MeshSeal Seal { get; }

        /// <summary>The same settings with another seal (the world seals each chunk on its own faces).</summary>
        public ChunkMeshSettings WithSeal(MeshSeal seal)
        {
            return new ChunkMeshSettings(IsoLevel, VoxelSize, EdgePlacement, Shading, Backend, MaterialDisplay, seal);
        }
    }
}
