using System.Collections.Generic;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// The buildings of a planned settlement as voxels (GL613, GL710, GL711), built to their catalog row
    /// (<see cref="BuildingDesign"/>): a stone plinth and corner posts, board walls story over story with timber bands
    /// between, a shop's open front and counter or a house's door and windows, and a gable roof at the building's own
    /// pitch. The roof is square blocks underneath (the chisel's steps, seen from inside) with a skin of part-solid samples
    /// over them, so marching cubes draws its top as one slant (<see cref="RoofSkin"/>). A work yard in front may have low
    /// stone walls (GL713). <see cref="Blocks"/> puts it all on its terrace in the world.
    /// </summary>
    public static class SettlementStructures
    {
        /// <summary>The door's middle along the front and its width and height, in metres.</summary>
        public const float DoorX = 1.5f;
        public const float DoorWidth = 1f;
        public const float DoorHeight = 2f;

        private const float Plinth = 0.75f;
        private const float Post = 0.5f;
        private const float WindowLow = 1f;
        private const float WindowHigh = 1.75f;
        private const float YardWallHeight = 0.75f;

        // The plain house, for a building with no catalog row.
        private static readonly BuildingDesign PlainHouse = new BuildingDesign { Id = "house", Name = "House" };

        // The plain shop: the trading post's frame and roof.
        private static readonly BuildingDesign PlainShop = new BuildingDesign { Id = "shop", Name = "Shop", ShopFront = true };

        /// <summary>The plain house's voxels in its own frame (the GL613 house: one story, a door and a window in front).</summary>
        public static List<(Vector3 Centre, TownStructures.ShopPart Part)> HouseVoxels(float voxel)
        {
            return Voxels(PlainHouse, 0.5f, voxel);
        }

        /// <summary>The roof's height at a distance <paramref name="out"/> from the ridge, in metres over the pad.</summary>
        public static float RoofAt(BuildingDesign design, float pitch, float voxel, float outward)
        {
            float top = TownLayout.FloorHeight + design.Stories * design.StoryHeight;
            if (design.Battlements)
            {
                // GL715: a flat roof; its parapet and turrets are the highest it reaches.
                return top + BuildingDesign.ParapetHeight + (design.Turrets > 0f ? BuildingDesign.TurretRise : 0f);
            }
            float reach = design.Depth * 0.5f + Eave(voxel);
            return top + Mathf.Max(0f, reach - Mathf.Abs(outward)) * pitch;
        }

        // The roof reaches two voxels past the front and back walls, one past the gable ends.
        private static float Eave(float voxel) => 2f * voxel;

        /// <summary>
        /// A building's voxels in its own frame, by their centres (x, height over the pad, z), with what each is made of.
        /// </summary>
        public static List<(Vector3 Centre, TownStructures.ShopPart Part)> Voxels(BuildingDesign design, float pitch, float voxel)
        {
            var cells = new List<(Vector3, TownStructures.ShopPart)>();
            float w = design.Width * 0.5f;
            float d = design.Depth * 0.5f;
            float floor = TownLayout.FloorHeight;
            int stories = Mathf.Clamp(design.Stories, 1, 3);
            float story = design.StoryHeight;
            float top = floor + stories * story;
            float eave = Eave(voxel);
            float ridge = RoofAt(design, pitch, voxel, 0f);
            float yardFront = d + (design.YardWalls ? design.YardDepth : 0f);
            float h = voxel * 0.5f;
            for (float z = -d - eave + h; z < yardFront; z += voxel)
            {
                for (float x = -w - voxel + h; x < w + voxel; x += voxel)
                {
                    bool inside = Mathf.Abs(x) < w && Mathf.Abs(z) < d;
                    bool sideWall = Mathf.Abs(x) > w - voxel && Mathf.Abs(x) < w && Mathf.Abs(z) < d;
                    bool backWall = z < -d + voxel && z > -d && Mathf.Abs(x) < w;
                    bool frontWall = z > d - voxel && z < d && Mathf.Abs(x) < w;
                    bool post = Mathf.Abs(x) > w - Post && Mathf.Abs(x) < w && Mathf.Abs(z) > d - Post && Mathf.Abs(z) < d;
                    bool roofed = Mathf.Abs(z) < d + eave;
                    // The courtyard's walls (GL713): low stone along the yard's sides.
                    bool yardWall = design.YardWalls && z >= d && Mathf.Abs(x) > w - voxel && Mathf.Abs(x) < w;
                    // The roof over this column: its lowest point (the column's outer edge) and the blocks' top under it.
                    float low = RoofAt(design, pitch, voxel, Mathf.Abs(z) + h);
                    float blockTop = top + Mathf.Floor((low - top) / voxel + 0.001f) * voxel;
                    for (float y = h; y < ridge + voxel; y += voxel)
                    {
                        TownStructures.ShopPart? part = null;
                        if (yardWall)
                        {
                            part = y < floor + YardWallHeight ? TownStructures.ShopPart.Stone : (TownStructures.ShopPart?)null;
                        }
                        else if (y < floor)
                        {
                            part = inside ? TownStructures.ShopPart.Boards : (TownStructures.ShopPart?)null;
                        }
                        else if (y < top)
                        {
                            part = Wall(design, x, y, z, w, d, floor, story, voxel, inside, sideWall, backWall, frontWall, post);
                            // GL715: a castle's or a tower's walls are stone through.
                            if (design.Masonry && part.HasValue && (sideWall || backWall || frontWall))
                            {
                                part = TownStructures.ShopPart.Stone;
                            }
                        }
                        else if (design.Battlements)
                        {
                            part = Battlement(design, x, y, z, w, d, top, voxel, sideWall || backWall || frontWall, inside);
                        }
                        else if (roofed && Mathf.Abs(x) < w + voxel && y < blockTop && y > blockTop - 2f * voxel)
                        {
                            // Two layers of steps; their top is the slant's (RoofSkin).
                            part = TownStructures.ShopPart.Boards;
                        }
                        else if (sideWall && y < blockTop)
                        {
                            // The gable ends fill up under the roof.
                            part = TownStructures.ShopPart.Boards;
                        }
                        if (part.HasValue)
                        {
                            cells.Add((new Vector3(x, y, z), part.Value));
                        }
                    }
                }
            }
            return cells;
        }

        // GL715: over a battlemented building's walls: a flat stone roof, a parapet round it with merlons every other
        // half metre, and a hollow turret at each corner a storey higher, crenellated too.
        private static TownStructures.ShopPart? Battlement(BuildingDesign design, float x, float y, float z, float w, float d, float top, float voxel,
            bool wall, bool inside)
        {
            if (!inside)
            {
                return null;
            }
            float over = y - top;
            float turret = design.Turrets;
            bool inTurret = turret > 0f && Mathf.Abs(x) > w - turret && Mathf.Abs(z) > d - turret;
            if (inTurret)
            {
                float tx = Mathf.Abs(x) - (w - turret), tz = Mathf.Abs(z) - (d - turret);
                bool shell = tx < 0.5f || tz < 0.5f || wall;
                float height = BuildingDesign.ParapetHeight + BuildingDesign.TurretRise;
                if (!shell)
                {
                    return over < voxel || (over > BuildingDesign.ParapetHeight - voxel && over < BuildingDesign.ParapetHeight) ? TownStructures.ShopPart.Stone : (TownStructures.ShopPart?)null;
                }
                float edge = Mathf.Abs(x) > w - voxel || tx < 0.5f ? z : x;
                bool merlon = Mathf.Repeat(edge * 2f, 2f) < 1f;
                return over < height - 0.5f || (merlon && over < height) ? TownStructures.ShopPart.Stone : (TownStructures.ShopPart?)null;
            }
            if (over < voxel)
            {
                // The flat roof.
                return TownStructures.ShopPart.Stone;
            }
            if (wall)
            {
                float along = Mathf.Abs(x) > w - voxel ? z : x;
                bool merlon = Mathf.Repeat(along * 2f, 2f) < 1f;
                float parapet = BuildingDesign.ParapetHeight;
                return over < parapet - 0.5f || (merlon && over < parapet) ? TownStructures.ShopPart.Stone : (TownStructures.ShopPart?)null;
            }
            return null;
        }

        // What the wall of a story is made of at a point, or null for air (a room, a door, a window, a shop's open front).
        private static TownStructures.ShopPart? Wall(BuildingDesign design, float x, float y, float z, float w, float d, float floor, float story,
            float voxel, bool inside, bool sideWall, bool backWall, bool frontWall, bool post)
        {
            int level = Mathf.Min(design.Stories - 1, Mathf.FloorToInt((y - floor) / story));
            float over = y - floor - level * story;
            bool wall = sideWall || backWall || frontWall;
            if (level > 0 && inside && !wall && over < voxel)
            {
                // The floor of an upper story.
                return TownStructures.ShopPart.Boards;
            }
            if (!wall)
            {
                if (level == 0 && design.ShopFront && inside)
                {
                    bool counter = Mathf.Abs(x) < TownLayout.CounterLength * 0.5f && z > TownLayout.CounterZ - 0.5f && z < TownLayout.CounterZ;
                    bool shelf = x > -w + 0.5f && x < -0.5f && z > -d + voxel && z < -d + 2f * voxel;
                    if (counter && over < TownLayout.CounterHeight)
                    {
                        return TownStructures.ShopPart.Trim;
                    }
                    if (shelf && over > 1.25f && over < 1.25f + voxel)
                    {
                        return TownStructures.ShopPart.Trim;
                    }
                }
                return null;
            }
            if (post)
            {
                // Stone corners on the ground story, timber above.
                return level == 0 ? TownStructures.ShopPart.Stone : TownStructures.ShopPart.Trim;
            }
            if (level == 0 && design.ShopFront && frontWall)
            {
                // A shop's open front, a beam over it.
                return over > story - voxel ? TownStructures.ShopPart.Trim : (TownStructures.ShopPart?)null;
            }
            if (level == 0 && frontWall)
            {
                bool door = Mathf.Abs(x - DoorX) < DoorWidth * 0.5f;
                bool window = Mathf.Abs(x + DoorX) < 0.5f;
                if (door)
                {
                    return over < DoorHeight ? (TownStructures.ShopPart?)null : TownStructures.ShopPart.Trim;
                }
                if (window)
                {
                    return over > WindowLow && over < WindowHigh ? (TownStructures.ShopPart?)null : TownStructures.ShopPart.Trim;
                }
            }
            if (level > 0)
            {
                // Windows in the upper stories, every two metres along the front and back, one in each side.
                float along = sideWall ? z : x;
                float span = sideWall ? d : w;
                bool window = sideWall ? Mathf.Abs(along) < 0.5f : Mathf.Abs(Mathf.Repeat(along + span, 2f) - 1f) < 0.4f && Mathf.Abs(along) < span - 0.75f;
                if (window && over > WindowLow && over < WindowHigh)
                {
                    return null;
                }
                // A timber band where one story meets the next.
                return over < voxel ? TownStructures.ShopPart.Trim : TownStructures.ShopPart.Boards;
            }
            return over < Plinth ? TownStructures.ShopPart.Stone : TownStructures.ShopPart.Boards;
        }

        /// <summary>
        /// GL711: the samples over a building's stepped roof that make its top one slant: each sample within a voxel of the
        /// roof's plane, by its global index, with the density that puts the surface on the plane (0.5 on it, rising a half
        /// a voxel under). Written after the blocks (their top corners too); the steps stay square inside.
        /// </summary>
        public static Dictionary<Vector3Int, float> RoofSkin(SettlementBuilding building, float voxel, int slice = 0, int slices = 1)
        {
            BuildingDesign design = DesignOf(building);
            var skin = new Dictionary<Vector3Int, float>();
            if (design.Battlements)
            {
                // GL715: a flat roof has no slant to draw.
                return skin;
            }
            float w = design.Width * 0.5f + voxel;
            float reach = design.Depth * 0.5f + Eave(voxel);
            float top = TownLayout.FloorHeight + Mathf.Clamp(design.Stories, 1, 3) * design.StoryHeight;
            float width = design.Width + 4f * voxel;
            for (float z = -reach; z <= reach + 0.001f; z += voxel)
            {
                float plane = RoofAt(design, building.Pitch, voxel, z);
                for (float x = -w; x <= w + 0.001f; x += voxel)
                {
                    // A voxel's margin into the neighbouring slices: their blocks carve round them when written.
                    if (slices > 1 && !InSlice(x - voxel, width, slice, slices) && !InSlice(x, width, slice, slices) && !InSlice(x + voxel, width, slice, slices))
                    {
                        continue;
                    }
                    Vector2 world = building.ToWorld(new Vector2(x, z));
                    for (float y = Mathf.Floor(plane / voxel) * voxel - voxel; y <= plane + voxel; y += voxel)
                    {
                        if (y < top)
                        {
                            continue;
                        }
                        float density = Mathf.Clamp01(0.5f + 0.5f * (plane - y) / voxel);
                        if (density <= 0f)
                        {
                            continue;
                        }
                        var sample = new Vector3Int(Mathf.RoundToInt(world.x / voxel), Mathf.RoundToInt((building.Pad + y) / voxel), Mathf.RoundToInt(world.y / voxel));
                        skin[sample] = density;
                    }
                }
            }
            return skin;
        }

        private static bool InSlice(float x, float width, int slice, int slices)
        {
            return Mathf.Clamp((int)((x + width * 0.5f) / width * slices), 0, slices - 1) == slice;
        }

        /// <summary>The catalog row a building is built to, or the plain shop or house.</summary>
        public static BuildingDesign DesignOf(SettlementBuilding building)
        {
            return building.Design ?? (building.Kind == SettlementBuildingKind.Shop ? PlainShop : PlainHouse);
        }

        /// <summary>
        /// A building's voxels in the world, by voxel (its lowest corner, a global sample), with the material of each:
        /// stone for plinth and posts, <paramref name="boards"/> for floor, walls and roof, <paramref name="trim"/> for the rest.
        /// </summary>
        /// <param name="slice">Which slice across its width to give (0 to <paramref name="slices"/> - 1), for building it a part at a time.</param>
        public static Dictionary<Vector3Int, byte> Blocks(SettlementBuilding building, float voxel, byte stone, byte boards, byte trim, int slice = 0, int slices = 1)
        {
            BuildingDesign design = DesignOf(building);
            List<(Vector3 Centre, TownStructures.ShopPart Part)> cells = Voxels(design, building.Pitch, voxel);
            var blocks = new Dictionary<Vector3Int, byte>(cells.Count / Mathf.Max(1, slices) + 16);
            float width = design.Width + 4f * voxel;
            foreach ((Vector3 centre, TownStructures.ShopPart part) in cells)
            {
                if (slices > 1 && Mathf.Clamp((int)((centre.x + width * 0.5f) / width * slices), 0, slices - 1) != slice)
                {
                    continue;
                }
                Vector2 world = building.ToWorld(new Vector2(centre.x, centre.z));
                var at = new Vector3(world.x, building.Pad + centre.y, world.y);
                blocks[Vector3Int.FloorToInt(at / voxel)] = part == TownStructures.ShopPart.Stone ? stone : part == TownStructures.ShopPart.Trim ? trim : boards;
            }
            return blocks;
        }
    }
}
