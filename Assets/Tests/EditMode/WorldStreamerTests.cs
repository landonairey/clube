using NUnit.Framework;
using UnityEngine;

namespace Clube.Core.Tests
{
    /// <summary>
    /// The streamed world end to end (<see cref="WorldStreamer"/>, P1, P3): it settles around the
    /// focus with renderers only for chunks that have a surface, rebuilds an edited chunk in the
    /// frame of the edit, gives colliders to chunks near the focus, and unloads what the focus
    /// leaves behind.
    /// </summary>
    public class WorldStreamerTests
    {
        private const int Size = 8;
        private const int Layers = 3;

        private GameObject root;
        private World world;
        private ChunkRendererPool pool;
        private WorldStreamer streamer;

        [SetUp]
        public void MakeStreamer()
        {
            root = new GameObject("World Streamer Test");
            world = new World(new Vector3Int(Size, Size, Size), 1f, samples => new ByteVoxelStorage(samples));
            ITerrainGenerator generator = new FractalPerlin2DGenerator(3, 9f, 3f, 0.08f, octaves: 3);
            var pipeline = new ChunkPipeline(world.Grid, generator, null, null, world.PreferredFormat, world.StorageFactory);
            pool = new ChunkRendererPool(root.transform, true, new Material[0]);
            streamer = new WorldStreamer(world, pipeline, pool, () => new ChunkMeshSettings(0.5f, 1f), new StreamingSettings
            {
                RenderDistance = 2,
                HeightInChunks = Layers,
                FrameBudgetMilliseconds = 3f,
                Colliders = true,
                ColliderRadius = 10f,
                MaxEditsPerFrame = 32,
            });
        }

        [TearDown]
        public void DestroyStreamer()
        {
            streamer.UnloadAll();
            streamer.Dispose();
            Object.DestroyImmediate(root);
        }

        [Test]
        public void Settles_WithRenderersOnlyForSurfaceChunks()
        {
            Settle(Vector3.zero);

            StreamingStats stats = streamer.Stats;
            Assert.That(stats.Pending, Is.Zero);
            Assert.That(world.LoadedCount, Is.EqualTo(stats.Wanted));
            Assert.That(pool.Active.Count, Is.GreaterThan(0));
            Assert.That(pool.Active.Count, Is.LessThan(world.LoadedCount), "all-air and all-solid chunks get no renderer");
            foreach (var entry in pool.Active)
            {
                Assert.That(world.Chunks[entry.Key].IsUniform, Is.False, entry.Key.ToString());
                Assert.That(entry.Value.Mesh.vertexCount, Is.GreaterThan(0), entry.Key.ToString());
                Assert.That(world.Chunks[entry.Key].IsDirty, Is.False);
            }
            Assert.That(streamer.IsColumnSettled(Vector2Int.zero), Is.True);
        }

        [Test]
        public void ChunksNearTheFocus_GetColliders_FarOnesDont()
        {
            // Far enough out that some chunks lie well past the 10 m collider radius.
            streamer.RenderDistance = 4;
            Settle(Vector3.zero);

            int withCollider = 0;
            int without = 0;
            foreach (var entry in pool.Active)
            {
                // From the focus to the chunk's nearest point, horizontally, as the streamer measures it.
                Vector3 min = world.Grid.ChunkOrigin(entry.Key);
                float dx = Mathf.Max(min.x, 0f, -(min.x + Size));
                float dz = Mathf.Max(min.z, 0f, -(min.z + Size));
                float distance = Mathf.Sqrt(dx * dx + dz * dz);
                bool has = entry.Value.Collider.Version == entry.Value.MeshVersion;
                if (distance <= 10f)
                {
                    Assert.That(has, Is.True, $"{entry.Key} is within the collider radius");
                    withCollider++;
                }
                else if (distance > 10f + Size)
                {
                    Assert.That(has, Is.False, $"{entry.Key} is well outside the collider radius");
                    without++;
                }
            }
            Assert.That(withCollider, Is.GreaterThan(0));
            Assert.That(without, Is.GreaterThan(0));
        }

