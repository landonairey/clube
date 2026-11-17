using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Clube.Core.Tests
{
    /// <summary>GL718: settlements' low-poly stand-ins from the building plan, and the roads painted for the far terrain.</summary>
    public class FarViewTests
    {
        private const float Voxel = 0.25f;

        private readonly List<Object> made = new List<Object>();

        [TearDown]
        public void DestroyAll()
        {
            foreach (Object thing in made)
            {
                Object.DestroyImmediate(thing);
            }
            made.Clear();
        }

        private static SettlementBuilding Building(BuildingDesign design, float yaw = 0f, float pitch = 0.6f)
        {
            return new SettlementBuilding { Design = design, Centre = new Vector2(100f, 50f), Pad = 20f, Yaw = yaw, Pitch = pitch };
        }

        // A point of a part's unit shape in the world.
        private static Vector3 At(StandInPart part, float x, float y, float z) => part.Matrix.MultiplyPoint3x4(new Vector3(x, y, z));

        [Test]
        public void AGableBuilding_IsWallsToTheEaves_AndARoofUpToTheVoxelRidge()
        {
            var design = new BuildingDesign { Width = 8f, Depth = 6f, Stories = 2, StoryHeight = 2.5f };
            var parts = new List<StandInPart>();

            BuildingStandIns.Collect(Building(design), Voxel, parts);

            Assert.AreEqual(2, parts.Count);
            StandInPart walls = parts[0], roof = parts[1];
            Assert.AreEqual(StandInShape.Walls, walls.Shape);
            Assert.AreEqual(StandInShape.Roof, roof.Shape);
            float eaves = 20f + TownLayout.FloorHeight + 5f;
            Assert.AreEqual(eaves, At(walls, 0f, 1f, 0f).y, 1e-3f, "the walls reach the eaves");
            Assert.AreEqual(20f - BuildingStandIns.Footing, At(walls, 0f, 0f, 0f).y, 1e-3f, "and run on under the pad");
            Assert.AreEqual(8f, Vector3.Distance(At(walls, -0.5f, 0f, 0f), At(walls, 0.5f, 0f, 0f)), 1e-3f);
            Assert.AreEqual(6f, Vector3.Distance(At(walls, 0f, 0f, -0.5f), At(walls, 0f, 0f, 0.5f)), 1e-3f);
            float ridge = 20f + SettlementStructures.RoofAt(design, 0.6f, Voxel, 0f);
            Assert.AreEqual(ridge, At(roof, 0f, 1f, 0f).y, 1e-3f, "the ridge is the voxel roof's");
            Assert.AreEqual(eaves, At(roof, 0f, 0f, 0.5f).y, 1e-3f);
            float run = Vector3.Distance(At(roof, 0f, 0f, 0.5f), At(roof, 0f, 0f, 0f));
            Assert.AreEqual(0.6f, (ridge - eaves) / run, 1e-3f, "at the building's pitch");
        }

        [Test]
        public void ABuilding_FollowsItsTurn()
        {
            var design = new BuildingDesign { Width = 8f, Depth = 4f };
            SettlementBuilding building = Building(design, yaw: 90f);
            var parts = new List<StandInPart>();

            BuildingStandIns.Collect(building, Voxel, parts);

            Vector3 front = At(parts[0], 0f, 0f, 0.5f);
            Vector2 expected = building.ToWorld(new Vector2(0f, 2f));
            Assert.AreEqual(expected.x, front.x, 1e-3f);
            Assert.AreEqual(expected.y, front.z, 1e-3f);
        }

        [Test]
        public void ABattlementedTower_IsOneStoneBox_ToItsParapet()
        {
            var design = new BuildingDesign { Width = 5f, Depth = 5f, Stories = 3, Masonry = true, Battlements = true };
            var parts = new List<StandInPart>();

            BuildingStandIns.Collect(Building(design), Voxel, parts);

            Assert.AreEqual(1, parts.Count);
            Assert.AreEqual(StandInShape.StoneWalls, parts[0].Shape);
            Assert.AreEqual(20f + SettlementStructures.RoofAt(design, 0f, Voxel, 0f), At(parts[0], 0f, 1f, 0f).y, 1e-3f);
        }

        [Test]
        public void ASite_GivesEveryBuildingItsParts_AndMergesIntoOneMesh()
        {
            var settlement = new PlannedSettlement
            {
                Index = 0, Type = SettlementType.Defaults().Find(t => t.Id == "town"), Position = new Vector2(500f, 500f), Name = "Test", Population = 12,
            };
            SettlementSite site = SettlementLayout.Plan(settlement, 32f, new SettlementSiteRules(), (x, z) => 40f);
            var parts = new List<StandInPart>();

            BuildingStandIns.Collect(site, Voxel, parts);

            Assert.GreaterOrEqual(parts.Count, site.Buildings.Count);
            Assert.LessOrEqual(parts.Count, site.Buildings.Count * 2);
            UnityEngine.Mesh merged = StandInMeshes.Merge(parts, new Vector3(500f, 40f, 500f), new[] { Color.red, Color.gray, Color.blue });
            made.Add(merged);
            Assert.Greater(merged.vertexCount, 0);
            Assert.Less(merged.bounds.extents.magnitude, 200f, "around the settlement's middle");
            Assert.AreEqual(merged.vertexCount, merged.colors.Length);
        }

        [Test]
        public void UnitMeshes_FaceOutward()
        {
            foreach (StandInShape shape in new[] { StandInShape.Walls, StandInShape.Roof })
            {
                UnityEngine.Mesh mesh = StandInMeshes.Unit(shape, Color.white);
                made.Add(mesh);
                Vector3 middle = new Vector3(0f, shape == StandInShape.Roof ? 0.3f : 0.5f, 0f);
                Vector3[] vertices = mesh.vertices, normals = mesh.normals;
                for (int i = 0; i < vertices.Length; i++)
                {
                    Assert.Greater(Vector3.Dot(normals[i], vertices[i] - middle), 0f, $"{shape} vertex {i}");
                }
            }
        }

        [Test]
        public void ARoad_IsPaintedAlongItsLine_InItsColour_AndNotOverItsTunnel()
        {
            var mask = new RoadMask(new Rect(0f, 0f, 200f, 200f), 100);
            var line = new List<Vector2> { new Vector2(20f, 100f), new Vector2(100f, 100f), new Vector2(180f, 100f) };
            var tunnel = new RoadTunnel { From = new Vector2(100f, 100f), To = new Vector2(180f, 100f) };

            mask.Paint(new RoadMask.Road(line, 2f, new Color32(200, 100, 50, 255), new[] { tunnel }));

            Assert.AreEqual(1f, mask.CoverageAt(new Vector2(60f, 100.5f)), 1e-3f, "on the road");
            Assert.AreEqual(0f, mask.CoverageAt(new Vector2(60f, 110f)), "off it");
            Assert.AreEqual(0f, mask.CoverageAt(new Vector2(140f, 100.5f)), "over the tunnel");
            Color32 texel = mask.Texels[mask.TexelOf(new Vector2(60f, 100.5f)).y * mask.Size + mask.TexelOf(new Vector2(60f, 100.5f)).x];
            Assert.AreEqual(200, texel.r);
            Assert.AreEqual(50, texel.b);
        }

        [Test]
        public void TheHaze_KeepsNearLandClear_AndBuildsWithDistance()
        {
            Assert.AreEqual(0.5f, AirHaze.HazeAt(7000f, 7000f), 1e-4f, "half lost at the half distance");
            Assert.Less(AirHaze.HazeAt(1000f, 7000f), 0.02f, "the local mountain keeps its colours");
            float nextPeak = AirHaze.HazeAt(3000f, 7000f);
            Assert.Greater(nextPeak, 0.08f, "the next peak is muted");
            Assert.Less(nextPeak, 0.2f, "slightly");
            Assert.Greater(AirHaze.HazeAt(15000f, 7000f), 0.9f, "the far land is mostly haze");
        }

        [Test]
        public void ARoadNarrowerThanATexel_StillShows_Fainter()
        {
            var mask = new RoadMask(new Rect(0f, 0f, 400f, 400f), 100);
            var line = new List<Vector2> { new Vector2(10f, 202f), new Vector2(390f, 202f) };

            mask.Paint(new RoadMask.Road(line, 0.75f, new Color32(255, 255, 255, 255)));

            float cover = mask.CoverageAt(new Vector2(200f, 202f));
            Assert.Greater(cover, 0.2f);
            Assert.Less(cover, 0.6f);
            Color32 texel = mask.Texels[mask.TexelOf(new Vector2(200f, 202f)).y * mask.Size + mask.TexelOf(new Vector2(200f, 202f)).x];
            Assert.AreEqual(texel.a, texel.r, 1, "the colour is premultiplied by the coverage");
        }
    }
}
