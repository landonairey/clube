using System;

namespace Clube.Core
{
    /// <summary>The ground a footstep lands on (GL271).</summary>
    public enum StepSurface
    {
        Grass,
        Dirt,
        Stone,
        Clay,
    }

    /// <summary>What a tool hit sounds like (GL271).</summary>
    public enum HitSound
    {
        /// <summary>A metal head on stone: a ringing clink.</summary>
        Clink,

        /// <summary>A stone head on stone: a dull knock.</summary>
        Knock,

        /// <summary>An axe into wood: a hollow thunk.</summary>
        Chop,

        /// <summary>A shovel into soil: a gritty scrape.</summary>
        Scrape,

        /// <summary>A chisel: a short bright tap.</summary>
        Tap,

        /// <summary>A bare hand: a soft slap.</summary>
        Slap,

        /// <summary>A pick into rock or ore: a sharp crack, then grit crushing and gravel falling (GL280).</summary>
        Crunch,
    }

    /// <summary>
    /// Sound effects made from scratch (GL271): footsteps, tool hits, ground breaking and gathering, birdsong, and
    /// looping wind and rustling leaves, built from noise, filters, enveloped tones and sweeps, each varied by a seed.
    /// Mono samples at <see cref="SampleRate"/>, peaks at most 1; <see cref="ToWav"/> writes them as a WAV file.
    /// </summary>
    /// <remarks>Plain math; *Clube → Audio → Generate sound effects* writes the game's clips with it.</remarks>
    public static class SoundSynth
    {
        public const int SampleRate = 44100;
        private const float TwoPi = (float)(Math.PI * 2d);

        /// <summary>One footstep: a heel, then the rest of the foot.</summary>
        public static float[] Footstep(StepSurface surface, int seed)
        {
            var random = new Random(seed);
            float[] samples = new float[Samples(0.26f)];
            float heel = 0f, roll = Range(random, 0.035f, 0.06f);
            switch (surface)
            {
                case StepSurface.Grass:
                    // A crunchy rustle: bright noise, a few crackles, a little weight underneath.
                    AddNoise(samples, random, heel, 0.002f, 0.05f, 0.5f, Band(2600f, 0.8f));
                    AddNoise(samples, random, roll, 0.004f, 0.07f, 0.35f, Band(3800f, 0.9f));
                    AddCrackles(samples, random, heel, 0.12f, 14, 0.25f, 4000f);
                    AddNoise(samples, random, heel, 0.002f, 0.04f, 0.3f, Low(500f));
                    break;
                case StepSurface.Dirt:
                    AddNoise(samples, random, heel, 0.002f, 0.06f, 0.6f, Low(700f));
                    AddTone(samples, heel, 75f, 60f, 0.002f, 0.05f, 0.45f);
                    AddNoise(samples, random, roll, 0.003f, 0.06f, 0.3f, Band(1500f, 0.7f));
                    break;
                case StepSurface.Stone:
                    AddNoise(samples, random, heel, 0.0008f, 0.02f, 0.55f, Band(2200f, 1.4f));
                    AddTone(samples, heel, 140f, 110f, 0.001f, 0.03f, 0.3f);
                    AddNoise(samples, random, roll, 0.004f, 0.05f, 0.35f, Band(3500f, 0.8f));
                    AddCrackles(samples, random, roll, 0.06f, 5, 0.2f, 5000f);
                    break;
                default:
                    // Clay: a soft squelch, its pitch rising as the foot lifts.
                    AddNoise(samples, random, heel, 0.003f, 0.07f, 0.55f, Low(450f));
                    AddSweptNoise(samples, random, roll, 0.12f, 350f, 1100f, 0.35f);
                    break;
            }
            return Normalize(samples, 0.9f);
        }

