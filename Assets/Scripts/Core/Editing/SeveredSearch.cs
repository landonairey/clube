using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// <see cref="TreeFelling.FindSevered"/> a slice at a time: each <see cref="Step"/> works until its time is up, so a big
    /// crown (90,000 leaves, most of a second of searching) is spread over frames instead of stalling one. Reads the world
    /// on the main thread between frames; a chunk that loads or drops while it waits is read as it is then.
    /// </summary>
    public sealed class SeveredSearch
    {
        private readonly WorldSampleReader reader;
        private readonly IEnumerator<int> steps;

        public SeveredSearch(World world, Vector3Int from, Vector3Int to, TreeMaterials trees, float iso)
        {
            reader = new WorldSampleReader(world);
            steps = TreeFelling.FindSeveredSteps(reader, from, to, trees, iso, this).GetEnumerator();
        }

        /// <summary>True once the search has finished; <see cref="Limb"/> is then its answer.</summary>
        public bool IsDone { get; private set; }

        /// <summary>The limb cut off, or null when nothing was (or the search hasn't finished).</summary>
        public FelledLimb Limb { get; internal set; }

        /// <summary>Time spent searching so far, over all steps.</summary>
        public double Milliseconds { get; private set; }

        /// <summary>How many steps it has taken.</summary>
        public int Steps { get; private set; }

        /// <summary>Searches on for about <paramref name="milliseconds"/>; true once it has finished.</summary>
        public bool Step(double milliseconds)
        {
            if (IsDone)
            {
                return true;
            }
            // Chunks may have loaded or dropped since the last step.
            reader.Forget();
            Steps++;
            var watch = Stopwatch.StartNew();
            while (watch.Elapsed.TotalMilliseconds < milliseconds)
            {
                if (!steps.MoveNext())
                {
                    IsDone = true;
                    break;
                }
            }
            Milliseconds += watch.Elapsed.TotalMilliseconds;
            return IsDone;
        }

        /// <summary>Searches to the end at once and gives the limb (null when nothing was cut off).</summary>
        public FelledLimb Finish()
        {
            Step(double.PositiveInfinity);
            return Limb;
        }
    }
}
