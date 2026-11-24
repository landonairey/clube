using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Clube.Core.Tests
{
    /// <summary>
    /// GL849: the tutorial's quarry pit on seed 1 had thin stone walls of uncut ground across its floor, one right in front of
    /// the spawn, and a tree drawn in stone.
    /// </summary>
    public class QuarryPitTests
    {
        private const float Voxel = 0.25f;
        private const float Iso = 0.5f;

        private readonly List<Object> made = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (Object thing in made)
            {
                Object.DestroyImmediate(thing);
            }
            made.Clear();
        }

        private static World NewWorld()
        {
            return new World(new Vector3Int(16, 16, 16), Voxel, samples => new ByteVoxelStorage(samples));
        }

        // Loads every chunk a box of ground needs, from a height to another.
        private static void LoadAround(World world, ITerrainGenerator generator, Rect box, float low, float high)
        {
            WorldGrid grid = world.Grid;
            Vector3Int min = grid.WorldToChunk(new Vector3(box.xMin - 1f, low, box.yMin - 1f));
            Vector3Int max = grid.WorldToChunk(new Vector3(box.xMax + 1f, high, box.yMax + 1f));
            for (int x = min.x; x <= max.x; x++)
            {
                for (int y = min.y; y <= max.y; y++)
                {
                    for (int z = min.z; z <= max.z; z++)
                    {
                        world.Load(new Vector3Int(x, y, z), generator);
                    }
                }
            }
        }

        private static bool Solid(World world, float x, float y, float z)
        {
            return (world.GetDensity(world.Grid.WorldToNearestSample(new Vector3(x, y, z))) ?? 0f) >= Iso;
        }

        [Test]
        public void TilesSharingAnEdgeOnARowOfSamples_BothLevelIt()
        {
            // Seed 1's pit (Crowworth Village): floor (152, 800.25) 40 x 52 m, sunk to 41.5 m; with its 3 m ramp the writer
            // cuts it into 20 rows of 2.9 m tiles, and the edges at 811.75, 826.25 and 840.75 fall on rows of samples.
            // Rounding put each of those rows a hair outside both tiles, and the land stood there at its own height.
            const float floor = 41.5f, land = 47.5f, ramp = 3f;
            var area = new Rect(152f, 800.25f, 40f, 52f);
            var outer = Rect.MinMaxRect(area.xMin - ramp, area.yMin - ramp, area.xMax + ramp, area.yMax + ramp);
            int deep = Mathf.CeilToInt(outer.height / SettlementWriter.TileSize);
            var strip = Rect.MinMaxRect(170f, outer.yMin, 174f, outer.yMax);
            World world = NewWorld();
            LoadAround(world, new FlatGenerator(land), strip, floor - 2f, land + 6f);

            for (int j = 0; j < deep; j++)
            {
                // The writer's own split of its rows (SettlementWriter's constructor).
                var box = new Rect(outer.xMin, outer.yMin + j * outer.height / deep, outer.width, outer.height / deep);
                GroundLeveling.Level(world, null, new GroundLeveling.Area
                {
                    Centre = box.center, Half = box.size * 0.5f, Natural = (x, z) => land, Headroom = 3f,
                    Surface = (x, z, ground) => floor,
                    Inside = (x, z) => x >= strip.xMin && x <= strip.xMax && SettlementSite.Beyond(area, new Vector2(x, z)) < ramp,
                }, Iso);
            }

            for (float z = area.yMin; z <= area.yMax; z += Voxel)
            {
                Assert.IsFalse(Solid(world, 172f, floor + 1f, z), $"no ground standing on the floor at z {z}");
                Assert.IsTrue(Solid(world, 172f, floor - 0.5f, z), $"the floor itself at z {z}");
            }
        }

        [Test]
        public void TheQuarryPit_IsOpenOverItsWholeFloor_AndLeavesATreeOverItWood()
        {
            VoxelMaterial grass = VoxelMaterial.Create(1, "Grass"), stone = VoxelMaterial.Create(2, "Stone"), oak = VoxelMaterial.Create(3, "Oak boards"),
                maple = VoxelMaterial.Create(4, "Maple boards"), wood = VoxelMaterial.Create(5, "Wood", VoxelMaterialCategory.Plant);
            made.AddRange(new Object[] { grass, stone, oak, maple, wood });
            MaterialRegistry registry = MaterialRegistry.Create(grass, stone, oak, maple, wood);
            made.Add(registry);
            const float land = 40f;
            var generator = new FlatGenerator(land);
            var rules = new SettlementSiteRules();
            var village = new PlannedSettlement
            {
                Index = 3, Type = SettlementType.Defaults().Find(t => t.Id == "village"), Position = new Vector2(1000f, 1000f), Name = "Test quarry",
                Population = 6, IsQuarry = true,
            };
            village.Shops.Add(new PlannedShop { Specialty = EconomySpecialty.Defaults()[0], Reason = "test" });
            SettlementSite site = SettlementLayout.Plan(village, 32f, rules, (x, z) => land, voxel: Voxel);
            QuarryYard pit = site.Quarry;
            Assert.IsNotNull(pit);
            var street = new RoadClassDefinition { Class = RoadClass.Footpath, Width = 2.5f, Surface = "Grass", GradeLength = 8f, MaxCut = 8f, Shoulder = 0.75f };
            var writer = new SettlementWriter(site, street, street);
            var materials = new SettlementWriter.Materials { Bank = stone, Stone = stone, Boards = oak, Trim = maple };
            World world = NewWorld();
            Rect bank = Rect.MinMaxRect(pit.Area.xMin - rules.Ramp, pit.Area.yMin - rules.Ramp, pit.Area.xMax + rules.Ramp, pit.Area.yMax + rules.Ramp);
            LoadAround(world, generator, bank, pit.Floor - QuarryYard.StoneDepth - 1f, land + 8f);
            // A tree on the bank leans its crown over the pit's corner, in the air above the land.
            Vector3Int crown = world.Grid.WorldToNearestSample(new Vector3(pit.Area.xMin + 1f, land + 4.5f, pit.Area.yMin + 1f));
            world.SetDensity(crown, 1f);
            world.SetMaterial(crown, wood.Id);

            for (int i = 0; i < writer.Pieces; i++)
            {
                Rect box = writer.Footprint(i);
                if (writer.KindOf(i) == 0 && box.Overlaps(pit.Area))
                {
                    writer.Write(world, registry, materials, (x, z) => land, Iso, i);
                }
            }

            // The spawn (the yard's south-west corner) looks north up the floor: open ground all the way, and none anywhere else.
            for (float z = pit.Area.yMin + 0.5f; z <= pit.Area.yMax - 0.5f; z += Voxel)
            {
                for (float x = pit.Area.xMin + 0.5f; x <= pit.Area.xMax - 0.5f; x += 1f)
                {
                    Assert.IsFalse(Solid(world, x, pit.Floor + 1f, z), $"no wall on the floor at ({x}, {z})");
                }
                Vector3Int under = world.Grid.WorldToNearestSample(new Vector3(pit.Area.center.x, pit.Floor - 0.5f, z));
                Assert.AreEqual(stone.Id, world.GetMaterial(under).Value, "the floor is stone");
            }
            Assert.AreEqual(wood.Id, world.GetMaterial(crown).Value, "the crown is still wood, not stone");
        }
    }
}
