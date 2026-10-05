using System.Diagnostics;
using Clube.Core;
using UnityEngine;

namespace Clube.Debug
{
    /// <summary>
    /// Fills the chunk with the terrain the config's <see cref="TerrainSettings"/>
    /// describe (K8–K10), and regenerates whenever the config changes, including a
    /// new chunk size (G1). In the game the chunk manager will do this; in the lab
    /// it's this component.
    /// </summary>
    /// <remarks>
    /// <see cref="ChunkTestFill"/> overrides it: while the test fill is on, this
    /// holds back, and as soon as the test fill is turned off it regenerates the
    /// terrain. Both write the whole chunk, so only one may own it at a time.
    /// </remarks>
    [RequireComponent(typeof(ChunkView))]
    public class ChunkTerrainFill : MonoBehaviour
    {
        private ChunkView chunkView;
        private ChunkTestFill testFill;
        private WorldConfig subscribedConfig;
        private bool testFillWasActive;

        /// <summary>How long the last fill took, generator and density writes together.</summary>
        public double LastFillMilliseconds { get; private set; }

        /// <summary>Why the last fill was skipped (e.g. no heightmap assigned), or null if it ran.</summary>
        public string Problem { get; private set; }

        /// <summary>True while the test fill overrides this one.</summary>
        public bool IsOverridden => testFill != null && testFill.isActiveAndEnabled;

        private void Awake()
        {
            chunkView = GetComponent<ChunkView>();
            testFill = GetComponent<ChunkTestFill>();
        }

        private void OnEnable()
        {
            chunkView.ChunkChanged += OnChunkChanged;

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
            chunkView.ChunkChanged -= OnChunkChanged;
            if (subscribedConfig != null)
            {
                subscribedConfig.Changed -= Fill;
                subscribedConfig = null;
            }
        }

        // Takes the chunk back when the test fill is turned off.
        private void LateUpdate()
        {
            bool overridden = IsOverridden;
            if (testFillWasActive && !overridden)
            {
                Fill();
            }
            testFillWasActive = overridden;
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

        private void OnChunkChanged(Chunk chunk)
        {
            Fill();
        }

        private void Fill()
        {
            Chunk chunk = chunkView.Chunk;
            WorldConfig config = chunkView.Config;
            if (chunk == null || config == null || IsOverridden)
            {
                return;
            }

            var stopwatch = Stopwatch.StartNew();
            ITerrainGenerator generator;
            try
            {
                generator = TerrainGenerators.Create(config.Terrain);
            }
            catch (System.Exception exception) when (exception is System.InvalidOperationException || exception is System.ArgumentException)
            {
                // e.g. the heightmap generator with no heightmap assigned: keep the chunk as it is.
                if (Problem != exception.Message)
                {
                    UnityEngine.Debug.LogWarning($"Terrain fill skipped: {exception.Message}", this);
                }
                Problem = exception.Message;
                return;
            }

            Problem = null;

            // The chunk sits at the world origin; later chunks are offset by their coordinate (M1).
            ChunkGenerator.Fill(chunk, generator, Vector3.zero, config.VoxelSize);
            LastFillMilliseconds = stopwatch.Elapsed.TotalMilliseconds;
        }
    }
}
