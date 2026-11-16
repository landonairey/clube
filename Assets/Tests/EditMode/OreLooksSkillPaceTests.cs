using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Clube.Core.Tests
{
    /// <summary>
    /// Batch L (GL627-GL630): magnetite and coal look apart from each other and from cassiterite, on the assets
    /// themselves; a skill level takes ten times the experience, and a save from before keeps its levels; a skill can be
    /// set to a level.
    /// </summary>
    public class OreLooksSkillPaceTests
    {
        private const string Materials = "Assets/Config/Materials/";

        private static VoxelMaterial Load(string name)
        {
            var material = AssetDatabase.LoadAssetAtPath<VoxelMaterial>(Materials + name + ".asset");
            Assert.IsNotNull(material, name);
            return material;
        }

        [Test]
        public void MagnetiteCoalAndCassiterite_AreDrawnThreeWays()
        {
            Assert.AreEqual(OreSketchStyle.Crystals, Load("Magnetite").SketchStyle);
            Assert.AreEqual(OreSketchStyle.Solid, Load("Coal").SketchStyle, "Coal is filled in black (GL628).");
            Assert.AreEqual(OreSketchStyle.Speckled, Load("Cassiterite").SketchStyle);
        }

        [Test]
        public void MagnetitesColour_IsBlue_NotCassiteritesBrownNorCoalsBlack()
        {
            Color magnetite = Load("Magnetite").DebugColor;
            Color cassiterite = Load("Cassiterite").DebugColor;
            Color coal = Load("Coal").DebugColor;
            Assert.Greater(magnetite.b, magnetite.r * 1.5f, "Steel blue: clearly more blue than red.");
            Assert.Greater(cassiterite.r, cassiterite.b * 1.5f, "Cassiterite is the brown one.");
            Assert.Less(Mathf.Max(coal.r, coal.g, coal.b), 0.1f, "Coal is black.");
            Assert.Greater(magnetite.b - coal.b, 0.2f, "Magnetite is not as dark as coal.");
        }

        [Test]
        public void MagnetitesTexture_IsBluerThanCassiterites_AndLighterThanCoals()
        {
            (float blueOverRed, float brightness) magnetite = Tone(Load("Magnetite").Albedo);
            (float blueOverRed, float brightness) cassiterite = Tone(Load("Cassiterite").Albedo);
            (float blueOverRed, float brightness) coal = Tone(Load("Coal").Albedo);
            Assert.Greater(magnetite.blueOverRed, cassiterite.blueOverRed + 0.05f, "Blue crystals against brown specks.");
            Assert.Greater(magnetite.brightness, coal.brightness + 0.15f, "Ore in grey stone against a black seam.");
        }

        // How blue the texture is on the whole, and how bright.
        private static (float blueOverRed, float brightness) Tone(Texture2D texture)
        {
            Assert.IsNotNull(texture);
            float r = 0f, g = 0f, b = 0f;
            Color[] pixels = texture.GetPixels();
            foreach (Color pixel in pixels)
            {
                r += pixel.r;
                g += pixel.g;
                b += pixel.b;
            }
            return (b / r, (r + g + b) / (3f * pixels.Length));
        }

        [Test]
        public void EveryLevel_CostsTenTimesWhatItDid()
        {
            for (int level = 0; level <= SkillSet.MaxLevel; level++)
            {
                // The curve before GL629: 30 for the first level, each 60% more.
                float before = 30f * (Mathf.Pow(1.6f, level) - 1f) / 0.6f;
                Assert.AreEqual(before * 10f, SkillSet.ExperienceFor(level), before * 1e-4f, $"Level {level}");
            }
        }

        [Test]
        public void ExperienceFromTheOldCurve_KeepsItsLevel_AndItsWayToTheNext()
        {
            for (int level = 0; level < SkillSet.MaxLevel; level++)
            {
                float oldStart = 30f * (Mathf.Pow(1.6f, level) - 1f) / 0.6f;
                float oldNext = 30f * (Mathf.Pow(1.6f, level + 1) - 1f) / 0.6f;
                var skills = new SkillSet();
                // A save from before notes no curve: a cost of 0.
                skills.SetExperience(SkillType.Mining, SkillSet.FromCurve(Mathf.Lerp(oldStart, oldNext, 0.5f), 0f));
                Assert.AreEqual(level, skills.Level(SkillType.Mining), $"Half way through level {level}");
                float share = Mathf.InverseLerp(SkillSet.ExperienceFor(level), SkillSet.ExperienceFor(level + 1), skills.Experience(SkillType.Mining));
                Assert.AreEqual(0.5f, share, 1e-3f);
            }
            Assert.AreEqual(123f, SkillSet.FromCurve(123f, SkillSet.FirstLevelCost), 1e-4f, "A save on today's curve is left alone.");
        }

        [Test]
        public void SetLevel_GoesUpAndDown_ToExactlyThatLevel()
        {
            var skills = new SkillSet();
            var reached = new List<int>();
            int changes = 0;
            skills.LevelReached += (skill, level) => reached.Add(level);
            skills.Changed += () => changes++;

            skills.SetLevel(SkillType.Smelting, 4);
            Assert.AreEqual(4, skills.Level(SkillType.Smelting));
            Assert.AreEqual(SkillSet.ExperienceFor(4), skills.Experience(SkillType.Smelting));
            CollectionAssert.AreEqual(new[] { 1, 2, 3, 4 }, reached, "Every level passed is announced, as earning it would.");

            reached.Clear();
            skills.SetLevel(SkillType.Smelting, 2);
            Assert.AreEqual(2, skills.Level(SkillType.Smelting), "Unlike RaiseTo, it lowers.");
            CollectionAssert.IsEmpty(reached);
            Assert.AreEqual(2, changes);

            skills.SetLevel(SkillType.Smelting, 99);
            Assert.AreEqual(SkillSet.MaxLevel, skills.Level(SkillType.Smelting));
            skills.SetLevel(SkillType.Smelting, -3);
            Assert.AreEqual(0, skills.Level(SkillType.Smelting));
            Assert.AreEqual(0, skills.Level(SkillType.Mining), "Skills are separate.");

            skills.Suppressed = true;
            skills.SetLevel(SkillType.Mining, 1);
            Assert.AreEqual(1, skills.Level(SkillType.Mining), "A cheat works where nothing is earned (the tutorial).");
            skills.SetLevel(SkillType.None, 5);
            Assert.AreEqual(0f, skills.Experience(SkillType.None));
        }

        [Test]
        public void EveryLevelUpToTheTop_IsReachedExactly()
        {
            var skills = new SkillSet();
            for (int level = 0; level <= SkillSet.MaxLevel; level++)
            {
                skills.SetLevel(SkillType.Crafting, level);
                Assert.AreEqual(level, skills.Level(SkillType.Crafting));
            }
        }

        [Test]
        public void Types_ListEverySkillButNone()
        {
            Array all = Enum.GetValues(typeof(SkillType));
            var types = new List<SkillType>(SkillSet.Types);
            Assert.AreEqual(all.Length - 1, types.Count);
            CollectionAssert.DoesNotContain(types, SkillType.None);
            CollectionAssert.AllItemsAreUnique(types);
            CollectionAssert.Contains(types, SkillType.Goldsmithing);
        }
    }
}
