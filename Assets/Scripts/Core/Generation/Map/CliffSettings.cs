using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>One band of the strata (GL737): a rock and how thick it lies.</summary>
    [Serializable]
    public class StratumDefinition
    {
        [SerializeField]
        private VoxelMaterial material;

        [Tooltip("Metres thick.")]
        [SerializeField, Min(0.25f)]
        private float thickness = 3f;

        public StratumDefinition()
        {
        }

        public StratumDefinition(VoxelMaterial material, float thickness)
        {
            this.material = material;
            this.thickness = thickness;
        }

        public VoxelMaterial Material => material;
        public float Thickness => Mathf.Max(0.25f, thickness);
    }

    /// <summary>
    /// A bounded map's cliffs and canyons (GL737), as data: inland cliffs (escarpments) of varying run length and drop,
    /// whose top swells up to the edge; sea cliffs along stretches of the coast, standing as tall as the land behind
    /// them; and canyons in the dry lands, cut by the rivers into a tableland and walled in ledges of layered rock.
    /// </summary>
    [Serializable]
    public class CliffSettings
    {
        [Header("Inland cliffs")]
        [Tooltip("Metres between neighbouring cliff lines, roughly; 0 for no inland cliffs.")]
        [SerializeField, Min(0f)]
        private float spacing = 850f;

        [Tooltip("Metres over which a cliff comes and goes along its line: its run length.")]
        [SerializeField, Min(20f)]
        private float runLength = 420f;

        [Tooltip("Share of the cliff lines that stand as cliffs.")]
        [SerializeField, Range(0f, 1f)]
        private float coverage = 0.45f;

        [Tooltip("Least and most metres a cliff drops.")]
        [SerializeField]
        private Vector2 drop = new Vector2(6f, 24f);

        [Tooltip("Metres across a cliff's face.")]
        [SerializeField, Min(0.5f)]
        private float face = 3.5f;

        [Tooltip("Metres the top swells up towards the edge, and how far back from it the swell starts.")]
        [SerializeField]
        private Vector2 swell = new Vector2(3f, 50f);

        [Header("Sea cliffs")]
        [Tooltip("Share of the coast that is cliff; 0 for none.")]
        [SerializeField, Range(0f, 1f)]
        private float seaCliffCoverage = 0.4f;

        [Tooltip("Metres along the coast over which sea cliffs come and go.")]
        [SerializeField, Min(50f)]
        private float seaCliffRun = 1100f;

        [Tooltip("Metres a sea cliff's top stands over the plains, at most (up to the land's inland rise: the land behind stays level). Hills and ranges behind the coast stand on it, so cliffs under them stand far taller.")]
        [SerializeField, Min(0f)]
        private float seaCliffHeight = 40f;

        [Tooltip("Metres across a sea cliff's face.")]
        [SerializeField, Min(1f)]
        private float seaCliffFace = 7f;

        [Header("Canyons")]
        [Tooltip("Metres the dry land's tableland stands over its rivers, lakes and the sea; 0 for no canyons.")]
        [SerializeField, Min(0f)]
        private float canyonDepth = 34f;

        [Tooltip("Metres of canyon floor from the water to the foot of the walls.")]
        [SerializeField, Min(0f)]
        private float canyonFloor = 8f;

        [Tooltip("Metres from the foot of a canyon's walls to its rim.")]
        [SerializeField, Min(4f)]
        private float canyonWall = 40f;

        [Tooltip("Ledges in a canyon's walls.")]
        [SerializeField, Range(1, 8)]
        private int canyonLedges = 4;

        [Tooltip("Moisture (0 dry to 1 wet) under which the land is wholly tableland, and over which none is: the steppe and the desert.")]
        [SerializeField]
        private Vector2 dryness = new Vector2(0.27f, 0.32f);

        [Header("Strata")]
        [Tooltip("The bands of rock the layered biomes' rock lies in, bottom first; they repeat up and down. Empty: no strata.")]
        [SerializeField]
        private List<StratumDefinition> strata = new List<StratumDefinition>();

        [Tooltip("Metres under the surface the bands reach; deeper rock is the base material.")]
        [SerializeField, Min(0.5f)]
        private float strataDepth = 8f;

        [Tooltip("Metres the bands rise and fall either way across the land.")]
        [SerializeField, Min(0f)]
        private float strataWander = 3f;

        private const int StrataSalt = 0x53545241;

        public float Spacing { get => spacing; set => spacing = Mathf.Max(0f, value); }
        public float RunLength => Mathf.Max(20f, runLength);
        public float Coverage => Mathf.Clamp01(coverage);
        public Vector2 Drop => new Vector2(Mathf.Max(0f, drop.x), Mathf.Max(drop.x, drop.y));
        public float Face => Mathf.Max(0.5f, face);
        public Vector2 Swell => new Vector2(Mathf.Max(0f, swell.x), Mathf.Max(1f, swell.y));

        public float SeaCliffCoverage { get => seaCliffCoverage; set => seaCliffCoverage = Mathf.Clamp01(value); }
        public float SeaCliffRun => Mathf.Max(50f, seaCliffRun);
        public float SeaCliffHeight => Mathf.Max(0f, seaCliffHeight);
        public float SeaCliffFace => Mathf.Max(1f, seaCliffFace);

        public float CanyonDepth { get => canyonDepth; set => canyonDepth = Mathf.Max(0f, value); }
        public float CanyonFloor => Mathf.Max(0f, canyonFloor);
        public float CanyonWall => Mathf.Max(4f, canyonWall);
        public int CanyonLedges => Mathf.Clamp(canyonLedges, 1, 8);
        public Vector2 Dryness => new Vector2(dryness.x, Mathf.Max(dryness.x + 0.005f, dryness.y));

        /// <summary>The bands of rock, bottom first; add to it for more (at most <see cref="RockStrata.MaxBands"/> are used).</summary>
        public List<StratumDefinition> Strata => strata ??= new List<StratumDefinition>();

        public float StrataDepth => Mathf.Max(0.5f, strataDepth);
        public float StrataWander => Mathf.Max(0f, strataWander);

        /// <summary>The inland cliffs as plain values for the height field; none when <see cref="Spacing"/> or the drop is 0.</summary>
        public Escarpments ToEscarpments(int seed, float seaLevel)
        {
            if (Spacing <= 0f || Drop.y <= 0f || Coverage <= 0f)
            {
                return default;
            }
            return new Escarpments
            {
                Seed = math.hash(new int2(seed, Escarpments.Salt)),
                Spacing = Spacing,
                RunLength = RunLength,
                Coverage = Coverage,
                MinDrop = Drop.x,
                MaxDrop = Drop.y,
                Face = Face,
                SwellHeight = Swell.x,
                SwellWidth = Swell.y,
                SeaLevel = seaLevel,
            };
        }

        /// <summary>The canyons' shape as plain values (their water reach is filled in by <see cref="Canyons.Build"/>).</summary>
        public Canyons ToCanyons(int seed, float regionSize)
        {
            return new Canyons
            {
                Seed = math.hash(new int2(seed, Canyons.Salt)),
                Depth = CanyonDepth,
                Floor = CanyonFloor,
                Wall = CanyonWall,
                Ledges = CanyonLedges,
                DryFrom = Dryness.x,
                DryTo = Dryness.y,
                RegionSize = regionSize,
            };
        }

        /// <summary>The strata as plain values for the fill; none when the list has no rock in it.</summary>
        public RockStrata ToStrata(int seed)
        {
            var bands = new RockStrata
            {
                Depth = StrataDepth,
                Wander = StrataWander,
                Seed = math.hash(new int2(seed, StrataSalt)),
            };
            foreach (StratumDefinition stratum in Strata)
            {
                if (stratum?.Material != null)
                {
                    bands.Add(stratum.Material.Id, stratum.Thickness);
                }
            }
            return bands;
        }

        /// <summary>
        /// Everything that changes the land the water is worked out on, for cache keys: the inland and sea cliffs. The
        /// canyons are cut after the water (<see cref="CanyonKey"/>) and the strata change only materials.
        /// </summary>
        public string Key()
        {
            return $"{Spacing}|{RunLength}|{Coverage}|{Drop}|{Face}|{Swell}|{SeaCliffCoverage}|{SeaCliffRun}|{SeaCliffHeight}|{SeaCliffFace}";
        }

        /// <summary>Everything that changes the canyons, for cache keys.</summary>
        public string CanyonKey()
        {
            return $"{CanyonDepth}|{CanyonFloor}|{CanyonWall}|{CanyonLedges}|{Dryness}";
        }
    }
}
