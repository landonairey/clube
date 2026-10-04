using NUnit.Framework;
using UnityEngine;

namespace Clube.Core.Tests
{
    public class SurfaceRaycastTests
    {
        private const float Tolerance = 1e-4f;
        private static readonly ChunkMeshSettings Settings = new ChunkMeshSettings(0.5f, 1f);

        [Test]
        public void RayTriangle_HitsInsideAndMissesOutside()
        {
            Vector3 a = new Vector3(0f, 0f, 0f), b = new Vector3(1f, 0f, 0f), c = new Vector3(0f, 0f, 1f);

            Assert.That(RayTriangle.Intersect(new Ray(new Vector3(0.2f, 2f, 0.2f), Vector3.down), a, b, c, out float distance), Is.True);
            Assert.That(distance, Is.EqualTo(2f).Within(Tolerance));
            Assert.That(RayTriangle.Intersect(new Ray(new Vector3(0.8f, 2f, 0.8f), Vector3.down), a, b, c, out _), Is.False);
            Assert.That(RayTriangle.Intersect(new Ray(new Vector3(0.2f, 2f, 0.2f), Vector3.up), a, b, c, out _), Is.False, "Behind the origin.");
            Assert.That(RayTriangle.Intersect(new Ray(new Vector3(0.2f, 0f, -1f), Vector3.forward), a, b, c, out _), Is.False, "Parallel.");
        }

        [Test]
        public void RayDown_HitsTheFloorSurfaceVoxel()
        {
            // Solid on sample layer y = 0, so the floor surface sits at y = 0.5 in voxel row 0.
            Chunk chunk = Floor(new Vector3Int(3, 3, 3));

            bool hit = SurfaceRaycast.Cast(new Ray(new Vector3(1.3f, 10f, 2.6f), Vector3.down), chunk, Settings, out Vector3Int voxel, out Vector3 point);

            Assert.That(hit, Is.True);
            Assert.That(voxel, Is.EqualTo(new Vector3Int(1, 0, 2)));
            Assert.That(point.y, Is.EqualTo(0.5f).Within(Tolerance));
        }

        [Test]
        public void RayStraightDownAVoxelFace_StillHitsTheSurface()
        {
            // A camera looking straight down gives a ray with a vanishing sideways part. On a
            // voxel boundary (x = 1) the walk commits to one column, where the surface only
            // meets the ray on a triangle edge.
            Chunk chunk = Floor(new Vector3Int(3, 3, 3));
            var ray = new Ray(new Vector3(1f, 10f, 1f), new Vector3(-4e-8f, -1f, -4e-8f));

            bool hit = SurfaceRaycast.Cast(ray, chunk, Settings, out _, out Vector3 point);

            Assert.That(hit, Is.True);
            Assert.That(point.y, Is.EqualTo(0.5f).Within(Tolerance));
        }

        [Test]
        public void EmptyChunk_HasNothingToHit()
        {
            var chunk = new Chunk(new Vector3Int(3, 3, 3));

            Assert.That(SurfaceRaycast.Cast(new Ray(new Vector3(1.5f, 10f, 1.5f), Vector3.down), chunk, Settings, out _, out _), Is.False);
        }

        [Test]
        public void RayThroughASurfaceVoxelThatMissesItsTriangles_IsNotAHit()
        {
            // One solid corner sample: voxel (0,0,0) holds only a small corner triangle near
            // the origin. A ray down its far corner passes through the voxel but misses it.
            var chunk = new Chunk(new Vector3Int(2, 2, 2));
            chunk.SetDensity(Vector3Int.zero, 1f);

            bool hit = SurfaceRaycast.Cast(new Ray(new Vector3(0.9f, 10f, 0.9f), Vector3.down), chunk, Settings, out _, out _);

            Assert.That(hit, Is.False);
        }

        [Test]
        public void VoxelSize_ScalesTheHitPoint()
        {
            Chunk chunk = Floor(new Vector3Int(3, 3, 3));
            var settings = new ChunkMeshSettings(0.5f, 2f);

            SurfaceRaycast.Cast(new Ray(new Vector3(3f, 20f, 3f), Vector3.down), chunk, settings, out Vector3Int voxel, out Vector3 point);

            Assert.That(voxel, Is.EqualTo(new Vector3Int(1, 0, 1)));
            Assert.That(point.y, Is.EqualTo(1f).Within(Tolerance));
        }

        private static Chunk Floor(Vector3Int voxelCount)
        {
            var chunk = new Chunk(voxelCount);
            for (int z = 0; z <= voxelCount.z; z++)
            {
                for (int x = 0; x <= voxelCount.x; x++)
                {
                    chunk.SetDensity(new Vector3Int(x, 0, z), 1f);
                }
            }
            return chunk;
        }
    }
}
