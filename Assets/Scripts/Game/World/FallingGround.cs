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

        /// <summary>Samples waiting to be looked at (for tests and the HUD).</summary>
        public int AwakeCount => settling?.AwakeCount ?? 0;

        private void Awake()
        {
            worldView = GetComponent<WorldView>();
        }

        private void Update()
        {
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
                    worldView.Config.IsoLevel);
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
