using System;
using System.Collections.Generic;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// The chunk manager (M1, M3): keeps the chunks around a focus point (the camera or
    /// player) loaded and rendered, and unloads the rest. Chunks within the render
    /// distance horizontally, across every layer of the world's height, are wanted;
    /// missing ones load nearest first, a few per frame, so moving doesn't hitch.
    /// The world's sample (0,0,0) sits at this transform's origin.
    /// </summary>
    /// <remarks>
    /// <para>The data lives in <see cref="World"/>, which keeps chunk borders seamless
    /// (M2) and edits across borders consistent (M5); each loaded chunk gets a pooled
    /// <see cref="ChunkRenderer"/>.</para>
    /// <para>The render distance is a player setting (A3, M3), so it's set from outside:
    /// the game's settings in the Game scene, the lab panel in WorldLab.</para>
    /// <para>A config change that keeps the terrain (iso level, edge placement, shading)
    /// just rebuilds every mesh; one that changes it (generator, seed, sizes) regenerates
    /// the world, dropping edits.</para>
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

        [Tooltip("Chunks generated per frame at most, so loading is spread out.")]
        [SerializeField, Min(1)]
        private int chunksPerFrame = 4;

        private readonly Dictionary<Vector3Int, ChunkRenderer> renderers = new Dictionary<Vector3Int, ChunkRenderer>();
        private readonly Stack<ChunkRenderer> pool = new Stack<ChunkRenderer>();
        private readonly List<Vector3Int> wanted = new List<Vector3Int>();
        private readonly HashSet<Vector3Int> wantedSet = new HashSet<Vector3Int>();
        private readonly List<Vector3Int> scratch = new List<Vector3Int>();

        private WorldConfig runtimeConfig;
        private ITerrainGenerator generator;
        private string terrainFingerprint;
        private Vector3Int? wantedCentre;
        private int wantedDistance;
        private bool regenerateRequested;
        private bool remeshRequested;

        /// <summary>Raised when a chunk is loaded and given a renderer.</summary>
        public event Action<Vector3Int, ChunkRenderer> ChunkLoaded;

        /// <summary>Raised when a chunk is unloaded.</summary>
        public event Action<Vector3Int> ChunkUnloaded;

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
            set => renderDistance = Mathf.Clamp(value, MinRenderDistance, MaxRenderDistance);
        }

        public Transform Focus
        {
            get => focus != null ? focus : Camera.main != null ? Camera.main.transform : null;
            set => focus = value;
        }

        /// <summary>Wanted chunks still waiting to load.</summary>
        public int PendingCount
        {
            get
            {
                int pending = 0;
                foreach (Vector3Int coord in wanted)
                {
                    if (!renderers.ContainsKey(coord))
                    {
                        pending++;
                    }
                }
                return pending;
            }
        }

        /// <summary>Every loaded chunk's renderer by coordinate.</summary>
        public IReadOnlyDictionary<Vector3Int, ChunkRenderer> Renderers => renderers;

        /// <summary>The chunk under the focus point.</summary>
        public Vector3Int FocusChunk => World.Grid.WorldToChunk(transform.InverseTransformPoint(Focus.position));

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

        public int ApplyBrush(Vector3 worldCentre, BrushSettings brush, BrushOperation operation)
        {
            return World != null ? World.ApplyBrush(transform.InverseTransformPoint(worldCentre), brush, operation) : 0;
        }

        /// <summary>World position of a chunk's origin.</summary>
        public Vector3 ChunkWorldOrigin(Vector3Int coord)
        {
            return transform.TransformPoint(World.Grid.ChunkOrigin(coord));
        }

        public bool TryGetRenderer(Vector3Int coord, out ChunkRenderer chunkRenderer)
        {
            return renderers.TryGetValue(coord, out chunkRenderer);
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
                RestartWorld();
            }
            else if (remeshRequested)
            {
                remeshRequested = false;
                foreach (Chunk chunk in World.Chunks.Values)
                {
                    chunk.MarkDirty();
                }
            }

            UpdateWanted();
            UnloadUnwanted();
            LoadMissing();
        }

        private void StartWorld()
        {
            World = new World(Config.ChunkSize, Config.VoxelSize);
            terrainFingerprint = TerrainFingerprint();
            CreateGenerator();
            wantedCentre = null;
        }

        private void RestartWorld()
        {
            foreach (KeyValuePair<Vector3Int, ChunkRenderer> entry in renderers)
            {
                entry.Value.Hide();
                pool.Push(entry.Value);
                ChunkUnloaded?.Invoke(entry.Key);
            }
            renderers.Clear();
            StartWorld();
        }

        private void CreateGenerator()
        {
            try
            {
                generator = TerrainGenerators.Create(Config.Terrain);
                Problem = null;
            }
            catch (Exception exception) when (exception is InvalidOperationException || exception is ArgumentException)
            {
                // e.g. the heightmap generator with no heightmap: load empty chunks and say why.
                generator = null;
                Problem = exception.Message;
            }
        }

        // Only the terrain-shaping values force a regenerate; the rest just remesh.
        private string TerrainFingerprint()
        {
            return $"{Config.ChunkSize}|{Config.VoxelSize}|{Config.WorldHeightInChunks}|{JsonUtility.ToJson(Config.Terrain)}";
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

        // The wanted area only changes when the focus crosses into another chunk or the
        // distance changes, so it's rebuilt then, nearest chunks first.
        private void UpdateWanted()
        {
            Vector3Int centre = FocusChunk;
            if (wantedCentre == centre && wantedDistance == renderDistance)
            {
                return;
            }
            wantedCentre = centre;
            wantedDistance = renderDistance;

            wanted.Clear();
            wantedSet.Clear();
            int height = Config.WorldHeightInChunks;
            for (int dz = -renderDistance; dz <= renderDistance; dz++)
            {
                for (int dx = -renderDistance; dx <= renderDistance; dx++)
                {
                    if (dx * dx + dz * dz > renderDistance * renderDistance)
                    {
                        continue;
                    }
                    for (int y = 0; y < height; y++)
                    {
                        var coord = new Vector3Int(centre.x + dx, y, centre.z + dz);
                        wanted.Add(coord);
                        wantedSet.Add(coord);
                    }
                }
            }
            wanted.Sort((a, b) => HorizontalDistanceSquared(a, centre).CompareTo(HorizontalDistanceSquared(b, centre)));
        }

        // Chunks just outside the wanted area are kept (one chunk of slack), so walking
        // back and forth across a chunk border doesn't load and unload the same chunks.
        private void UnloadUnwanted()
        {
            Vector3Int centre = wantedCentre.Value;
            int keep = (renderDistance + 1) * (renderDistance + 1);
            scratch.Clear();
            foreach (Vector3Int coord in renderers.Keys)
            {
                if (!wantedSet.Contains(coord) && HorizontalDistanceSquared(coord, centre) > keep)
                {
                    scratch.Add(coord);
                }
            }
            foreach (Vector3Int coord in scratch)
            {
                ChunkRenderer chunkRenderer = renderers[coord];
                renderers.Remove(coord);
                chunkRenderer.Hide();
                pool.Push(chunkRenderer);
                World.Unload(coord);
                ChunkUnloaded?.Invoke(coord);
            }
        }

        private void LoadMissing()
        {
            int loadedThisFrame = 0;
            foreach (Vector3Int coord in wanted)
            {
                if (loadedThisFrame >= chunksPerFrame)
                {
                    break;
                }
                if (renderers.ContainsKey(coord))
                {
                    continue;
                }

                Chunk chunk = World.Load(coord, generator);
                ChunkRenderer chunkRenderer = pool.Count > 0 ? pool.Pop() : CreateRenderer();
                chunkRenderer.transform.localPosition = World.Grid.ChunkOrigin(coord);
                chunkRenderer.Show(coord, chunk, () => Config.MeshSettings);
                renderers.Add(coord, chunkRenderer);
                loadedThisFrame++;
                ChunkLoaded?.Invoke(coord, chunkRenderer);
            }
        }

        private ChunkRenderer CreateRenderer()
        {
            var chunkObject = new GameObject("Chunk", typeof(MeshFilter), typeof(MeshRenderer), typeof(ChunkRenderer));
            chunkObject.transform.SetParent(transform, false);
            chunkObject.GetComponent<MeshRenderer>().sharedMaterials = chunkMaterials;
            return chunkObject.GetComponent<ChunkRenderer>();
        }

        private static int HorizontalDistanceSquared(Vector3Int coord, Vector3Int centre)
        {
            int dx = coord.x - centre.x;
            int dz = coord.z - centre.z;
            return dx * dx + dz * dz;
        }
    }
}
