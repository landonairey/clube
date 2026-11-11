using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// The tutorial map's terrain touches, written through the world's edit path once the ground has streamed in
    /// (GL545, GL547): a short stone wall from each side of the warehouse into the mountain, and rock instead of grass at the mountain's foot.
    /// Both only add to the ground, so they can be run again as more of it loads.
    /// </summary>
    public static class TutorialTerrain
    {
        private const float WallHeight = 4f;
        private const float WallThickness = 1.5f;
        // How far the walls reach out from the building (the mountain starts 2 m past it).
        private const float WallReach = 8f;

        // The mountain's grass reaches this far up and out from the fields' edge; past it the face is already rock.
        private const float FootHeight = 5f;
        private const float FootReach = 8f;

        /// <summary>Stamps the walls and turns the mountain's foot to stone. Map space.</summary>
        public static void Apply(WorldView world)
        {
            MaterialRegistry materials = world.Config.Materials;
            if (materials == null || world.World == null)
            {
                return;
            }
            VoxelMaterial wall = materials.Find("Loose stone");
            VoxelMaterial stone = materials.Find("Stone");
            float iso = world.Config.IsoLevel;
            if (wall != null)
            {
                StampWalls(world.World, wall.Id, iso);
            }
            if (stone != null)
            {
                StoneFoot(world.World, stone.Id, iso);
            }
        }

        // One short wall from the middle of each side of the building out into the mountain, so the only way to the front
        // doors is through the building (GL532). The mountain is steeper than a player can walk, so the wall ends well
        // inside its face, with no gap round the end.
        private static void StampWalls(World world, byte material, float iso)
        {
            Rect r = CompanyWork.Warehouse;
            float midZ = r.center.y;
            TownStructures.StampWallRun(world, new Vector2(r.xMin + 0.3f, midZ), new Vector2(r.xMin - WallReach, midZ), WallThickness, WallHeight, Ground, material, iso);
            TownStructures.StampWallRun(world, new Vector2(r.xMax - 0.3f, midZ), new Vector2(r.xMax + WallReach, midZ), WallThickness, WallHeight, Ground, material, iso);
        }

        private static float Ground(Vector2 at)
        {
            return CompanyWork.Height(at.x, at.y);
        }

        // Grass and dirt on the mountain side, from the foot up to about head height, become stone.
        private static void StoneFoot(World world, byte stone, float iso)
        {
            WorldGrid grid = world.Grid;
            Rect f = CompanyWork.Fields;
            float g = CompanyWork.Ground;
            Vector3Int min = grid.WorldToNearestSample(new Vector3(f.xMin - FootReach, g - 1f, CompanyWork.Warehouse.yMin - FootReach));
            Vector3Int max = grid.WorldToNearestSample(new Vector3(f.xMax + FootReach, g + FootHeight, f.yMax + FootReach));
            for (int x = min.x; x <= max.x; x++)
            {
                for (int z = min.z; z <= max.z; z++)
                {
                    Vector3 column = grid.SampleToWorld(new Vector3Int(x, 0, z));
                    if (CompanyWork.OutOfFields(column.x, column.z) <= 0.2f)
                    {
                        continue;
                    }
                    for (int y = min.y; y <= max.y; y++)
                    {
                        var sample = new Vector3Int(x, y, z);
                        if (world.TryGetOwner(sample, out Chunk chunk, out Vector3Int local) && chunk.GetDensity(local) >= iso)
                        {
                            world.SetMaterial(sample, stone);
                        }
                    }
                }
            }
        }
    }
}