        [Test]
        public void Edit_IsRebuiltInTheSameFrame()
        {
            Settle(Vector3.zero);
            Assert.That(world.Raycast(new Ray(new Vector3(4f, 30f, 4f), Vector3.down), new ChunkMeshSettings(0.5f, 1f), 100f, out WorldHit hit), Is.True);
            Assert.That(pool.TryGet(hit.Chunk, out ChunkRenderer chunkRenderer), Is.True);
            int version = chunkRenderer.MeshVersion;
            int vertices = chunkRenderer.Mesh.vertexCount;

            world.ApplyBrush(hit.Point, new BrushSettings(2.5f), BrushOperation.Remove);
            streamer.RebuildEdited(Vector3.zero);

            Assert.That(chunkRenderer.MeshVersion, Is.GreaterThan(version), "new mesh this frame");
            Assert.That(chunkRenderer.Mesh.vertexCount, Is.Not.EqualTo(vertices));
            Assert.That(world.Chunks[hit.Chunk].IsDirty, Is.False);
            Assert.That(chunkRenderer.Collider.Version, Is.EqualTo(chunkRenderer.MeshVersion), "and its collider, near the focus");
        }

        [Test]
        public void PlacingTerrainInTheSky_GivesAnAirChunkARenderer()
        {
            Settle(Vector3.zero);
            var skyChunk = new Vector3Int(0, Layers - 1, 0);
            Assert.That(world.Chunks[skyChunk].IsUniform, Is.True, "the top layer is all air");
            Assert.That(pool.TryGet(skyChunk, out _), Is.False);

            Vector3 centre = world.Grid.ChunkOrigin(skyChunk) + Vector3.one * (Size * 0.5f);
            world.ApplyBrush(centre, new BrushSettings(2f), BrushOperation.Add);
            streamer.RebuildEdited(Vector3.zero);

            Assert.That(pool.TryGet(skyChunk, out ChunkRenderer chunkRenderer), Is.True);
            Assert.That(chunkRenderer.Mesh.vertexCount, Is.GreaterThan(0));
        }

        [Test]
        public void MovingAway_UnloadsWhatWasLeftBehind_KeepingEdits()
        {
            Settle(Vector3.zero);
            world.SetDensity(new Vector3Int(3, 3, 3), 0.25f);
            Assert.That(world.IsEdited(Vector3Int.zero), Is.True);

            Vector3 farAway = new Vector3(10 * Size, 0f, 0f);
            Settle(farAway);

            Assert.That(world.IsLoaded(Vector3Int.zero), Is.False);
            Assert.That(world.KeptChunks.ContainsKey(Vector3Int.zero), Is.True, "edited chunks are kept");
            Assert.That(pool.TryGet(Vector3Int.zero, out _), Is.False);

            Settle(Vector3.zero);
            Assert.That(world.Chunks[Vector3Int.zero].GetDensity(new Vector3Int(3, 3, 3)), Is.EqualTo(ByteVoxelStorage.ToDensity(ByteVoxelStorage.Quantize(0.25f))));
        }

        [Test]
        public void LeavingAndComingBack_BeforeJobsFinish_StillShowsEverything()
        {
            // Start loading, leave at once (jobs cancelled mid-flight), come straight back.
            streamer.Update(Vector3.zero);
            streamer.Update(new Vector3(20 * Size, 0f, 0f));
            streamer.Update(Vector3.zero);
            Settle(Vector3.zero);

            foreach (var entry in world.Chunks)
            {
                Assert.That(entry.Value.IsDirty, Is.False, $"{entry.Key} was meshed");
                bool shown = pool.TryGet(entry.Key, out ChunkRenderer chunkRenderer);
                if (!entry.Value.IsUniform && shown)
                {
                    Assert.That(chunkRenderer.Chunk, Is.SameAs(entry.Value), $"{entry.Key} shows its own chunk");
                }
            }
            Assert.That(pool.Active.Count, Is.GreaterThan(0));
        }

        // Runs frames until nothing is pending or running.
        private void Settle(Vector3 focus)
        {
            for (int frame = 0; frame < 5000; frame++)
            {
                streamer.Update(focus);
                streamer.RebuildEdited(focus);
                StreamingStats stats = streamer.Stats;
                if (frame > 0 && stats.Pending == 0 && stats.Generating == 0 && stats.Meshing == 0 && stats.Baking == 0)
                {
                    return;
                }
                System.Threading.Thread.Sleep(1);
            }
            Assert.Fail("The world never settled.");
        }
    }
}
