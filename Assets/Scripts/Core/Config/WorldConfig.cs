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
        [Tooltip("Voxels per chunk along each axis. Read when a chunk is created; live resizing comes with K1.")]
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

        /// <summary>Raised when a value is edited in the Inspector.</summary>
        public event Action Changed;

        public Vector3Int ChunkSize => chunkSize;

        public float VoxelSize => voxelSize;

        public float IsoLevel => isoLevel;

        public EdgePlacement EdgePlacement => edgePlacement;

        public Shading Shading => shading;

        public ChunkMeshSettings MeshSettings => new ChunkMeshSettings(isoLevel, voxelSize, edgePlacement, shading);

        private void OnValidate()
        {
            chunkSize = Vector3Int.Max(chunkSize, Vector3Int.one);
            Changed?.Invoke();
        }
    }
}
