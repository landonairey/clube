using System.Collections.Generic;
using System.Globalization;
using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// The lone adventurer's camp (GL476): from the dawn after the fifth night (<see cref="AdventurerVisit"/>) an
    /// <see cref="Adventurer"/> stands by a campfire on the highest ground within <see cref="searchRadius"/> of the parcel
    /// the player started on (<see cref="PeakSearch"/> over the generated surface). The fire's flames dance all the time
    /// and its light shines out at night, so the peak can be picked out from below. Saves whether they've come, their
    /// purse and their disposition.
    /// </summary>
    public class AdventurerCamp : MonoBehaviour, ISavedValues
    {
        private const string KeyPrefix = "adventurer";

        [SerializeField]
        private WorldView worldView;

        [Tooltip("The player: the parcel it stands on when the scene starts is the one the nearest peak is searched round.")]
        [SerializeField]
        private Transform spawn;

        [Tooltip("The merchant price list: the adventurer buys and sells everything on it.")]
        [SerializeField]
        private PriceList prices;

        [Tooltip("The Clube/Low Poly material the adventurer is drawn with.")]
        [SerializeField]
        private Material material;

        [Tooltip("The game's campfire, whose look the camp copies (no station behind it).")]
        [SerializeField]
        private GameObject campfirePrefab;

        [Tooltip("The campfire's flames (Embers) and smoke (Drying steam).")]
        [SerializeField]
        private Material flameMaterial;

        [SerializeField]
        private Material smokeMaterial;

        [Tooltip("Metres round the starting parcel's centre searched for the peak.")]
        [SerializeField, Min(20f)]
        private float searchRadius = 250f;

        [Tooltip("Metres between the coarse search's samples; the search then looks closer round the best one.")]
        [SerializeField, Range(1f, 32f)]
        private float searchStep = 8f;

        [Tooltip("The fire's light at night (it fades out by day).")]
        [SerializeField, Min(0f)]
        private float nightLight = 4f;

        private Vector2 spawnPoint;
        private DayNightCycle clock;
        private World placedIn;
        private Transform camp;
        private FlameCluster flame;
        private Light glow;
        private World searchedIn;
        private Vector2 peak;
        private float peakHeight;

        /// <summary>True once the adventurer has come (saved).</summary>
        public bool Arrived { get; private set; }

        /// <summary>Their coins (saved).</summary>
        public Wallet Purse { get; private set; } = new Wallet(AdventurerVisit.Purse);

        /// <summary>How they feel about the player (saved).</summary>
        public Disposition Disposition { get; } = new Disposition();

        /// <summary>Where the camp is, once built (world position of the fire).</summary>
        public Vector3? CampPosition => camp != null ? camp.position : (Vector3?)null;

        /// <summary>Milliseconds the last peak search took.</summary>
        public float SearchMilliseconds { get; private set; }

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
            if (clock == null && !worldView.TryGetComponent(out clock))
            {
                clock = FindAnyObjectByType<DayNightCycle>();
            }
            if (!Arrived && clock != null && AdventurerVisit.HasArrived(clock.Day, clock.Hour, DayNightCycle.SunriseHour))
            {
                Arrived = true;
            }
            if (searchedIn != worldView.World)
            {
                FindPeak();
            }
            if (camp != null && worldView.World != placedIn)
            {
                Destroy(camp.gameObject);
                camp = null;
            }
            if (Arrived && camp == null)
            {
                Build();
            }
            if (camp != null)
            {
                flame?.Animate(1f);
                if (glow != null)
                {
                    float day = clock != null ? clock.Daylight : 1f;
                    glow.intensity = nightLight * (1f - day) * (0.9f + 0.1f * Mathf.PerlinNoise(Time.time * 3f, 0.3f));
                    glow.enabled = glow.intensity > 0.05f;
                }
            }
        }

        // The starting parcel's centre: the town keeps to parcels too (GL408).
        private Vector2 ParcelCentre()
        {
            PlayerParcels parcels = spawn != null ? spawn.GetComponent<PlayerParcels>() : null;
            float size = (parcels != null ? parcels.SizeInChunks : 8) * worldView.World.Grid.ChunkWorldSize.x;
            return new Vector2((Mathf.Floor(spawnPoint.x / size) + 0.5f) * size, (Mathf.Floor(spawnPoint.y / size) + 0.5f) * size);
        }

        private float Top => worldView.Config.WorldHeightInChunks * worldView.World.Grid.ChunkWorldSize.y;

        // The highest ground near the start is on the nearest big hill; the search then climbs it to its top, past the
        // radius if need be. Done as soon as the world is up, among the loading hitches, not when the adventurer arrives.
        private void FindPeak()
        {
            searchedIn = worldView.World;
            WorldConfig config = worldView.Config;
            WorldGrid grid = worldView.World.Grid;
            Rect? area = null;
            RectInt? fixedColumns = config.FixedColumns;
            if (fixedColumns.HasValue)
            {
                // Inside the fixed area, clear of its edge.
                RectInt c = fixedColumns.Value;
                const float Margin = 6f;
                area = Rect.MinMaxRect(c.xMin * grid.ChunkWorldSize.x + Margin, c.yMin * grid.ChunkWorldSize.z + Margin,
                    c.xMax * grid.ChunkWorldSize.x - Margin, c.yMax * grid.ChunkWorldSize.z - Margin);
            }
            ITerrainGenerator generator = worldView.Generator;
            float top = Top;
            System.Func<float, float, float> heightAt = (x, z) => TerrainSurface.HeightAt(generator, x, z, 0f, top);
            float start = Time.realtimeSinceStartup;
            Vector2 near = PeakSearch.Highest(heightAt, ParcelCentre(), searchRadius, searchStep, area, out _);
            peak = PeakSearch.Summit(heightAt, near, searchStep, area, out peakHeight);
            SearchMilliseconds = (Time.realtimeSinceStartup - start) * 1000f;
            UnityEngine.Debug.Log($"AdventurerCamp: the peak is at ({peak.x:0}, {peakHeight:0.0}, {peak.y:0}), searched in {SearchMilliseconds:0.0} ms.");
        }

        private void Build()
        {
            placedIn = worldView.World;
            WorldConfig config = worldView.Config;
            ITerrainGenerator generator = worldView.Generator;
            float top = Top;
            float height = peakHeight;
            camp = new GameObject("Adventurer camp").transform;
            camp.SetParent(transform, false);
            camp.position = worldView.transform.TransformPoint(new Vector3(peak.x, height, peak.y));
            camp.rotation = worldView.transform.rotation;
            if (campfirePrefab != null)
            {
                ModelCopy.Copy(campfirePrefab, camp, "Campfire");
            }
            if (flameMaterial != null)
            {
                flame = new FlameCluster(camp, new Vector3(0f, 0.05f, 0f), 0.6f, flameMaterial);
            }
            if (smokeMaterial != null)
            {
                TorchFlame.MakeSmoke(camp, new Vector3(0f, 0.45f, 0f), smokeMaterial, 3f, 1f); // bigger and blacker (GL494)
            }
            var light = new GameObject("Fire light");
            light.transform.SetParent(camp, false);
            light.transform.localPosition = new Vector3(0f, 0.6f, 0f);
            glow = light.AddComponent<Light>();
            glow.type = LightType.Point;
            glow.color = new Color(1f, 0.62f, 0.3f);
            glow.range = 16f;
            glow.shadows = LightShadows.None;

            // The adventurer stands a step downhill of the fire, facing it.
            Vector2 side = new Vector2(1.3f, 0.4f);
            float standing = TerrainSurface.HeightAt(generator, peak.x + side.x, peak.y + side.y, 0f, top);
            var person = new GameObject("Lone adventurer");
            person.transform.SetParent(camp, false);
            person.transform.position = worldView.transform.TransformPoint(new Vector3(peak.x + side.x, standing, peak.y + side.y));
            person.transform.rotation = worldView.transform.rotation * Quaternion.LookRotation(new Vector3(-side.x, 0f, -side.y));
            int seed = config.Terrain != null ? config.Terrain.Seed : 0;
            person.AddComponent<Adventurer>().Setup(prices, Purse, Disposition, material, seed + 476);
        }

        private static string Key(string name)
        {
            return $"{KeyPrefix}.{name}";
        }

        public void SaveValues(IDictionary<string, string> values)
        {
            if (!Arrived)
            {
                return;
            }
            values[Key("arrived")] = "1";
            values[Key("purse")] = Purse.Coins.ToString("R", CultureInfo.InvariantCulture);
            values[Key("disposition")] = string.Format(CultureInfo.InvariantCulture, "{0};{1:R}", Disposition.Value, Disposition.Carried);
        }

        public void RestoreValues(IReadOnlyDictionary<string, string> values)
        {
            Arrived = values.TryGetValue(Key("arrived"), out string arrived) && arrived == "1";
            float coins = AdventurerVisit.Purse;
            if (values.TryGetValue(Key("purse"), out string purse))
            {
                float.TryParse(purse, NumberStyles.Float, CultureInfo.InvariantCulture, out coins);
            }
            // The adventurer holds this wallet, so change its coins rather than replace it.
            Purse.Spend(Purse.Coins);
            Purse.Earn(coins);
            if (values.TryGetValue(Key("disposition"), out string text))
            {
                string[] parts = text.Split(';');
                if (parts.Length == 2 && int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)
                    && float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float carried))
                {
                    Disposition.Restore(value, carried);
                }
            }
            // A save from before they came: no camp until the dawn comes round again.
            if (!Arrived && camp != null)
            {
                Destroy(camp.gameObject);
                camp = null;
            }
        }
    }
}