        /// <summary>One tool hit.</summary>
        public static float[] Hit(HitSound kind, int seed)
        {
            var random = new Random(seed);
            float[] samples;
            switch (kind)
            {
                case HitSound.Clink:
                {
                    samples = new float[Samples(0.55f)];
                    float f0 = Range(random, 950f, 1300f);
                    // Inharmonic partials, as a struck bar rings.
                    float[] ratios = { 1f, 2.76f, 5.4f, 8.93f };
                    float[] decays = { 0.28f, 0.16f, 0.08f, 0.05f };
                    float[] levels = { 0.5f, 0.35f, 0.22f, 0.12f };
                    for (int i = 0; i < ratios.Length; i++)
                    {
                        AddTone(samples, 0f, f0 * ratios[i], f0 * ratios[i], 0.0005f, decays[i], levels[i]);
                    }
                    AddNoise(samples, random, 0f, 0.0004f, 0.006f, 0.6f, Band(3200f, 1.2f));
                    AddTone(samples, 0f, 160f, 120f, 0.001f, 0.04f, 0.3f);
                    AddCrackles(samples, random, 0.01f, 0.15f, 8, 0.12f, 5000f);
                    break;
                }
                case HitSound.Knock:
                    samples = new float[Samples(0.4f)];
                    AddNoise(samples, random, 0f, 0.0006f, 0.04f, 0.6f, Band(Range(random, 700f, 1000f), 2f));
                    AddTone(samples, 0f, Range(random, 200f, 260f), 170f, 0.001f, 0.06f, 0.45f);
                    AddCrackles(samples, random, 0.01f, 0.2f, 12, 0.2f, 4000f);
                    break;
                case HitSound.Chop:
                    // An axe biting wood (GL303): a sharp, dry crack as the edge goes in, the splintering of fibres
                    // right after, and only a short, dull knock of the trunk under it, so it doesn't boom like a drum.
                    samples = new float[Samples(0.35f)];
                    AddNoise(samples, random, 0f, 0.0002f, 0.006f, 0.8f, Band(Range(random, 2200f, 3000f), 1.3f));
                    AddNoise(samples, random, 0f, 0.0004f, 0.018f, 0.5f, Band(Range(random, 900f, 1300f), 1.6f));
                    AddCrackles(samples, random, 0.004f, 0.07f, 26, 0.3f, 2600f);
                    AddCrackles(samples, random, 0.02f, 0.12f, 10, 0.16f, 4200f);
                    AddTone(samples, 0f, Range(random, 210f, 260f), 170f, 0.0008f, 0.018f, 0.28f);
                    break;
                case HitSound.Scrape:
                    samples = new float[Samples(0.4f)];
                    AddNoise(samples, random, 0f, 0.015f, 0.12f, 0.5f, Band(Range(random, 1000f, 1400f), 0.9f));
                    AddCrackles(samples, random, 0.01f, 0.2f, 20, 0.18f, 3000f);
                    AddNoise(samples, random, 0f, 0.002f, 0.05f, 0.4f, Low(400f));
                    break;
                case HitSound.Tap:
                    samples = new float[Samples(0.3f)];
                    float tap = Range(random, 1900f, 2400f);
                    AddTone(samples, 0f, tap, tap, 0.0003f, 0.07f, 0.45f);
                    AddTone(samples, 0f, tap * 2.76f, tap * 2.76f, 0.0003f, 0.03f, 0.25f);
                    AddNoise(samples, random, 0f, 0.0003f, 0.005f, 0.5f, Band(4500f, 1f));
                    AddCrackles(samples, random, 0.005f, 0.08f, 5, 0.12f, 6000f);
                    break;
                case HitSound.Crunch:
                {
                    samples = new float[Samples(0.6f)];
                    // The point going in: a hard crack with a little weight under it.
                    AddNoise(samples, random, 0f, 0.0003f, 0.012f, 0.75f, Band(Range(random, 1500f, 2100f), 1.1f));
                    AddTone(samples, 0f, Range(random, 150f, 190f), 95f, 0.0008f, 0.045f, 0.4f);
                    AddNoise(samples, random, 0f, 0.001f, 0.05f, 0.35f, Low(420f));
                    // The rock giving way: dense grit, coarse then finer, and a few chips skittering after.
                    AddCrackles(samples, random, 0.004f, 0.12f, 70, 0.32f, 1800f);
                    AddCrackles(samples, random, 0.02f, 0.25f, 50, 0.22f, 3600f);
                    AddNoise(samples, random, 0.01f, 0.008f, 0.09f, 0.25f, Band(Range(random, 900f, 1300f), 0.7f));
                    AddCrackles(samples, random, 0.12f, 0.35f, 14, 0.16f, 5000f);
                    break;
                }
                default:
                    samples = new float[Samples(0.25f)];
                    AddNoise(samples, random, 0f, 0.002f, 0.04f, 0.6f, Low(900f));
                    AddNoise(samples, random, 0.01f, 0.003f, 0.05f, 0.25f, Band(2500f, 0.8f));
                    break;
            }
            return Normalize(samples, 0.9f);
        }

