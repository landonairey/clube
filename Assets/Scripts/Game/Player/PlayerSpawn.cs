using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// Puts the player on the ground (M6): the world streams around the player, and once
    /// every chunk in the column under it has loaded, been meshed and had its collider cooked
    /// (<see cref="WorldView.HasGround"/>), the player is set on the highest surface under its
    /// whole capsule there and starts walking. While it waits it's held at the top of the world,
    /// so its view is never inside a hill. Falling below the world, or being found inside solid
    /// ground (dropped into a slope, or a collider that wasn't there), spawns it again on top.
    /// </summary>
    /// <remarks>
    /// Spawns at the player's own x and z when enabled, so whatever enables the player
    /// (scene start, or the lab's camera toggle, M7) chooses where by moving it first.
    /// </remarks>
    [RequireComponent(typeof(PlayerController))]
    public class PlayerSpawn : MonoBehaviour
    {
        // Points around the capsule's edge sampled for the highest ground under it.
        private const int FootprintPoints = 8;

        [SerializeField]
        private WorldView worldView;

        [Tooltip("How far below the world's bottom the player may fall before spawning again.")]
        [SerializeField, Min(0f)]
        private float fallLimit = 20f;

        [Tooltip("Seconds the player's body may stay inside solid ground before it's put back on the surface.")]
        [SerializeField, Min(0f)]
        private float stuckSeconds = 0.25f;

        [Tooltip("The ground within this many metres must be loaded, written (roads, settlements) and meshed before the player is set down (GL653); 0 waits only for the column under the player.")]
        [SerializeField, Min(0f)]
        private float settleRadius = 32f;

        private PlayerController player;
        private bool isPlaced;

        /// <summary>While waiting for ground: the share of the columns round the spawn that are ready, 0 to 1.</summary>
        public float Progress { get; private set; }
        private float buriedFor;

        // A saved position to set the player down at instead of the highest ground (GL205), once.
        private Vector3? placeAt;

        /// <summary>True once the player stands in the world; false while waiting for ground.</summary>
        public bool IsPlaced => isPlaced;

        /// <summary>
        /// Sets the player down where a save left it (GL205), in a cave or a pit too, once the ground there has
        /// loaded; until then it waits at the top of the world like any spawn.
        /// </summary>
        public void PlaceAt(Vector3 position)
        {
            placeAt = position;
            transform.position = position;
            Wait();
        }

        /// <summary>
        /// Moves the player over a point (world space) and sets it down on the highest ground there once it has loaded,
        /// like a fresh spawn (GL479: the tutorial's quarry, then the starting parcel).
        /// </summary>
        public void SetDownOver(Vector3 position)
        {
            placeAt = null;
            transform.position = position;
            Wait();
        }

        private void Awake()
        {
            player = GetComponent<PlayerController>();
            // Every game scene covers its wait for ground (GL780), not only the ones whose prefab was given a curtain.
            if (GetComponent<LoadCurtain>() == null)
            {
                gameObject.AddComponent<LoadCurtain>();
            }
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
            SetHurry(false);
            if (worldView != null && worldView.Focus == transform)
            {
                worldView.Focus = null;
            }
        }

        // While the player waits nothing is being watched: the world may spend its frames on getting the ground ready.
        private void SetHurry(bool hurry)
        {
            if (worldView != null)
            {
                worldView.Hurry = hurry;
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
                return;
            }
            if (worldView.transform.InverseTransformPoint(transform.position).y < -fallLimit)
            {
                Wait();
                return;
            }

            // Inside the ground for more than a moment: back on the surface above.
            buriedFor = IsBuried() ? buriedFor + Time.deltaTime : 0f;
            if (buriedFor > stuckSeconds)
            {
                Wait();
            }
        }

        // Stops the player and holds it at the top of the world over its column until there's ground.
        private void Wait()
        {
            isPlaced = false;
            SetHurry(true);
            Progress = 0f;
            buriedFor = 0f;
            player.enabled = false;
            if (worldView != null && worldView.IsReady)
            {
                player.Teleport(TopOfColumn());
            }
        }

        private void TryPlace()
        {
            // Every chunk in the column loaded, meshed, and its collider cooked: ground to stand on.
            // And the ground round it: nothing is seen loading or being written from where the player stands.
            bool around = worldView.IsSettledAround(transform.position, settleRadius, out int settled, out int columns);
            Progress = columns > 0 ? (float)settled / columns : 0f;
            if (!around || !worldView.HasGround(transform.position))
            {
                return;
            }
            Progress = 1f;

            if (placeAt.HasValue)
            {
                player.Teleport(placeAt.Value);
                placeAt = null;
                player.enabled = true;
                isPlaced = true;
                SetHurry(false);
                return;
            }

            if (!TryHighestGround(out Vector3 ground))
            {
                // Nothing to stand on in this column (dug through?): drop in from the top.
                ground = TopOfColumn();
            }

            player.Teleport(ground + worldView.transform.up * 0.05f);
            player.enabled = true;
            isPlaced = true;
            SetHurry(false);
        }

        // Just above the world's top layer, over the player's x and z (world space).
        private Vector3 TopOfColumn()
        {
            WorldGrid grid = worldView.World.Grid;
            Vector3 local = worldView.transform.InverseTransformPoint(transform.position);
            float top = worldView.Config.WorldHeightInChunks * grid.ChunkWorldSize.y + 1f;
            return worldView.transform.TransformPoint(new Vector3(local.x, top, local.z));
        }

        // The highest surface under the capsule: its centre and points around its edge, so a
        // capsule set on a steep slope doesn't start half inside the hillside.
        private bool TryHighestGround(out Vector3 ground)
        {
            Vector3 origin = TopOfColumn();
            Vector3 down = -worldView.transform.up;
            float radius = player.Body.radius;
            bool found = false;
            ground = default;
            float highest = float.NegativeInfinity;
            for (int i = 0; i <= FootprintPoints; i++)
            {
                Vector3 offset = Vector3.zero;
                if (i > 0)
                {
                    float angle = (i - 1) * Mathf.PI * 2f / FootprintPoints;
                    offset = worldView.transform.TransformDirection(new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle))) * radius;
                }
                if (!worldView.Raycast(new Ray(origin + offset, down), out Vector3 hit))
                {
                    continue;
                }
                float height = worldView.transform.InverseTransformPoint(hit).y;
                if (height > highest)
                {
                    highest = height;
                    found = true;
                }
            }
            if (found)
            {
                Vector3 local = worldView.transform.InverseTransformPoint(origin);
                ground = worldView.transform.TransformPoint(new Vector3(local.x, highest, local.z));
            }
            return found;
        }

        // True when the body's middle and the head are both in solid ground (world data, so it
        // holds even where a collider is missing).
        private bool IsBuried()
        {
            Vector3 head = player.Head != null ? player.Head.position : transform.position + transform.up * player.Body.height;
            Vector3 middle = transform.position + transform.up * (player.Body.height * 0.5f);
            return IsSolid(middle) && IsSolid(head);
        }

        private bool IsSolid(Vector3 worldPosition)
        {
            World world = worldView.World;
            Vector3 local = worldView.transform.InverseTransformPoint(worldPosition);
            Vector3Int sample = Vector3Int.RoundToInt(local / world.Grid.VoxelSize);
            float? density = world.GetDensity(sample);
            return density.HasValue && density.Value >= worldView.Config.IsoLevel;
        }
    }
}
