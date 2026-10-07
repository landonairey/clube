using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Clube.Core.Tests
{
    public class ToolStrikeTests
    {
        private const int Size = 4;
        private const float Iso = 0.5f;

        private ItemDefinition stoneItem;
        private VoxelMaterial stone;
        private VoxelMaterial cracked;
        private VoxelMaterial loose;
        private MaterialRegistry registry;
        private World world;

        [SetUp]
        public void SetUp()
        {
            stoneItem = ItemDefinition.Create("stone", "Stone");
            loose = VoxelMaterial.Create(7, "Loose stone").WithBreaking(0.25f, stoneItem);
            cracked = VoxelMaterial.Create(6, "Cracked stone").WithBreaking(1.5f, stoneItem, loose);
            stone = VoxelMaterial.Create(0, "Stone").WithBreaking(3f, stoneItem, cracked);
            registry = MaterialRegistry.Create(stone, cracked, loose);

            // Two chunks side by side along x; ground up to y = 1, air above.
            world = new World(new Vector3Int(Size, Size, Size), 1f);
            world.Load(Vector3Int.zero, null);
            world.Load(Vector3Int.right, null);
            for (int z = 0; z <= Size; z++)
            {
                for (int y = 0; y <= Size; y++)
                {
                    for (int x = 0; x <= Size * 2; x++)
                    {
                        var sample = new Vector3Int(x, y, z);
                        world.SetDensity(sample, y <= 1 ? 1f : 0f);
                        world.SetMaterial(sample, stone.Id);
                    }
                }
            }
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(registry);
            Object.DestroyImmediate(stone);
            Object.DestroyImmediate(cracked);
            Object.DestroyImmediate(loose);
            Object.DestroyImmediate(stoneItem);
        }

        private static ToolDefinition Pickaxe()
        {
            return ToolDefinition.Create("pickaxe", "Pickaxe", 3, 1.5f);
        }

        private List<Vector3Int> Targets(VoxelBox box)
        {
            var targets = new List<Vector3Int>();
            ToolStrike.FindTargets(world, box, Iso, targets);
            return targets;
        }

        [Test]
        public void AimedVoxel_IsTheHitVoxelInGlobalIndices()
        {
            var hit = new WorldHit(Vector3Int.right, new Vector3Int(2, 1, 2), new Vector3(6.6f, 1.5f, 2.7f));

            Assert.AreEqual(new Vector3Int(Size + 2, 1, 2), ToolStrike.AimedVoxel(world, hit));
        }

        [Test]
        public void FindTargets_ReachesEverySolidCornerOfTheVoxels()
        {
            // The hand's single voxel on the floor: its four lower corners are solid.
            Assert.AreEqual(4, Targets(VoxelBox.Around(new Vector3Int(2, 1, 2), 1)).Count);

            // The pickaxe's 3x3x3 voxels: 4x4 corners across, solid in the two lowest
            // layers (the floor and the buried one under it), air above.
            List<Vector3Int> targets = Targets(VoxelBox.Around(new Vector3Int(2, 1, 2), 3));
            Assert.AreEqual(32, targets.Count);
            CollectionAssert.Contains(targets, new Vector3Int(1, 0, 1));
            CollectionAssert.DoesNotContain(targets, new Vector3Int(2, 2, 2));
        }

        [Test]
        public void Hit_BreaksStoneThroughCrackedAndLooseBeforeRemovingIt()
        {
            var aimed = new Vector3Int(2, 1, 2);
            var targets = new List<Vector3Int> { aimed };
            var damage = new StrikeDamage();
            var collected = new List<ItemDefinition>();
            ToolDefinition pickaxe = Pickaxe();

            ToolStrike.Hit(world, targets, pickaxe, registry, damage, collected);
            Assert.AreEqual(stone.Id, world.GetMaterial(aimed), "Stone takes two hits of 1.5.");

            ToolStrike.Hit(world, targets, pickaxe, registry, damage, collected);
            Assert.AreEqual(cracked.Id, world.GetMaterial(aimed));

            ToolStrike.Hit(world, targets, pickaxe, registry, damage, collected);
            Assert.AreEqual(loose.Id, world.GetMaterial(aimed));
            Assert.AreEqual(1f, world.GetDensity(aimed), "Breaking into a new stage keeps the ground.");
            CollectionAssert.IsEmpty(collected);

            StrikeResult result = ToolStrike.Hit(world, targets, pickaxe, registry, damage, collected);
            Assert.AreEqual(1, result.Removed);
            Assert.AreEqual(0f, world.GetDensity(aimed));
            CollectionAssert.AreEqual(new[] { stoneItem }, collected);
            Assert.AreEqual(0, damage.Count, "Broken samples are forgotten.");
            Object.DestroyImmediate(pickaxe);
        }

        [Test]
        public void Hit_WeakToolTakesHardnessOverPowerHits()
        {
            var aimed = new Vector3Int(2, 1, 2);
            var targets = new List<Vector3Int> { aimed };
            var damage = new StrikeDamage();
            ToolDefinition hand = ToolDefinition.Create("hand", "Hand", 1, 0.5f);

            for (int i = 0; i < 5; i++)
            {
                ToolStrike.Hit(world, targets, hand, registry, damage, null);
            }
            Assert.AreEqual(stone.Id, world.GetMaterial(aimed));

            ToolStrike.Hit(world, targets, hand, registry, damage, null);
            Assert.AreEqual(cracked.Id, world.GetMaterial(aimed));
            Object.DestroyImmediate(hand);
        }

        [Test]
        public void Hit_OnABorderSample_ChangesEveryCopy()
        {
            // x = 4 is shared by chunk 0 (its last sample) and chunk 1 (its first).
            var aimed = new Vector3Int(Size, 1, 2);
            var targets = new List<Vector3Int> { aimed };
            var damage = new StrikeDamage();
            ToolDefinition pickaxe = Pickaxe();

            for (int i = 0; i < 4; i++)
            {
                ToolStrike.Hit(world, targets, pickaxe, registry, damage, null);
            }

            world.TryGetChunk(Vector3Int.zero, out Chunk left);
            world.TryGetChunk(Vector3Int.right, out Chunk right);
            Assert.AreEqual(0f, left.GetDensity(new Vector3Int(Size, 1, 2)));
            Assert.AreEqual(0f, right.GetDensity(new Vector3Int(0, 1, 2)));
            Object.DestroyImmediate(pickaxe);
        }

        [Test]
        public void SurfaceOutline_GivesTheTriangleEdgesInsideAVoxel()
        {
            var settings = new ChunkMeshSettings(Iso, 1f);
            var lines = new List<Vector3>();

            // A floor voxel: solid below, air above, so a flat square of two triangles at
            // y = 1.5, three edges each.
            SurfaceOutline.AddEdges(world, new Vector3Int(2, 1, 2), settings, lines);
            Assert.AreEqual(12, lines.Count);
            foreach (Vector3 point in lines)
            {
                Assert.AreEqual(1.5f, point.y, 1e-5f);
            }

            // A buried voxel has no surface.
            lines.Clear();
            SurfaceOutline.AddEdges(world, new Vector3Int(2, 0, 2), settings, lines);
            CollectionAssert.IsEmpty(lines);
        }
    }
}
