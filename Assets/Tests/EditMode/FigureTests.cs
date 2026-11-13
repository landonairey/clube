using System;
using NUnit.Framework;
using UnityEngine;

namespace Clube.Core.Tests
{
    /// <summary>
    /// The low-poly people (GL608): every preset builds valid meshes inside the triangle budget, the shops' keepers
    /// resolve, a seed always gives the same person, and the poses move the way a body does.
    /// </summary>
    public class FigureTests
    {
        // A whole figure, both arms and legs counted; a town of 32 is then under 40 000 triangles.
        private const int TriangleBudget = 1200;

        [Test]
        public void EveryPresetBuildsValidMeshesWithinTheTriangleBudget()
        {
            foreach (FigurePreset preset in FigureLooks.All)
            {
                for (int seed = 0; seed < 4; seed++)
                {
                    FigureParts parts = FigureMeshes.Build(FigureLooks.Varied(preset, seed));
                    foreach (Mesh mesh in parts.All)
                    {
                        Assert.That(mesh, Is.Not.Null, $"{preset}");
                        Assert.That(mesh.vertexCount, Is.GreaterThan(0), $"{preset} {mesh.name}");
                        Assert.That(mesh.colors.Length, Is.EqualTo(mesh.vertexCount), $"{preset} {mesh.name}");
                        Assert.That(mesh.normals.Length, Is.EqualTo(mesh.vertexCount), $"{preset} {mesh.name}");
                        foreach (Vector3 vertex in mesh.vertices)
                        {
                            Assert.That(float.IsNaN(vertex.x + vertex.y + vertex.z), Is.False, $"{preset} {mesh.name}");
                        }
                        Bounds bounds = mesh.bounds;
                        Assert.That(bounds.size.x, Is.LessThan(0.8f), $"{preset} {mesh.name} width");
                        Assert.That(bounds.size.y, Is.LessThan(1.2f), $"{preset} {mesh.name} height");
                        UnityEngine.Object.DestroyImmediate(mesh);
                    }
                }
                FigureParts plain = FigureMeshes.Build(FigureLooks.Of(preset));
                Assert.That(plain.Triangles, Is.InRange(300, TriangleBudget), $"{preset} triangles");
                foreach (Mesh mesh in plain.All)
                {
                    UnityEngine.Object.DestroyImmediate(mesh);
                }
            }
        }

        [Test]
        public void FeetStandOnTheGroundAndTheHeadIsAManHigh()
        {
            FigureParts parts = FigureMeshes.Build(FigureLooks.Of(FigurePreset.Player));
            float sole = FigureSkeleton.HipHeight - FigureSkeleton.ThighLength + parts.Shin.bounds.min.y;
            Assert.That(sole, Is.EqualTo(0f).Within(0.005f));
            float top = FigureSkeleton.HipHeight + FigureSkeleton.NeckHeight + parts.Head.bounds.max.y;
            Assert.That(top, Is.EqualTo(FigureSkeleton.Tall).Within(0.08f));
        }

        [Test]
        public void EveryShopKindHasItsOwnKeeper()
        {
            var seen = new System.Collections.Generic.HashSet<FigurePreset>();
            foreach (ShopKind kind in Enum.GetValues(typeof(ShopKind)))
            {
                FigurePreset preset = FigureLooks.Of(kind);
                Assert.That(seen.Add(preset), Is.True, $"{kind} shares {preset}");
                Assert.That(FigureLooks.Of(preset).Prop, Is.Not.EqualTo(FigureProp.None), $"{kind} has no tool");
            }
        }

        [Test]
        public void PresetsDifferAndEveryPropHasAMesh()
        {
            var keys = new System.Collections.Generic.HashSet<string>();
            foreach (FigurePreset preset in FigureLooks.All)
            {
                Assert.That(keys.Add(FigureLooks.Of(preset).MeshKey()), Is.True, $"{preset} looks like another preset");
            }
            foreach (FigureProp prop in Enum.GetValues(typeof(FigureProp)))
            {
                Mesh mesh = FigureProps.For(prop);
                Assert.That(mesh != null, Is.EqualTo(prop != FigureProp.None), $"{prop}");
            }
        }

        [Test]
        public void ASeedAlwaysGivesTheSamePersonAndSeedsGiveDifferentPeople()
        {
            var people = new System.Collections.Generic.HashSet<string>();
            for (int seed = 0; seed < 24; seed++)
            {
                FigureLook a = FigureLooks.Varied(FigurePreset.Worker, seed), b = FigureLooks.Varied(FigurePreset.Worker, seed);
                Assert.That(a.MeshKey(), Is.EqualTo(b.MeshKey()));
                Assert.That(a.Height, Is.EqualTo(b.Height));
                Assert.That(a.Height, Is.InRange(0.9f, 1.1f));
                Assert.That(a.Build, Is.InRange(0.8f, 1.3f));
                people.Add(a.MeshKey() + a.Height);
            }
            Assert.That(people.Count, Is.GreaterThan(12), "24 workers should not be a few clones");
            Assert.That(FigureLooks.Varied(FigurePreset.Player, 1).MeshKey(), Is.EqualTo(FigureLooks.Varied(FigurePreset.Player, 2).MeshKey()));
        }

