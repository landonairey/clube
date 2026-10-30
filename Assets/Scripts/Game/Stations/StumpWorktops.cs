using System.Collections.Generic;
using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// Makes worktop terrain (<see cref="VoxelMaterial.IsWorktop"/>: a tree's stump, GL36, or Flat Rock
    /// chiselled from a surface rock, GL88) usable like a station. A worktop is only terrain data, so when <see cref="PlayerInteractor"/> aims
    /// at a chunk's surface this finds the worktop samples there, takes the connected piece as
    /// one stump, and hands back a <see cref="StationObject"/> made for it the first time. The
    /// station keeps what's been put on it for as long as the game runs.
    /// </summary>
    public class StumpWorktops : MonoBehaviour
    {
        /// <summary>A worktop material other than the stump, with its own name and recipes (GL88).</summary>
        [System.Serializable]
        public class Kind
        {
            public VoxelMaterial Material;
            public string Name = "Flat Rock";
            public StationKind Station = StationKind.FlatRock;
            public List<Recipe> Recipes = new List<Recipe>();
        }

        // The largest stump looked at, in samples; a safety stop for the search.
        private const int MaxSamples = 4096;

        // How far under a trunk hit a stump is looked for (GL231), in samples: 10 at 0.25 m reaches 2.5 m down, so the
        // trunk at eye height finds it; and how far to the side of the line down.
        private const int StumpSearchDepth = 10;
        private const int StumpSearchRadius = 2;

        private static readonly Vector3Int[] Neighbours = BuildNeighbours();

        [Tooltip("What can be made at a stump.")]
        [SerializeField]
        private List<Recipe> recipes = new List<Recipe>();

        [Tooltip("Other worktop materials (Flat Rock, GL88): what each is called and what can be made at it. Any other worktop is a stump.")]
        [SerializeField]
        private List<Kind> kinds = new List<Kind>();

        private readonly Dictionary<Vector3Int, StationObject> stations = new Dictionary<Vector3Int, StationObject>();
        private readonly HashSet<Vector3Int> seen = new HashSet<Vector3Int>();
        private readonly Stack<Vector3Int> pending = new Stack<Vector3Int>();

        /// <summary>The worktop under a ray hit on the terrain, or null when it isn't one.</summary>
        public IInteractable Find(RaycastHit hit)
        {
            return Find(hit, out _);
        }

        /// <summary>
        /// The worktop under a ray hit on the terrain, or null when it isn't one. A hit on the lower trunk of a tree finds
        /// the stump under it (GL231), so it can be used without clearing the wood away first; <paramref name="onTrunk"/>
        /// says so, and tools still chop the trunk.
        /// </summary>
        public IInteractable Find(RaycastHit hit, out bool onTrunk)
        {
            onTrunk = false;
            WorldView view = hit.collider.GetComponentInParent<WorldView>();
            if (view == null || !view.IsReady)
            {
                return null;
            }

            World world = view.World;
            MaterialRegistry registry = view.Config.Materials;
            if (registry == null)
            {
                return null;
            }

            // The surface lies between samples: look at the ones around the hit, from just inside it.
            Vector3 local = view.transform.InverseTransformPoint(hit.point - hit.normal * view.VoxelSize * 0.5f);
            Vector3Int near = world.Grid.WorldToNearestSample(local);
            bool found = false;
            Vector3Int start = default;
            float best = float.PositiveInfinity;
            for (int z = -1; z <= 1; z++)
            {
                for (int y = -1; y <= 1; y++)
                {
                    for (int x = -1; x <= 1; x++)
                    {
                        Vector3Int sample = near + new Vector3Int(x, y, z);
                        if (!IsWorktopSample(world, registry, view.Config.IsoLevel, sample))
                        {
                            continue;
                        }
                        float distance = (world.Grid.SampleToWorld(sample) - local).sqrMagnitude;
                        if (distance < best)
                        {
                            best = distance;
                            start = sample;
                            found = true;
                        }
                    }
                }
            }
            if (!found)
            {
                // The trunk grows out of the stump and covers most of it at this voxel size: look under the wood.
                if (!IsPlantNear(world, registry, view.Config.IsoLevel, near) || !FindStumpBelow(world, registry, view.Config.IsoLevel, near, out start))
                {
                    return null;
                }
                onTrunk = true;
            }

            Vector3Int key = LowestSample(world, registry, view.Config.IsoLevel, start);
            if (!stations.TryGetValue(key, out StationObject station) || station == null)
            {
                Kind kind = KindOf(world.GetMaterial(key));
                var made = new GameObject(kind != null ? kind.Name + " worktop" : "Stump worktop");
                made.transform.SetParent(transform, false);
                made.transform.position = view.transform.TransformPoint(world.Grid.SampleToWorld(key));
                station = made.AddComponent<StationObject>();
                if (kind != null)
                {
                    station.Configure(kind.Name, kind.Station, kind.Recipes);
                }
                else
                {
                    station.Configure("Stump", StationKind.Worktop, recipes);
                }
                stations[key] = station;
            }
            return station;
        }

        // Whether the hit is on a tree: a solid plant sample (wood) around it.
        private static bool IsPlantNear(World world, MaterialRegistry registry, float iso, Vector3Int near)
        {
            for (int z = -1; z <= 1; z++)
            {
                for (int y = -1; y <= 1; y++)
                {
                    for (int x = -1; x <= 1; x++)
                    {
                        Vector3Int sample = near + new Vector3Int(x, y, z);
                        float? density = world.GetDensity(sample);
                        byte? id = world.GetMaterial(sample);
                        if (density.HasValue && density.Value >= iso && id.HasValue && registry.Get(id.Value) is VoxelMaterial material
                            && material.Category == VoxelMaterialCategory.Plant)
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        // The nearest stump sample under a trunk hit: straight down first, within a little of the trunk's line.
        private static bool FindStumpBelow(World world, MaterialRegistry registry, float iso, Vector3Int near, out Vector3Int stump)
        {
            for (int down = 0; down <= StumpSearchDepth; down++)
            {
                for (int ring = 0; ring <= StumpSearchRadius; ring++)
                {
                    for (int z = -ring; z <= ring; z++)
                    {
                        for (int x = -ring; x <= ring; x++)
                        {
                            if (Mathf.Max(Mathf.Abs(x), Mathf.Abs(z)) != ring)
                            {
                                continue;
                            }
                            Vector3Int sample = near + new Vector3Int(x, -down, z);
                            if (IsWorktopSample(world, registry, iso, sample))
                            {
                                stump = sample;
                                return true;
                            }
                        }
                    }
                }
            }
            stump = default;
            return false;
        }

        private Kind KindOf(byte? material)
        {
            foreach (Kind kind in kinds)
            {
                if (kind.Material != null && material.HasValue && kind.Material.Id == material.Value)
                {
                    return kind;
                }
            }
            return null;
        }

        // The connected piece of worktop samples is one stump; its lowest sample names it.
        private Vector3Int LowestSample(World world, MaterialRegistry registry, float iso, Vector3Int start)
        {
            seen.Clear();
            pending.Clear();
            seen.Add(start);
            pending.Push(start);
            Vector3Int lowest = start;
            while (pending.Count > 0 && seen.Count < MaxSamples)
            {
                Vector3Int sample = pending.Pop();
                if (IsLower(sample, lowest))
                {
                    lowest = sample;
                }
                foreach (Vector3Int step in Neighbours)
                {
                    Vector3Int next = sample + step;
                    if (!seen.Contains(next) && IsWorktopSample(world, registry, iso, next))
                    {
                        seen.Add(next);
                        pending.Push(next);
                    }
                }
            }
            return lowest;
        }

        private static bool IsLower(Vector3Int a, Vector3Int b)
        {
            if (a.y != b.y)
            {
                return a.y < b.y;
            }
            return a.x != b.x ? a.x < b.x : a.z < b.z;
        }

        private static bool IsWorktopSample(World world, MaterialRegistry registry, float iso, Vector3Int sample)
        {
            float? density = world.GetDensity(sample);
            if (!density.HasValue || density.Value < iso)
            {
                return false;
            }
            byte? id = world.GetMaterial(sample);
            if (!id.HasValue)
            {
                return false;
            }
            VoxelMaterial material = registry.Get(id.Value);
            return material != null && material.IsWorktop;
        }

        private static Vector3Int[] BuildNeighbours()
        {
            var steps = new List<Vector3Int>();
            for (int z = -1; z <= 1; z++)
            {
                for (int y = -1; y <= 1; y++)
                {
                    for (int x = -1; x <= 1; x++)
                    {
                        if (x != 0 || y != 0 || z != 0)
                        {
                            steps.Add(new Vector3Int(x, y, z));
                        }
                    }
                }
            }
            return steps.ToArray();
        }
    }
}
