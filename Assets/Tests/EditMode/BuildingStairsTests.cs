using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Clube.Core.Tests
{
    /// <summary>Stairs inside multi-storey houses (GL873) and the cobblestone guard tower's wooden stairs (GL874).</summary>
    public class BuildingStairsTests
    {
        private const float Voxel = 0.25f;

        private static IEnumerable<BuildingDesign> Climbable()
        {
            return BuildingDesign.Defaults().Where(design => design.Stories > 1 || design.Battlements);
        }

        // The building's solid voxels, by their index in its frame (centre / voxel, floored).
        private static Dictionary<Vector3Int, TownStructures.ShopPart> Solid(BuildingDesign design)
        {
            var solid = new Dictionary<Vector3Int, TownStructures.ShopPart>();
            foreach ((Vector3 centre, TownStructures.ShopPart part) in SettlementStructures.Voxels(design, 0.7f, Voxel))
            {
                solid[Vector3Int.FloorToInt(centre / Voxel)] = part;
            }
            return solid;
        }

        private static Vector3Int Index(float x, float y, float z) => Vector3Int.FloorToInt(new Vector3(x, y, z) / Voxel);

        [Test]
        public void EveryMultiStoreyBuilding_HasStairs_ThatClimbAVoxelAtATimeToTheTop()
        {
            List<BuildingDesign> designs = Climbable().ToList();
            Assert.That(designs.Select(d => d.Id), Is.SupersetOf(new[] { "two-story-house", "townhouse", "tavern", "mill", "two-story-shop", "castle", "guard-tower" }));
            foreach (BuildingDesign design in designs)
            {
                BuildingStairs stairs = BuildingStairs.For(design, Voxel);
                Assert.IsNotNull(stairs, design.Id);
                IReadOnlyList<BuildingStairs.Tread> treads = stairs.Treads;
                Assert.AreEqual(TownLayout.FloorHeight, treads[0].Surface, 1e-4f, $"{design.Id} starts on the ground floor");
                Assert.AreEqual(BuildingStairs.TargetOf(design, Voxel), stairs.Top, 1e-4f, $"{design.Id} reaches the top");
                for (int i = 1; i < treads.Count; i++)
                {
                    float climb = treads[i].Surface - treads[i - 1].Surface;
                    Assert.That(climb, Is.InRange(0f, Voxel + 1e-4f), $"{design.Id} tread {i} climbs one voxel at most");
                    // Each tread touches the one before it, side to side.
                    Rect a = treads[i - 1].Area, b = treads[i].Area;
                    bool touch = Mathf.Max(a.xMin, b.xMin) <= Mathf.Min(a.xMax, b.xMax) + 1e-4f && Mathf.Max(a.yMin, b.yMin) <= Mathf.Min(a.yMax, b.yMax) + 1e-4f;
                    Assert.IsTrue(touch, $"{design.Id} tread {i} follows on from the last");
                    Assert.GreaterOrEqual(Mathf.Max(b.width, b.height), BuildingStairs.Width - 1e-4f, $"{design.Id} tread {i} is a stair wide");
                    // Inside the walls (and clear of any turret).
                    float inside = design.Width * 0.5f - Voxel - design.Turrets;
                    float deep = design.Depth * 0.5f - Voxel - design.Turrets;
                    Assert.IsTrue(b.xMin >= -inside - 1e-4f && b.xMax <= inside + 1e-4f && b.yMin >= -deep - 1e-4f && b.yMax <= deep + 1e-4f, $"{design.Id} tread {i} inside");
                }
                // Every storey's floor is reached on the way.
                for (int level = 1; level < design.Stories; level++)
                {
                    float floor = TownLayout.FloorHeight + level * design.StoryHeight + Voxel;
                    Assert.IsTrue(treads.Any(t => Mathf.Abs(t.Surface - floor) < 1e-4f), $"{design.Id} reaches storey {level + 1}");
                }
            }
            Assert.IsNull(BuildingStairs.For(BuildingDesign.Find(BuildingDesign.Defaults(), "cottage"), Voxel), "a one-storey house has none");
        }

        [Test]
        public void EveryTread_IsSolidUnderfoot_WithHeadroomOverIt_AndEveryFloorCanBeSteppedOnto()
        {
            foreach (BuildingDesign design in Climbable())
            {
                Dictionary<Vector3Int, TownStructures.ShopPart> solid = Solid(design);
                IReadOnlyList<BuildingStairs.Tread> treads = BuildingStairs.For(design, Voxel).Treads;
                foreach (BuildingStairs.Tread tread in treads)
                {
                    for (float x = tread.Area.xMin + Voxel * 0.5f; x < tread.Area.xMax; x += Voxel)
                    {
                        for (float z = tread.Area.yMin + Voxel * 0.5f; z < tread.Area.yMax; z += Voxel)
                        {
                            Assert.IsTrue(solid.TryGetValue(Index(x, tread.Surface - Voxel * 0.5f, z), out TownStructures.ShopPart part), $"{design.Id}: a tread at {x}, {tread.Surface}, {z}");
                            Assert.AreEqual(TownStructures.ShopPart.Boards, part, $"{design.Id}: wooden treads");
                            for (float y = tread.Surface + Voxel * 0.5f; y < tread.Surface + 2f; y += Voxel)
                            {
                                Assert.IsFalse(solid.ContainsKey(Index(x, y, z)), $"{design.Id}: headroom at {x}, {y}, {z} over a tread at {tread.Surface}");
                            }
                        }
                    }
                }
                // At each upper floor (and a battlemented roof) a tread is level with it, beside floor the player steps onto.
                int floors = design.Battlements ? design.Stories : design.Stories - 1;
                for (int level = 1; level <= floors; level++)
                {
                    float floor = TownLayout.FloorHeight + level * design.StoryHeight + Voxel;
                    BuildingStairs.Tread arrival = treads.First(t => Mathf.Abs(t.Surface - floor) < 1e-4f);
                    Vector2 middle = arrival.Area.center;
                    // One stair's width further in from the walls, towards the building's middle.
                    Vector2 inward = middle + new Vector2(-Mathf.Sign(middle.x), -Mathf.Sign(middle.y)) * BuildingStairs.Width;
                    bool stepOff = false;
                    foreach (Vector2 beside in new[] { new Vector2(inward.x, middle.y), new Vector2(middle.x, inward.y) })
                    {
                        bool floorThere = solid.ContainsKey(Index(beside.x, floor - Voxel * 0.5f, beside.y));
                        bool clearOver = !solid.ContainsKey(Index(beside.x, floor + 0.5f, beside.y)) && !solid.ContainsKey(Index(beside.x, floor + 1.5f, beside.y));
                        stepOff |= floorThere && clearOver;
                    }
                    Assert.IsTrue(stepOff, $"{design.Id}: storey {level + 1} can be stepped onto from the stairs");
                }
            }
        }

        [Test]
        public void TheFrontDoor_IsNotBlockedByTheStairs()
        {
            foreach (BuildingDesign design in Climbable().Where(d => !d.ShopFront))
            {
                Dictionary<Vector3Int, TownStructures.ShopPart> solid = Solid(design);
                float front = design.Depth * 0.5f;
                // From the doorway a metre in, a door's width across (short of a narrow building's corner post) and its
                // height up, is clear.
                float right = Mathf.Min(SettlementStructures.DoorX + 0.5f, design.Width * 0.5f - 0.5f);
                for (float z = front - 0.125f; z > front - 1f; z -= Voxel)
                {
                    for (float x = SettlementStructures.DoorX - 0.375f; x < right; x += Voxel)
                    {
                        for (float y = TownLayout.FloorHeight + 0.125f; y < SettlementStructures.DoorHeight; y += Voxel)
                        {
                            Assert.IsFalse(solid.ContainsKey(Index(x, y, z)), $"{design.Id}: the way in at {x}, {y}, {z}");
                        }
                    }
                }
            }
        }

        [Test]
        public void TheGuardTower_IsCobblestone_WithWoodenStairsUpThroughItsRoof()
        {
            BuildingDesign tower = BuildingDesign.Find(BuildingDesign.Defaults(), "guard-tower");
            Assert.IsTrue(tower.Cobblestone);
            Assert.IsFalse(BuildingDesign.Find(BuildingDesign.Defaults(), "castle").Cobblestone, "the castle keeps its dressed stone");
            const byte stone = 3, boards = 4, trim = 5, cobble = 9;
            var building = new SettlementBuilding { Design = tower, Pitch = 0.5f, Centre = Vector2.zero, Pad = 0f };
            Dictionary<Vector3Int, byte> blocks = SettlementStructures.Blocks(building, Voxel, stone, boards, trim, cobble: cobble);
            Assert.IsFalse(blocks.ContainsValue(stone), "no dressed stone left");
            Assert.IsTrue(blocks.ContainsValue(cobble), "cobblestone walls");
            // Without a cobble material it is built of stone as before.
            Assert.IsTrue(SettlementStructures.Blocks(building, Voxel, stone, boards, trim).ContainsValue(stone));

            BuildingStairs stairs = BuildingStairs.For(tower, Voxel);
            float roof = TownLayout.FloorHeight + tower.Stories * tower.StoryHeight;
            Assert.AreEqual(roof + Voxel, stairs.Top, 1e-4f, "the stairs come out on the roof");
            BuildingStairs.Tread last = stairs.Treads[stairs.Treads.Count - 1];
            Vector2 at = last.Area.center;
            Vector3Int tread = Vector3Int.FloorToInt(new Vector3(building.ToWorld(at).x, roof + Voxel * 0.5f, building.ToWorld(at).y) / Voxel);
            Assert.AreEqual(boards, blocks[tread], "the last tread is wood, level with the roof");
            // The roof is open over the flight just before it.
            BuildingStairs.Tread below = stairs.Treads.Last(t => t.Surface < roof - 1f);
            Vector2 under = below.Area.center;
            Vector3Int opening = Vector3Int.FloorToInt(new Vector3(building.ToWorld(under).x, roof + Voxel * 0.5f, building.ToWorld(under).y) / Voxel);
            Assert.IsFalse(blocks.ContainsKey(opening), "an opening in the roof over the stairs");
        }
    }
}
