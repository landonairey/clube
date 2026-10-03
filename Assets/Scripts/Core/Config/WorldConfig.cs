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

        [Tooltip("How the terrain is generated: generator, surface level and amplitude, noise shape, seed (K8-K10).")]
        [SerializeField]
        private TerrainSettings terrain = new TerrainSettings();

        /// <summary>Raised when a value is edited in the Inspector.</summary>
        public event Action Changed;

        public Vector3Int ChunkSize => chunkSize;

        public float VoxelSize => voxelSize;

        public float IsoLevel => isoLevel;

        public EdgePlacement EdgePlacement => edgePlacement;

        public Shading Shading => shading;

        public ChunkMeshSettings MeshSettings => new ChunkMeshSettings(isoLevel, voxelSize, edgePlacement, shading);

        public TerrainSettings Terrain => terrain;

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
