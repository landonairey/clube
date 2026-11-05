using System.Collections.Generic;
using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// Puts the town in the world (GL399, GL400, GL408-GL414): once the world and its water map are ready, picks a land
    /// parcel with <see cref="TownSiteSearch"/> (near tin, copper, silver and gold ore and water; inside the fixed area, or
    /// within <see cref="maxDistance"/> of the player's spawn in a streamed world), locks it so it can't be bought, works out
    /// the town's <see cref="TownPlan"/> and builds its objects with <see cref="TownBuilder"/>. When the chunks under it
    /// have loaded it writes the town into the terrain once: the square cleared and paved on the land as it lies, a level
    /// pad for each shop, the mine shafts, the wall of stones and the shops of stone and boards. The site follows from the
    /// seed, so a loaded game gets the same town; ground already edited there (a save) isn't written again.
    /// </summary>
    public class TownSite : MonoBehaviour
    {
        // Metres of air made over a shop's pad (clearing a boulder off it), and of ground made under it.
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

        [Tooltip("The Clube/World Text material for the words on signs.")]
        [SerializeField]
        private Material textMaterial;

        [Tooltip("When the shopkeepers trade, do chores and sleep (GL404-GL406).")]
        [SerializeField]
        private ShopHours hours = ShopHours.Default;

        [Tooltip("In a streamed world, the most metres from the spawn to the town's centre (GL399).")]
        [SerializeField, Min(100f)]
        private float maxDistance = 1000f;

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

        // The ore groups the town trades, in the order the site search and the shafts count them (GL399, GL414).
        private static readonly (string Name, string[] Materials)[] OreGroupNames =
        {
            ("Tin", new[] { "Cassiterite" }),
            ("Copper", new[] { "Chalcopyrite", "Malachite" }),
            ("Silver", new[] { "Silver" }),
            ("Gold", new[] { "Gold" }),
        };

        private TownSiteSearch search;
        private float searchMilliseconds;
        private Vector2 spawnPoint;
        private World placedIn;
        private float waitedForWater;
        private bool searched;
        private bool levelled;
        private bool locked;
        private PlayerParcels parcels;
        private readonly List<string> oreNames = new List<string>();
        private readonly Dictionary<int, HashSet<byte>> groupOres = new Dictionary<int, HashSet<byte>>();

        /// <summary>The site picked, once searched; <see cref="TownPlacement.Found"/> is false when none qualified.</summary>
        public TownPlacement Site { get; private set; }

        /// <summary>The town's plan, once the site is found.</summary>
        public TownPlan Plan { get; private set; }

        /// <summary>The town's root object, once built.</summary>
        public Transform Town { get; private set; }

        /// <summary>The town's turn about y: the gate faces the spawn.</summary>
        public float Yaw => Plan != null ? Plan.Yaw : 0f;

        /// <summary>The land parcel the town fills (GL408).</summary>
        public Vector2Int Parcel { get; private set; }

        /// <summary>The shopkeepers, once built.</summary>
        public IReadOnlyList<Shopkeeper> Keepers { get; private set; } = new List<Shopkeeper>();

        /// <summary>True once the town has been written into the terrain (or was found already there).</summary>
        public bool IsLevelled => levelled;

        /// <summary>Milliseconds writing the town into the terrain took, for the docs and tuning.</summary>
        public float LevelMilliseconds { get; private set; }

        private void Awake()
        {
            Vector3 at = spawn != null ? spawn.position : Vector3.zero;
            if (worldView != null)
            {
                at = worldView.transform.InverseTransformPoint(at);
            }
            spawnPoint = new Vector2(at.x, at.z);
            parcels = spawn != null ? spawn.GetComponent<PlayerParcels>() : null;
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
            LockParcel();
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
            Plan = null;
            Keepers = new List<Shopkeeper>();
            searched = false;
            search = null;
            levelled = false;
            locked = false;
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

        // The width of a land parcel: the town fills one (GL408).
        private float ParcelSize => (parcels != null ? parcels.SizeInChunks : 8) * worldView.World.Grid.ChunkWorldSize.x;

        private void StartSearch()
        {
            placedIn = worldView.World;
            searchMilliseconds = 0f;
            WorldConfig config = worldView.Config;
            WorldGrid grid = worldView.World.Grid;
            float top = config.WorldHeightInChunks * grid.ChunkWorldSize.y;
            float parcel = ParcelSize;
            if (parcel < TownLayout.ParcelSize - 0.01f)
            {
                UnityEngine.Debug.LogWarning($"TownSite: land parcels are {parcel} m; the town needs {TownLayout.ParcelSize} m and will spill over.");
            }
            var request = new TownSiteRequest
            {
                Spawn = spawnPoint,
                // Not the parcel the player starts on: every point of it is within this of its centre.
                KeepFromSpawn = parcel * 0.72f,
                Footprint = TownLayout.Footprint,
                Step = parcel,
                GridOrigin = Vector2.one * (parcel * 0.5f),
                Bottom = 0f,
                Top = top,
                RiseWeight = 6f,
                OreGroups = OreGroups(config.Materials),
            };
            RectInt? fixedColumns = config.FixedColumns;
            if (fixedColumns.HasValue)
            {
                // Inside the fixed area.
                Vector2 chunk = new Vector2(grid.ChunkWorldSize.x, grid.ChunkWorldSize.z);
                RectInt c = fixedColumns.Value;
                float margin = parcel * 0.5f;
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

        // Tin, copper (either ore), silver, gold (GL399); also which material ids each group mines (GL415).
        private List<byte[]> OreGroups(MaterialRegistry materials)
        {
            var groups = new List<byte[]>();
            oreNames.Clear();
            groupOres.Clear();
            if (materials == null)
            {
                return groups;
            }
            foreach ((string name, string[] names) in OreGroupNames)
            {
                var ids = new List<byte>();
                foreach (string material in names)
                {
                    VoxelMaterial found = materials.Find(material);
                    if (found != null)
                    {
                        ids.Add(found.Id);
                    }
                }
                if (ids.Count > 0)
                {
                    groupOres[groups.Count] = new HashSet<byte>(ids);
                    groups.Add(ids.ToArray());
                    oreNames.Add(name);
                }
            }
            return groups;
        }

        private float Natural(float x, float z)
        {
            float top = worldView.Config.WorldHeightInChunks * worldView.World.Grid.ChunkWorldSize.y;
            return TerrainSurface.HeightAt(worldView.Generator, x, z, 0f, top);
        }

        private void Build()
        {
            float yaw = TownLayout.GateYawTowards(Site.Centre, spawnPoint);
            int seed = worldView.Ores != null ? worldView.Ores.Seed : 0;
            Plan = new TownPlan(Site, yaw, Natural, seed);
            Parcel = new Vector2Int(Mathf.FloorToInt(Site.Centre.x / ParcelSize), Mathf.FloorToInt(Site.Centre.y / ParcelSize));
            var root = new GameObject("Town").transform;
            root.SetParent(transform, false);
            root.SetPositionAndRotation(worldView.transform.TransformPoint(new Vector3(Site.Centre.x, Site.Ground, Site.Centre.y)),
                worldView.transform.rotation * Quaternion.Euler(0f, yaw, 0f));
            Town = root;
            if (ItemSlotGui.Prices == null && prices != null)
            {
                ItemSlotGui.Prices = prices;
            }
            var context = new TownContext(Plan, worldView, root, groupOres);
            Keepers = TownBuilder.Build(new TownBuilder.Parts
            {
                Shops = shops,
                Prices = prices,
                Material = material,
                Clock = FindClock(),
                Hours = hours,
                Town = context,
                OreNames = oreNames,
                TextMaterial = textMaterial,
            });
        }

        // The town's parcel can't be bought (GL408); the land is set up a little after the world, so keep trying.
        private void LockParcel()
        {
            if (locked || Plan == null || parcels == null || parcels.Parcels == null)
            {
                return;
            }
            parcels.Parcels.Lock(Parcel);
            locked = true;
        }

        private DayNightCycle FindClock()
        {
            DayNightCycle clock = worldView.GetComponent<DayNightCycle>();
            return clock != null ? clock : FindAnyObjectByType<DayNightCycle>();
        }

        // Everything the town writes into the terrain: its parcel, the ramps round the shops' pads, and the shafts.
        private Rect Footprint()
        {
            float half = TownLayout.ParcelSize * 0.5f + 1f;
            Rect area = new Rect(Site.Centre - Vector2.one * half, Vector2.one * half * 2f);
            foreach (TownShaft shaft in Plan.Shafts)
            {
                foreach (Vector3 point in shaft.Path)
                {
                    area = Rect.MinMaxRect(Mathf.Min(area.xMin, point.x - 2f), Mathf.Min(area.yMin, point.z - 2f),
                        Mathf.Max(area.xMax, point.x + 2f), Mathf.Max(area.yMax, point.z + 2f));
                }
            }
            return area;
        }

        // The chunk columns under the footprint, top to bottom: edits only reach loaded chunks.
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
            Rect area = Footprint();
            Vector3Int low = grid.WorldToChunk(new Vector3(area.xMin, 0f, area.yMin));
            Vector3Int high = grid.WorldToChunk(new Vector3(area.xMax, 0f, area.yMax));
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
                    // A save brought this ground back as it was left: the town is in it already.
                    return;
                }
            }
            WorldConfig config = worldView.Config;
            MaterialRegistry materials = config.Materials;
            float iso = config.IsoLevel;
            VoxelMaterial paving = materials != null ? materials.Find(pathMaterial) : null;
            VoxelMaterial ground = materials != null ? materials.Find(groundMaterial) : null;
            VoxelMaterial stone = materials != null ? materials.Find("Stone") : null;
            VoxelMaterial oak = materials != null ? materials.Find("Oak boards") : null;
            VoxelMaterial maple = materials != null ? materials.Find("Maple boards") : null;
            TownPlan plan = Plan;
            float started = Time.realtimeSinceStartup;

            // 1. The square as the land lies (GL413): trees and rocks cleared, the paths cobbled, dirt between.
            float half = TownLayout.ParcelSize * 0.5f;
            GroundLeveling.Level(world, materials, new GroundLeveling.Area
            {
                Centre = Site.Centre,
                Half = Vector2.one * half,
                FollowGround = true,
                Natural = Natural,
                Headroom = Headroom,
                Paving = paving,
                Paved = (x, z) => TownLayout.IsPath(plan.ToTown(new Vector2(x, z))),
                Ground = ground,
            }, iso);

            // 2. A level pad for each shop, ramping back to the land round it.
            foreach (TownLayout.ShopPlot plot in TownLayout.Shops)
            {
                Rect pad = plot.PadRect;
                Vector2 a = plan.ToWorld(pad.min), b = plan.ToWorld(pad.max);
                var worldPad = Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
                GroundLeveling.Level(world, materials, new GroundLeveling.Area
                {
                    Centre = worldPad.center,
                    Half = worldPad.size * 0.5f,
                    Height = plan.PadHeight(plot.Kind),
                    Ramp = TownLayout.PadRamp,
                    Natural = Natural,
                    Headroom = Headroom,
                    Fill = Fill,
                    Paving = paving,
                    Paved = (x, z) => TownLayout.IsPath(plan.ToTown(new Vector2(x, z))),
                    Ground = ground,
                }, iso);
            }

            // 3. The mine shafts (GL414), lined with stone where they pass through ground that would fall.
            foreach (TownShaft shaft in plan.Shafts)
            {
                SquareVoxels.Carve(world, TownStructures.ShaftVoxels(world, shaft.Path, iso), materials, stone, iso);
            }

            // 4. The wall of stones (GL409).
            if (stone != null)
            {
                TownStructures.StampStones(world, plan.Stones, stone.Id, iso);
            }

            // 5. The shops in stone and boards (GL412): oak or maple walls with the other for the counter and trim.
            if (stone != null && oak != null && maple != null)
            {
                foreach (TownLayout.ShopPlot plot in TownLayout.Shops)
                {
                    bool mapleShop = plot.Kind == ShopKind.Lumber || plot.Kind == ShopKind.Prospector;
                    byte boards = mapleShop ? maple.Id : oak.Id;
                    byte trim = mapleShop ? oak.Id : maple.Id;
                    SquareVoxels.Build(world, plan.ShopBlocks(plot.Kind, world.Grid.VoxelSize, stone.Id, boards, trim), iso);
                }
            }
            LevelMilliseconds = (Time.realtimeSinceStartup - started) * 1000f;
            UnityEngine.Debug.Log($"TownSite: wrote the town into the terrain in {LevelMilliseconds:0} ms ({plan.Shafts.Count} shafts).");
        }
    }
}
