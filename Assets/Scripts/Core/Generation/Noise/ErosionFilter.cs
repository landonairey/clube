using System;
using Unity.Mathematics;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// The settings of an <see cref="ErosionFilter"/>, part of <see cref="TerrainSettings"/> (P12, eroded landscape).
    /// Lengths are in metres; slopes are rise over run.
    /// </summary>
    [Serializable]
    public class ErosionSettings
    {
        [Tooltip("Detail of the smooth massifs under the erosion (in place of Landscape's ridges). Keep it low: the gullies follow their slopes.")]
        [SerializeField, Range(1, 8)]
        private int baseOctaves = 3;

        [Tooltip("Width of the biggest gullies, in metres. Each octave's gullies are half the last's.")]
        [SerializeField, Range(20f, 400f)]
        private float gullySize = 110f;

        [Tooltip("How deep the gullies cut, as a share of their width (the biggest cut about this times Gully size, the rest less).")]
        [SerializeField, Range(0f, 0.6f)]
        private float strength = 0.15f;

        [Tooltip("Gully octaves: each adds gullies half the size, branching off the last.")]
        [SerializeField, Range(1, 7)]
        private int octaves = 5;

        [Tooltip("How much of the gullies' own shape shows. Lower keeps peaks pointy and only cuts the gullies a little.")]
        [SerializeField, Range(0f, 1f)]
        private float gullyWeight = 0.5f;

        [Tooltip("Higher lets small gullies reach gentler slopes; lower keeps them on the steep ones.")]
        [SerializeField, Range(0.5f, 4f)]
        private float detail = 1.5f;

        [Tooltip("How steep a slope must be before gullies cut it fully (higher: gentler slopes erode too).")]
        [SerializeField, Range(0.25f, 4f)]
        private float onset = 1.25f;

        [Tooltip("The slope gullies run down as if the land had it everywhere, so the first gullies don't only gather near the peaks.")]
        [SerializeField, Range(0.1f, 2f)]
        private float assumedSlope = 0.7f;

        [Tooltip("Rounds off ridge crests (0: razor sharp).")]
        [SerializeField, Range(0f, 0.5f)]
        private float ridgeRounding = 0.1f;

        [Tooltip("Rounds off gully floors, as sediment would (0: V-shaped).")]
        [SerializeField, Range(0f, 0.5f)]
        private float creaseRounding = 0.05f;

        [Tooltip("How much summits rise, as a share of the gullies' depth (1: the filter's full lift, which can push peaks past the world's top).")]
        [SerializeField, Range(0f, 1f)]
        private float peakLift = 0.4f;

        [Tooltip("Hollows smaller than this, in square metres, hold no pond (for settings that leave many small pits in the gullies).")]
        [SerializeField, Range(0f, 5000f)]
        private float minPondArea = 0f;

        [Tooltip("Valleys from this many of the biggest gully octaves start rivers (see the water settings).")]
        [SerializeField, Range(1, 4)]
        private int valleyOctaves = 2;

        public int BaseOctaves
        {
            get => Mathf.Clamp(baseOctaves, 1, 8);
            set => baseOctaves = Mathf.Clamp(value, 1, 8);
        }

        public float GullySize
        {
            get => Mathf.Clamp(gullySize, 20f, 400f);
            set => gullySize = Mathf.Clamp(value, 20f, 400f);
        }

        public float Strength
        {
            get => Mathf.Clamp(strength, 0f, 0.6f);
            set => strength = Mathf.Clamp(value, 0f, 0.6f);
        }

        public int Octaves
        {
            get => Mathf.Clamp(octaves, 1, 7);
            set => octaves = Mathf.Clamp(value, 1, 7);
        }

        public float GullyWeight
        {
            get => Mathf.Clamp01(gullyWeight);
            set => gullyWeight = Mathf.Clamp01(value);
        }

        public float Detail
        {
            get => Mathf.Clamp(detail, 0.5f, 4f);
            set => detail = Mathf.Clamp(value, 0.5f, 4f);
        }

        public float Onset
        {
            get => Mathf.Clamp(onset, 0.25f, 4f);
            set => onset = Mathf.Clamp(value, 0.25f, 4f);
        }

        public float AssumedSlope
        {
            get => Mathf.Clamp(assumedSlope, 0.1f, 2f);
            set => assumedSlope = Mathf.Clamp(value, 0.1f, 2f);
        }

        public float RidgeRounding
        {
            get => Mathf.Clamp(ridgeRounding, 0f, 0.5f);
            set => ridgeRounding = Mathf.Clamp(value, 0f, 0.5f);
        }

        public float CreaseRounding
        {
            get => Mathf.Clamp(creaseRounding, 0f, 0.5f);
            set => creaseRounding = Mathf.Clamp(value, 0f, 0.5f);
        }

        public float PeakLift
        {
            get => Mathf.Clamp01(peakLift);
            set => peakLift = Mathf.Clamp01(value);
        }

        /// <summary>Smallest hollow that holds a pond on eroded land, in square metres.</summary>
        public float MinPondArea
        {
            get => Mathf.Clamp(minPondArea, 0f, 5000f);
            set => minPondArea = Mathf.Clamp(value, 0f, 5000f);
        }

        public int ValleyOctaves
        {
            get => Mathf.Clamp(valleyOctaves, 1, 4);
            set => valleyOctaves = Mathf.Clamp(value, 1, 4);
        }

        /// <summary>The filter's job data.</summary>
        public ErosionFilter ToFilter(int seed)
        {
            return new ErosionFilter(seed, GullySize, Strength, Octaves, GullyWeight, Detail, Onset, AssumedSlope,
                RidgeRounding, CreaseRounding, ValleyOctaves, PeakLift);
        }

        /// <summary>A key that changes whenever the eroded shape does.</summary>
        public string Key => $"{baseOctaves}|{gullySize}|{strength}|{octaves}|{gullyWeight}|{detail}|{onset}|{assumedSlope}|{ridgeRounding}|{creaseRounding}|{valleyOctaves}|{minPondArea}|{peakLift}";
    }

    /// <summary>
    /// Erosion as a filter on a heightfield (P12), after Rune Skovbo Johansen's erosion filter
    /// (blog.runevision.com, 2026-03, "Fast and Gorgeous Erosion Filter"), written here from its description.
    /// Stripes run down the slope and make gullies and ridges; each octave adds stripes half the size along the
    /// slopes the last left, so smaller gullies branch off bigger ones. Nothing is simulated: every point is worked
    /// out on its own from the height and slope there, so chunks, levels of detail and the water map all agree.
    /// <list type="bullet">
    /// <item>Stripes are blended from a 4 x 4 block of cells, each with its own randomly placed pivot, so they bend
    /// with the slope without smearing; the blend is renormalised so gullies keep their depth (normalised gullies).</item>
    /// <item>Gullies fade out where the land is flat, towards a target from -1 (valley floors) to 1 (peaks), so
    /// peaks stay pointy, valleys V-shaped and plains untouched; each octave's own ridges and creases add to the
    /// fade, so small gullies stay off big ridges (stacked fading).</item>
    /// <item>The slope that steers the next octave counts a gully's side as evenly steep from top to bottom, so
    /// small gullies branch off cleanly instead of curling along big ones (straight gullies).</item>
    /// </list>
    /// Also gives a ridge map, -1 on gully floors to 1 on ridge crests, and the same from only the biggest
    /// octaves (<see cref="ValleyOctaves"/>), where rivers start (<see cref="Hydrology"/>).
    /// </summary>
    public struct ErosionFilter
    {
        private const float Normalization = 0.5f;
        private const float CellScale = 0.7f;
        private const float Gain = 0.5f;
        private const float Lacunarity = 2f;

        private uint seed;
        private float scale;
        private float strength;
        private int octaves;
        private float gullyWeight;
        private float detail;
        private float onset;
        private float assumedSlope;
        private float ridgeRounding;
        private float creaseRounding;
        private int valleyOctaves;
        private float peakLift;

        public ErosionFilter(int seed, float gullySize, float strength, int octaves, float gullyWeight, float detail, float onset,
            float assumedSlope, float ridgeRounding, float creaseRounding, int valleyOctaves, float peakLift = 1f)
        {
            this.seed = math.hash(new int2(seed, 0x3c1a));
            scale = math.max(1f, gullySize);
            this.strength = math.max(0f, strength);
            this.octaves = math.clamp(octaves, 1, 7);
            this.gullyWeight = math.saturate(gullyWeight);
            this.detail = math.max(0.1f, detail);
            this.onset = math.max(0.01f, onset);
            this.assumedSlope = math.max(0.01f, assumedSlope);
            this.ridgeRounding = math.max(0f, ridgeRounding);
            this.creaseRounding = math.max(0f, creaseRounding);
            this.valleyOctaves = math.clamp(valleyOctaves, 1, this.octaves);
            this.peakLift = math.saturate(peakLift);
        }

        /// <summary>True for settings that change nothing (no strength).</summary>
        public bool IsNone => strength <= 0f;

        public int ValleyOctaves => valleyOctaves;

        /// <summary>
        /// The height the filter moves a point by, in metres. <paramref name="slope"/> is the input height's
        /// gradient (rise over run); <paramref name="fadeTarget"/> -1 on valley floors to 1 on peaks.
        /// <paramref name="amount"/> scales the gullies' depth (0-1). <paramref name="ridge"/> is the ridge map,
        /// <paramref name="valley"/> the same from the biggest octaves only.
        /// Flat land moves by the fade target times the gullies' depth (peaks up, valley floors down).
        /// </summary>
        public float Offset(float2 position, float2 slope, float fadeTarget, float amount, out float ridge, out float valley)
        {
            fadeTarget = math.clamp(fadeTarget, -1f, 1f);
            // Summits rise by only part of the depth, so peaks stay inside the world.
            fadeTarget = fadeTarget > 0f ? fadeTarget * peakLift : fadeTarget;
            float s = strength * scale * math.saturate(amount);
            if (s <= 0f)
            {
                ridge = 0f;
                valley = 0f;
                return 0f;
            }
            float frequency = 1f / (scale * CellScale);
            float slopeLength = math.max(math.length(slope), 1e-6f);

            float rounding = math.lerp(creaseRounding, ridgeRounding, math.saturate(fadeTarget * 0.5f + 0.5f));
            float mask = EaseOut(SmoothStart(slopeLength * onset, rounding * onset));
            float ridgeMask = EaseOut(slopeLength * onset * 2f);
            float ridgeTarget = fadeTarget;
            float offset = 0f;
            float roundingScale = 1f;
            valley = 0f;
            // The slope that steers the gullies: the land's direction, at an assumed steepness.
            float2 gullySlope = slope / slopeLength * assumedSlope;

            for (int octave = 0; octave < octaves; octave++)
            {
                // Plains: nothing left to cut, and the rest would only add the fade target back.
                if (mask < 1e-4f && ridgeMask < 1e-4f)
                {
                    float rest = s * (1f - math.pow(Gain, octaves - octave)) / (1f - Gain);
                    offset += fadeTarget * rest;
                    if (octave < valleyOctaves)
                    {
                        valley = 0f;
                    }
                    break;
                }
                float4 stripes = Stripes(position * frequency, math.normalizesafe(gullySlope, new float2(1f, 0f)), CellScale);
                // The gully's slope per unit of its sine: d(cos phase)/dx = -sin * d(phase)/dx.
                float2 side = -stripes.zw * frequency;
                float sloping = math.abs(stripes.y);
                // Straight gullies: the steering slope counts a gully's side as evenly steep.
                gullySlope += math.sign(stripes.y) * side * s * gullyWeight;

                float gully = stripes.x;
                float faded = math.lerp(fadeTarget, gully * gullyWeight, mask);
                offset += faded * s;
                fadeTarget = faded;

                float octaveRounding = math.lerp(creaseRounding, ridgeRounding, math.saturate(gully * 0.5f + 0.5f)) * roundingScale;
                float newMask = EaseOut(SmoothStart(sloping * onset, octaveRounding * onset));
                mask = PowInverse(mask, detail) * newMask;

                ridgeTarget = math.lerp(ridgeTarget, gully, ridgeMask);
                ridgeMask *= EaseOut(sloping * onset * 1.2f);
                if (octave == valleyOctaves - 1)
                {
                    valley = ridgeTarget * (1f - ridgeMask);
                }

                s *= Gain;
                frequency *= Lacunarity;
                roundingScale *= Lacunarity;
            }
            ridge = ridgeTarget * (1f - ridgeMask);
            // Valleys count on sloping land only: plains fade to "valley floor" too, and every plain isn't a river's head.
            valley *= EaseOut(slopeLength * onset);
            // Flat land moves by its fade target (peaks up, valley floors down); the caller fades the target to 0 where
            // land should stay put.
            return offset;
        }

        // Stripes down the slope, blended from a 4 x 4 block of cells (Phacelle-style directional noise):
        // x the height (cosine, -1 to 1), y the slope's sine, zw the stripes' side direction (per cell unit).
        private float4 Stripes(float2 p, float2 direction, float stripeFrequency)
        {
            float2 sideDirection = new float2(-direction.y, direction.x) * stripeFrequency * (2f * math.PI);
            float2 cell = math.floor(p);
            float2 within = p - cell;
            float2 phase = float2.zero;
            float weightSum = 0f;
            for (int j = -1; j <= 2; j++)
            {
                for (int i = -1; i <= 2; i++)
                {
                    var offset = new float2(i, j);
                    uint h = math.hash(new int3((int)cell.x + i, (int)cell.y + j, (int)seed));
                    var pivot = new float2(h & 0xffff, h >> 16) * (0.5f / 65535f);
                    float2 fromPivot = within - offset - pivot;
                    float weight = math.max(0f, math.exp(-math.dot(fromPivot, fromPivot) * 2f) - 0.01111f);
                    weightSum += weight;
                    float wave = math.dot(fromPivot, sideDirection) + 0.5f * math.PI;
                    math.sincos(wave, out float sin, out float cos);
                    phase += new float2(cos, sin) * weight;
                }
            }
            float2 blended = phase / math.max(weightSum, 1e-6f);
            // Normalised gullies: blends longer than half are scaled back to full depth.
            float length = math.max(math.length(blended), 1f - Normalization);
            blended /= length;
            return new float4(blended, sideDirection);
        }

        private static float EaseOut(float t)
        {
            float v = 1f - math.saturate(t);
            return 1f - v * v;
        }

        // Slows the start of a ramp over its first r (a parabola, then the line r/2 lower), rounding the ridges or
        // creases it masks; still 0 at 0, so flat land stays untouched.
        private static float SmoothStart(float t, float r)
        {
            if (r <= 0f)
            {
                return t;
            }
            return t < r ? t * t / (2f * r) : t - r * 0.5f;
        }

        private static float PowInverse(float t, float power)
        {
            return 1f - math.pow(1f - math.saturate(t), power);
        }
    }
}
