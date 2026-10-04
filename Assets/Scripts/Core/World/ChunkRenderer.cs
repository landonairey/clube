using System;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// Renders one chunk of a <see cref="WorldView"/>: rebuilds its mesh once at the end
    /// of any frame the chunk is dirty (G1), with the shared <see cref="ChunkMeshBuilder"/>.
    /// Created and pooled by the world view, which also places it at the chunk's origin.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class ChunkRenderer : MonoBehaviour
    {
        private ChunkMeshBuilder meshBuilder;
        private Func<ChunkMeshSettings> settings;

        /// <summary>Raised after each rebuild, with the coordinate and the new mesh (A4: lab tools hook in here).</summary>
        public event Action<Vector3Int, Mesh> MeshRebuilt;

        /// <summary>The chunk shown, or null while pooled.</summary>
        public Chunk Chunk { get; private set; }

        /// <summary>The chunk's coordinate in the world (M1).</summary>
        public Vector3Int Coord { get; private set; }

        public ChunkMeshStats LastBuildStats => meshBuilder != null ? meshBuilder.LastStats : default;

        public Mesh Mesh => meshBuilder?.Mesh;

        /// <summary>Shows a chunk; its mesh builds at the end of the frame.</summary>
        /// <param name="meshSettings">Read at each rebuild, so config changes apply.</param>
        public void Show(Vector3Int coord, Chunk chunk, Func<ChunkMeshSettings> meshSettings)
        {
            if (meshBuilder == null)
            {
                meshBuilder = new ChunkMeshBuilder("World Chunk");
                GetComponent<MeshFilter>().sharedMesh = meshBuilder.Mesh;
            }

            Coord = coord;
            Chunk = chunk;
            settings = meshSettings;
            name = $"Chunk {coord.x}, {coord.y}, {coord.z}";
            chunk.MarkDirty();
            gameObject.SetActive(true);
        }

        /// <summary>Stops showing the chunk, ready to be reused.</summary>
        public void Hide()
        {
            Chunk = null;
            gameObject.SetActive(false);
        }

        private void LateUpdate()
        {
            if (Chunk != null && Chunk.IsDirty)
            {
                meshBuilder.Build(Chunk, settings());
                MeshRebuilt?.Invoke(Coord, meshBuilder.Mesh);
            }
        }

        private void OnDestroy()
        {
            meshBuilder?.Dispose();
        }
    }
}