        /// <summary>A swing through the air (GL302): a soft rush of wind rising and falling.</summary>
        public static float[] Whoosh(int seed)
        {
            var random = new Random(seed);
            float[] samples = new float[Samples(0.32f)];
            AddSweptNoise(samples, random, 0f, 0.3f, Range(random, 350f, 500f), Range(random, 1400f, 1900f), 0.5f);
            AddNoise(samples, random, 0.05f, 0.08f, 0.06f, 0.25f, Low(600f));
            return Normalize(samples, 0.7f);
        }

        /// <summary>A tool that can't work what it hit (GL302): a short, low "err" buzz, falling.</summary>
        public static float[] Err(int seed)
        {
            var random = new Random(seed);
            float[] samples = new float[Samples(0.26f)];
            float pitch = Range(random, 120f, 140f);
            for (int harmonic = 1; harmonic <= 5; harmonic++)
            {
                AddTone(samples, 0f, pitch * harmonic, pitch * 0.85f * harmonic, 0.01f, 0.07f, 0.5f / harmonic);
            }
            AddNoise(samples, random, 0f, 0.005f, 0.05f, 0.12f, Band(pitch * 3f, 2f));
            return Normalize(samples, 0.75f);
        }

        /// <summary>Rock coming away: a crumble of grit falling.</summary>
        public static float[] Crumble(int seed)
        {
            var random = new Random(seed);
            float[] samples = new float[Samples(0.6f)];
            // Chunks cracking off, then gravel pouring and settling: crunchier than grit alone (GL280).
            AddNoise(samples, random, 0f, 0.0004f, 0.015f, 0.5f, Band(Range(random, 1200f, 1700f), 1f));
            AddCrackles(samples, random, 0f, 0.2f, 80, 0.38f, 2000f);
            AddCrackles(samples, random, 0.03f, 0.45f, 70, 0.28f, 3800f);
            AddNoise(samples, random, 0f, 0.01f, 0.18f, 0.3f, Low(350f));
            return Normalize(samples, 0.85f);
        }

        /// <summary>Soft ground coming away: a thud of earth.</summary>
        public static float[] Thud(int seed)
        {
            var random = new Random(seed);
            float[] samples = new float[Samples(0.4f)];
            AddNoise(samples, random, 0f, 0.004f, 0.1f, 0.6f, Low(320f));
            AddTone(samples, 0f, 65f, 50f, 0.003f, 0.09f, 0.5f);
            AddCrackles(samples, random, 0.02f, 0.2f, 10, 0.12f, 2000f);
            return Normalize(samples, 0.85f);
        }

        /// <summary>Leaves and twigs: a dry rustle.</summary>
        public static float[] Rustle(int seed, float seconds = 0.45f)
        {
            var random = new Random(seed);
            float[] samples = new float[Samples(seconds)];
            float[] noise = Noise(random, samples.Length);
            Filter(noise, Band(4200f, 0.7f));
            for (int i = 0; i < samples.Length; i++)
            {
                float t = (float)i / samples.Length;
                float envelope = (float)Math.Sin(Math.PI * Math.Min(1f, t * 1.4f)) * (1f - t * 0.3f);
                samples[i] = noise[i] * envelope;
            }
            Flutter(samples, random, 28f);
            AddCrackles(samples, random, 0f, seconds * 0.8f, 18, 0.2f, 5000f);
            return Normalize(samples, 0.8f);
        }

