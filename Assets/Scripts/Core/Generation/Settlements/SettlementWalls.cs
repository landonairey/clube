using System;
using System.Collections.Generic;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// A walled settlement's town wall as voxels (GL715: "walls around the capital"): stone along its ring
    /// (<see cref="SettlementSite.Wall"/>), following the ground, crenellated along its outer face, with a square tower at
    /// each corner and a gateway left open wherever a road or a lane comes through. The writer builds it a stretch at a
    /// time (<see cref="Stretches"/>, <see cref="Blocks"/>).
    /// </summary>
    public static class SettlementWalls
    {
        /// <summary>Metres of wall in one stretch: one piece of the writer.</summary>
        public const float StretchLength = 4f;

        // Metres of merlon and of gap between merlons along the parapet, and the parapet's height.
        private const float Merlon = 1f;
        private const float Parapet = 0.75f;

        /// <summary>
        /// GL801: whether (x, z) lies within a closed wall ring (<see cref="SettlementSite.Wall"/>), by the even-odd rule;
        /// false for a ring of fewer than three corners.
        /// </summary>
        public static bool Inside(IReadOnlyList<Vector2> ring, Vector2 point)
        {
            if (ring == null || ring.Count < 3)
            {
                return false;
            }
            bool inside = false;
            for (int i = 0, j = ring.Count - 1; i < ring.Count; j = i++)
            {
                Vector2 a = ring[i], b = ring[j];
                if ((a.y > point.y) != (b.y > point.y) && point.x < (b.x - a.x) * (point.y - a.y) / (b.y - a.y) + a.x)
                {
                    inside = !inside;
                }
            }
            return inside;
        }

        /// <summary>A stretch of the wall: its ends (x, z), and whether it starts at one of the ring's corners (it then holds that corner's tower).</summary>
        public readonly struct Stretch
        {
            public Stretch(Vector2 from, Vector2 to, bool corner)
            {
                From = from;
                To = to;
                Corner = corner;
            }

            public readonly Vector2 From, To;
            public readonly bool Corner;
        }

        /// <summary>The wall cut into stretches of at most <see cref="StretchLength"/>; none when the site has no wall.</summary>
        public static List<Stretch> Stretches(SettlementSite site)
        {
            var stretches = new List<Stretch>();
            for (int i = 1; i < site.Wall.Count; i++)
            {
                Vector2 a = site.Wall[i - 1], b = site.Wall[i];
                int count = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(a, b) / StretchLength));
                for (int k = 0; k < count; k++)
                {
                    stretches.Add(new Stretch(Vector2.Lerp(a, b, k / (float)count), Vector2.Lerp(a, b, (k + 1) / (float)count), k == 0));
                }
            }
            return stretches;
        }

        /// <summary>The ground a stretch may change (x, z): its line, its thickness, and a corner's tower.</summary>
        public static Rect Box(SettlementSite site, Stretch stretch)
        {
            float pad = Mathf.Max(site.Rules.WallThickness, stretch.Corner ? site.Rules.WallTower : 0f) * 0.5f + 0.5f;
            return Rect.MinMaxRect(Mathf.Min(stretch.From.x, stretch.To.x) - pad, Mathf.Min(stretch.From.y, stretch.To.y) - pad,
                Mathf.Max(stretch.From.x, stretch.To.x) + pad, Mathf.Max(stretch.From.y, stretch.To.y) + pad);
        }

        /// <summary>True where the wall leaves a gateway: a planned road or one of the settlement's lanes passes within half <see cref="SettlementSiteRules.GateWidth"/>.</summary>
        public static bool IsGate(SettlementSite site, Vector2 point)
        {
            float half = site.Rules.GateWidth * 0.5f;
            if (site.RoadBeyond(point.x, point.y) < half)
            {
                return true;
            }
            foreach (SettlementLane lane in site.Lanes)
            {
                for (int i = 1; i < lane.Points.Count; i++)
                {
                    if (SettlementSite.Distance(point, lane.Points[i - 1], lane.Points[i]) < half)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// A stretch's voxels (lowest corner, a global sample), all of <paramref name="stone"/>: from a metre under the
        /// ground up to the wall's height, the outer half of its top a parapet of merlons; a corner's tower hollow, a storey
        /// higher, crenellated all round. Columns in a gateway or over water (<paramref name="wetAt"/>) are left out.
        /// </summary>
        public static Dictionary<Vector3Int, byte> Blocks(SettlementSite site, Stretch stretch, float voxel, byte stone, Func<float, float, bool> wetAt = null)
        {
            var blocks = new Dictionary<Vector3Int, byte>();
            SettlementSiteRules rules = site.Rules;
            Vector2 centre = site.Footprint.center;
            Vector2 run = stretch.To - stretch.From;
            float length = run.magnitude;
            if (length < 1e-4f)
            {
                return blocks;
            }
            Vector2 along = run / length;
            var across = new Vector2(-along.y, along.x);
            if (Vector2.Dot(across, (stretch.From + stretch.To) * 0.5f - centre) < 0f)
            {
                across = -across;
            }
            float half = rules.WallThickness * 0.5f;
            float h = voxel * 0.5f;
            // The run along the ring from the corner, for the merlons' rhythm to carry on from stretch to stretch.
            for (float s = h; s < length; s += voxel)
            {
                for (float t = -half + h; t < half; t += voxel)
                {
                    Vector2 point = stretch.From + along * s + across * t;
                    float rhythm = Mathf.Abs(point.x) + Mathf.Abs(point.y);
                    bool merlon = t > 0f && Mathf.Repeat(rhythm, Merlon * 2f) < Merlon;
                    Column(site, blocks, point, voxel, stone, merlon ? rules.WallHeight : rules.WallHeight - Parapet, wetAt);
                }
            }
            if (stretch.Corner)
            {
                // The corner's tower: a hollow square, its walls half a metre thick, crenellated round its top.
                float side = rules.WallTower * 0.5f;
                float top = rules.WallHeight + rules.WallTowerRise;
                for (float x = -side + h; x < side; x += voxel)
                {
                    for (float z = -side + h; z < side; z += voxel)
                    {
                        Vector2 point = stretch.From + new Vector2(x, z);
                        bool shell = Mathf.Abs(x) > side - 0.5f || Mathf.Abs(z) > side - 0.5f;
                        float edge = Mathf.Abs(x) > Mathf.Abs(z) ? z : x;
                        bool merlon = shell && Mathf.Repeat(edge + side, Merlon * 2f) < Merlon;
                        // Inside, a floor at the wall's walk.
                        float height = shell ? (merlon ? top : top - Parapet) : rules.WallHeight - Parapet;
                        Column(site, blocks, point, voxel, stone, height, wetAt, !shell);
                    }
                }
            }
            return blocks;
        }

        // A column of stone from a metre under the ground to a height over it (the ground's height at the column, kept
        // on the voxel grid); a floor only fills its top voxel, and clears nothing.
        private static void Column(SettlementSite site, Dictionary<Vector3Int, byte> blocks, Vector2 point, float voxel, byte stone, float height,
            Func<float, float, bool> wetAt, bool floorOnly = false)
        {
            if (IsGate(site, point) || (wetAt != null && wetAt(point.x, point.y)))
            {
                return;
            }
            float ground = Mathf.Round(site.HeightAt(point.x, point.y) / voxel) * voxel;
            int x = Mathf.FloorToInt(point.x / voxel), z = Mathf.FloorToInt(point.y / voxel);
            int top = Mathf.RoundToInt((ground + height) / voxel);
            int bottom = floorOnly ? top - 1 : Mathf.RoundToInt((ground - 1f) / voxel);
            for (int y = bottom; y < top; y++)
            {
                blocks[new Vector3Int(x, y, z)] = stone;
            }
        }
    }
}
