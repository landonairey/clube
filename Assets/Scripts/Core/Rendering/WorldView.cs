using System;
using System.Collections.Generic;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// A streamed world in the scene (M1, M3): keeps the chunks around a focus point (the
    /// camera or player) loaded, rendered and, near the focus, collidable, and unloads the
    /// rest. The world's sample (0,0,0) sits at this transform's origin.
    /// </summary>
    /// <remarks>
    /// <para>A façade over four parts, each with one job:</para>
    /// <list type="bullet">
    /// <item><see cref="World"/>: the chunk data, seamless borders (M2) and the edit paths (M5).</item>
    /// <item><see cref="ChunkPipeline"/>: generation, meshing and collider jobs on the worker
    /// threads (K35, P3).</item>
    /// <item><see cref="WorldStreamer"/>: what to load, mesh and bake each frame, nearest first,
    /// within a main-thread time budget; edits rebuild in the frame they happen.</item>
    /// <item><see cref="ChunkRendererPool"/>: the pooled GameObjects drawing chunks with a surface.</item>
    /// </list>
    /// <para>The render distance is a player setting (A3, M3), so it's set from outside:
    /// the game's settings in the Game scene, the lab panel in WorldLab.</para>
    /// <para>A config change that keeps the terrain (iso level, edge placement, shading,
    /// material display) rebuilds every mesh over the next frames; one that changes it
    /// (generator, seed, sizes) regenerates the world, dropping edits.</para>
    /// </remarks>
    public class WorldView : MonoBehaviour, IEditableTerrain
    {
        public const int MinRenderDistance = 1;
        public const int MaxRenderDistance = 32;

        [SerializeField]
        private WorldConfig config;

        [Tooltip("Materials for every chunk renderer (the terrain material, then ChunkDepthPrepass).")]
        [SerializeField]
        private Material[] chunkMaterials;

        [Tooltip("What chunks load around: the camera or player. Defaults to the main camera.")]
        [SerializeField]
        private Transform focus;

        [Tooltip("Radius in chunks, horizontally, of the area kept loaded (M3). Set by the player setting at runtime.")]
        [SerializeField, Range(MinRenderDistance, MaxRenderDistance)]
        private int renderDistance = 4;

        [Tooltip("Main-thread time per frame for taking finished chunks and meshes, in milliseconds. " +
                 "The jobs themselves run on worker threads; this caps how long the frame waits on their results.")]
        [SerializeField, Range(0.5f, 16f)]
        private float frameBudgetMilliseconds = 3f;

        [Tooltip("Jobs of each kind (generation, meshing, colliders) running at once. 0: two per worker thread.")]
        [SerializeField, Min(0)]
        private int maxJobsInFlight;

        [Tooltip("Give chunks near the focus a MeshCollider, so a player can walk on the terrain (M6).")]
        [SerializeField]
        private bool chunkColliders = true;

        [Tooltip("How far from the focus, horizontally in metres, chunks keep a collider. Collision shapes are cooked in jobs.")]
        [SerializeField, Min(1f)]
        private float colliderRadius = 24f;

        private WorldConfig runtimeConfig;
        private ITerrainGenerator generator;
        private OreField ores;
        private ChunkRendererPool renderers;
        private WorldStreamer streamer;
        private string terrainFingerprint;
        private bool regenerateRequested;
        private bool remeshRequested;

        /// <summary>Raised when a chunk's data is loaded into the world (it may not have a renderer: see <see cref="ChunkMeshed"/>).</summary>
        public event Action<Vector3Int> ChunkLoaded;

        /// <summary>Raised when a chunk is unloaded.</summary>
        public event Action<Vector3Int> ChunkUnloaded;

        /// <summary>Raised after a chunk's mesh is (re)built; the renderer is null for a chunk without a surface or renderer.</summary>
        public event Action<Vector3Int, ChunkRenderer> ChunkMeshed;

        public World World { get; private set; }

        /// <summary>The config in use: the Play mode copy while playing, otherwise the assigned asset.</summary>
        public WorldConfig Config => runtimeConfig != null ? runtimeConfig : config;

        /// <summary>Why the terrain couldn't be generated (e.g. no heightmap), or null.</summary>
        public string Problem { get; private set; }

        public bool IsReady => World != null;

        /// <summary>Radius in chunks of the loaded area, horizontally (M3).</summary>
        public int RenderDistance
        {
            get => renderDistance;
            set
            {
                renderDistance = Mathf.Clamp(value, MinRenderDistance, MaxRenderDistance);
                if (streamer != null)
                {
                    streamer.RenderDistance = renderDistance;
                }
            }
        }

        public Transform Focus
        {
            get => focus != null ? focus : Camera.main != null ? Camera.main.transform : null;
            set => focus = value;
        }

        /// <summary>Wanted chunks still waiting to load.</summary>
        public int PendingCount => streamer?.PendingCount ?? 0;

        /// <summary>Live streaming counts: jobs in flight, renderers, vertices, main-thread time.</summary>
        public StreamingStats Stats => streamer?.Stats ?? default;

        /// <summary>Every renderer showing a chunk, by coordinate. Chunks without a surface have none.</summary>
        public IReadOnlyDictionary<Vector3Int, ChunkRenderer> Renderers => renderers != null
            ? renderers.Active
            : (IReadOnlyDictionary<Vector3Int, ChunkRenderer>)new Dictionary<Vector3Int, ChunkRenderer>();

        /// <summary>Where ore is placed (3D), for lab views; null when the terrain has no ores.</summary>
        public OreField Ores => ores;

        /// <summary>The chunk under the focus point.</summary>
        public Vector3Int FocusChunk => World.Grid.WorldToChunk(transform.InverseTransformPoint(Focus.position));

        public float VoxelSize => Config.VoxelSize;

        public bool Raycast(Ray worldRay, out Vector3 worldPoint)
        {
            bool hit = Raycast(worldRay, out WorldHit worldHit);
            worldPoint = hit ? transform.TransformPoint(worldHit.Point) : default;
            return hit;
        }

        /// <summary>Where a world-space ray first meets the loaded terrain, with the chunk and voxel hit.</summary>
        public bool Raycast(Ray worldRay, out WorldHit hit)
        {
            hit = default;
            if (World == null)
            {
                return false;
            }
            var localRay = new Ray(transform.InverseTransformPoint(worldRay.origin), transform.InverseTransformDirection(worldRay.direction));
            float maxDistance = (renderDistance + 2) * World.Grid.ChunkWorldSize.magnitude * 2f;
            return World.Raycast(localRay, Config.MeshSettings, maxDistance, out hit);
        }

        public BrushResult ApplyBrush(Vector3 worldCentre, BrushSettings brush, BrushOperation operation)
        {
            return World != null ? World.ApplyBrush(transform.InverseTransformPoint(worldCentre), brush, operation) : default;
        }

        /// <summary>World position of a chunk's origin.</summary>
        public Vector3 ChunkWorldOrigin(Vector3Int coord)
        {
            return transform.TransformPoint(World.Grid.ChunkOrigin(coord));
        }

        public bool TryGetRenderer(Vector3Int coord, out ChunkRenderer chunkRenderer)
        {
            chunkRenderer = null;
            return renderers != null && renderers.TryGet(coord, out chunkRenderer);
        }

        /// <summary>
        /// True once the chunk column under a world position is loaded, meshed and has its
        /// colliders: the ground there can be stood on (M6).
        /// </summary>
        public bool HasGround(Vector3 worldPosition)
        {
            if (streamer == null)
            {
                return false;
            }
            Vector3Int coord = World.Grid.WorldToChunk(transform.InverseTransformPoint(worldPosition));
            return streamer.IsColumnSettled(new Vector2Int(coord.x, coord.z));
        }

        private void Awake()
        {
            if (config == null)
            {
                UnityEngine.Debug.LogError($"{nameof(WorldView)} on '{name}' has no {nameof(WorldConfig)} assigned.", this);
                enabled = false;
                return;
            }

            runtimeConfig = Instantiate(config);
            runtimeConfig.name = $"{config.name} (Play mode copy)";
            renderers = new ChunkRendererPool(transform, chunkColliders, TerrainRenderMaterials.For(chunkMaterials, Config));
            StartWorld();
        }

        private void OnEnable()
        {
            if (Config != null)
            {
                Config.Changed += OnConfigChanged;
            }
        }

        private void OnDisable()
        {
            if (Config != null)
            {
                Config.Changed -= OnConfigChanged;
            }
        }

        private void OnDestroy()
        {
            // The scene is going: free the jobs' native memory, leave the renderers to Unity.
            streamer?.Dispose();
            streamer = null;
            if (runtimeConfig != null)
            {
                Destroy(runtimeConfig);
            }
        }

        private void Update()
        {
            if (World == null || Focus == null)
            {
                return;
            }

            if (regenerateRequested)
            {
                regenerateRequested = false;
                remeshRequested = false;
                renderers.SetMaterials(TerrainRenderMaterials.For(chunkMaterials, Config));
                StopWorld();
                StartWorld();
            }
            else if (remeshRequested)
            {
                remeshRequested = false;
                renderers.SetMaterials(TerrainRenderMaterials.For(chunkMaterials, Config));
                streamer.SealEdges = Config.SealEdges;
                streamer.RemeshAll();
            }

            streamer.Update(FocusLocal);
        }

        // After the frame's edits (tools and the player edit in Update).
        private void LateUpdate()
        {
            if (streamer != null && Focus != null)
            {
                streamer.RebuildEdited(FocusLocal);
            }
        }

        private Vector3 FocusLocal => transform.InverseTransformPoint(Focus.position);

        private void StartWorld()
        {
            if (!ChunkIndexBudget.Fits16Bit(Config.ChunkSize) && !SystemInfo.supports32bitsIndexBuffer)
            {
                UnityEngine.Debug.LogWarning(
                    $"{Config.ChunkSize.x}x{Config.ChunkSize.y}x{Config.ChunkSize.z} chunks can need up to " +
                    $"{ChunkIndexBudget.MaxVertices(Config.ChunkSize):N0} vertices, but this GPU has no 32-bit index buffers; " +
                    "use 16³ chunks or smaller.", this);
            }
            World = new World(Config.ChunkSize, Config.VoxelSize, Config.CreateStorage);
            terrainFingerprint = TerrainFingerprint();
            CreateGenerator();
            var pipeline = new ChunkPipeline(World.Grid, generator, Config.Terrain.Layers, ores, World.PreferredFormat, World.StorageFactory);
            streamer = new WorldStreamer(World, pipeline, renderers, () => Config.MeshSettings, new StreamingSettings
            {
                RenderDistance = renderDistance,
                HeightInChunks = Config.WorldHeightInChunks,
                MaxJobsInFlight = maxJobsInFlight,
                FrameBudgetMilliseconds = frameBudgetMilliseconds,
                Colliders = chunkColliders,
                ColliderRadius = colliderRadius,
                MaxEditsPerFrame = 32,
                FixedColumns = Config.FixedColumns,
                SealEdges = Config.SealEdges,
            });
            streamer.ChunkLoaded += coord => ChunkLoaded?.Invoke(coord);
            streamer.ChunkUnloaded += coord => ChunkUnloaded?.Invoke(coord);
            streamer.ChunkMeshed += (coord, chunkRenderer) => ChunkMeshed?.Invoke(coord, chunkRenderer);
        }

        // Waits for every job and pools every renderer; the world's data goes with it.
        private void StopWorld()
        {
            streamer?.UnloadAll();
            streamer?.Dispose();
            streamer = null;

            // Edits go with the old terrain; their memory goes back to the pool for the new one.
            World?.Clear();
        }

        private void CreateGenerator()
        {
            try
            {
                generator = TerrainGenerators.Create(Config.Terrain);
                ores = OreField.Create(Config.Terrain, generator);
                Problem = null;
            }
            catch (Exception exception) when (exception is InvalidOperationException || exception is ArgumentException)
            {
                // e.g. the heightmap generator with no heightmap: load empty chunks and say why.
                generator = null;
                ores = null;
                Problem = exception.Message;
            }
        }

        // Only the terrain-shaping values force a regenerate; the rest just remesh.
        private string TerrainFingerprint()
        {
            return $"{Config.ChunkSize}|{Config.VoxelSize}|{Config.WorldHeightInChunks}|{Config.Storage}|{Config.OctreeMaxDepth}|{Config.FixedColumns}|" +
                   JsonUtility.ToJson(Config.Terrain);
        }

        private void OnConfigChanged()
        {
            if (TerrainFingerprint() != terrainFingerprint)
            {
                regenerateRequested = true;
            }
            else
            {
                remeshRequested = true;
            }
        }
    }
}