        [Test]
        public void LooksThatMatchShareTheirMeshes()
        {
            FigureLook look = FigureLooks.Varied(FigurePreset.Carter, 3);
            FigureLook taller = look;
            taller.Height *= 1.05f;
            Assert.That(FigureMeshes.For(taller).Torso, Is.SameAs(FigureMeshes.For(look).Torso));
            Assert.That(FigureMeshes.For(FigureLooks.Of(FigurePreset.Thief)).Torso, Is.Not.SameAs(FigureMeshes.For(look).Torso));
        }

        [Test]
        public void EveryPoseIsFiniteAndRepeatable()
        {
            foreach (FigureAction action in Enum.GetValues(typeof(FigureAction)))
            {
                for (float t = 0f; t < 8f; t += 0.37f)
                {
                    FigurePose pose = FigurePoses.At(action, t, t * FigurePoses.StepRate);
                    float sum = pose.HipsHeight + pose.Breath + pose.Torso.sqrMagnitude + pose.Head.sqrMagnitude + pose.ArmLeft.sqrMagnitude
                        + pose.ArmRight.sqrMagnitude + pose.LegLeft.sqrMagnitude + pose.LegRight.sqrMagnitude + pose.ElbowLeft + pose.ElbowRight
                        + pose.KneeLeft + pose.KneeRight;
                    Assert.That(float.IsNaN(sum) || float.IsInfinity(sum), Is.False, $"{action} at {t}");
                    Assert.That(pose.HipsHeight, Is.InRange(0.3f, 1f), $"{action} hips");
                    Assert.That(pose.ElbowLeft, Is.InRange(0f, 150f), $"{action} elbow");
                    Assert.That(pose.KneeRight, Is.InRange(0f, 150f), $"{action} knee");
                    Assert.That(FigurePoses.At(action, t, t * FigurePoses.StepRate).ArmRight, Is.EqualTo(pose.ArmRight));
                }
            }
        }

        [Test]
        public void WalkingSwingsOppositeLimbsAndLongerStridesSwingFurther()
        {
            float step = Mathf.PI * 0.5f;
            FigurePose walk = FigurePoses.At(FigureAction.Walk, 0f, step, 1f);
            Assert.That(walk.LegLeft.x - walk.LegRight.x, Is.GreaterThan(40f), "legs apart");
            Assert.That(walk.ArmLeft.x, Is.LessThan(0f), "the left arm goes forward as the left leg goes back");
            Assert.That(walk.ArmRight.x, Is.GreaterThan(0f));
            FigurePose slow = FigurePoses.At(FigureAction.Walk, 0f, step, 0.5f);
            Assert.That(Mathf.Abs(slow.LegLeft.x - slow.LegRight.x), Is.LessThan(walk.LegLeft.x - walk.LegRight.x));
        }

        [Test]
        public void TheMiningSwingLandsOnItsBeat()
        {
            float before = FigurePoses.At(FigureAction.Mine, 0.5f / 0.7f).ArmRight.x;
            float landing = FigurePoses.At(FigureAction.Mine, FigurePoses.MineImpact / 0.7f).ArmRight.x;
            float after = FigurePoses.At(FigureAction.Mine, 0.85f / 0.7f).ArmRight.x;
            Assert.That(FigurePoses.MineBeat(FigurePoses.MineImpact / 0.7f), Is.EqualTo(FigurePoses.MineImpact).Within(0.001f));
            Assert.That(before, Is.LessThan(landing - 40f), "the arm is high over the shoulder before the beat");
            Assert.That(after, Is.LessThan(landing), "and recoils a little after landing");
            Assert.That(after, Is.GreaterThan(before), "but not back up");
        }

        [Test]
        public void ToolsShowOnlyForTheirChoreButAStaffIsAlwaysCarried()
        {
            Assert.That(FigurePoses.PropShown(FigureAction.Stand, FigureProp.Pick), Is.False);
            Assert.That(FigurePoses.PropShown(FigureAction.Mine, FigureProp.Pick), Is.True);
            Assert.That(FigurePoses.PropShown(FigureAction.Walk, FigureProp.Staff), Is.True);
            Assert.That(FigurePoses.PropShown(FigureAction.Lie, FigureProp.Staff), Is.False);
            Assert.That(FigurePoses.PropShown(FigureAction.Mine, FigureProp.None), Is.False);
        }
    }
}
