using System.Collections.Generic;
using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// Lets the ground fall (GL308, GL327): runs a <see cref="GroundSettling"/> on the world of the
    /// <see cref="WorldView"/> beside it, a step every <see cref="stepSeconds"/>, so ground hanging further out than its
    /// material holds drops a sample (a quarter metre at the game's voxel size) a step and sand runs down steep slopes.
    /// </summary>
    [RequireComponent(typeof(WorldView))]
    public sealed class FallingGround : MonoBehaviour
    {
        [Tooltip("Seconds between steps; each step moves falling ground one sample.")]
        [SerializeField, Range(0.02f, 0.5f)]
        private float stepSeconds = 0.06f;

        [Tooltip("Most samples moved in a step; the rest wait for the next.")]
        [SerializeField, Range(50, 10000)]
        private int maxMovesPerStep = 1500;

        private WorldView worldView;
        private GroundSettling settling;
        private float nextStep;
        private readonly HashSet<Vector3Int> pinned = new HashSet<Vector3Int>();

        /// <summary>Samples waiting to be looked at (for tests and the HUD).</summary>
        public int AwakeCount => settling?.AwakeCount ?? 0;

        /// <summary>Holds these samples still, and what rests on them up, whatever they're made of (a structure's stones, GL688).</summary>
        public void Pin(IEnumerable<Vector3Int> samples)
        {
            pinned.UnionWith(samples);
        }

        /// <summary>Lets these samples fall like their material again; they wake, so ground left hanging by them goes too.</summary>
        public void Unpin(IEnumerable<Vector3Int> samples)
        {
            foreach (Vector3Int sample in samples)
            {
                if (pinned.Remove(sample))
                {
                    settling?.Wake(sample);
                }
            }
        }

        private void Awake()
        {
            worldView = GetComponent<WorldView>();
        }

        private void Update()
        {
            using FrameCost.Scope cost = FrameCost.Of("Falling ground");
            World world = worldView.World;
            if (world == null)
            {
                return;
            }
            if (settling == null || settling.World != world)
            {
                settling?.Dispose();
                MaterialRegistry materials = worldView.Config.Materials;
                settling = new GroundSettling(world, id => materials != null && materials.Get(id) != null ? materials.Get(id).Hold : default,
                    worldView.Config.IsoLevel, pinned);
            }
            if (Time.time < nextStep)
            {
                return;
            }
            nextStep = Time.time + stepSeconds;
            settling.Step(maxMovesPerStep);
        }

        private void OnDestroy()
        {
            settling?.Dispose();
        }
    }
}
