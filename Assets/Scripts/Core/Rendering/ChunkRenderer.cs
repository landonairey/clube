using System;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// Shows one chunk of a <see cref="WorldView"/>: owns its mesh, and is handed each new
    /// build by the world's <see cref="WorldStreamer"/> (which runs the mesh jobs, K35).
    /// Created and pooled by <see cref="ChunkRendererPool"/>, which places it at the chunk's
    /// origin; only chunks with a surface get one.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class ChunkRenderer : MonoBehaviour, IRenderedChunk
    {
        private Mesh mesh;
        private MeshRenderer meshRenderer;
        private Func<ChunkMeshSettings> settings;
        private World world;

        /// <summary>Raised after each new mesh (A4: lab tools hook in here); the chunk is at <see cref="Coord"/>.</summary>
        public event Action<Mesh> MeshRebuilt;

        /// <summary>Raised when the renderer is given another chunk, or pooled (null).</summary>
        public event Action<Chunk> ChunkChanged;

        /// <summary>The chunk shown, or null while pooled.</summary>
        public Chunk Chunk { get; private set; }

        /// <summary>The chunk's coordinate in the world (M1).</summary>
        public Vector3Int Coord { get; private set; }

        public ChunkMeshStats LastBuildStats { get; private set; }

        /// <summary>Counts up with every new mesh, so a collider baked for an older one can be spotted.</summary>
        public int MeshVersion { get; private set; }

        public Mesh Mesh => mesh;

        public Transform Transform => transform;

        public ChunkMeshSettings MeshSettings => settings();

        public Renderer Renderer => meshRenderer;

        /// <summary>The chunk's collider (M6), or null when the world has none.</summary>
        public ChunkCollider Collider { get; private set; }

        /// <summary>Shows a chunk; its mesh arrives from the streamer.</summary>
        /// <param name="world">The world the chunk belongs to; edits go through it (M2).</param>
        /// <param name="meshSettings">The settings meshes are built with, read when asked, so config changes apply.</param>
        public void Show(World world, Vector3Int coord, Chunk chunk, Func<ChunkMeshSettings> meshSettings)
        {
            EnsureComponents();
            this.world = world;
            Coord = coord;
            Chunk = chunk;
            settings = meshSettings;
            name = $"Chunk {coord.x}, {coord.y}, {coord.z}";
            gameObject.SetActive(true);
            ChunkChanged?.Invoke(chunk);
        }

        /// <summary>Stops showing the chunk, ready to be reused.</summary>
        public void Hide()
        {
            Chunk = null;
            world = null;
            Collider?.Clear();
            mesh?.Clear();
            // The counts were the old chunk's; the streamer's totals already took them off.
            LastBuildStats = default;
            gameObject.SetActive(false);
            ChunkChanged?.Invoke(null);
        }

        /// <summary>
        /// Takes a finished mesh job's result (main thread) and tells the lab tools. The caller
        /// first finishes any collider bake still reading the old mesh. The stats' meshing time
        /// is 0: it ran in a job, off this thread; the upload time is handing it to the mesh.
        /// </summary>
        public void ApplyMesh(ChunkPipeline.MeshResult result)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            result.ApplyTo(mesh);
            MeshVersion++;
            LastBuildStats = new ChunkMeshStats(mesh.vertexCount, (int)mesh.GetIndexCount(0) / 3, 0, stopwatch.Elapsed.TotalMilliseconds);
            MeshRebuilt?.Invoke(mesh);
        }

        /// <summary>Empties the mesh: the chunk lost its surface (dug or filled out of it).</summary>
        public void ClearMesh()
        {
            mesh.Clear();
            MeshVersion++;
            LastBuildStats = default;
            MeshRebuilt?.Invoke(mesh);
        }

        /// <summary>
        /// Writes one of this chunk's samples through <see cref="World.SetDensity"/>, so
        /// every chunk sharing a border sample gets the same value (M2, A7).
        /// </summary>
        public void SetDensity(Vector3Int sample, float density)
        {
            world?.SetDensity(world.Grid.ChunkFirstSample(Coord) + sample, density);
        }

        private void EnsureComponents()
        {
            if (mesh != null)
            {
                return;
            }
            mesh = new Mesh { name = "World Chunk" };
            mesh.MarkDynamic();
            GetComponent<MeshFilter>().sharedMesh = mesh;
            meshRenderer = GetComponent<MeshRenderer>();
            Collider = GetComponent<ChunkCollider>();
        }

        private void OnDestroy()
        {
            if (mesh == null)
            {
                return;
            }
            // Benchmarks stream worlds outside Play mode, where only DestroyImmediate works.
            if (Application.isPlaying)
            {
                Destroy(mesh);
            }
            else
            {
                DestroyImmediate(mesh);
            }
        }
    }
}
