using System.Collections.Generic;
using System.Globalization;
using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// The travelling trade cart (GL477). Once the trading post is built, a distant town is placed 10+ parcels out of its
    /// gate (<see cref="CartRoute.DistantTown"/>, named by <see cref="SettlementNames"/>) and a road roughed in between them
    /// (<see cref="CartRoute"/>, written by <see cref="CartTrack"/>). Every two or three days (<see cref="CartSchedule"/>)
    /// the cart comes down the road from the distant town, stops outside the post's gate and goes back; where it is follows
    /// from the seed and the clock. Its two crates are filled with goods for each trip and can be looted: what's left in
    /// them is saved for the trip it belongs to, as are the road's written pieces. The distant town is only its trade
    /// yard for now: a cobbled square with its name sign and stacked crates (built once its chunks load).
    /// </summary>
    public class TradeCart : MonoBehaviour, ISavedValues
    {
        private const string KeyPrefix = "cart";

        // Metres out of the gate the road starts, either side of the road's middle the track runs, and the cart's speed.
        private const float RoadStart = 6f;
        private const float TrackHalfWidth = 1.2f;
        private const float GateCone = 60f;

        // Kinds of goods in each crate.
        private const int GoodsPerCrate = 3;

        [SerializeField]
        private WorldView worldView;

        [SerializeField]
        private TownSite town;

        [Tooltip("The merchant price list the cart's goods are picked from.")]
        [SerializeField]
        private PriceList prices;

        [Tooltip("The Clube/Low Poly material for the cart, the carter and the signs.")]
        [SerializeField]
        private Material material;

        [Tooltip("The Clube/World Text material for the words on signs.")]
        [SerializeField]
        private Material textMaterial;

        [Tooltip("The wooden crate (a storage chest) the goods ride in.")]
        [SerializeField]
        private GameObject cratePrefab;

        [Tooltip("The material the road is laid in.")]
        [SerializeField]
        private string roadMaterial = "Dirt";

        [Tooltip("The material the distant town's yard is cobbled with.")]
        [SerializeField]
        private string yardMaterial = "Loose stone";

        [Tooltip("The cart's speed in metres a real second (at the clock's normal rate).")]
        [SerializeField, Range(0.5f, 5f)]
        private float speed = 1.4f;

        [Tooltip("Game hours the cart stops at the trading post.")]
        [SerializeField, Range(0.5f, 8f)]
        private float stopHours = 2.5f;

        [Tooltip("Milliseconds a frame the road may take to write.")]
        [SerializeField, Range(0.5f, 10f)]
        private float writeBudgetMilliseconds = 2f;

        private World placedIn;
        private DayNightCycle clock;
        private Transform root;
        private System.Collections.Generic.List<RouteBridges.Span> bridges;
        private bool[] bridgeBuilt;
        private TradeCartModel cart;
        private CartTrack track;
        private Rect? area;
        private bool yardBuilt;
        private int seed;
        private int filledTrip = int.MinValue;
        private string restoredTrack;
        private int restoredTrip = int.MinValue;
        private readonly Dictionary<int, string> restoredCrates = new Dictionary<int, string>();

        /// <summary>The road, once planned.</summary>
        public CartRoute Route { get; private set; }

        /// <summary>The road's writer, once planned.</summary>
        public CartTrack Track => track;

        /// <summary>The distant town's name and where it is (x, z), once planned.</summary>
        public string DistantName { get; private set; } = "";

        public Vector2 DistantCentre { get; private set; }

        /// <summary>Where the cart is in its trip, this frame.</summary>
        public CartState State { get; private set; }

        /// <summary>The cart's model, once built.</summary>
        public TradeCartModel Cart => cart;

        /// <summary>The low-poly material the cart is made of, so another cart can be built (GL583).</summary>
        public Material LowPolyMaterial => material;

        private void Update()
        {
            if (worldView == null || !worldView.IsReady || town == null)
            {
                return;
            }
            if (root != null && worldView.World != placedIn)
            {
                Clear();
            }
            if (Route == null)
            {
                if (town.Plan == null)
                {
                    return;
                }
                Plan();
            }
            WorldConfig config = worldView.Config;
            MaterialRegistry materials = config.Materials;
            track.Step(worldView.World, config, Natural, materials != null ? materials.Find(roadMaterial) : null, writeBudgetMilliseconds);
            BuildBridges();
            if (!yardBuilt && YardLoaded())
            {
                BuildYard();
            }
            Move();
        }

        private void Clear()
        {
            if (root != null)
            {
                Destroy(root.gameObject);
            }
            root = null;
            bridges = null;
            cart = null;
            Route = null;
            track = null;
            yardBuilt = false;
            filledTrip = int.MinValue;
        }

        // A bridge (GL570) is built once the chunks round its ends and middle have loaded, so its colliders meet the ground.
        private void BuildBridges()
        {
            if (bridges == null || material == null)
            {
                return;
            }
            World world = worldView.World;
            for (int i = 0; i < bridges.Count; i++)
            {
                if (bridgeBuilt[i])
                {
                    continue;
                }
                RouteBridges.Span span = bridges[i];
                bool loaded = true;
                for (int k = 0; k <= 4 && loaded; k++)
                {
                    Vector2 p = Vector2.Lerp(span.From, span.To, k / 4f);
                    float y = Mathf.Lerp(span.FromHeight, span.ToHeight, k / 4f);
                    loaded = world.IsLoaded(world.Grid.WorldToChunk(new Vector3(p.x, y, p.y)));
                }
                if (loaded)
                {
                    bridgeBuilt[i] = true;
                    RoadBridge.Build(span, material, root, seed + i);
                }
            }
        }

        private float Top => worldView.Config.WorldHeightInChunks * worldView.World.Grid.ChunkWorldSize.y;

        private float Natural(float x, float z)
        {
            return TerrainSurface.HeightAt(worldView.Generator, x, z, 0f, Top);
        }

        private void Plan()
        {
            placedIn = worldView.World;
            WorldConfig config = worldView.Config;
            WorldGrid grid = worldView.World.Grid;
            seed = config.Terrain != null ? config.Terrain.Seed : 0;
            area = null;
            if (config.FixedColumns.HasValue)
            {
                RectInt c = config.FixedColumns.Value;
                area = Rect.MinMaxRect(c.xMin * grid.ChunkWorldSize.x, c.yMin * grid.ChunkWorldSize.z, c.xMax * grid.ChunkWorldSize.x, c.yMax * grid.ChunkWorldSize.z);
            }
            TownPlan plan = town.Plan;
            // Out of the gate (the town's -z side), so the road never crosses the town.
            Vector2 gate = plan.ToWorld(new Vector2(0f, -TownLayout.OuterHalf));
            Vector2 outward = (gate - plan.ToWorld(Vector2.zero)).normalized;
            Vector2 start = gate + outward * RoadStart;
            float parcel = TownLayout.ParcelSize;
            float started = Time.realtimeSinceStartup;
            DistantCentre = CartRoute.DistantTown(town.Site.Centre, parcel, seed, area, outward, GateCone);
            Vector2Int distantParcel = new Vector2Int(Mathf.FloorToInt(DistantCentre.x / parcel), Mathf.FloorToInt(DistantCentre.y / parcel));
            DistantName = SettlementNames.Generate(seed, distantParcel);
            // The road ends at the yard's near side.
            Vector2 end = DistantCentre - (DistantCentre - start).normalized * YardHalf;
            Route = new CartRoute(start, end, Natural, seed);
            track = new CartTrack(Route, TrackHalfWidth);
            track.Restore(restoredTrack);
            float took = (Time.realtimeSinceStartup - started) * 1000f;
            UnityEngine.Debug.Log($"TradeCart: road to {DistantName} at ({DistantCentre.x:0}, {DistantCentre.y:0}), {Route.Length:0} m in {Route.Points.Count} corners, planned in {took:0.0} ms.");

            root = new GameObject("Trade road").transform;
            root.SetParent(transform, false);
            bridges = RouteBridges.Find(Route, worldView.Generator is HeightfieldGenerator heightfield ? heightfield.Water : WaterMap.None);
            bridgeBuilt = new bool[bridges.Count];
            // Read on the way out of the gate.
            Sign(root, $"To {DistantName}", start - outward * 1.5f + new Vector2(-outward.y, outward.x) * 2.2f, -outward);
            var cartObject = new GameObject("Trade cart");
            cartObject.transform.SetParent(root, false);
            cart = cartObject.AddComponent<TradeCartModel>();
            cart.Build(material, cratePrefab, seed + 477);
        }

        // Half the distant town's yard.
        private const float YardHalf = 6f;

        private bool YardLoaded()
        {
            if (area.HasValue && !area.Value.Overlaps(new Rect(DistantCentre - Vector2.one * YardHalf, Vector2.one * YardHalf * 2f)))
            {
                // Beyond a fixed world's edge: there's no ground to build on.
                return false;
            }
            WorldGrid grid = worldView.World.Grid;
            float h = Natural(DistantCentre.x, DistantCentre.y);
            float reach = YardHalf + 4f;
            Vector3Int min = grid.WorldToChunk(new Vector3(DistantCentre.x - reach, Mathf.Max(0f, h - 4f), DistantCentre.y - reach));
            Vector3Int max = grid.WorldToChunk(new Vector3(DistantCentre.x + reach, h + 6f, DistantCentre.y + reach));
            for (int y = Mathf.Max(0, min.y); y <= Mathf.Min(worldView.Config.WorldHeightInChunks - 1, max.y); y++)
            {
                for (int z = min.z; z <= max.z; z++)
                {
                    for (int x = min.x; x <= max.x; x++)
                    {
                        if (!worldView.World.IsLoaded(new Vector3Int(x, y, z)))
                        {
                            return false;
                        }
                    }
                }
            }
            return true;
        }

        // The distant town's trade yard: a level cobbled square, its name sign facing the road, crates stacked at the back.
        private void BuildYard()
        {
            yardBuilt = true;
            WorldConfig config = worldView.Config;
            MaterialRegistry materials = config.Materials;
            float height = Natural(DistantCentre.x, DistantCentre.y);
            GroundLeveling.Level(worldView.World, materials, new GroundLeveling.Area
            {
                Centre = DistantCentre,
                Half = Vector2.one * YardHalf,
                Height = height,
                Ramp = 4f,
                Natural = Natural,
                Headroom = 6f,
                Fill = 2f,
                Paving = materials != null ? materials.Find(yardMaterial) : null,
            }, config.IsoLevel);
            Vector2 towardsRoad = (Route.Flat(Route.Length - 4f) - DistantCentre).normalized;
            var across = new Vector2(-towardsRoad.y, towardsRoad.x);
            Sign(root, $"{DistantName}\nTrade Yard", DistantCentre + towardsRoad * (YardHalf - 0.5f) + across * 2.2f, towardsRoad, height);
            if (cratePrefab == null)
            {
                return;
            }
            for (int i = 0; i < 4; i++)
            {
                Vector2 at = DistantCentre - towardsRoad * (YardHalf - 1.2f) + across * ((i % 3) - 1) * 1.1f;
                GameObject crate = ModelCopy.Copy(cratePrefab, root, "Yard crate", true, 0.9f);
                crate.transform.position = worldView.transform.TransformPoint(new Vector3(at.x, height + (i == 3 ? 0.9f : 0f), at.y));
                crate.transform.rotation = worldView.transform.rotation * Quaternion.LookRotation(new Vector3(towardsRoad.x, 0f, towardsRoad.y));
            }
        }

        // A signpost whose board faces along facing (x, z), on the ground (or at height).
        private void Sign(Transform parent, string text, Vector2 at, Vector2 facing, float height = float.NaN)
        {
            float ground = float.IsNaN(height) ? Natural(at.x, at.y) : height;
            var sign = new GameObject("Road sign");
            sign.transform.SetParent(parent, false);
            sign.transform.position = worldView.transform.TransformPoint(new Vector3(at.x, ground, at.y));
            sign.transform.rotation = worldView.transform.rotation * Quaternion.LookRotation(new Vector3(facing.x, 0f, facing.y));
            sign.AddComponent<MeshFilter>().sharedMesh = TownMeshes.SignPost(1.9f, 0.62f);
            sign.AddComponent<MeshRenderer>().sharedMaterial = material;
            TownBuilder.Text(sign.transform, text, new Vector3(0f, 1.62f, 0.03f), textMaterial, 0.03f);
            var box = sign.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 1.1f, 0f);
            box.size = new Vector3(1.9f, 2.2f, 0.12f);
        }

        private void Move()
        {
            if (clock == null && !worldView.TryGetComponent(out clock))
            {
                clock = FindAnyObjectByType<DayNightCycle>();
            }
            if (clock == null || clock.Clock == null || cart == null)
            {
                return;
            }
            // A trip (there, a stop, back) always fits between two trips.
            double legDays = Route.Length / speed / clock.Clock.DayLengthSeconds;
            double waitDays = stopHours / 24d;
            legDays = System.Math.Min(legDays, (CartSchedule.MinGap - waitDays - 0.1d) * 0.5d);
            State = CartSchedule.At(seed, clock.Clock.Days, legDays, waitDays);
            if (State.Trip != filledTrip)
            {
                Fill(State.Trip);
            }
            // Never quite on the road's ends: the carter walks a little ahead of the cart.
            const float EndGap = 3f;
            float distance = Mathf.Lerp(Route.Length - EndGap, EndGap, State.Progress);
            bool towardsPost = State.Phase == CartPhase.ToPost || State.Phase == CartPhase.Away;
            bool moving = State.Phase == CartPhase.ToPost || State.Phase == CartPhase.ToTown;
            Vector2 flat = Route.Flat(distance);
            // Off a fixed world's edge it can't be seen.
            bool shown = !area.HasValue || area.Value.Contains(flat);
            if (cart.gameObject.activeSelf != shown)
            {
                cart.gameObject.SetActive(shown);
            }
            if (shown)
            {
                cart.Place(Route, distance, towardsPost, moving, worldView.transform);
            }
        }

        // Each trip brings new goods; a save's leftovers replace them for the trip it was made on.
        private void Fill(int trip)
        {
            filledTrip = trip;
            List<ItemPrice> goods = Goods();
            var random = new System.Random(seed * 7919 + trip * 104729 + 477);
            for (int i = 0; i < cart.Crates.Count; i++)
            {
                StorageChest crate = cart.Crates[i];
                if (trip == restoredTrip && restoredCrates.TryGetValue(i, out string text))
                {
                    StorageText.Fill(crate.Storage, text);
                    continue;
                }
                StorageText.Fill(crate.Storage, "");
                for (int k = 0; k < GoodsPerCrate && goods.Count > 0; k++)
                {
                    ItemPrice good = goods[random.Next(goods.Count)];
                    crate.Storage.Add(good.Item, good.Lot * (1 + random.Next(3)));
                }
            }
        }

        // Trade goods: the dearer half of what the price list values that isn't a tool, drink or thing to place
        // (TownBuilder.IsValuable), so the crates hold ingots and the like rather than sticks.
        private List<ItemPrice> Goods()
        {
            var goods = new List<ItemPrice>();
            if (prices == null)
            {
                return goods;
            }
            foreach (ItemPrice price in prices.Prices)
            {
                if (TownBuilder.IsValuable(price.Item))
                {
                    goods.Add(price);
                }
            }
            goods.Sort((a, b) => (b.Coins / (float)b.Lot).CompareTo(a.Coins / (float)a.Lot));
            int keep = Mathf.Max(Mathf.Min(6, goods.Count), goods.Count / 2);
            goods.RemoveRange(keep, goods.Count - keep);
            return goods;
        }

        private static string Key(string name)
        {
            return $"{KeyPrefix}.{name}";
        }

        public void SaveValues(IDictionary<string, string> values)
        {
            if (track != null)
            {
                values[Key("road")] = track.Save();
            }
            else if (restoredTrack != null)
            {
                values[Key("road")] = restoredTrack;
            }
            if (cart != null && filledTrip != int.MinValue)
            {
                values[Key("trip")] = filledTrip.ToString(CultureInfo.InvariantCulture);
                for (int i = 0; i < cart.Crates.Count; i++)
                {
                    values[Key($"crate{i}")] = StorageText.Encode(cart.Crates[i].Storage);
                }
            }
        }

        public void RestoreValues(IReadOnlyDictionary<string, string> values)
        {
            restoredTrack = values.TryGetValue(Key("road"), out string road) ? road : null;
            track?.Restore(restoredTrack);
            restoredCrates.Clear();
            restoredTrip = int.MinValue;
            if (values.TryGetValue(Key("trip"), out string trip) && int.TryParse(trip, NumberStyles.Integer, CultureInfo.InvariantCulture, out int t))
            {
                restoredTrip = t;
                for (int i = 0; i < 4; i++)
                {
                    if (values.TryGetValue(Key($"crate{i}"), out string crate))
                    {
                        restoredCrates[i] = crate;
                    }
                }
            }
            // Fill again on the next frame, from the save if it's the same trip.
            filledTrip = int.MinValue;
        }
    }
}
