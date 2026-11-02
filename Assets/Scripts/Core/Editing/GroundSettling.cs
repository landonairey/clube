using System;
using System.Collections.Generic;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// Ground that falls (GL308): dirt, grass and sand (<see cref="VoxelMaterial.Fall"/>) with air under it
    /// moves down one sample a step, and sand also slides down a side where the slope is steeper than 45°
    /// (both the side and the sample under it are air). Only samples near a change are looked at: every
    /// density change in the <see cref="World"/> wakes the samples round it, and every move wakes the ones
    /// round where it left and landed, so a dug-out bank crumbles and stops once everything rests. Moves
    /// swap the two samples' densities and materials through the world's edit path (A7), so the ground
    /// keeps its volume and chunk borders and meshes follow.
    /// </summary>
    public sealed class GroundSettling : IDisposable
    {
        private static readonly Vector3Int[] Sides = { Vector3Int.right, Vector3Int.left, new Vector3Int(0, 0, 1), new Vector3Int(0, 0, -1) };

        private readonly GroundFall[] falls = new GroundFall[256];
        private readonly float iso;
        private HashSet<Vector3Int> awake = new HashSet<Vector3Int>();
        private HashSet<Vector3Int> next = new HashSet<Vector3Int>();
        private readonly List<Vector3Int> batch = new List<Vector3Int>();
        private int turn;

        /// <param name="fallOf">How each material id falls.</param>
        public GroundSettling(World world, Func<byte, GroundFall> fallOf, float iso)
        {
            World = world;
            this.iso = iso;
            for (int id = 0; id < falls.Length; id++)
            {
                falls[id] = fallOf((byte)id);
            }
            world.DensityChanged += Wake;
        }

        public World World { get; }

        /// <summary>Samples waiting to be looked at.</summary>
        public int AwakeCount => awake.Count;

        /// <summary>Wakes a sample and the ones that could fall into it: those above, and those beside (sand slides).</summary>
        public void Wake(Vector3Int sample)
        {
            for (int dz = -1; dz <= 1; dz++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    awake.Add(sample + new Vector3Int(dx, 1, dz));
                    awake.Add(sample + new Vector3Int(dx, 0, dz));
                }
            }
        }

        /// <summary>
        /// Moves every awake sample that can fall by one sample, lowest first, at most <paramref name="maxMoves"/>
        /// (the rest wait); returns how many moved. Samples woken by these moves wait for the next step.
        /// </summary>
        public int Step(int maxMoves = 2000)
        {
            if (awake.Count == 0)
            {
                return 0;
            }
            batch.Clear();
            batch.AddRange(awake);
            HashSet<Vector3Int> stepping = awake;
            awake = next;
            awake.Clear();
            next = stepping;
            batch.Sort((a, b) => a.y.CompareTo(b.y));
            turn++;
            int moves = 0;
            foreach (Vector3Int sample in batch)
            {
                if (moves >= maxMoves)
                {
                    awake.Add(sample);
                    continue;
                }
                if (TryFall(sample))
                {
                    moves++;
                }
            }
            return moves;
        }

        private bool TryFall(Vector3Int sample)
        {
            float? density = World.GetDensity(sample);
            if (!density.HasValue || density.Value < iso)
            {
                return false;
            }
            byte? material = World.GetMaterial(sample);
            GroundFall fall = material.HasValue ? falls[material.Value] : GroundFall.Stays;
            if (fall == GroundFall.Stays)
            {
                return false;
            }
            Vector3Int below = sample + Vector3Int.down;
            if (IsAir(below))
            {
                Move(sample, below, density.Value, material.Value);
                return true;
            }
            if (fall != GroundFall.Slides)
            {
                return false;
            }
            // Sides in a turning order, so a heap spreads evenly instead of always one way.
            int first = (sample.x * 7 + sample.z * 13 + turn) & 3;
            for (int k = 0; k < 4; k++)
            {
                Vector3Int side = Sides[(first + k) & 3];
                if (IsAir(sample + side) && IsAir(below + side))
                {
                    Move(sample, below + side, density.Value, material.Value);
                    return true;
                }
            }
            return false;
        }

        // Air, and loaded: unloaded ground counts as solid, so nothing falls out of the world.
        private bool IsAir(Vector3Int sample)
        {
            float? density = World.GetDensity(sample);
            return density.HasValue && density.Value < iso;
        }

        private void Move(Vector3Int from, Vector3Int to, float density, byte material)
        {
            float left = World.GetDensity(to) ?? 0f;
            byte leftMaterial = World.GetMaterial(to) ?? material;
            World.SetMaterial(to, material);
            World.SetDensity(to, density);
            World.SetDensity(from, left);
            World.SetMaterial(from, leftMaterial);
        }

        public void Dispose()
        {
            World.DensityChanged -= Wake;
        }
    }
}
