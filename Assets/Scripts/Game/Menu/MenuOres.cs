using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// Stamps the title page's ore nodes into its small real world (GL512), by <see cref="World"/>'s edit paths so the
    /// chunks remesh themselves: gold at the bottom of the cut face (z = -8), then silver, chalcopyrite, malachite and tin
    /// near the top, a tin node standing out of the grass for the miner, and a stump. Placed by hand, not rolled, so each
    /// one shows on the cut face whatever the seed's terrain does.
    /// </summary>
    public sealed class MenuOres
    {
        // The cut face of the 16 m block, and how far into it a node's centre sits (so a ball of ore shows on the face).
        private const float Face = -8f;
        private const float FaceInset = 0.2f;

        private readonly World world;
        private readonly MaterialRegistry registry;

        public MenuOres(World world, MaterialRegistry registry)
        {
            this.world = world;
            this.registry = registry;
        }

        /// <summary>Where the miner's tin node stands (set by <see cref="StampAll"/>).</summary>
        public Vector3 TinOnSurface { get; private set; }

        /// <summary>The height of the ground at a horizontal position, found by scanning down from the top.</summary>
        public float SurfaceAt(float x, float z)
        {
            float step = world.Grid.VoxelSize;
            for (float y = 14f; y > 0f; y -= step)
            {
                float? density = world.GetDensity(world.Grid.WorldToNearestSample(new Vector3(x, y, z)));
                if (density.HasValue && density.Value >= 0.5f)
                {
                    return y;
                }
            }
            return 0f;
        }

        /// <summary>Puts every ore, the surface tin and the stump in, measured down from <paramref name="ground"/>.</summary>
        public void StampAll(float ground)
        {
            float z = Face + FaceInset;
            Ball("Gold", new Vector3(2.4f, 1.5f, z), 1.4f);
            Ball("Gold", new Vector3(-3.8f, 2.4f, z), 1.0f);
            Ball("Silver", new Vector3(-0.8f, ground - 4.8f, z), 1.3f);
            Ball("Chalcopyrite", new Vector3(3.6f, ground - 3.8f, z), 1.2f);
            Ball("Malachite", new Vector3(-3.6f, ground - 3.0f, z), 1.1f);
            Ball("Malachite", new Vector3(0.6f, ground - 3.2f, z), 0.8f);
            Ball("Cassiterite", new Vector3(-1.8f, ground - 1.7f, z), 0.9f);
            Ball("Cassiterite", new Vector3(5.2f, ground - 1.5f, z), 0.8f);

            // The miner's node: a lump of tin standing out of the grass.
            float x = 2.2f;
            float nodeZ = -3.4f;
            TinOnSurface = new Vector3(x, SurfaceAt(x, nodeZ), nodeZ);
            Ball("Cassiterite", TinOnSurface + Vector3.up * 0.1f, 0.55f, true);

            // The stump beside the fire, in the real stump material.
            float stumpX = 4.6f;
            float stumpZ = -1.0f;
            Cylinder("Stump", new Vector3(stumpX, SurfaceAt(stumpX, stumpZ) - 0.2f, stumpZ), 0.5f, 0.8f);
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

        private void Cylinder(string material, Vector3 baseCentre, float radius, float height)
        {
            byte? id = registry.Find(material)?.Id;
            if (id != null)
            {
                Fill(baseCentre + Vector3.up * height * 0.5f, radius, height * 0.5f, id.Value, true);
            }
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
