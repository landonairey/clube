using System.Collections.Generic;
using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// Puts the animals in the world (GL605), on the World object. Herds live at <see cref="HerdSites"/>: places fixed by
    /// the world's seed, each with a species of the biome there (the map's <see cref="BiomeMap"/>; in a world without one,
    /// <see cref="HerdSites.StandInBiome"/> from the ground's material). Only the herds near the player exist: a herd is
    /// made when its site comes within <see cref="spawnRadius"/> and its ground is loaded, each animal on dry open ground
    /// near the site, and removed past <see cref="despawnRadius"/>; no more than <see cref="maxAnimals"/> live at once.
    /// An animal killed stays dead for the session, and in the save (<see cref="ISavedValues"/>): its key is remembered
    /// and its herd comes back without it. Also what the animals ask about the world: the hour, the player, the ground.
    /// </summary>
    [RequireComponent(typeof(WorldView))]
    public class AnimalHerds : MonoBehaviour, ISavedValues
    {
        private const string DeadKey = "fauna.dead";

        /// <summary>A herd in the world: its site and its living animals.</summary>
        public sealed class Herd
        {
            public HerdSite Site;
            public readonly List<Animal> Members = new List<Animal>();

            /// <summary>The middle of its living animals, on the map (x, z); its site's when none is left.</summary>
            public Vector2 Centre;
        }

        [Tooltip("Finds the drops' items by id.")]
        [SerializeField]
        private ItemCatalog catalog;

        [Tooltip("The animals' material: the lit vertex-colour LowPoly one.")]
        [SerializeField]
        private Material material;

        [Tooltip("Gives the hour for the day and night routines; found in the scene when empty.")]
        [SerializeField]
        private DayNightCycle dayNight;

        [Tooltip("A herd appears when its home is this near the player, in metres.")]
        [SerializeField, Min(10f)]
        private float spawnRadius = 90f;

        [Tooltip("A herd is removed when all of it is this far from the player, in metres.")]
        [SerializeField, Min(20f)]
        private float despawnRadius = 130f;

        [Tooltip("The most living animals at once.")]
        [SerializeField, Min(0)]
        private int maxAnimals = 40;

        [Tooltip("Seconds a carcass stays after its last drop is taken.")]
        [SerializeField, Min(0f)]
        private float emptiedCarcassSeconds = 15f;

        [Tooltip("Seconds an untouched carcass stays, once the player has walked away.")]
        [SerializeField, Min(0f)]
        private float carcassSeconds = 600f;

        private readonly Dictionary<Vector2Int, Herd> herds = new Dictionary<Vector2Int, Herd>();
        private readonly List<Animal> animals = new List<Animal>();
        private readonly HashSet<string> dead = new HashSet<string>();
        private readonly List<HerdSite> near = new List<HerdSite>();
        private readonly List<Vector2Int> leaving = new List<Vector2Int>();
        private Dictionary<string, BiomeDefinition> standIns;
        private WorldView worldView;
        private PlayerController player;
        private ChestPanel chestPanel;
        private Transform root;
        private float scanTimer;
        private bool groundUnknown;

        public ItemCatalog Catalog => catalog;

        /// <summary>The living animals.</summary>
        public IReadOnlyList<Animal> Animals => animals;

        public int HerdCount => herds.Count;

        /// <summary>Animals killed this session (and in the save), by <see cref="HerdSite.AnimalKey"/>.</summary>
        public IReadOnlyCollection<string> Dead => dead;

        /// <summary>Hour of the day, 0-24; noon in a scene with no clock.</summary>
        public float Hour => dayNight != null ? dayNight.Hour : 12f;

        /// <summary>True while a player walks the world: the animals watch for it.</summary>
        public bool HasPlayer { get; private set; }

        /// <summary>Where the player is, on the map (x, z).</summary>
        public Vector2 PlayerAt { get; private set; }

        public float EmptiedCarcassSeconds => emptiedCarcassSeconds;

        public float CarcassSeconds => carcassSeconds;

        private void Awake()
        {
            worldView = GetComponent<WorldView>();
        }

        private void OnDisable()
        {
            Clear();
        }

        /// <summary>Removes every herd and carcass; the dead stay remembered.</summary>
        public void Clear()
        {
            herds.Clear();
            animals.Clear();
            if (root != null)
            {
                Destroy(root.gameObject);
                root = null;
            }
        }

        private void Update()
        {
            if (worldView == null || !worldView.IsReady || material == null)
            {
                return;
            }
            if (player == null)
            {
                player = FindAnyObjectByType<PlayerController>();
            }
            if (dayNight == null)
            {
                dayNight = FindAnyObjectByType<DayNightCycle>();
            }
            Transform viewer = player != null ? player.transform : Camera.main != null ? Camera.main.transform : null;
            if (viewer == null)
            {
                return;
            }
            Vector3 local = transform.InverseTransformPoint(viewer.position);
            PlayerAt = new Vector2(local.x, local.z);
            HasPlayer = player != null && player.enabled;

            foreach (Herd herd in herds.Values)
            {
                Vector2 sum = Vector2.zero;
                foreach (Animal animal in herd.Members)
                {
                    Vector3 at = animal.transform.localPosition;
                    sum += new Vector2(at.x, at.z);
                }
                herd.Centre = herd.Members.Count > 0 ? sum / herd.Members.Count : herd.Site.Centre;
            }

            scanTimer -= Time.deltaTime;
            if (scanTimer <= 0f)
            {
                scanTimer = 1f;
                Scan(local.y);
            }
        }

        // Makes the herds that came near and removes the ones left behind.
        private void Scan(float playerHeight)
        {
            leaving.Clear();
            foreach (KeyValuePair<Vector2Int, Herd> pair in herds)
            {
                Herd herd = pair.Value;
                if ((herd.Centre - PlayerAt).sqrMagnitude > despawnRadius * despawnRadius
                    && (herd.Site.Centre - PlayerAt).sqrMagnitude > spawnRadius * spawnRadius)
                {
                    leaving.Add(pair.Key);
                }
            }
            foreach (Vector2Int cell in leaving)
            {
                foreach (Animal animal in herds[cell].Members)
                {
                    animals.Remove(animal);
                    Destroy(animal.gameObject);
                }
                herds.Remove(cell);
            }

            near.Clear();
            HerdSites.Near(WorldSeed, PlayerAt, spawnRadius, BiomeAt, AnimalSpeciesTable.All, near);
            // The nearest first, so the cap leaves out the far ones.
            near.Sort((a, b) => (a.Centre - PlayerAt).sqrMagnitude.CompareTo((b.Centre - PlayerAt).sqrMagnitude));
            foreach (HerdSite site in near)
            {
                if (!herds.ContainsKey(site.Cell) && animals.Count + site.Count <= maxAnimals)
                {
                    TrySpawn(site, playerHeight);
                }
            }
        }

        private int WorldSeed => worldView.Config != null && worldView.Config.Terrain != null ? worldView.Config.Terrain.Seed : 0;

        // The biome at a map point: the map's own, else the stand-in from the ground there (null while it isn't loaded).
        private BiomeDefinition BiomeAt(float x, float z)
        {
            BiomeMap map = BiomeMap.For(worldView.Generator);
            if (map != null)
            {
                return map.At(x, z);
            }
            if (!TryOpenGround(x, z, PlayerHeight, out _, out VoxelMaterial ground, out bool wet) || wet)
            {
                return null;
            }
            if (standIns == null)
            {
                standIns = new Dictionary<string, BiomeDefinition>();
                foreach (BiomeDefinition biome in BiomeDefinition.Defaults(null, null, null))
                {
                    standIns[biome.Name] = biome;
                }
            }
            bool sand = ground != null && ground.DisplayName.ToLowerInvariant().Contains("sand");
            bool rock = ground != null && ground.Kind == MaterialKind.Rock;
            return standIns[HerdSites.StandInBiome(sand, rock, HerdSites.CellOf(x, z))];
        }

        private float PlayerHeight => player != null ? transform.InverseTransformPoint(player.transform.position).y
            : Camera.main != null ? transform.InverseTransformPoint(Camera.main.transform.position).y : 0f;

        // A herd at its site: each animal not yet killed, on open dry ground near the site. Nothing while the ground isn't loaded.
        private void TrySpawn(HerdSite site, float playerHeight)
        {
            if (!TryOpenGround(site.Centre.x, site.Centre.y, playerHeight, out _, out _, out _))
            {
                return;
            }
            var herd = new Herd { Site = site, Centre = site.Centre };
            herds[site.Cell] = herd;
            if (root == null)
            {
                root = new GameObject("Animals").transform;
                root.SetParent(transform, false);
            }
            float spread = site.Species.Range * 0.5f;
            for (int i = 0; i < site.Count; i++)
            {
                string key = site.AnimalKey(i);
                if (dead.Contains(key))
                {
                    continue;
                }
                int seed = unchecked(site.Seed + i * 7919);
                var random = new System.Random(seed);
                for (int attempt = 0; attempt < 12; attempt++)
                {
                    float angle = (float)random.NextDouble() * Mathf.PI * 2f, far = Mathf.Sqrt((float)random.NextDouble()) * spread;
                    float x = site.Centre.x + Mathf.Cos(angle) * far, z = site.Centre.y + Mathf.Sin(angle) * far;
                    if (!TryOpenGround(x, z, playerHeight, out float height, out VoxelMaterial ground, out bool wet)
                        || wet || (ground != null && ground.Kind == MaterialKind.Plant))
                    {
                        continue;
                    }
                    var go = new GameObject(site.Species.Name);
                    go.transform.SetParent(root, false);
                    go.transform.localPosition = new Vector3(x, height, z);
                    var animal = go.AddComponent<Animal>();
                    animal.Setup(this, herd, site.Species, seed, key, material);
                    herd.Members.Add(animal);
                    animals.Add(animal);
                    break;
                }
            }
        }

        /// <summary>An animal died: it leaves its herd and is remembered as dead; its body stays as the carcass.</summary>
        internal void Died(Animal animal, Herd herd)
        {
            herd.Members.Remove(animal);
            animals.Remove(animal);
            dead.Add(animal.Key);
        }

        /// <summary>
        /// The ground's height under a map point, looking down from a little over <paramref name="near"/> (so a walker
        /// under a tree finds the ground, not the leaves), and whether water stands on it. False where there is none in
        /// reach: a drop, or ground not loaded.
        /// </summary>
        public bool TryGround(float x, float z, float near, out float height, out bool wet)
        {
            height = 0f;
            wet = false;
            World world = worldView.World;
            if (world == null)
            {
                return false;
            }
            var ray = new Ray(new Vector3(x, near + 1.5f, z), Vector3.down);
            if (!world.Raycast(ray, worldView.Config.MeshSettings, 6f, out WorldHit hit) || hit.Point.y < near - 4f)
            {
                return false;
            }
            height = hit.Point.y;
            wet = WaterQuery.IsUnderwater(world, new Vector3(x, height + world.Grid.VoxelSize * 0.6f, z));
            return true;
        }

        // The first ground under the sky at a map point, with what it is made of.
        private bool TryOpenGround(float x, float z, float around, out float height, out VoxelMaterial ground, out bool wet)
        {
            height = 0f;
            ground = null;
            wet = false;
            World world = worldView.World;
            if (world == null)
            {
                return false;
            }
            var ray = new Ray(new Vector3(x, around + 120f, z), Vector3.down);
            if (!world.Raycast(ray, worldView.Config.MeshSettings, 400f, out WorldHit hit))
            {
                return false;
            }
            height = hit.Point.y;
            float voxel = world.Grid.VoxelSize;
            byte? id = world.GetMaterial(world.Grid.WorldToNearestSample(hit.Point + Vector3.down * (voxel * 0.5f)));
            ground = id.HasValue && worldView.Config.Materials != null ? worldView.Config.Materials.Get(id.Value) : null;
            wet = WaterQuery.IsUnderwater(world, new Vector3(x, height + voxel * 0.6f, z));
            return true;
        }

        /// <summary>True when the player is within a distance of a place (in the world's own metres).</summary>
        public bool IsNearPlayer(Vector3 local, float distance)
        {
            return (new Vector2(local.x, local.z) - PlayerAt).sqrMagnitude <= distance * distance;
        }

        /// <summary>True while the player has a carcass's contents open on screen.</summary>
        public bool IsOpen(StorageChest carcass)
        {
            if (chestPanel == null && player != null)
            {
                chestPanel = player.GetComponent<ChestPanel>();
            }
            return chestPanel != null && chestPanel.Current == carcass;
        }

        public void SaveValues(IDictionary<string, string> values)
        {
            values[DeadKey] = string.Join(";", dead);
        }

        public void RestoreValues(IReadOnlyDictionary<string, string> values)
        {
            dead.Clear();
            if (values.TryGetValue(DeadKey, out string saved) && !string.IsNullOrEmpty(saved))
            {
                dead.UnionWith(saved.Split(';'));
            }
            Clear();
        }
    }
}
