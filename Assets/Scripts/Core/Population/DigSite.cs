using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>One dig of a <see cref="DigSite"/>: what to take out, where it starts, and which of it is ground as generated.</summary>
    public sealed class Dig
    {
        public DigPlan Plan;

        /// <summary>The sample at the top of the pit's middle, or on the tunnel's floor at its mouth.</summary>
        public Vector3Int Top;

        /// <summary>The world position of <see cref="Top"/>.</summary>
        public Vector3 Middle;

        /// <summary>True for a tunnel, driven along <see cref="Along"/> (x, z).</summary>
        public bool Bore;

        public Vector2 Along;

        /// <summary>For each voxel of the plan, true when it was under the ground as generated: what digging it takes work for.</summary>
        public bool[] Ground;

        /// <summary>How many of the plan's voxels were under the ground: the dig's volume, in voxels.</summary>
        public int GroundVoxels;
    }

    /// <summary>
    /// Where one shop's miners dig (GL615, GL730): a run of open pits beside the settlement, or of tunnels driven into
    /// the nearest slopes, one after the other as each is dug out. Worked out from the ground as generated, so the same
    /// digs are found whether the settlement is loaded or not: the people on screen dig them a blow at a time, and the
    /// work done while nobody watched is applied to the first <see cref="Dig.GroundVoxels"/> of them in order.
    /// </summary>
    public sealed class DigSite
    {
        /// <summary>The pit: voxels across at the top (half), and deep. The tunnel: across, high and long, in voxels.</summary>
        public const int PitRadius = 8, PitDepth = 8;
        public const int BoreWidth = 6, BoreHeight = 9, BoreLength = 56;

        /// <summary>A slope a tunnel is driven into rises this many metres over the next few.</summary>
        public const float MouthRise = 2.6f, MouthRun = 4f;

        /// <summary>The most digs a site has: past them its miners have nothing left to dig.</summary>
        public const int MostDigs = 8;

        private readonly int seed;
        private readonly bool tunnel;
        private readonly Rect town;
        private readonly float range;
        private readonly WorldGrid grid;
        private readonly Func<float, float, float> ground;
        private readonly Func<Vector2, bool> isDry;
        private readonly Func<Vector2, float, bool> mayWork;
        private readonly List<Dig> digs = new List<Dig>();
        private const int VoxelsPerStep = 256;

        private bool done;
        private int attempts;
        private IEnumerator laying;

        /// <param name="seed">The same for every worker of the shop.</param>
        /// <param name="tunnel">Tunnels rather than pits.</param>
        /// <param name="town">The settlement's bounds (x, z).</param>
        /// <param name="range">Metres out of the settlement within which the digs are sited.</param>
        /// <param name="ground">The ground's height as generated at (x, z).</param>
        /// <param name="isDry">False on sea and river.</param>
        /// <param name="mayWork">False where nobody may dig, within a margin (metres) of a point (<see cref="WorkLand.MayWork"/>).</param>
        public DigSite(int seed, bool tunnel, Rect town, float range, WorldGrid grid, Func<float, float, float> ground, Func<Vector2, bool> isDry,
            Func<Vector2, float, bool> mayWork)
        {
            this.seed = seed;
            this.tunnel = tunnel;
            this.town = town;
            this.range = range;
            this.grid = grid;
            this.ground = ground;
            this.isDry = isDry;
            this.mayWork = mayWork;
        }

        /// <summary>The seed of a shop's site: what every worker of the shop digs (the same as before GL730).</summary>
        public static int SeedOf(int settlement, int shop)
        {
            return settlement * 7919 + shop * 131 + 17;
        }

        /// <summary>True for a site of tunnels.</summary>
        public bool Tunnel => tunnel;

        /// <summary>
        /// The dig at a place in the run; false past the last. Worked out on the first ask: a tunnel needs a slope near its
        /// spot, and a spot with none is passed over.
        /// </summary>
        public bool TryGet(int index, out Dig dig)
        {
            while (digs.Count <= index && !done)
            {
                LayNext();
            }
            dig = index < digs.Count ? digs[index] : null;
            return dig != null;
        }

        /// <summary>One more try at laying the run, all at once; false once it is all laid.</summary>
        public bool LayNext()
        {
            IEnumerator attempt = laying ?? (laying = LayAttempt());
            while (attempt.MoveNext())
            {
            }
            return !done;
        }

        /// <summary>
        /// The whole run in small steps for <see cref="SlicedWork"/> (GL771): looking for a tunnel's mouth took 90 ms. A dig
        /// asked for meanwhile finishes the try in hand first, so the run comes out the same.
        /// </summary>
        public IEnumerator LaySteps()
        {
            while (!done)
            {
                IEnumerator attempt = laying ?? (laying = LayAttempt());
                while (attempt.MoveNext())
                {
                    yield return null;
                }
                yield return null;
            }
        }

        /// <summary>Every dig of the run, worked out.</summary>
        public int Count
        {
            get
            {
                TryGet(MostDigs, out _);
                return digs.Count;
            }
        }

        // Lays the dig of a try: a flat dry spot round the settlement, then for a tunnel the nearest slope to it.
        // One try at the next dig of the run. A single iterator, so that a dig asked for at once can finish it.
        private IEnumerator LayAttempt()
        {
            if (done || attempts >= MostDigs * 3)
            {
                done = true;
                laying = null;
                yield break;
            }
            var random = new System.Random(seed + attempts++ * 104729);
            float edge = Mathf.Max(town.width, town.height) * 0.5f + 8f;
            Vector2 spot = town.center;
            bool found = false;
            for (int i = 0; i < 24 && !found; i++)
            {
                float angle = (float)random.NextDouble() * Mathf.PI * 2f;
                spot = town.center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (edge + (float)random.NextDouble() * range * 0.5f);
                float here = ground(spot.x, spot.y);
                found = mayWork(spot, 6f) && isDry(spot)
                    && Mathf.Abs(ground(spot.x + 2f, spot.y) - here) < 1f && Mathf.Abs(ground(spot.x, spot.y + 2f) - here) < 1f;
                yield return null;
            }
            Dig next = null;
            if (found)
            {
                var dig = new Dig();
                bool sited = !tunnel;
                // A tunnel starts at the nearest slope that rises steeply enough, ring by ring round the spot.
                for (float distance = 0f; !sited && distance <= range; distance += 3f)
                {
                    sited = FindMouthAt(spot, distance, out Vector2 mouth, out Vector2 into);
                    if (sited)
                    {
                        spot = mouth;
                        dig.Along = into;
                        dig.Bore = true;
                    }
                    yield return null;
                }
                if (sited)
                {
                    dig.Top = grid.WorldToNearestSample(new Vector3(spot.x, ground(spot.x, spot.y), spot.y));
                    dig.Middle = grid.SampleToWorld(dig.Top);
                    dig.Plan = dig.Bore ? DigPlan.Tunnel(dig.Top, dig.Along, BoreWidth, BoreHeight, BoreLength) : DigPlan.Pit(dig.Top, PitRadius, PitDepth);
                    yield return null;
                    IReadOnlyList<Vector3Int> voxels = dig.Plan.Voxels;
                    dig.Ground = new bool[voxels.Count];
                    // GL771: the voxels of a column share its ground: asked per voxel, one tunnel took over 40 ms.
                    var columns = new Dictionary<Vector2Int, float>();
                    for (int i = 0; i < voxels.Count; i++)
                    {
                        Vector3 at = grid.SampleToWorld(voxels[i]);
                        var column = new Vector2Int(voxels[i].x, voxels[i].z);
                        if (!columns.TryGetValue(column, out float top))
                        {
                            top = ground(at.x, at.z);
                            columns.Add(column, top);
                        }
                        dig.Ground[i] = at.y < top;
                        if (dig.Ground[i])
                        {
                            dig.GroundVoxels++;
                        }
                        if (i % VoxelsPerStep == VoxelsPerStep - 1)
                        {
                            yield return null;
                        }
                    }
                    next = dig;
                }
            }
            if (next != null)
            {
                digs.Add(next);
                done |= digs.Count >= MostDigs;
            }
            else if (!tunnel)
            {
                // No flat dry spot round the settlement: none will be found for a later pit either.
                done = true;
            }
            laying = null;
        }

        // A place on the ring this far from the spot where a slope rises steeply enough for a tunnel's mouth.
        private bool FindMouthAt(Vector2 spot, float distance, out Vector2 mouth, out Vector2 into)
        {
            for (int a = 0; a < 16; a++)
            {
                float angle = a * Mathf.PI / 8f;
                var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                Vector2 at = spot + direction * distance;
                if (mayWork(at, 6f) && isDry(at)
                    && ground(at.x + direction.x * MouthRun, at.y + direction.y * MouthRun) - ground(at.x, at.y) >= MouthRise)
                {
                    mouth = at;
                    into = direction;
                    return true;
                }
            }
            mouth = spot;
            into = Vector2.up;
            return false;
        }
    }
}