        /// <summary>Something going into the pack: a soft rising pop.</summary>
        public static float[] Pickup(int seed)
        {
            var random = new Random(seed);
            float[] samples = new float[Samples(0.16f)];
            float start = Range(random, 380f, 460f);
            AddTone(samples, 0f, start, start * 2.1f, 0.004f, 0.05f, 0.5f);
            AddNoise(samples, random, 0f, 0.001f, 0.02f, 0.15f, Band(2000f, 1f));
            return Normalize(samples, 0.6f);
        }

        /// <summary>A bird's call: a few quick chirps, a trill or a whistled phrase, by the seed.</summary>
        public static float[] Chirp(int seed)
        {
            var random = new Random(seed);
            int style = random.Next(3);
            float[] samples = new float[Samples(1.4f)];
            float at = 0.02f;
            int notes = style == 1 ? random.Next(6, 11) : random.Next(2, 6);
            float baseFrequency = Range(random, 2600f, 4200f);
            for (int n = 0; n < notes && at < 1.2f; n++)
            {
                float length = style == 1 ? Range(random, 0.025f, 0.04f) : style == 2 ? Range(random, 0.12f, 0.25f) : Range(random, 0.04f, 0.09f);
                float from = baseFrequency * Range(random, 0.85f, 1.1f);
                float to = style == 2 ? from * Range(random, 0.7f, 1.4f) : from * Range(random, 1.15f, 1.6f);
                AddSweep(samples, at, length, from, to, Range(random, 0.5f, 0.8f));
                at += length + (style == 1 ? Range(random, 0.012f, 0.025f) : Range(random, 0.04f, 0.12f));
            }
            return Normalize(samples, 0.7f);
        }

        /// <summary>A breeze that loops without a seam: low rushing noise in slow gusts, with a faint whistle on top.</summary>
        public static float[] WindLoop(float seconds, int seed)
        {
            var random = new Random(seed);
            int length = Samples(seconds);
            int fade = Samples(1.5f);
            float[] samples = new float[length + fade];
            float brown = 0f;
            float[] whistle = Noise(random, samples.Length);
            Filter(whistle, Band(Range(random, 700f, 1000f), 6f));
            float phaseA = Range(random, 0f, 6f), phaseB = Range(random, 0f, 6f);
            for (int i = 0; i < samples.Length; i++)
            {
                float t = (float)i / SampleRate;
                brown = brown * 0.996f + Noise(random) * 0.06f;
                float gust = 0.55f + 0.3f * (float)Math.Sin(TwoPi * 0.11f * t + phaseA) + 0.15f * (float)Math.Sin(TwoPi * 0.27f * t + phaseB);
                samples[i] = brown * gust + whistle[i] * 0.25f * gust * gust;
            }
            Filter(samples, Low(650f));
            return Normalize(Loop(samples, length, fade), 0.7f);
        }

        /// <summary>Leaves rustling in the wind, looping without a seam: dry flutter swelling with the gusts.</summary>
        public static float[] LeavesLoop(float seconds, int seed)
        {
            var random = new Random(seed);
            int length = Samples(seconds);
            int fade = Samples(1f);
            float[] samples = Noise(random, length + fade);
            Filter(samples, Band(4500f, 0.6f));
            float phaseA = Range(random, 0f, 6f), phaseB = Range(random, 0f, 6f);
            for (int i = 0; i < samples.Length; i++)
            {
                float t = (float)i / SampleRate;
                float gust = 0.5f + 0.35f * (float)Math.Sin(TwoPi * 0.17f * t + phaseA) + 0.15f * (float)Math.Sin(TwoPi * 0.41f * t + phaseB);
                samples[i] *= gust;
            }
            Flutter(samples, random, 22f);
            return Normalize(Loop(samples, length, fade), 0.6f);
        }

