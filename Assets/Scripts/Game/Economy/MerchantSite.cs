using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// A merchant's table standing at a fixed place in the world (GL38): once the chunk column
    /// there has loaded, levels one build square of ground under it and puts the table on it.
    /// The leveling goes through <see cref="World.SetDensity"/> like any edit, so it's kept with
    /// the chunk and the borders agree. Done once per world: a regenerated world gets the table placed again.
    /// </summary>
    /// <remarks>
    /// The square is a cell of the build grid inside the chunk at x = 0, z = 0, so one chunk
    /// holds all of it. A ramp of one sample around the square lets the ground meet the level
    /// part without a lip.
    /// </remarks>
    public class MerchantSite : MonoBehaviour
    {
        // Metres of air left above the level square, for the table and the player at it.
        private const float Headroom = 3f;

        // Metres of ground made solid below it, so no gap shows if the ground was lower beside it.
        private const float Fill = 2f;

        [SerializeField]
        private WorldView worldView;

        [Tooltip("The table to stand on the level square.")]
        [SerializeField]
        private GameObject tablePrefab;

        [Tooltip("The build cell to level, counted from the world origin in build cells (x, z).")]
        [SerializeField]
        private Vector2Int cell = new Vector2Int(1, 1);

        private bool done;

        // The world the table was placed in: a regenerated world (another generator or seed) is new ground, so the table
        // is placed again on it (GL341).
        private World placedIn;

        /// <summary>The table once placed, or null.</summary>
        public GameObject Table { get; private set; }

        private void Update()
        {
            if (done && worldView != null && worldView.World != placedIn)
            {
                if (Table != null)
                {
                    Destroy(Table);
                    Table = null;
                }
                done = false;
            }
            if (done || worldView == null || !worldView.IsReady || tablePrefab == null)
            {
                return;
            }
            if (!ColumnLoaded())
            {
                return;
            }
            done = true;
            placedIn = worldView.World;
            Place();
        }

        // Every chunk layer of the column holding the cell, which is also where the ground is read.
        private bool ColumnLoaded()
        {
            World world = worldView.World;
            Vector3 centre = CellCentre();
            Vector3Int column = world.Grid.WorldToChunk(new Vector3(centre.x, 0f, centre.z));
            for (int y = 0; y < worldView.Config.WorldHeightInChunks; y++)
            {
                if (!world.IsLoaded(new Vector3Int(column.x, y, column.z)))
                {
                    return false;
                }
            }
            return true;
        }

        private Vector3 CellCentre()
        {
            float size = worldView.Config.BuildCellSize;
            return new Vector3((cell.x + 0.5f) * size, 0f, (cell.y + 0.5f) * size);
        }

        private void Place()
        {
            World world = worldView.World;
            float iso = worldView.Config.IsoLevel;
            Vector3 centre = CellCentre();
            MaterialRegistry materials = worldView.Config.Materials;
            float ground = GroundLeveling.GroundHeight(world, materials, centre, iso, worldView.Config.WorldHeightInChunks);

            // The square, and one sample around it for the ramp; trees on it are cut down whole (GL338).
            float half = worldView.Config.BuildCellSize * 0.5f + world.Grid.VoxelSize;
            GroundLeveling.Level(world, materials, new GroundLeveling.Area
            {
                Centre = new Vector2(centre.x, centre.z),
                Half = new Vector2(half, half),
                Height = ground,
                Headroom = Headroom,
                Fill = Fill,
            }, iso);

            Vector3 at = worldView.transform.TransformPoint(new Vector3(centre.x, ground, centre.z));
            Table = Instantiate(tablePrefab, at, Quaternion.identity, transform);
            Table.name = "Merchant table (spawn)";
        }
    }
}
