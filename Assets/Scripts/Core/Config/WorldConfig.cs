using System;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// World-definition values (A2). The game reads them once at startup; lab
    /// scenes may edit them live in the Inspector, which raises <see cref="Changed"/>
    /// so views can regenerate.
    /// </summary>
    [CreateAssetMenu(fileName = "WorldConfig", menuName = "Clube/World Config")]
    public class WorldConfig : ScriptableObject
    {
        [Tooltip("Voxels per chunk along each axis (K1). Changing it in a lab replaces the chunk with a new, empty one.")]
        [SerializeField]
        private Vector3Int chunkSize = Vector3Int.one;

        [Tooltip("How many layers of chunks the world stacks from chunk y = 0 upwards (M1). " +
                 "Chunks are cubes stacked vertically; render distance only limits them horizontally. Single-chunk labs ignore it.")]
        [SerializeField, Min(1)]
        private int worldHeightInChunks = 2;

        [Tooltip("Edge length of one voxel in world units.")]
        [SerializeField, Min(0.01f)]
        private float voxelSize = 1f;

        [Tooltip("Densities at or above this value count as solid (V1).")]
        [SerializeField, Range(0f, 1f)]
        private float isoLevel = 0.5f;

        [Tooltip("Where surface vertices sit on crossed edges (V3). Labs switch it; the game locks one in.")]
        [SerializeField]
        private EdgePlacement edgePlacement = EdgePlacement.Interpolated;

        [Tooltip("Flat (per-face normals) or smooth (shared vertices) shading (V4).")]
        [SerializeField]
        private Shading shading = Shading.Flat;

        [Tooltip("Managed C# mesher, or the Burst-compiled job (K12). Both give the same mesh; step-through always records the managed one.")]
        [SerializeField]
        private MesherBackend mesher = MesherBackend.Managed;

        [Tooltip("How each chunk stores its densities (A12, 2G): flat floats, single bytes, runs along an axis, or a sparse octree. Changing it regenerates.")]
        [SerializeField]
        private VoxelStorageType storage = VoxelStorageType.Flat;

        [Tooltip("Octree storage only: how many times the octree may divide (K25). Deeper collapses finer but costs more nodes.")]
        [SerializeField, Range(1, 8)]
        private int octreeMaxDepth = VoxelStorages.DefaultOctreeMaxDepth;

        [Tooltip("How the terrain is generated: generator, surface level and amplitude, noise shape, seed (K8-K10).")]
        [SerializeField]
        private TerrainSettings terrain = new TerrainSettings();

        /// <summary>Raised when a value is edited in the Inspector.</summary>
        public event Action Changed;

        // The setters are for lab controls (A2: labs may change the config). Call
        // NotifyChanged afterwards so views rebuild; the game only reads these.

        public Vector3Int ChunkSize
        {
            get => chunkSize;
            set => chunkSize = Vector3Int.Max(Vector3Int.one, value);
        }

        /// <summary>Layers of chunks stacked from chunk y = 0 (M1).</summary>
        public int WorldHeightInChunks
        {
            get => worldHeightInChunks;
            set => worldHeightInChunks = Mathf.Max(1, value);
        }

        public float VoxelSize
        {
            get => voxelSize;
            set => voxelSize = Mathf.Max(0.01f, value);
        }

        public float IsoLevel
        {
            get => isoLevel;
            set => isoLevel = Mathf.Clamp01(value);
        }

        public EdgePlacement EdgePlacement
        {
            get => edgePlacement;
            set => edgePlacement = value;
        }

        public Shading Shading
        {
            get => shading;
            set => shading = value;
        }

        public MesherBackend Mesher
        {
            get => mesher;
            set => mesher = value;
        }

        /// <summary>How each chunk stores its densities (2G). Changing it makes new chunks.</summary>
        public VoxelStorageType Storage
        {
            get => storage;
            set => storage = value;
        }

        public int OctreeMaxDepth
        {
            get => octreeMaxDepth;
            set => octreeMaxDepth = Mathf.Clamp(value, 1, 8);
        }

        /// <summary>True when <paramref name="existing"/> is of the configured scheme, so a chunk can keep it.</summary>
        public bool StorageMatches(IVoxelStorage existing)
        {
            // An octree clamps its depth to what its size allows, so compare against the same clamp.
            return existing.Type == storage
                   && (!(existing is OctreeVoxelStorage octree) || octree.MaxDepth == Mathf.Clamp(octreeMaxDepth, 1, octree.DeepestDepth));
        }

        /// <summary>A new, empty storage of the configured scheme for a chunk of <paramref name="sampleCount"/> samples.</summary>
        public IVoxelStorage CreateStorage(Vector3Int sampleCount)
        {
            return VoxelStorages.Create(storage, sampleCount, octreeMaxDepth);
        }

        public ChunkMeshSettings MeshSettings => new ChunkMeshSettings(isoLevel, voxelSize, edgePlacement, shading, mesher);

        public TerrainSettings Terrain => terrain;

        /// <summary>
        /// Raises <see cref="Changed"/> after a lab tool edits the config from code. Inspector
        /// edits raise it through OnValidate, which never runs in a build, so in-game lab
        /// controls (K33) call this instead.
        /// </summary>
        public void NotifyChanged()
        {
            Changed?.Invoke();
        }

        // What the heightmap fields held at the last validation, to spot a newly imported heightmap.
        [NonSerialized] private bool heightmapStateKnown;
        [NonSerialized] private UnityEngine.Object lastHeightmapSource;
        [NonSerialized] private TerrainGeneratorType lastGenerator;

        private void OnEnable()
        {
            RememberHeightmapState();
        }

        private void OnValidate()
        {
            chunkSize = Vector3Int.Max(chunkSize, Vector3Int.one);
            FitNewHeightmapToChunk();
            Changed?.Invoke();
        }

        /// <summary>
        /// When a heightmap is assigned, or the Heightmap generator picked, resets the
        /// terrain's level and amplitude so the image first shows as it was exported.
        /// </summary>
        private void FitNewHeightmapToChunk()
        {
            UnityEngine.Object source = terrain.HeightmapSource;
            bool imported = heightmapStateKnown && source != null && source != lastHeightmapSource;
            bool switchedTo = heightmapStateKnown && terrain.Generator == TerrainGeneratorType.Heightmap
                              && lastGenerator != TerrainGeneratorType.Heightmap;
            if (imported || switchedTo)
            {
                terrain.FitHeightmapToChunk(chunkSize.y * voxelSize, voxelSize);
            }
            RememberHeightmapState();
        }

        private void RememberHeightmapState()
        {
            lastHeightmapSource = terrain.HeightmapSource;
            lastGenerator = terrain.Generator;
            heightmapStateKnown = true;
        }
    }
}
