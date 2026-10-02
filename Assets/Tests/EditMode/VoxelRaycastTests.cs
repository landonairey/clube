using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Clube.Core.Tests
{
    public class VoxelRaycastTests
    {
        private static readonly Vector3Int Grid = new Vector3Int(4, 4, 4);

        [Test]
        public void RayAlongX_VisitsVoxelsInOrder()
        {
            var visited = new List<Vector3Int>();
            var ray = new Ray(new Vector3(-1f, 1.5f, 2.5f), Vector3.right);

            bool hit = VoxelRaycast.Cast(ray, Grid, 1f, voxel => { visited.Add(voxel); return false; }, out _);

            Assert.That(hit, Is.False);
            Assert.That(visited, Is.EqualTo(new[]
            {
                new Vector3Int(0, 1, 2), new Vector3Int(1, 1, 2), new Vector3Int(2, 1, 2), new Vector3Int(3, 1, 2),
            }));
        }

        [Test]
        public void StopsAtFirstAcceptedVoxel()
        {
            var ray = new Ray(new Vector3(1.5f, 10f, 1.5f), Vector3.down);

            bool hit = VoxelRaycast.Cast(ray, Grid, 1f, voxel => voxel.y <= 1, out Vector3Int found);

            Assert.That(hit, Is.True);
            Assert.That(found, Is.EqualTo(new Vector3Int(1, 1, 1)));
        }

        [Test]
        public void RayMissingTheChunk_ReturnsFalse()
        {
            var ray = new Ray(new Vector3(-1f, 10f, -1f), Vector3.right);

            Assert.That(VoxelRaycast.Cast(ray, Grid, 1f, _ => true, out _), Is.False);
        }

        [Test]
        public void RayPointingAway_ReturnsFalse()
        {
            var ray = new Ray(new Vector3(-1f, 1.5f, 1.5f), Vector3.left);

            Assert.That(VoxelRaycast.Cast(ray, Grid, 1f, _ => true, out _), Is.False);
        }

        [Test]
        public void RayStartingInside_StartsAtItsOwnVoxel()
        {
            var ray = new Ray(new Vector3(2.5f, 2.5f, 2.5f), Vector3.forward);

            VoxelRaycast.Cast(ray, Grid, 1f, _ => true, out Vector3Int found);

            Assert.That(found, Is.EqualTo(new Vector3Int(2, 2, 2)));
        }

        [Test]
        public void DiagonalRay_StepsOneAxisAtATime()
        {
            var visited = new List<Vector3Int>();
            var ray = new Ray(new Vector3(0.5f, 0.2f, 0.5f), new Vector3(1f, 1f, 0f));

            VoxelRaycast.Cast(ray, Grid, 1f, voxel => { visited.Add(voxel); return false; }, out _);

            for (int i = 1; i < visited.Count; i++)
            {
                Vector3Int delta = visited[i] - visited[i - 1];
                Assert.That(Mathf.Abs(delta.x) + Mathf.Abs(delta.y) + Mathf.Abs(delta.z), Is.EqualTo(1),
                    $"Jumped from {visited[i - 1]} to {visited[i]}.");
            }
            Assert.That(visited[0], Is.EqualTo(new Vector3Int(0, 0, 0)));
        }

        [Test]
        public void VoxelSize_ScalesTheGrid()
        {
            var ray = new Ray(new Vector3(1.25f, 10f, 0.25f), Vector3.down);

            VoxelRaycast.Cast(ray, Grid, 0.5f, voxel => voxel.y == 0, out Vector3Int found);

            Assert.That(found, Is.EqualTo(new Vector3Int(2, 0, 0)));
        }
    }
}
