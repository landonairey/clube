using System.Diagnostics;
using Clube.Core;
using UnityEngine;

namespace Clube.Debug
{
    /// <summary>
    /// Fills the chunk with the terrain the config's <see cref="TerrainSettings"/>
    /// describe (K8–K10), and regenerates whenever the config changes, including a
    /// new chunk size (G1). In the game the chunk manager will do this; in the lab
    /// it's this component. Turning it on turns <see cref="ChunkTestFill"/> off, and
    /// the other way round, since both write the whole chunk.
    /// </summary>
    [RequireComponent(typeof(ChunkView))]
    public class ChunkTerrainFill : MonoBehaviour
    {
        private ChunkView chunkView;
        private WorldConfig subscribedConfig;

        /// <summary>How long the last fill took, generator and density writes together.</summary>
        public double LastFillMilliseconds { get; private set; }

        private void Awake()
        {
            chunkView = GetComponent<ChunkView>();
        }

        private void OnEnable()
        {
            if (TryGetComponent(out ChunkTestFill testFill))
            {
                testFill.enabled = false;
            }

            chunkView.ChunkCreated += OnChunkCreated;

            // When the scene starts, this can run before ChunkView's Awake makes the Play
            // mode config copy and the chunk; Start handles that case. When re-enabled later,
            // everything already exists.
            SubscribeToConfig();
            Fill();
        }

        private void Start()
        {
            SubscribeToConfig();
            Fill();
        }

        private void OnDisable()
        {
            chunkView.ChunkCreated -= OnChunkCreated;
            if (subscribedConfig != null)
            {
                subscribedConfig.Changed -= Fill;
                subscribedConfig = null;
            }
        }

        // Follows whichever config the view uses now: the Play mode copy once it exists,
        // which is the one Inspector edits go to.
        private void SubscribeToConfig()
        {
            WorldConfig config = chunkView.Config;
            if (config == subscribedConfig)
            {
                return;
            }

            if (subscribedConfig != null)
            {
                subscribedConfig.Changed -= Fill;
            }
            subscribedConfig = config;
            if (subscribedConfig != null)
            {
                subscribedConfig.Changed += Fill;
            }
        }

        private void OnChunkCreated(Chunk chunk)
        {
            Fill();
        }

        private void Fill()
        {
            Chunk chunk = chunkView.Chunk;
            WorldConfig config = chunkView.Config;
            if (chunk == null || config == null)
            {
                return;
            }

            var stopwatch = Stopwatch.StartNew();
            ITerrainGenerator generator = TerrainGenerators.Create(config.Terrain);

            // The chunk sits at the world origin; later chunks are offset by their coordinate (M1).
            ChunkGenerator.Fill(chunk, generator, Vector3.zero, config.VoxelSize);
            LastFillMilliseconds = stopwatch.Elapsed.TotalMilliseconds;
        }
    }
}
