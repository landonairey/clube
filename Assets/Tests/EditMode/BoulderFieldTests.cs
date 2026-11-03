using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;

namespace Clube.Core.Tests
{
    /// <summary>Large boulders (GL339, GL342): rare, metres across, faceted, often wider at the waist than at the base.</summary>
    public class BoulderFieldTests
    {
        private const float Ground = 10f;
        private const float RockChance = 0.004f;
        private const float Voxel = 0.25f;

        private static BoulderField Field(Rect keepClear = default)
        {
            return new BoulderField(5, RockChance, Voxel, new FlatGenerator(Ground), 0, keepClear);
        }

        private static IEnumerable<IReadOnlyList<TreePart>> Boulders(BoulderField field, int cells)
        {
            for (int z = -cells; z < cells; z++)
            {
                for (int x = -cells; x < cells; x++)
                {
                    IReadOnlyList<TreePart> parts = field.PartsIn(new Vector2Int(x, z));
                    if (parts.Count > 0)
                    {
                        yield return parts;
                    }
                }
            }
        }

        [Test]
        public void LargeBoulders_AreAFewPercentOfBoulders()
        {
            BoulderField field = Field();
            // Boulders per square metre from the rock chance, against large ones per cell.
            float boulders = RockChance / ChunkFillKernel.BoulderOneIn / (Voxel * Voxel) * BoulderField.CellSize * BoulderField.CellSize;
            Assert.That(field.ChancePerCell / boulders, Is.EqualTo(BoulderField.Share).Within(1e-4f));
            int count = 0;
            foreach (IReadOnlyList<TreePart> _ in Boulders(field, 30))
            {
                count++;
            }
            float expected = field.ChancePerCell * 60 * 60;
            Assert.That(count, Is.InRange(expected * 0.75f, expected * 1.25f), "Cells hold a boulder at about the chance.");
        }

        [Test]
        public void LargeBoulders_AreMetresAcross_AndFaceted()
        {
            foreach (IReadOnlyList<TreePart> parts in Boulders(Field(), 20))
            {
                TreePart main = parts[0];
                Assert.That(main.IsBoulder);
                Assert.That(math.cmax(main.Radii), Is.InRange(BoulderField.MinRadius, BoulderField.MaxRadius));
                int cuts = 0;
                foreach (float4 cut in new[] { main.Cut0, main.Cut1, main.Cut2, main.Cut3 })
                {
                    cuts += math.any(cut.xyz != 0f) ? 1 : 0;
                }
                Assert.That(cuts, Is.InRange(2, 4), "Two to four flat faces.");
            }
        }

        [Test]
        public void SomeBoulders_StandOnABaseNarrowerThanTheirWaist()
        {
            int overhangs = 0;
            int total = 0;
            foreach (IReadOnlyList<TreePart> parts in Boulders(Field(), 20))
            {
                total++;
                TreePart main = parts[0];
                float3 centre = main.From;
                if (centre.y <= Ground)
                {
                    continue;
                }
                // At the waist's height, out along the boulder's own x: solid there, yet open ground below at the base.
                for (float out_ = 0.3f; out_ < main.Radii.x; out_ += 0.25f)
                {
                    var waist = new float3(centre.x + main.Turn.x * out_, centre.y, centre.z + main.Turn.y * out_);
                    var foot = new float3(waist.x, Ground + 0.2f, waist.z);
                    if (main.Depth(waist) > 0f && main.Depth(foot) < 0f)
                    {
                        overhangs++;
                        break;
                    }
                }
            }
            Assert.That(total, Is.GreaterThan(20));
            Assert.That(overhangs, Is.GreaterThan(total / 5), $"Many boulders overhang their base ({overhangs} of {total}).");
        }

        [Test]
        public void Boulders_AreTheSameEveryTime_AndKeepOutOfTheStartArea()
        {
            var start = new Rect(-60f, -60f, 120f, 120f);
            BoulderField a = Field(start);
            BoulderField b = Field(start);
            foreach (IReadOnlyList<TreePart> parts in Boulders(a, 12))
            {
                float3 centre = parts[0].From;
                Assert.That(start.Contains(new Vector2(centre.x, centre.z)), Is.False, "None in the start area.");
                var cell = new Vector2Int(Mathf.FloorToInt(centre.x / BoulderField.CellSize), Mathf.FloorToInt(centre.z / BoulderField.CellSize));
                IReadOnlyList<TreePart> again = b.PartsIn(cell);
                Assert.That(again.Count, Is.EqualTo(parts.Count));
                Assert.That(again[0].From, Is.EqualTo(parts[0].From));
            }
        }
    }
}
