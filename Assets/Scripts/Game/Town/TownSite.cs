using System.Collections.Generic;
using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// Puts the town in the world (GL399, GL400): once the world and its water map are ready, picks the site with
    /// <see cref="TownSiteFinder"/> (near tin, copper, silver and gold ore and water; inside the fixed area, or within
    /// <see cref="maxDistance"/> of the player's spawn in a streamed world), builds the town there with
    /// <see cref="TownBuilder"/>, and when the chunks under it have loaded levels its ground and lays the cobbled paths
    /// (<see cref="GroundLeveling"/>, through the world's edit path). The site follows from the seed, so a loaded game gets
    /// the same town; ground already edited there (a save) isn't levelled again. A regenerated world gets a new town.
    /// </summary>
    public class TownSite : MonoBehaviour
    {
        // Metres of air made over the levelled ground, enough to clear a boulder off it, and of ground made under it.
        private const float Headroom = 10f;
        private const float Fill = 2f;

        [SerializeField]
        private WorldView worldView;

        [Tooltip("The player: where it stands when the scene starts is the spawn the town keeps near and faces its gate to.")]
        [SerializeField]
        private Transform spawn;

        [Tooltip("The town's shops, one of each kind (GL401).")]
        [SerializeField]
        private List<ShopSpecialty> shops = new List<ShopSpecialty>();

        [Tooltip("The merchant price list every shop's prices come from.")]
        [SerializeField]
        private PriceList prices;

        [Tooltip("The Clube/Low Poly material the town's models are drawn with.")]
        [SerializeField]
        private Material material;

        [Tooltip("When the shopkeepers trade, do chores and sleep (GL404-GL406).")]
        [SerializeField]
        private ShopHours hours = ShopHours.Default;

        [Tooltip("In a streamed world, the most metres from the spawn to the town's centre (GL399).")]
        [SerializeField, Min(100f)]
        private float maxDistance = 1000f;

        [Tooltip("The fewest metres from the spawn to the town's centre, so the starting land stays clear.")]
        [SerializeField, Min(0f)]
        private float keepFromSpawn = 70f;

        [Tooltip("Seconds to wait for the water map before placing the town without water.")]
        [SerializeField, Min(0f)]
        private float waterWaitSeconds = 30f;

        [Tooltip("The material the paths are cobbled with (GL407).")]
        [SerializeField]
        private string pathMaterial = "Loose stone";

        [Tooltip("The material of the trodden ground inside the walls, between the paths.")]
        [SerializeField]
        private string groundMaterial = "Dirt";

        [Tooltip("Milliseconds a frame the site search may take (GL399).")]
        [SerializeField, Range(0.5f, 20f)]
        private float searchBudgetMilliseconds = 4f;

        private TownSiteSearch search;
        private float searchMilliseconds;

        private Vector2 spawnPoint;
        private World placedIn;
        private float waitedForWater;
        private bool searched;
        private bool levelled;

        /// <summary>The site picked, once searched; <see cref="TownPlacement.Found"/> is false when none qualified.</summary>
        public TownPlacement Site { get; private set; }

        /// <summary>The town's root (its centre on the levelled ground), once built.</summary>
        public Transform Town { get; private set; }

        /// <summary>The town's turn about y: the gate faces the spawn.</summary>
        public float Yaw { get; private set; }

        /// <summary>The shopkeepers, once built.</summary>
        public IReadOnlyList<Shopkeeper> Keepers { get; private set; } = new List<Shopkeeper>();

        /// <summary>True once the ground under the town has been levelled (or was found already edited).</summary>
        public bool IsLevelled => levelled;

        /// <summary>Milliseconds the levelling took, for the docs and tuning.</summary>
        public float LevelMilliseconds { get; private set; }

        private void Awake()
        {
            Vector3 at = spawn != null ? spawn.position : Vector3.zero;
            if (worldView != null)
            {
                at = worldView.transform.InverseTransformPoint(at);
            }
            spawnPoint = new Vector2(at.x, at.z);
        }

        private void Update()
        {
            if (worldView == null || !worldView.IsReady)
            {
                return;
            }
            if ((searched || search != null) && worldView.World != placedIn)
            {
                Clear();
            }
            if (!searched)
            {
                if (search == null)
                {
                    if (!WaterReady())
                    {
                        return;
                    }
                    StartSearch();
                }
                // A few milliseconds a frame, so the search never stalls the game.
                searchMilliseconds += Time.unscaledDeltaTime * 1000f;
                if (search.Step(searchBudgetMilliseconds))
                {
                    FinishSearch();
                }
            }
            if (Town != null && !levelled && FootprintLoaded())
            {
                Level();
            }
        }

        private void Clear()
        {
            if (Town != null)
            {
                Destroy(Town.gameObject);
            }
            Town = null;
            Keepers = new List<Shopkeeper>();
            searched = false;
            search = null;
            levelled = false;
            waitedForWater = 0f;
        }

        private WaterView Water => worldView.GetComponent<WaterView>();

        // The water map is worked out after the world starts; wait for it a while (a world without water never gets one).
        private bool WaterReady()
        {
            WaterView water = Water;
            if (water == null || !water.isActiveAndEnabled || water.Map.IsCreated)
            {
                return true;
            }
            waitedForWater += Time.unscaledDeltaTime;
            return waitedForWater >= waterWaitSeconds;
        }

        private void StartSearch()
        {
            placedIn = worldView.World;
            searchMilliseconds = 0f;
            WorldConfig config = worldView.Config;
            WorldGrid grid = worldView.World.Grid;
            float top = config.WorldHeightInChunks * grid.ChunkWorldSize.y;
            var request = new TownSiteRequest
            {
                Spawn = spawnPoint,
                KeepFromSpawn = keepFromSpawn,
                Footprint = TownLayout.Footprint,
                Bottom = 0f,
                Top = top,
                OreGroups = OreGroups(config.Materials),
            };
            RectInt? fixedColumns = config.FixedColumns;
            if (fixedColumns.HasValue)
            {
                // Inside the fixed area, with room for the ramp round the levelled ground.
                Vector2 chunk = new Vector2(grid.ChunkWorldSize.x, grid.ChunkWorldSize.z);
                RectInt c = fixedColumns.Value;
                float margin = TownLayout.LevelHalf + TownLayout.Ramp + 2f;
                request.Area = Rect.MinMaxRect(c.xMin * chunk.x + margin, c.yMin * chunk.y + margin, c.xMax * chunk.x - margin, c.yMax * chunk.y - margin);
                request.MaxDistance = 0f;
            }
            else
            {
                request.Area = new Rect(spawnPoint - Vector2.one * maxDistance, Vector2.one * maxDistance * 2f);
                request.MaxDistance = maxDistance;
            }
            WaterView water = Water;
            search = new TownSiteSearch(request, worldView.Generator, worldView.Ores, water != null ? water.Map : WaterMap.None);
        }

        private void FinishSearch()
        {
            searched = true;
            Site = search.Result;
            search = null;
            float took = searchMilliseconds;
            if (!Site.Found)
            {
                UnityEngine.Debug.LogWarning("TownSite: no site for the town in this world (no dry, flat enough ground near the spawn).");
                return;
            }
            UnityEngine.Debug.Log($"TownSite: town at ({Site.Centre.x:0}, {Site.Ground:0.0}, {Site.Centre.y:0}), rise {Site.Rise:0.0} m, water {Site.WaterDistance:0} m, ores {string.Join(", ", System.Array.ConvertAll(Site.OreDistances, d => d.ToString("0")))} m; searched over {took:0} ms of play.");
            Build();
        }

        // Tin, copper (either ore), silver, gold (GL399).
        private static List<byte[]> OreGroups(MaterialRegistry materials)
        {
            var groups = new List<byte[]>();
            if (materials == null)
            {
                return groups;
            }
            foreach (string[] names in new[] { new[] { "Cassiterite" }, new[] { "Chalcopyrite", "Malachite" }, new[] { "Silver" }, new[] { "Gold" } })
            {
                var ids = new List<byte>();
                foreach (string name in names)
                {
                    VoxelMaterial found = materials.Find(name);
                    if (found != null)
                    {
                        ids.Add(found.Id);
                    }
                }
                if (ids.Count > 0)
                {
                    groups.Add(ids.ToArray());
                }
            }
            return groups;
        }

        private void Build()
        {
            Yaw = TownLayout.GateYawTowards(Site.Centre, spawnPoint);
            var root = new GameObject("Town").transform;
            root.SetParent(transform, false);
            root.position = worldView.transform.TransformPoint(new Vector3(Site.Centre.x, Site.Ground, Site.Centre.y));
            root.rotation = worldView.transform.rotation * Quaternion.Euler(0f, Yaw, 0f);
            Town = root;
            if (ItemSlotGui.Prices == null && prices != null)
            {
                ItemSlotGui.Prices = prices;
            }
            Keepers = TownBuilder.Build(root, new TownBuilder.Parts
            {
                Shops = shops,
                Prices = prices,
                Material = material,
                Clock = FindClock(),
                Hours = hours,
                Seed = worldView.Ores != null ? worldView.Ores.Seed : 0,
            });
        }

        private DayNightCycle FindClock()
        {
            DayNightCycle clock = worldView.GetComponent<DayNightCycle>();
            return clock != null ? clock : FindAnyObjectByType<DayNightCycle>();
        }

        // The chunk columns under the levelled ground and its ramp, top to bottom: edits only reach loaded chunks.
        private bool FootprintLoaded()
        {
            World world = worldView.World;
            int layers = worldView.Config.WorldHeightInChunks;
            foreach (Vector2Int column in FootprintColumns())
            {
                for (int y = 0; y < layers; y++)
                {
                    if (!world.IsLoaded(new Vector3Int(column.x, y, column.y)))
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        private IEnumerable<Vector2Int> FootprintColumns()
        {
            WorldGrid grid = worldView.World.Grid;
            float reach = TownLayout.LevelHalf + TownLayout.Ramp + 1f;
            Vector3Int low = grid.WorldToChunk(new Vector3(Site.Centre.x - reach, 0f, Site.Centre.y - reach));
            Vector3Int high = grid.WorldToChunk(new Vector3(Site.Centre.x + reach, 0f, Site.Centre.y + reach));
            for (int z = low.z; z <= high.z; z++)
            {
                for (int x = low.x; x <= high.x; x++)
                {
                    yield return new Vector2Int(x, z);
                }
            }
        }

        private void Level()
        {
            levelled = true;
            World world = worldView.World;
            foreach (Vector2Int column in FootprintColumns())
            {
                if (world.HasEditsIn(column))
                {
                    // A save brought this ground back as it was left: the town's levelling is in it already.
                    return;
                }
            }
            WorldConfig config = worldView.Config;
            MaterialRegistry materials = config.Materials;
            ITerrainGenerator generator = worldView.Generator;
            float top = config.WorldHeightInChunks * world.Grid.ChunkWorldSize.y;
            Vector2 centre = Site.Centre;
            float yaw = Yaw;
            float started = Time.realtimeSinceStartup;
            GroundLeveling.Level(world, materials, new GroundLeveling.Area
            {
                Centre = centre,
                Half = Vector2.one * TownLayout.LevelHalf,
                Height = Site.Ground,
                Ramp = TownLayout.Ramp,
                Natural = (x, z) => TerrainSurface.HeightAt(generator, x, z, 0f, top),
                Headroom = Headroom,
                Fill = Fill,
                Paving = materials != null ? materials.Find(pathMaterial) : null,
                Paved = (x, z) => TownLayout.IsPath(TownLayout.Turn(new Vector2(x, z) - centre, -yaw)),
                Ground = materials != null ? materials.Find(groundMaterial) : null,
            }, config.IsoLevel);
            LevelMilliseconds = (Time.realtimeSinceStartup - started) * 1000f;
            UnityEngine.Debug.Log($"TownSite: levelled the town's ground in {LevelMilliseconds:0} ms.");
        }
    }
}
