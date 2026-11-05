using System.Collections.Generic;
using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// Leaves that landed after felling rot away (GL350): every in-game hour each one has a <see cref="DecayChancePerHour"/>
    /// chance to go, at random, so a felled crown's pile thins out over a few days. Only samples still holding fallen leaves
    /// decay; one dug up, built over or replaced is forgotten. Kept on World, fed by <see cref="TreeFeller"/>. Not saved yet:
    /// a loaded game's fallen leaves stay.
    /// </summary>
    public class FallenLeaves : MonoBehaviour
    {
        /// <summary>Chance a fallen leaf sample decays in an in-game hour.</summary>
        public const float DecayChancePerHour = 0.1f;

        // How far down a fallen leaf that dropped is looked for, in samples.
        private const int MaxFallSamples = 24;

        [SerializeField]
        private WorldView worldView;

        [SerializeField]
        private GameClock clock;

        private readonly List<Vector3Int> lying = new List<Vector3Int>();
        private TreeMaterials trees;
        private World treesFor;
        private double lastHour = double.NaN;
        private System.Random random = new System.Random(1234);

        /// <summary>Fallen leaf samples still lying.</summary>
        public int Count => lying.Count;

        /// <summary>Starts these samples decaying.</summary>
        public void Add(IEnumerable<Vector3Int> samples)
        {
            lying.AddRange(samples);
        }

        private void Update()
        {
            if (worldView == null || clock == null || !worldView.IsReady || lying.Count == 0)
            {
                return;
            }
            double hour = System.Math.Floor(clock.Days * 24.0);
            if (double.IsNaN(lastHour))
            {
                lastHour = hour;
                return;
            }
            // Every hour that passed (resting skips several at once) is a roll for each leaf.
            int hours = (int)System.Math.Min(48.0, hour - lastHour);
            if (hours <= 0)
            {
                return;
            }
            lastHour = hour;
            Decay(hours);
        }

        // Each leaf survives an hour at 1 - chance, so over several hours at that to the power of the hours.
        private void Decay(int hours)
        {
            World world = worldView.World;
            if (trees == null || treesFor != world)
            {
                trees = TreeMaterials.From(worldView.Config.Terrain.Trees);
                treesFor = world;
            }
            float iso = worldView.Config.IsoLevel;
            double keep = System.Math.Pow(1.0 - DecayChancePerHour, hours);
            for (int i = lying.Count - 1; i >= 0; i--)
            {
                Vector3Int sample = lying[i];
                float? density = world.GetDensity(sample);
                if (!density.HasValue)
                {
                    // Not loaded: it waits.
                    continue;
                }
                if (density.Value < iso || !trees.IsFallenLeaves(world.GetMaterial(sample) ?? 0))
                {
                    // Fallen leaves drop like dirt (GL382): one that fell is followed down to where it lies now.
                    if (TryFindBelow(world, iso, sample, out Vector3Int landed))
                    {
                        lying[i] = landed;
                        sample = landed;
                    }
                    else
                    {
                        lying.RemoveAt(i);
                        continue;
                    }
                }
                if (random.NextDouble() >= keep)
                {
                    world.SetDensity(sample, 0f);
                    lying.RemoveAt(i);
                }
            }
        }

        // The first solid sample under an emptied one, within a few metres, if it's fallen leaves nobody else follows.
        private bool TryFindBelow(World world, float iso, Vector3Int from, out Vector3Int landed)
        {
            for (int down = 1; down <= MaxFallSamples; down++)
            {
                Vector3Int below = from + Vector3Int.down * down;
                float? density = world.GetDensity(below);
                if (!density.HasValue)
                {
                    break;
                }
                if (density.Value < iso)
                {
                    continue;
                }
                landed = below;
                return trees.IsFallenLeaves(world.GetMaterial(below) ?? 0) && !lying.Contains(below);
            }
            landed = default;
            return false;
        }
    }
}
