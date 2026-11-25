using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Clube.Core.Tests
{
    /// <summary>Claiming a stump or boulder (prototype, backlog 2026-11-24 item 8): the marks written into the terrain.</summary>
    public class WorksiteClaimTests
    {
        private const float Iso = 0.5f;
        private const byte Ground = 1;
        private const byte Wood = 15;
        private const byte Rock = 2;
        private const byte Twine = 42;
        private const byte Crack = 43;
        private const byte Worked = 44;

        [Test]
        public void ClaimStump_KeepsTheWoodAndWrapsItsMiddle()
        {
            World world = EmptyWorld();
            var piece = new List<Vector3Int>();
            for (int z = 3; z <= 4; z++)
            {
                for (int x = 3; x <= 4; x++)
                {
                    // One column stands higher: it is left as it is (backlog 2026-11-24b item 13).
                    int top = x == 4 && z == 4 ? 8 : 7;
                    for (int y = 1; y <= top; y++)
                    {
                        var sample = new Vector3Int(x, y, z);
                        world.SetDensity(sample, 1f);
                        world.SetMaterial(sample, Wood);
                        piece.Add(sample);
                    }
                }
            }
            Assert.IsFalse(WorksiteClaim.IsClaimed(world, piece, Twine, Twine));

            WorksiteClaim.ClaimStump(world, piece, Iso, Twine);

            Assert.GreaterOrEqual(world.GetDensity(new Vector3Int(4, 8, 4)).Value, Iso, "The wood is not cut.");
            Assert.AreEqual(Wood, world.GetMaterial(new Vector3Int(4, 8, 4)), "The top keeps its wood.");
            var bands = new HashSet<int>();
            foreach (Vector3Int sample in piece)
            {
                if (world.GetMaterial(sample) == Twine)
                {
                    bands.Add(sample.y);
                }
            }
            Assert.AreEqual(1, bands.Count, "One row of twine (bark with loops of twine in its texture).");
            Assert.IsTrue(bands.Contains(4), "Halfway up the stump.");
            Assert.IsTrue(WorksiteClaim.IsClaimed(world, piece, Twine, Twine));
        }

        [Test]
        public void ClaimBoulder_WorksTheTopAndCracksTheSides()
        {
            World world = EmptyWorld();
            for (int z = 1; z <= 6; z++)
            {
                for (int y = 1; y <= 6; y++)
                {
                    for (int x = 1; x <= 6; x++)
                    {
                        var sample = new Vector3Int(x, y, z);
                        world.SetDensity(sample, 1f);
                        world.SetMaterial(sample, Rock);
                    }
                }
            }
            var piece = new List<Vector3Int>();
            WorksiteClaim.FindBoulder(world, new Vector3Int(3, 3, 3), Iso, id => id == Rock || id == Crack || id == Worked, piece);
            Assert.AreEqual(216, piece.Count);
            Assert.IsFalse(WorksiteClaim.IsClaimed(world, piece, Crack, Worked));

            WorksiteClaim.ClaimBoulder(world, piece, Iso, Crack, Worked, Vector2.right);

            Assert.AreEqual(Worked, world.GetMaterial(new Vector3Int(3, 6, 3)), "The top is worked stone.");
            Assert.AreEqual(Rock, world.GetMaterial(new Vector3Int(1, 6, 1)), "Only the middle of the top is worked (backlog 2026-11-24b item 6).");
            Assert.AreEqual(Rock, world.GetMaterial(new Vector3Int(3, 3, 3)), "The inside is left alone.");
            int cracks = 0;
            foreach (Vector3Int sample in piece)
            {
                if (world.GetMaterial(sample) == Crack)
                {
                    cracks++;
                    bool middleOfTop = sample.y == 6 && Mathf.Abs(sample.x - 3.5f) <= 1f && Mathf.Abs(sample.z - 3.5f) <= 1f;
                    Assert.IsFalse(middleOfTop, "Cracks run down the sides and over the edge, not across the worked middle.");
                }
            }
            Assert.Greater(cracks, 0, "Cracks down the sides.");
            Assert.Less(cracks, 24, "A few thin cracks, not lines all round.");
            Assert.IsTrue(WorksiteClaim.IsClaimed(world, piece, Crack, Worked));
        }

        // One 8-voxel chunk of air over a floor of ground at y = 0.
        private static World EmptyWorld()
        {
            var world = new World(new Vector3Int(8, 8, 8), 1f);
            world.Load(Vector3Int.zero, null);
            for (int z = 0; z <= 8; z++)
            {
                for (int y = 0; y <= 8; y++)
                {
                    for (int x = 0; x <= 8; x++)
                    {
                        var sample = new Vector3Int(x, y, z);
                        world.SetDensity(sample, y == 0 ? 1f : 0f);
                        world.SetMaterial(sample, Ground);
                    }
                }
            }
            return world;
        }
    }
}
