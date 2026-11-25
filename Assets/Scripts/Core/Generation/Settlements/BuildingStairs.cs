using System.Collections.Generic;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// The wooden stairs inside a building of more than one storey (GL873, GL874): a square spiral a metre wide running
    /// round the inside of the walls, one voxel up for every two voxels along, with a landing at each corner. It starts on
    /// the ground floor in the front corner by the door (<see cref="SettlementStructures.DoorX"/>), climbs along the right
    /// side towards the back, and keeps turning until it is level with the top storey's floor, or with the flat roof of a
    /// battlemented building (a tower's stairs reach its top). Over every tread the floors and the roof are cut away for
    /// headroom, which leaves the stairwell's openings. A building with turrets keeps its stairs inside them, so they never
    /// come up in a turret. Plain data in the building's own frame (x across its front, y over the pad, z towards the front).
    /// </summary>
    public sealed class BuildingStairs
    {
        /// <summary>Metres across a flight and a landing.</summary>
        public const float Width = 1f;
        /// <summary>Metres of headroom kept clear over every tread.</summary>
        public const float Headroom = 2.25f;
        /// <summary>How thick a tread's boards are under its top, in metres.</summary>
        public const float Thickness = 0.5f;

        /// <summary>What a stair makes of a point: nothing, a tread's boards, or the air kept over a tread.</summary>
        public enum Cell
        {
            None,
            Tread,
            Headroom,
        }

        /// <summary>One step or landing: its area seen from above (x, z) and the height of its top over the pad.</summary>
        public readonly struct Tread
        {
            public Tread(Rect area, float surface)
            {
                Area = area;
                Surface = surface;
            }

            public Rect Area { get; }
            public float Surface { get; }
        }

        private readonly List<Tread> treads;
        // The band the spiral runs round in (outside the well in its middle), for a quick answer away from it.
        private readonly Rect outer, well;

        private BuildingStairs(List<Tread> treads, float cx, float cz)
        {
            this.treads = treads;
            outer = Rect.MinMaxRect(-cx - Width * 0.5f, -cz - Width * 0.5f, cx + Width * 0.5f, cz + Width * 0.5f);
            well = Rect.MinMaxRect(-cx + Width * 0.5f, -cz + Width * 0.5f, cx - Width * 0.5f, cz - Width * 0.5f);
        }

        /// <summary>The steps and landings from the ground floor up, in order.</summary>
        public IReadOnlyList<Tread> Treads => treads;

        /// <summary>The top of the highest tread: the floor it leads to.</summary>
        public float Top => treads[treads.Count - 1].Surface;

        /// <summary>
        /// The height of the floor the stairs lead to over the pad: the top storey's, or a battlemented roof's (each is a
        /// voxel of boards or stone laid on its storey line).
        /// </summary>
        public static float TargetOf(BuildingDesign design, float voxel)
        {
            int stories = Mathf.Clamp(design.Stories, 1, 3);
            int level = design.Battlements ? stories : stories - 1;
            return TownLayout.FloorHeight + level * design.StoryHeight + voxel;
        }

        /// <summary>
        /// The stairs of a building, or null when it has one storey and a gable roof (nothing to climb to) or is too small
        /// for one turn of the spiral to rise clear of the turn under it.
        /// </summary>
        public static BuildingStairs For(BuildingDesign design, float voxel)
        {
            if (design == null || (design.Stories < 2 && !design.Battlements))
            {
                return null;
            }
            float rise = voxel;
            float run = 2f * voxel;
            // The spiral's centre line: half a stair in from the walls' inner faces (or from the turrets).
            float cx = design.Width * 0.5f - voxel - design.Turrets - Width * 0.5f;
            float cz = design.Depth * 0.5f - voxel - design.Turrets - Width * 0.5f;
            Vector2[] corners = { new Vector2(cx, cz), new Vector2(cx, -cz), new Vector2(-cx, -cz), new Vector2(-cx, cz) };
            int[] steps = new int[4];
            int lap = 0;
            for (int side = 0; side < 4; side++)
            {
                float length = (corners[(side + 1) % 4] - corners[side]).magnitude - Width;
                steps[side] = Mathf.FloorToInt(length / run + 0.001f);
                if (steps[side] < 1)
                {
                    return null;
                }
                lap += steps[side];
            }
            // One turn must rise clear of the tread and headroom of the turn under it.
            if (lap * rise < Thickness + Headroom + rise)
            {
                return null;
            }
            float target = TargetOf(design, voxel);
            float surface = TownLayout.FloorHeight;
            var treads = new List<Tread> { new Tread(Landing(corners[0]), surface) };
            for (int side = 0; surface < target - 0.001f; side = (side + 1) % 4)
            {
                Vector2 from = corners[side], to = corners[(side + 1) % 4];
                float length = (to - from).magnitude - Width;
                Vector2 along = (to - from).normalized;
                for (int k = 0; k < steps[side] && surface < target - 0.001f; k++)
                {
                    float start = Width * 0.5f + k * run;
                    // The last step runs on to the landing, whatever is left of the side.
                    float end = k == steps[side] - 1 ? Width * 0.5f + length : start + run;
                    surface += rise;
                    treads.Add(new Tread(Step(from, along, start, end), surface));
                }
                if (surface < target - 0.001f)
                {
                    treads.Add(new Tread(Landing(to), surface));
                }
            }
            return new BuildingStairs(treads, cx, cz);
        }

        /// <summary>What the stairs make of a point in the building's frame: a tread's boards win over another's headroom.</summary>
        public Cell At(float x, float y, float z)
        {
            var point = new Vector2(x, z);
            Cell cell = Cell.None;
            if (!outer.Contains(point) || well.Contains(point) || y > Top + Headroom)
            {
                return cell;
            }
            foreach (Tread tread in treads)
            {
                if (!tread.Area.Contains(point))
                {
                    continue;
                }
                if (y < tread.Surface && y > tread.Surface - Thickness)
                {
                    return Cell.Tread;
                }
                if (y > tread.Surface && y < tread.Surface + Headroom)
                {
                    cell = Cell.Headroom;
                }
            }
            return cell;
        }

        private static Rect Landing(Vector2 corner)
        {
            return new Rect(corner.x - Width * 0.5f, corner.y - Width * 0.5f, Width, Width);
        }

        // A step between two distances along a side from its corner, a stair's width across.
        private static Rect Step(Vector2 from, Vector2 along, float start, float end)
        {
            Vector2 a = from + along * start, b = from + along * end;
            var across = new Vector2(Mathf.Abs(along.y), Mathf.Abs(along.x)) * (Width * 0.5f);
            Vector2 min = Vector2.Min(a, b) - across, max = Vector2.Max(a, b) + across;
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }
    }
}
