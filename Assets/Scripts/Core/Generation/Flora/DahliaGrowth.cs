using System;
using System.Collections.Generic;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// Where dahlias grow (GL265): rarely, a few at a time, among the grass straight above a node of
    /// <see cref="Ore"/> (gold), as plants that favour a mineral can show a prospector where to dig. Only some nodes
    /// grow them (<see cref="NodeShare"/>), so no flowers doesn't mean no gold. The patch is densest
    /// over the node's centre and thins out across the ground (<see cref="Spread"/>); a node deeper than
    /// <see cref="Deepest"/> under the ground shows nothing. Each patch is mostly one colour, picked by its node.
    /// </summary>
    [Serializable]
    public class DahliaGrowth
    {
        /// <summary>How many flower colours there are: <see cref="ColourOf"/> picks one of these.</summary>
        public const int Colours = 5;

        [Tooltip("The ore dahlias grow over (gold). None: no dahlias.")]
        [SerializeField]
        private VoxelMaterial ore;

        [Tooltip("The share of nodes that grow any dahlias at all: the rest show nothing.")]
        [SerializeField, Range(0f, 1f)]
        private float nodeShare = 0.3f;

        [Tooltip("The share of grass tufts that are dahlias straight over a node's centre.")]
        [SerializeField, Range(0f, 0.3f)]
        private float peakShare = 0.05f;

        [Tooltip("How far across the ground the patch spreads, metres (σ of its falloff).")]
        [SerializeField, Min(0.5f)]
        private float spread = 2f;

        [Tooltip("A node more than this many metres under the ground grows no dahlias.")]
        [SerializeField, Min(1f)]
        private float deepest = 30f;

        [Tooltip("A dahlia's height in metres, smallest and largest.")]
        [SerializeField]
        private Vector2 height = new Vector2(0.7f, 1.1f);

        /// <summary>The ore dahlias grow over; null for none.</summary>
        public VoxelMaterial Ore
        {
            get => ore;
            set => ore = value;
        }

        /// <summary>The share of nodes that grow dahlias at all.</summary>
        public float NodeShare
        {
            get => nodeShare;
            set => nodeShare = Mathf.Clamp01(value);
        }

        /// <summary>True when a node grows dahlias: about <see cref="NodeShare"/> of them, by a hash of where it is.</summary>
        public bool Blooms(OreNode node)
        {
            Vector3 c = node.Centre;
            return VoxelHash.Uniform(0, Mathf.RoundToInt(c.x * 4f), Mathf.RoundToInt(c.y * 4f), Mathf.RoundToInt(c.z * 4f), 0x424c) < nodeShare;
        }

        public float PeakShare
        {
            get => peakShare;
            set => peakShare = Mathf.Clamp(value, 0f, 0.3f);
        }

        public float Spread
        {
            get => spread;
            set => spread = Mathf.Max(0.5f, value);
        }

        public float Deepest
        {
            get => deepest;
            set => deepest = Mathf.Max(1f, value);
        }

        /// <summary>How far round a node's centre, across the ground, dahlias can grow: three of <see cref="Spread"/>.</summary>
        public float Reach => spread * 3f;

        /// <summary>A dahlia's height in metres from a roll 0-1.</summary>
        public float HeightFor(float roll)
        {
            return Rules.HeightFor(roll);
        }

        /// <summary>
        /// The nodes of <see cref="Ore"/> that can grow dahlias in <paramref name="ground"/> (a box round some ground):
        /// those under it, as deep as <see cref="Deepest"/>, out to <see cref="Reach"/> to the side.
        /// </summary>
        public void NodesUnder(OreField ores, Bounds ground, List<OreNode> nodes, List<OreNode> scratch)
        {
            nodes.Clear();
            if (ores == null || ore == null || peakShare <= 0f)
            {
                return;
            }
            Vector3 min = ground.min - new Vector3(Reach, deepest, Reach);
            Vector3 max = ground.max + new Vector3(Reach, 0f, Reach);
            var box = new Bounds((min + max) * 0.5f, max - min);
            ores.CollectNodes(box, scratch);
            foreach (OreNode node in scratch)
            {
                if (node.Material == ore.Id && box.Contains(node.Centre) && Blooms(node))
                {
                    nodes.Add(node);
                }
            }
        }

        /// <summary>
        /// The share of grass tufts at <paramref name="point"/> (on the ground) that are dahlias, from the strongest node
        /// under it, and which node that is (-1 for none).
        /// </summary>
        public float ShareAt(IReadOnlyList<OreNode> nodes, Vector3 point, out int strongest)
        {
            strongest = -1;
            float best = 0f;
            DahliaRules rules = Rules;
            for (int i = 0; i < nodes.Count; i++)
            {
                float share = rules.ShareOf(nodes[i].Centre, point);
                if (share > best)
                {
                    best = share;
                    strongest = i;
                }
            }
            return best;
        }

        /// <summary>A patch's flower colour (0 to <see cref="Colours"/> - 1), from its node; now and then a flower strays.</summary>
        public static int ColourOf(OreNode node, float stray, float roll)
        {
            return DahliaRules.ColourOf(PatchOf(node), stray, roll);
        }

        /// <summary>The colour most of a node's patch shows.</summary>
        public static int PatchOf(OreNode node)
        {
            Vector3 c = node.Centre;
            return (int)(VoxelHash.Hash(0, Mathf.RoundToInt(c.x * 4f), Mathf.RoundToInt(c.y * 4f), Mathf.RoundToInt(c.z * 4f), 0x4441) % Colours);
        }

        /// <summary>The settings as a struct Burst jobs can carry (GL775).</summary>
        public DahliaRules Rules => new DahliaRules { PeakShare = peakShare, Spread = spread, Deepest = deepest, Height = height };
    }

    /// <summary><see cref="DahliaGrowth"/>'s rules for one tuft, as a struct for Burst jobs (GL775) and the class alike.</summary>
    public struct DahliaRules
    {
        public float PeakShare;
        public float Spread;
        public float Deepest;
        public Vector2 Height;

        /// <summary>The share of tufts at <paramref name="point"/> that are dahlias from the node at <paramref name="centre"/>.</summary>
        public float ShareOf(Vector3 centre, Vector3 point)
        {
            float below = point.y - centre.y;
            if (below <= 0f || below > Deepest)
            {
                return 0f;
            }
            float inverse = 1f / (2f * Spread * Spread);
            float dx = point.x - centre.x, dz = point.z - centre.z;
            return PeakShare * Mathf.Exp(-(dx * dx + dz * dz) * inverse);
        }

        /// <summary><see cref="DahliaGrowth.ShareAt"/> over node centres.</summary>
        public float ShareAt(in Unity.Collections.NativeArray<Vector3> centres, Vector3 point, out int strongest)
        {
            strongest = -1;
            float best = 0f;
            for (int i = 0; i < centres.Length; i++)
            {
                float share = ShareOf(centres[i], point);
                if (share > best)
                {
                    best = share;
                    strongest = i;
                }
            }
            return best;
        }

        /// <summary>See <see cref="DahliaGrowth.HeightFor"/>.</summary>
        public float HeightFor(float roll)
        {
            return Mathf.Lerp(Height.x, Height.y, Mathf.Clamp01(roll));
        }

        /// <summary>A flower's colour from its patch's (<see cref="DahliaGrowth.PatchOf"/>); now and then one strays.</summary>
        public static int ColourOf(int patch, float stray, float roll)
        {
            return stray < 0.15f ? Mathf.Min(DahliaGrowth.Colours - 1, (int)(roll * DahliaGrowth.Colours)) : patch;
        }
    }
}
