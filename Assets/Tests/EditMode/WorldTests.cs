using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Clube.Core.Tests
{
    public class WorldTests
    {
        private const int Size = 8;
        private const float VoxelSize = 1f;

        private static readonly ChunkMeshSettings Settings = new ChunkMeshSettings(0.5f, VoxelSize);

        // Rolling terrain whose surface sits inside chunk layer y = 0.
        private static ITerrainGenerator Terrain()
        {
            return TerrainGenerators.Create(new TerrainSettings());
        }

        private static World NewWorld()
        {
            return new World(new Vector3Int(Size, Size, Size), VoxelSize);
        }

        [Test]
        public void GeneratedNeighbours_AgreeOnTheirSharedBorder()
        {
            World world = NewWorld();
            Chunk a = world.Load(Vector3Int.zero, Terrain());
            Chunk b = world.Load(Vector3Int.right, Terrain());

            AssertSharedFaceEqual(a, b);
        }

        [Test]
        public void GeneratedNeighbours_MeshWithoutCracks()
        {
            World world = NewWorld();
            Chunk a = world.Load(Vector3Int.zero, Terrain());
            Chunk b = world.Load(Vector3Int.right, Terrain());

            AssertBorderVerticesMatch(world, a, b);
        }

        [Test]
        public void SetDensity_WritesEveryCopyOfACornerSample()
        {
            World world = NewWorld();
            for (int z = 0; z < 2; z++)
            {
                for (int y = 0; y < 2; y++)
                {
                    for (int x = 0; x < 2; x++)
                    {
                        world.Load(new Vector3Int(x, y, z), null);
                    }
                }
            }

            var corner = new Vector3Int(Size, Size, Size);
            int changed = world.SetDensity(corner, 0.75f);

            Assert.That(changed, Is.EqualTo(8));
            foreach (KeyValuePair<Vector3Int, Chunk> entry in world.Chunks)
            {
                Assert.That(entry.Value.GetDensity(world.Grid.GlobalToLocal(corner, entry.Key)), Is.EqualTo(0.75f), entry.Key.ToString());
                Assert.That(world.IsEdited(entry.Key), Is.True);
            }
        }

        [Test]
        public void BrushAcrossABorder_EditsBothChunksAndKeepsTheBorderShared()
        {
            World world = NewWorld();
            Chunk a = world.Load(Vector3Int.zero, Terrain());
            Chunk b = world.Load(Vector3Int.right, Terrain());

            // Dig a hole centred on the shared face, at the surface height.
            var brush = new BrushSettings(2.5f, 1f, BrushFalloff.Hard);
            world.ApplyBrush(new Vector3(Size, 4f, 4f), brush, BrushOperation.Remove);

            Assert.That(world.IsEdited(Vector3Int.zero) && world.IsEdited(Vector3Int.right), Is.True);
            AssertSharedFaceEqual(a, b);
            AssertBorderVerticesMatch(world, a, b);
        }

        [Test]
        public void BrushOnABorder_CountsEachSharedSampleOnce()
        {
            World world = NewWorld();
            world.Load(Vector3Int.zero, null);
            world.Load(Vector3Int.right, null);

            // Radius 1 around a sample on the shared face: it and its six neighbours.
            BrushResult result = world.ApplyBrush(new Vector3(Size, 4f, 4f), new BrushSettings(1f), BrushOperation.Add);

            Assert.That(result.ChangedSamples, Is.EqualTo(7));
            Assert.That(result.DensityAdded, Is.EqualTo(7f).Within(1e-4f));
            Assert.That(world.GetDensity(new Vector3Int(Size, 4, 4)), Is.EqualTo(1f));
        }

        [Test]
        public void SoftBrush_SeesAcrossTheBorder()
        {
            World world = NewWorld();
            Chunk left = world.Load(Vector3Int.zero, null);
            Chunk right = world.Load(Vector3Int.right, null);

            // Solid only in the right chunk, just past the shared face.
            for (int y = 0; y <= Size; y++)
            {
                for (int z = 0; z <= Size; z++)
                {
                    world.SetDensity(new Vector3Int(Size + 1, y, z), 1f);
                }
            }

            // The shared face touches that solid from the left chunk's side, so it grows.
            world.ApplyBrush(new Vector3(Size, 4f, 4f), new BrushSettings(1.5f, falloff: BrushFalloff.Soft), BrushOperation.Add);

            Assert.That(left.GetDensity(new Vector3Int(Size, 4, 4)), Is.EqualTo(1f));
            Assert.That(right.GetDensity(new Vector3Int(0, 4, 4)), Is.EqualTo(1f));
            Assert.That(left.GetDensity(new Vector3Int(Size - 1, 4, 4)), Is.EqualTo(0f), "One layer per application");
        }

        [Test]
        public void LoadingNextToAnEditedChunk_TakesItsBorder()
        {
            World world = NewWorld();
            Chunk a = world.Load(Vector3Int.zero, Terrain());

            // Edit right up to the border while the neighbour isn't loaded yet.
            for (int y = 0; y <= Size; y++)
            {
                for (int z = 0; z <= Size; z++)
                {
                    world.SetDensity(new Vector3Int(Size, y, z), 1f);
                }
            }

            Chunk b = world.Load(Vector3Int.right, Terrain());
            AssertSharedFaceEqual(a, b);
            Assert.That(b.GetDensity(new Vector3Int(0, Size, 0)), Is.EqualTo(1f));
        }

        [Test]
        public void EditedChunk_SurvivesUnloadAndReload()
        {
            World world = NewWorld();
            world.Load(Vector3Int.zero, Terrain());
            world.SetDensity(new Vector3Int(3, 7, 3), 1f);

            world.Unload(Vector3Int.zero);
            Assert.That(world.IsLoaded(Vector3Int.zero), Is.False);

            Chunk reloaded = world.Load(Vector3Int.zero, Terrain());
            Assert.That(reloaded.GetDensity(new Vector3Int(3, 7, 3)), Is.EqualTo(1f));
        }

        [Test]
        public void UneditedChunk_IsRegeneratedAfterUnload()
        {
            World world = NewWorld();
            Chunk first = world.Load(Vector3Int.zero, Terrain());
            world.Unload(Vector3Int.zero);
            Chunk second = world.Load(Vector3Int.zero, Terrain());

            Assert.That(second, Is.Not.SameAs(first));
            Assert.That(second.GetDensity(new Vector3Int(4, 4, 4)), Is.EqualTo(first.GetDensity(new Vector3Int(4, 4, 4))));
        }

        [Test]
        public void Raycast_FindsTheSurfaceInTheChunkItCrossesInto()
        {
            World world = NewWorld();
            world.Load(Vector3Int.zero, Terrain());
            world.Load(Vector3Int.right, Terrain());

            // Straight down onto the second chunk.
            var ray = new Ray(new Vector3(Size + 3.5f, Size - 0.01f, 3.5f), Vector3.down);
            Assert.That(world.Raycast(ray, Settings, 100f, out WorldHit hit), Is.True);
            Assert.That(hit.Chunk, Is.EqualTo(Vector3Int.right));
            Assert.That(hit.Point.x, Is.EqualTo(Size + 3.5f).Within(1e-3f));

            // From the first chunk's air, slanting across the border: the surface is at most
            // y = 6, which this ray only reaches at x = 9.75, inside the second chunk.
            var slanted = new Ray(new Vector3(6f, Size - 0.5f, 3.5f), new Vector3(1f, -0.4f, 0f));
            Assert.That(world.Raycast(slanted, Settings, 100f, out WorldHit slantedHit), Is.True);
            Assert.That(slantedHit.Chunk, Is.EqualTo(Vector3Int.right));
        }

        [Test]
        public void Raycast_MissesWhenNothingIsLoaded()
        {
            World world = NewWorld();
            var ray = new Ray(new Vector3(3f, 20f, 3f), Vector3.down);
            Assert.That(world.Raycast(ray, Settings, 100f, out _), Is.False);
        }

        private static void AssertSharedFaceEqual(Chunk left, Chunk right)
        {
            for (int z = 0; z <= Size; z++)
            {
                for (int y = 0; y <= Size; y++)
                {
                    Assert.That(
                        right.GetDensity(new Vector3Int(0, y, z)),
                        Is.EqualTo(left.GetDensity(new Vector3Int(Size, y, z))),
                        $"Shared sample y={y} z={z}");
                }
            }
        }

        // Every vertex the left chunk puts on the shared face, the right chunk puts in the
        // same place, and the other way round: no cracks (M2).
        private static void AssertBorderVerticesMatch(World world, Chunk left, Chunk right)
        {
            HashSet<Vector3Int> leftBorder = BorderVertices(left, world.Grid.ChunkOrigin(Vector3Int.zero), Size);
            HashSet<Vector3Int> rightBorder = BorderVertices(right, world.Grid.ChunkOrigin(Vector3Int.right), 0);

            Assert.That(leftBorder.Count, Is.GreaterThan(0), "The surface should cross the border.");
            Assert.That(rightBorder, Is.EquivalentTo(leftBorder));
        }

        // Vertices lying on the plane x = localX of the chunk, in world units rounded to 1/1000.
        private static HashSet<Vector3Int> BorderVertices(Chunk chunk, Vector3 origin, int localX)
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            ChunkMesher.Build(chunk, Settings, vertices, triangles);

            return new HashSet<Vector3Int>(vertices
                .Where(vertex => Mathf.Abs(vertex.x - localX * VoxelSize) < 1e-4f)
                .Select(vertex => Vector3Int.RoundToInt((vertex + origin) * 1000f)));
        }
    }
}
