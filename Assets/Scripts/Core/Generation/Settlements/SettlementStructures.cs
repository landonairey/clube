using System.Collections.Generic;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// The buildings of a planned settlement as voxels (GL613): a shop is the trading post's own
    /// (<see cref="TownStructures.ShopVoxels"/>), a house the same frame closed at the front with a door and a window.
    /// <see cref="Blocks"/> puts either on its terrace in the world.
    /// </summary>
    public static class SettlementStructures
    {
        /// <summary>The door's middle along the front and its width and height, in metres.</summary>
        public const float DoorX = 1.5f;
        public const float DoorWidth = 1f;
        public const float DoorHeight = 2f;

        /// <summary>
        /// A house's voxels in its own frame, by their centres (x, height over the pad, z): a board floor, a stone plinth
        /// and corner posts, board walls on four sides with a door and a window in the front, and the shop's stepped roof.
        /// </summary>
        public static List<(Vector3 Centre, TownStructures.ShopPart Part)> HouseVoxels(float voxel)
        {
            var cells = new List<(Vector3, TownStructures.ShopPart)>();
            float w = TownLayout.ShopWidth * 0.5f;
            float d = TownLayout.ShopDepth * 0.5f;
            float floor = TownLayout.FloorHeight;
            float top = floor + TownLayout.ShopHeight;
            const float Plinth = 0.75f;
            const float Post = 0.5f;
            const float RoofRise = 1.25f;
            float h = voxel * 0.5f;
            for (float z = -d - voxel + h; z < d + voxel; z += voxel)
            {
                for (float x = -w - voxel + h; x < w + voxel; x += voxel)
                {
                    bool inside = Mathf.Abs(x) < w && Mathf.Abs(z) < d;
                    bool wall = inside && (Mathf.Abs(x) > w - voxel || Mathf.Abs(z) > d - voxel);
                    bool frontWall = wall && z > d - voxel;
                    bool post = inside && Mathf.Abs(x) > w - Post && Mathf.Abs(z) > d - Post;
                    bool door = frontWall && Mathf.Abs(x - DoorX) < DoorWidth * 0.5f;
                    bool window = frontWall && Mathf.Abs(x + DoorX) < 0.5f;
                    float roofY = Mathf.Min(top + Mathf.Floor((d + voxel - Mathf.Abs(z)) / (2f * voxel)) * voxel, top + RoofRise);
                    for (float y = h; y < top + RoofRise + voxel; y += voxel)
                    {
                        TownStructures.ShopPart? part = null;
                        if (y < floor)
                        {
                            part = inside ? TownStructures.ShopPart.Boards : (TownStructures.ShopPart?)null;
                        }
                        else if (y < top)
                        {
                            if (post)
                            {
                                part = TownStructures.ShopPart.Stone;
                            }
                            else if (door && y < floor + DoorHeight)
                            {
                                part = null;
                            }
                            else if (window && y > floor + 1f && y < floor + 1.75f)
                            {
                                part = null;
                            }
                            else if (door || window)
                            {
                                // The frame round the openings, in the trim boards.
                                part = TownStructures.ShopPart.Trim;
                            }
                            else if (wall)
                            {
                                part = y < floor + Plinth ? TownStructures.ShopPart.Stone : TownStructures.ShopPart.Boards;
                            }
                        }
                        else if (y < roofY + voxel)
                        {
                            // Two layers deep, so neighbouring steps share a face; the gable ends fill up under the roof.
                            if (y > roofY - voxel || (Mathf.Abs(x) > w - voxel && Mathf.Abs(x) < w && Mathf.Abs(z) < d))
                            {
                                part = TownStructures.ShopPart.Boards;
                            }
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

        /// <summary>
        /// A building's voxels in the world, by voxel (its lowest corner, a global sample), with the material of each:
        /// stone for plinth and posts, <paramref name="boards"/> for floor, walls and roof, <paramref name="trim"/> for the rest.
        /// </summary>
        /// <param name="slice">Which slice across its width to give (0 to <paramref name="slices"/> - 1), for building it a part at a time.</param>
        public static Dictionary<Vector3Int, byte> Blocks(SettlementBuilding building, float voxel, byte stone, byte boards, byte trim, int slice = 0, int slices = 1)
        {
            List<(Vector3 Centre, TownStructures.ShopPart Part)> cells = building.Kind == SettlementBuildingKind.Shop
                ? TownStructures.ShopVoxels(voxel)
                : HouseVoxels(voxel);
            var blocks = new Dictionary<Vector3Int, byte>(cells.Count / Mathf.Max(1, slices) + 16);
            float width = TownLayout.ShopWidth + 4f * voxel;
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
