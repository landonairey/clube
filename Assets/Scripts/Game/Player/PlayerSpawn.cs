using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// Puts the player on the ground (M6): the world streams around the player, and once
    /// every chunk in the column under it has loaded, the player is set on the highest
    /// surface there and starts walking. Falling below the world spawns it again.
    /// </summary>
    /// <remarks>
    /// Spawns at the player's own x and z when enabled, so whatever enables the player
    /// (scene start, or the lab's camera toggle, M7) chooses where by moving it first.
    /// </remarks>
    [RequireComponent(typeof(PlayerController))]
    public class PlayerSpawn : MonoBehaviour
    {
        [SerializeField]
        private WorldView worldView;

        [Tooltip("How far below the world's bottom the player may fall before spawning again.")]
        [SerializeField, Min(0f)]
        private float fallLimit = 20f;

        private PlayerController player;
        private bool isPlaced;

        /// <summary>True once the player stands in the world; false while waiting for ground.</summary>
        public bool IsPlaced => isPlaced;

        private void Awake()
        {
            player = GetComponent<PlayerController>();
        }

        private void OnEnable()
        {
            if (worldView == null)
            {
                UnityEngine.Debug.LogWarning($"{nameof(PlayerSpawn)} on '{name}' has no {nameof(WorldView)} assigned.", this);
                return;
            }
            worldView.Focus = transform;
            Wait();
        }

        private void OnDisable()
        {
            if (worldView != null && worldView.Focus == transform)
            {
                worldView.Focus = null;
            }
        }

        private void Update()
        {
            if (worldView == null || !worldView.IsReady)
            {
                return;
            }

            if (!isPlaced)
            {
                TryPlace();
            }
            else if (worldView.transform.InverseTransformPoint(transform.position).y < -fallLimit)
            {
                Wait();
            }
        }

        private void Wait()
        {
            isPlaced = false;
            player.enabled = false;
        }

        private void TryPlace()
        {
            WorldGrid grid = worldView.World.Grid;
            Vector3 local = worldView.transform.InverseTransformPoint(transform.position);
            Vector3Int coord = grid.WorldToChunk(local);
            int layers = worldView.Config.WorldHeightInChunks;
            for (int layer = 0; layer < layers; layer++)
            {
                if (!worldView.TryGetRenderer(new Vector3Int(coord.x, layer, coord.z), out _))
                {
                    return;
                }
            }

            var top = new Vector3(local.x, layers * grid.ChunkWorldSize.y + 1f, local.z);
            Vector3 origin = worldView.transform.TransformPoint(top);
            if (!worldView.Raycast(new Ray(origin, -worldView.transform.up), out Vector3 ground))
            {
                // Nothing to stand on in this column (dug through?): drop in from the top.
                ground = origin;
            }

            player.Teleport(ground + worldView.transform.up * 0.05f);
            player.enabled = true;
            isPlaced = true;
        }
    }
}
