using System.Collections.Generic;
using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// Keeps clams lying on the lake and ocean beds round the player (GL735): the cells of <see cref="ClamScatter"/> near
    /// the player that hold a clam and whose spot is under water get one, found from the ground down (<see cref="GroundProbe"/>
    /// and <see cref="WaterQuery"/>); clams far off are put away, and an opened clam's cell stays empty for the session.
    /// Made at run time by <see cref="ClamBedsStarter"/>, which also puts a <see cref="ClamPanel"/> on the player.
    /// </summary>
    public class ClamBeds : MonoBehaviour
    {
        private const float Radius = 22f, KeepRadius = 30f, Interval = 0.5f, MinDepth = 0.3f;
        private const int Seed = 735;

        private WorldView worldView;
        private PlayerController player;
        private readonly Dictionary<Vector2Int, Clam> clams = new Dictionary<Vector2Int, Clam>();
        private readonly HashSet<Vector2Int> gone = new HashSet<Vector2Int>();
        private readonly HashSet<Vector2Int> dry = new HashSet<Vector2Int>();
        private readonly List<Vector2Int> drop = new List<Vector2Int>();
        private float next;

        public int Count => clams.Count;

        public void Bind(WorldView view, PlayerController who)
        {
            worldView = view;
            player = who;
        }

        /// <summary>An opened clam's cell never holds another.</summary>
        public void Opened(Vector2Int cell)
        {
            gone.Add(cell);
            clams.Remove(cell);
        }

        private void Update()
        {
            if (worldView == null || !worldView.IsReady || player == null || Time.time < next)
            {
                return;
            }
            next = Time.time + Interval;
            Vector3 at = player.transform.position;

            drop.Clear();
            foreach (KeyValuePair<Vector2Int, Clam> pair in clams)
            {
                if (pair.Value == null)
                {
                    drop.Add(pair.Key);
                }
                else if ((pair.Value.transform.position - at).sqrMagnitude > KeepRadius * KeepRadius)
                {
                    Destroy(pair.Value.gameObject);
                    drop.Add(pair.Key);
                }
            }
            foreach (Vector2Int key in drop)
            {
                clams.Remove(key);
            }

            Vector2Int centre = ClamScatter.CellOf(at.x, at.z);
            int span = Mathf.CeilToInt(Radius / ClamScatter.CellSize);
            for (int dx = -span; dx <= span; dx++)
            {
                for (int dz = -span; dz <= span; dz++)
                {
                    var cell = new Vector2Int(centre.x + dx, centre.y + dz);
                    if (clams.ContainsKey(cell) || gone.Contains(cell) || dry.Contains(cell)
                        || !ClamScatter.TryClam(cell, Seed, out Vector2 spot)
                        || (new Vector2(at.x, at.z) - spot).sqrMagnitude > Radius * Radius)
                    {
                        continue;
                    }
                    if (TryBed(spot, at.y, out Vector3 bed, out bool loaded))
                    {
                        clams[cell] = Clam.Make(this, cell, bed);
                    }
                    else if (loaded)
                    {
                        // Dry land stays dry; a spot with no terrain loaded yet is looked at again.
                        dry.Add(cell);
                    }
                }
            }
        }

        private bool TryBed(Vector2 spot, float near, out Vector3 bed, out bool loaded)
        {
            bed = default;
            loaded = GroundProbe.TryGround(worldView, spot.x, spot.y, near, out float ground);
            if (!loaded)
            {
                return false;
            }
            bed = new Vector3(spot.x, ground, spot.y);
            return WaterQuery.IsUnderwater(worldView.World, bed + Vector3.up * MinDepth);
        }
    }
}
