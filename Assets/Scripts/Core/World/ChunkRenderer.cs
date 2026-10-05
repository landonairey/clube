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
    public class ChunkRenderer : MonoBehaviour, IRenderedChunk
    {
        private ChunkMeshBuilder meshBuilder;
        private Func<ChunkMeshSettings> settings;
        private World world;

        /// <summary>Raised after each rebuild (A4: lab tools hook in here); the chunk is at <see cref="Coord"/>.</summary>
        public event Action<Mesh> MeshRebuilt;

        /// <summary>Raised when the renderer is given another chunk, or pooled (null).</summary>
        public event Action<Chunk> ChunkChanged;

        /// <summary>The chunk shown, or null while pooled.</summary>
        public Chunk Chunk { get; private set; }

        /// <summary>The chunk's coordinate in the world (M1).</summary>
        public Vector3Int Coord { get; private set; }

        public ChunkMeshStats LastBuildStats => meshBuilder != null ? meshBuilder.LastStats : default;

        public Mesh Mesh => meshBuilder?.Mesh;

        public Transform Transform => transform;

        public ChunkMeshSettings MeshSettings => settings();

        public Renderer Renderer => GetComponent<MeshRenderer>();

        /// <summary>Shows a chunk; its mesh builds at the end of the frame.</summary>
        /// <param name="world">The world the chunk belongs to; edits go through it (M2).</param>
        /// <param name="meshSettings">Read at each rebuild, so config changes apply.</param>
        public void Show(World world, Vector3Int coord, Chunk chunk, Func<ChunkMeshSettings> meshSettings)
        {
            if (meshBuilder == null)
            {
                meshBuilder = new ChunkMeshBuilder("World Chunk");
                GetComponent<MeshFilter>().sharedMesh = meshBuilder.Mesh;
            }

            this.world = world;
            Coord = coord;
            Chunk = chunk;
            settings = meshSettings;
            name = $"Chunk {coord.x}, {coord.y}, {coord.z}";
            chunk.MarkDirty();
            gameObject.SetActive(true);
            ChunkChanged?.Invoke(chunk);
        }

        /// <summary>Stops showing the chunk, ready to be reused.</summary>
        public void Hide()
        {
            Chunk = null;
            world = null;
            gameObject.SetActive(false);
            ChunkChanged?.Invoke(null);
        }

        /// <summary>
        /// Writes one of this chunk's samples through <see cref="World.SetDensity"/>, so
        /// every chunk sharing a border sample gets the same value (M2, A7).
        /// </summary>
        public void SetDensity(Vector3Int sample, float density)
        {
            world?.SetDensity(world.Grid.ChunkFirstSample(Coord) + sample, density);
        }

        private void LateUpdate()
        {
            if (Chunk != null && Chunk.IsDirty)
            {
                meshBuilder.Build(Chunk, settings());
                MeshRebuilt?.Invoke(meshBuilder.Mesh);
            }
        }

        private void OnDestroy()
        {
            meshBuilder?.Dispose();
        }
    }
}
