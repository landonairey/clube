using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// Cuts a meteor's crash site into the terrain (GL871) from a <see cref="MeteorPlan"/>, all through
    /// <see cref="WorldView.ApplyBrush"/> (so through <c>World</c>, A14): the gouge along the approach line, the crater,
    /// the main ball of mostly iron ore with its other-ore lumps, and the scattered pieces. Every ore piece is first
    /// cleared, so it is solid ore through and through rather than ore only where there was air.
    /// </summary>
    public static class MeteorCrashSite
    {
        /// <summary>True once every chunk column the strike's edits reach is loaded and meshed.</summary>
        public static bool IsReady(WorldView view, MeteorPlan plan)
        {
            return view.IsSettledAround(plan.Impact, MeteorPlan.Reach, out _, out _);
        }

        /// <summary>Makes the crash site; ores the material registry lacks are left out.</summary>
        public static void Cut(WorldView view, MeteorPlan plan)
        {
            Vector3 at = plan.Impact;
            for (int i = 0; i < MeteorPlan.GougeCuts; i++)
            {
                MeteorCut cut = plan.Cut(i);
                float x = at.x + cut.Offset.x, z = at.z + cut.Offset.y;
                float ground = Ground(view, x, z, at.y);
                view.ApplyBrush(new Vector3(x, ground + cut.Lift, z), new BrushSettings(cut.Radius), BrushOperation.Remove);
            }
            view.ApplyBrush(at + Vector3.up * MeteorPlan.CraterLift, new BrushSettings(MeteorPlan.CraterRadius), BrushOperation.Remove);

            MaterialRegistry materials = view.Config.Materials;
            if (materials == null)
            {
                return;
            }
            MeteorPiece ball = plan.Ball;
            Clear(view, at + ball.Offset, ball.Radius);
            for (int i = 0; i < MeteorPlan.BallInclusions.Length; i++)
            {
                MeteorPiece lump = plan.Inclusion(i);
                Clear(view, at + lump.Offset, lump.Radius);
                Place(view, materials, at + lump.Offset, lump);
            }
            // After the lumps: adding only fills air, so the ball keeps them.
            Place(view, materials, at + ball.Offset, ball);

            for (int i = 0; i < MeteorPlan.ScatteredCount; i++)
            {
                MeteorPiece piece = plan.Scattered(i);
                float x = at.x + piece.Offset.x, z = at.z + piece.Offset.z;
                var centre = new Vector3(x, Ground(view, x, z, at.y) + piece.Offset.y, z);
                Clear(view, centre, piece.Radius);
                Place(view, materials, centre, piece);
            }
        }

        private static void Clear(WorldView view, Vector3 centre, float radius)
        {
            view.ApplyBrush(centre, new BrushSettings(radius), BrushOperation.Remove);
        }

        private static void Place(WorldView view, MaterialRegistry materials, Vector3 centre, MeteorPiece piece)
        {
            VoxelMaterial ore = materials.Find(piece.Ore);
            if (ore != null)
            {
                view.ApplyBrush(centre, new BrushSettings(piece.Radius, material: ore.Id), BrushOperation.Add);
            }
        }

        // The ground under (x, z), or the given height when none is found.
        private static float Ground(WorldView view, float x, float z, float near)
        {
            return TryGround(view, x, z, near, out float height) ? height : near;
        }

        /// <summary>
        /// The ground under (x, z), searched over a wide band (about 80 m) round <paramref name="near"/>: hills along the
        /// gouge, or a target called from high ground. False where none is loaded.
        /// </summary>
        public static bool TryGround(WorldView view, float x, float z, float near, out float height)
        {
            for (int step = 0; step < 11; step++)
            {
                // 0, +8, -8, +16, -16 ...
                float offset = ((step + 1) / 2) * GroundProbe.Depth * (step % 2 == 1 ? 1f : -1f);
                if (GroundProbe.TryGround(view, x, z, near + offset, out height))
                {
                    return true;
                }
            }
            height = near;
            return false;
        }
    }
}