        /// <summary>The samples as a 16-bit mono WAV file.</summary>
        public static byte[] ToWav(float[] samples)
        {
            int dataBytes = samples.Length * 2;
            var bytes = new byte[44 + dataBytes];
            void Text(int at, string s)
            {
                for (int i = 0; i < 4; i++)
                {
                    bytes[at + i] = (byte)s[i];
                }
            }
            void Int(int at, int value) => BitConverter.GetBytes(value).CopyTo(bytes, at);
            void Short(int at, short value) => BitConverter.GetBytes(value).CopyTo(bytes, at);
            Text(0, "RIFF");
            Int(4, 36 + dataBytes);
            Text(8, "WAVE");
            Text(12, "fmt ");
            Int(16, 16);
            Short(20, 1);
            Short(22, 1);
            Int(24, SampleRate);
            Int(28, SampleRate * 2);
            Short(32, 2);
            Short(34, 16);
            Text(36, "data");
            Int(40, dataBytes);
            for (int i = 0; i < samples.Length; i++)
            {
                float clamped = Math.Max(-1f, Math.Min(1f, samples[i]));
                Short(44 + i * 2, (short)Math.Round(clamped * 32767f));
            }
            return bytes;
        }

        /// <summary>Scales the samples so their loudest is <paramref name="peak"/>.</summary>
        public static float[] Normalize(float[] samples, float peak)
        {
            float loudest = 0f;
            foreach (float s in samples)
            {
                loudest = Math.Max(loudest, Math.Abs(s));
            }
            if (loudest > 1e-6f)
            {
                float scale = peak / loudest;
                for (int i = 0; i < samples.Length; i++)
                {
                    samples[i] *= scale;
                }
            }
            return samples;
        }

        // ---- Building blocks.

        private struct Biquad
        {
            public float B0, B1, B2, A1, A2;
        }

        private static int Samples(float seconds)
        {
            return Math.Max(1, (int)(seconds * SampleRate));
        }

        private static float Range(Random random, float min, float max)
        {
            return min + (float)random.NextDouble() * (max - min);
        }

        private static float Noise(Random random)
        {
            return (float)random.NextDouble() * 2f - 1f;
        }

        private static float[] Noise(Random random, int length)
        {
            var noise = new float[length];
            for (int i = 0; i < length; i++)
            {
                noise[i] = Noise(random);
            }
            return noise;
        }

        // A band-pass filter round a centre frequency (RBJ's cookbook).
        private static Biquad Band(float centre, float q)
        {
            float w = TwoPi * centre / SampleRate;
            float alpha = (float)Math.Sin(w) / (2f * q);
            float a0 = 1f + alpha;
            return new Biquad { B0 = alpha / a0, B1 = 0f, B2 = -alpha / a0, A1 = -2f * (float)Math.Cos(w) / a0, A2 = (1f - alpha) / a0 };
        }

        // A low-pass filter.
        private static Biquad Low(float cutoff)
        {
            float w = TwoPi * cutoff / SampleRate;
            float alpha = (float)Math.Sin(w) / (2f * 0.707f);
            float cos = (float)Math.Cos(w);
            float a0 = 1f + alpha;
            return new Biquad { B0 = (1f - cos) * 0.5f / a0, B1 = (1f - cos) / a0, B2 = (1f - cos) * 0.5f / a0, A1 = -2f * cos / a0, A2 = (1f - alpha) / a0 };
        }

        private static void Filter(float[] samples, Biquad f)
        {
            float x1 = 0f, x2 = 0f, y1 = 0f, y2 = 0f;
            for (int i = 0; i < samples.Length; i++)
            {
                float x = samples[i];
                float y = f.B0 * x + f.B1 * x1 + f.B2 * x2 - f.A1 * y1 - f.A2 * y2;
                x2 = x1;
                x1 = x;
                y2 = y1;
                y1 = y;
                samples[i] = y;
            }
        }

        // Rises over attack seconds and dies away over decay seconds.
        private static float Envelope(float t, float attack, float decay)
        {
            return t < 0f ? 0f : (1f - (float)Math.Exp(-t / Math.Max(attack, 1e-5f))) * (float)Math.Exp(-t / Math.Max(decay, 1e-5f));
        }

