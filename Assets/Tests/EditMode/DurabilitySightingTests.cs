using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Clube.Core.Tests
{
    /// <summary>X23: ore discovery on its own stack (GL796), hand axe durability by grade (GL794), the durability bar (GL795), skill XP text (GL793).</summary>
    public class DurabilitySightingTests
    {
        private readonly List<Object> made = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (Object o in made)
            {
                Object.DestroyImmediate(o);
            }
            made.Clear();
        }

        private T Keep<T>(T o) where T : Object
        {
            made.Add(o);
            return o;
        }

        [Test]
        public void OreIsSeenOnlyAsItsOwnStack()
        {
            ItemDefinition cassiteriteItem = Keep(ItemDefinition.Create("cassiterite", "Cassiterite"));
            ItemDefinition crushed = Keep(ItemDefinition.Create("crushed-cassiterite", "Crushed cassiterite")).WithPurityAs(cassiteriteItem);
            ItemDefinition dirtItem = Keep(ItemDefinition.Create("dirt", "Dirt"));
            VoxelMaterial cassiterite = Keep(VoxelMaterial.Create(5, "Cassiterite")).WithBreaking(1f, cassiteriteItem);
            Keep(VoxelMaterial.Create(1, "Dirt")).WithBreaking(1f, dirtItem);
            var ores = new List<VoxelMaterial> { cassiterite };

            // A shovel load of dirt with cassiterite in it is a stack of dirt: no discovery.
            Assert.IsNull(OreSighting.OreShownBy(dirtItem, ores));
            // Mined as its own stack, or popped out of refining as a separate item: discovered.
            Assert.AreSame(cassiterite, OreSighting.OreShownBy(cassiteriteItem, ores));
            Assert.AreSame(cassiterite, OreSighting.OreShownBy(crushed, ores));
            Assert.IsNull(OreSighting.OreShownBy(null, ores));
        }

        [Test]
        public void OreSeenThroughABrokenDownStage()
        {
            ItemDefinition loose = Keep(ItemDefinition.Create("loose-ore", "Loose ore"));
            VoxelMaterial looseStage = Keep(VoxelMaterial.Create(6, "Loose ore")).WithBreaking(1f, loose);
            VoxelMaterial ore = Keep(VoxelMaterial.Create(7, "Ore")).WithBreaking(1f, null, looseStage);
            Assert.AreSame(ore, OreSighting.OreShownBy(loose, new List<VoxelMaterial> { ore }));
        }

        [Test]
        public void HandAxeDurabilityFollowsTheStoneGrade()
        {
            float[] rows = ToolDurability.DefaultByGrade;
            Assert.AreEqual(1f, ToolDurability.FromGrade(1f, rows), 1e-4f);
            Assert.AreEqual(0.9f, ToolDurability.FromGrade(0.93f, rows), 1e-4f);
            Assert.AreEqual(0.8f, ToolDurability.FromGrade(0.8f, rows), 1e-4f);
            Assert.AreEqual(0.7f, ToolDurability.FromGrade(0.6f, rows), 1e-4f);
            Assert.AreEqual(0.6f, ToolDurability.FromGrade(0.3f, rows), 1e-4f);
            // Rows are data: a table that doesn't cover a grade leaves the tool full.
            Assert.AreEqual(1f, ToolDurability.FromGrade(0.3f, new float[0]), 1e-4f);
        }

        [Test]
        public void DurabilityBarShowsOnlyBelowFull()
        {
            ToolDefinition axe = Keep(ToolDefinition.Create("axe", "Hand axe", 1, 1f)).WithDurability(80);
            Assert.IsFalse(ToolDurability.ShowsBar(new ItemStack(axe, 1)), "a full new tool is just its icon");
            Assert.IsTrue(ToolDurability.ShowsBar(new ItemStack(axe, 1, null, 0, 0.9f)), "made under 100% shows its bar at once");
            Assert.IsTrue(ToolDurability.ShowsBar(new ItemStack(axe, 1, null, 3, 1f)), "worn shows its bar");
            ItemDefinition stick = Keep(ItemDefinition.Create("stick", "Stick"));
            Assert.IsFalse(ToolDurability.ShowsBar(new ItemStack(stick, 1, null, 0, 0.6f)), "not a tool");
        }

        [Test]
        public void SkillProgressReadsXpIntoTheLevelOverItsCost()
        {
            Assert.AreEqual("0.0/300", SkillSet.ProgressText(0f));
            Assert.AreEqual("120.0/300", SkillSet.ProgressText(120f));
            Assert.AreEqual("120.5/300", SkillSet.ProgressText(120.5f));
            Assert.AreEqual("299.9/300", SkillSet.ProgressText(299.99f));
            // Level 1 is reached at 300; level 2 costs 480 more.
            Assert.AreEqual("0.0/480", SkillSet.ProgressText(300f));
            Assert.AreEqual("100.0/480", SkillSet.ProgressText(400f));
            Assert.AreEqual("max", SkillSet.ProgressText(SkillSet.ExperienceFor(SkillSet.MaxLevel)));
        }
    }
}
