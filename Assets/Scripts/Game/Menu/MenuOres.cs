using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// Stamps the title page's ore nodes into its small real world (GL512), by <see cref="World"/>'s edit paths so the
    /// chunks remesh themselves: gold at the bottom of the cut face (z = -8), then silver, chalcopyrite, malachite and tin
    /// near the top, and a tin node standing out of the grass for the miner (the stump that sat by the fire read as a stray dirt ball, GL537). Placed by hand, not rolled, so each
    /// one shows on the cut face whatever the seed's terrain does.
    /// </summary>
    public sealed class MenuOres
    {
        // How far into the cut face a node's centre sits (so a ball of ore shows on the face).
        private const float FaceInset = 0.4f;
        // The hand-placed layout was made for a 32 m block (half 16); a bigger block spreads it out by this much (GL535).
        private const float DesignHalf = 16f;

        private readonly World world;
        private readonly MaterialRegistry registry;

        private readonly float top;
        private readonly float scale;
        private float Face => -half;
        private readonly float half;

        public MenuOres(World world, MaterialRegistry registry, float top, float halfExtent = DesignHalf)
        {
            half = halfExtent;
            scale = halfExtent / DesignHalf;
            this.world = world;
            this.registry = registry;
            this.top = top;
        }

        /// <summary>Where the miner's tin node stands (set by <see cref="StampAll"/>).</summary>
        public Vector3 TinOnSurface { get; private set; }

        /// <summary>The height of the ground at a horizontal position, found by scanning down from the top.</summary>
        public float SurfaceAt(float x, float z)
        {
            float step = world.Grid.VoxelSize;
            for (float y = top - step; y > 0f; y -= step)
            {
                float? density = world.GetDensity(world.Grid.WorldToNearestSample(new Vector3(x, y, z)));
                if (density.HasValue && density.Value >= 0.5f)
                {
                    return y;
                }
            }
            return 0f;
        }

        /// <summary>
        /// Turns the soil on the block's outer faces to stone, so the cut skirt
        /// shows stone and ore, not hanging bars of dirt where a soil layer happens to meet the edge (GL535).
        /// </summary>
        public void StripSoilFromSkirt()
        {
            byte? dirt = registry.Find("Dirt")?.Id;
            byte? stone = registry.Find("Stone")?.Id;
            if (dirt == null || stone == null)
            {
                return;
            }
            float step = world.Grid.VoxelSize;
            // The lowest ground round the rim: a tree or rock on the edge makes SurfaceAt too high to bound the soil.
            float lowest = float.MaxValue;
            for (float a = -half + 0.5f; a < half; a += 2f)
            {
                lowest = Mathf.Min(lowest, SurfaceAt(a, -half + 0.5f), SurfaceAt(a, half - 0.5f), SurfaceAt(-half + 0.5f, a), SurfaceAt(half - 0.5f, a));
            }
            for (float a = -half; a <= half; a += step)
            {
                for (float b = -half; b < -half + step * 6f; b += step)
                {
                    StripColumn(a, b, lowest, dirt.Value, stone.Value);
                    StripColumn(a, -b, lowest, dirt.Value, stone.Value);
                    StripColumn(b, a, lowest, dirt.Value, stone.Value);
                    StripColumn(-b, a, lowest, dirt.Value, stone.Value);
                }
            }
        }

        private void StripColumn(float x, float z, float lowest, byte dirt, byte stone)
        {
            float step = world.Grid.VoxelSize;
            for (float y = top - step; y > lowest - 8f; y -= step)
            {
                Vector3Int sample = world.Grid.WorldToNearestSample(new Vector3(x, y, z));
                if (world.GetMaterial(sample) == dirt)
                {
                    world.SetMaterial(sample, stone);
                }
            }
        }

        /// <summary>Puts every ore, and the surface tin in, measured down from <paramref name="ground"/>.</summary>
        public void StampAll()
        {
            float z = Face + FaceInset;
            // Gold lowest, then silver, chalcopyrite, malachite and tin nearest the surface; each measured down from the
            // ground at its own spot (the ground tilts: a mountain's edge).
            DeepBall("Gold", 4.8f * scale, 13f, z, 2.8f);
            DeepBall("Gold", -7.6f * scale, 11.5f, z, 2.0f);
            DeepBall("Silver", -1.6f * scale, 9.6f, z, 2.6f);
            DeepBall("Chalcopyrite", 7.2f * scale, 7.6f, z, 2.4f);
            DeepBall("Malachite", -7.2f * scale, 6f, z, 2.2f);
            DeepBall("Malachite", 1.2f * scale, 6.4f, z, 1.6f);
            DeepBall("Cassiterite", -3.6f * scale, 3.4f, z, 1.8f);
            DeepBall("Cassiterite", 10.4f * scale, 3f, z, 1.6f);

            // The miner's node: a lump of tin standing out of the grass.
            TinOnSurface = new Vector3(TinX, SurfaceAt(TinX, TinZ), TinZ);
            Ball("Cassiterite", TinOnSurface + Vector3.up * 0.2f, 1.1f, true);
        }

        /// <summary>Where the miner's tin node stands (x, z).</summary>
        public float TinX => 4.4f * scale;
        public float TinZ => -half + 6f;

        private void DeepBall(string material, float x, float depth, float z, float radius)
        {
            Ball(material, new Vector3(x, SurfaceAt(x, Face + 1f) - depth, z), radius);
        }

        // A ball of one material: its solid samples change material; with raise, air samples become ground too.
        private void Ball(string material, Vector3 centre, float radius, bool raise = false)
        {
            byte? id = registry.Find(material)?.Id;
            if (id == null)
            {
                UnityEngine.Debug.LogWarning($"MenuOres: no material called '{material}'.");
                return;
            }
            Fill(centre, radius, radius, id.Value, raise);
        }

        private void Fill(Vector3 centre, float radius, float halfHeight, byte id, bool raise)
        {
            WorldGrid grid = world.Grid;
            Vector3Int min = grid.WorldToNearestSample(centre - new Vector3(radius, halfHeight, radius));
            Vector3Int max = grid.WorldToNearestSample(centre + new Vector3(radius, halfHeight, radius));
            for (int x = min.x; x <= max.x; x++)
            {
                for (int y = min.y; y <= max.y; y++)
                {
                    for (int z = min.z; z <= max.z; z++)
                    {
                        var sample = new Vector3Int(x, y, z);
                        Vector3 offset = grid.SampleToWorld(sample) - centre;
                        float d = Mathf.Sqrt(offset.x * offset.x + offset.z * offset.z) / radius;
                        float v = Mathf.Abs(offset.y) / halfHeight;
                        if (d * d + v * v > 1f)
                        {
                            continue;
                        }
                        if (raise)
                        {
                            world.SetDensity(sample, 1f);
                        }
                        float? density = world.GetDensity(sample);
                        if (density.HasValue && density.Value >= 0.5f)
                        {
                            world.SetMaterial(sample, id);
                        }
                    }
                }
            }
        }
    }
}