        // Filtered noise, shaped by an envelope, added from <paramref name="at"/> seconds.
        private static void AddNoise(float[] samples, Random random, float at, float attack, float decay, float level, Biquad filter)
        {
            int start = Samples(at);
            int length = Math.Min(samples.Length - start, Samples(attack + decay * 6f));
            if (length <= 0)
            {
                return;
            }
            float[] noise = Noise(random, length);
            Filter(noise, filter);
            float gain = level * 4f;
            for (int i = 0; i < length; i++)
            {
                samples[start + i] += noise[i] * gain * Envelope((float)i / SampleRate, attack, decay);
            }
        }

        // A tone gliding from one pitch to another under an envelope.
        private static void AddTone(float[] samples, float at, float from, float to, float attack, float decay, float level)
        {
            int start = Samples(at);
            int length = Math.Min(samples.Length - start, Samples(attack + decay * 6f));
            float phase = 0f;
            for (int i = 0; i < length; i++)
            {
                float t = (float)i / SampleRate;
                float frequency = to + (from - to) * (float)Math.Exp(-t / Math.Max(decay, 1e-4f));
                phase += TwoPi * frequency / SampleRate;
                samples[start + i] += (float)Math.Sin(phase) * level * Envelope(t, attack, decay);
            }
        }

        // A whistled note sweeping from one pitch to another, with a little second harmonic.
        private static void AddSweep(float[] samples, float at, float length, float from, float to, float level)
        {
            int start = Samples(at);
            int count = Math.Min(samples.Length - start, Samples(length));
            float phase = 0f;
            for (int i = 0; i < count; i++)
            {
                float t = (float)i / count;
                float frequency = from + (to - from) * t * t * (3f - 2f * t);
                phase += TwoPi * frequency / SampleRate;
                float envelope = (float)Math.Sin(Math.PI * t);
                samples[start + i] += ((float)Math.Sin(phase) + 0.2f * (float)Math.Sin(phase * 2f)) * level * envelope * envelope;
            }
        }

        // Noise through a band that sweeps up: a squelch.
        private static void AddSweptNoise(float[] samples, Random random, float at, float length, float from, float to, float level)
        {
            int start = Samples(at);
            int count = Math.Min(samples.Length - start, Samples(length));
            int pieces = 8;
            for (int p = 0; p < pieces; p++)
            {
                float centre = from + (to - from) * p / (pieces - 1f);
                AddNoise(samples, random, at + length * p / pieces, 0.002f, length / pieces, level * (1f - p / (float)pieces), Band(centre, 1.5f));
            }
        }

        // Tiny clicks scattered over a time span, fading: grit, crackling leaves.
        private static void AddCrackles(float[] samples, Random random, float at, float span, int count, float level, float brightness)
        {
            for (int c = 0; c < count; c++)
            {
                float when = at + span * (float)Math.Pow(random.NextDouble(), 1.6);
                float fade = 1f - (when - at) / Math.Max(span, 1e-4f) * 0.7f;
                AddNoise(samples, random, when, 0.0002f, Range(random, 0.0008f, 0.003f), level * fade * Range(random, 0.4f, 1f),
                    Band(brightness * Range(random, 0.6f, 1.4f), 1.2f));
            }
        }

        // Quick random swells in loudness: leaves fluttering.
        private static void Flutter(float[] samples, Random random, float rate)
        {
            float level = 1f, target = 1f;
            int step = Math.Max(1, (int)(SampleRate / rate));
            for (int i = 0; i < samples.Length; i++)
            {
                if (i % step == 0)
                {
                    target = Range(random, 0.25f, 1f);
                }
                level += (target - level) * 0.002f;
                samples[i] *= level;
            }
        }

        // The first <paramref name="length"/> samples, with the last <paramref name="fade"/> crossfaded over the start.
        private static float[] Loop(float[] samples, int length, int fade)
        {
            var loop = new float[length];
            Array.Copy(samples, loop, length);
            for (int i = 0; i < fade && i < length; i++)
            {
                float t = (float)i / fade;
                loop[i] = samples[i] * (float)Math.Sqrt(t) + samples[length + i] * (float)Math.Sqrt(1f - t);
            }
            return loop;
        }
    }
}
