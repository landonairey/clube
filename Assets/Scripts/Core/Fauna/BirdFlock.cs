using System.Collections.Generic;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>How a group of birds flies while it's about (GL250).</summary>
    public enum FlightPattern
    {
        /// <summary>Straight over and away, wandering a little.</summary>
        Pass,

        /// <summary>Soaring round in wide circles, each bird on its own, as hawks on a thermal.</summary>
        Circle,

        /// <summary>A tight flock swirling and folding about a moving point, as starlings do.</summary>
        Swirl,

        /// <summary>A V, as geese fly: a leader and two trailing lines.</summary>
        Vee,
    }

    /// <summary>One bird: where it is, where it's going, and its wingbeat.</summary>
    public struct Bird
    {
        public Vector3 Position;
        public Vector3 Velocity;

        /// <summary>Where it is in its wingbeat, in beats.</summary>
        public float Beat;

        /// <summary>Wingbeats a second; 0 while it glides.</summary>
        public float BeatRate;

        /// <summary>Seconds left before it next switches between flapping and gliding.</summary>
        public float Switch;

        /// <summary>Its own share of the group's path: a phase round a circle, a place in a V.</summary>
        public float Offset;
    }

    /// <summary>
    /// A group of birds in the sky (GL250): one to a few dozen, flying a <see cref="FlightPattern"/> about a point for a
    /// while, then leaving in a direction until they're far out of sight (<see cref="IsGone"/>). Each bird steers like a
    /// boid: towards its place in the pattern, apart from the nearest birds, and along with them, at its own pace, turning
    /// smoothly and now and then gliding. Plain simulation; <c>BirdFlocks</c> spawns and draws them.
    /// </summary>
    public sealed class BirdFlock
    {
        private const float Separation = 2.2f;
        private const float TurnRate = 1.6f;

        private readonly List<Bird> birds = new List<Bird>();
        private readonly System.Random random;
        private Vector3 anchor;
        private readonly Vector3 drift;
        private readonly Vector3 exit;
        private readonly float radius;
        private readonly float speed;
        private float stay;
        private float time;

        /// <param name="anchor">The point the pattern is flown about, in world space.</param>
        /// <param name="entry">The direction the birds come from (they start far out along it).</param>
        /// <param name="exit">The direction they leave in.</param>
        /// <param name="stay">Seconds they fly the pattern before leaving.</param>
        public BirdFlock(FlightPattern pattern, int count, Vector3 anchor, Vector3 entry, Vector3 exit, float stay, int seed, float entryDistance = 220f)
        {
            Pattern = pattern;
            random = new System.Random(seed);
            this.anchor = anchor;
            this.exit = Flat(exit);
            this.stay = stay;
            Vector3 flatEntry = Flat(entry);
            drift = Vector3.Cross(Vector3.up, flatEntry) * Range(-0.8f, 0.8f);
            radius = pattern == FlightPattern.Circle ? Range(18f, 35f) : pattern == FlightPattern.Swirl ? Range(14f, 26f) : 0f;
            speed = pattern == FlightPattern.Circle ? Range(6f, 8f) : pattern == FlightPattern.Vee ? Range(11f, 13f) : Range(9f, 12f);
            Vector3 start = anchor + flatEntry * entryDistance;
            Vector3 heading = -flatEntry;
            for (int i = 0; i < Mathf.Max(1, count); i++)
            {
                float spread = pattern == FlightPattern.Swirl ? 6f : 10f;
                var bird = new Bird
                {
                    Position = start + new Vector3(Range(-spread, spread), Range(-spread, spread) * 0.4f, Range(-spread, spread)),
                    Velocity = heading * speed,
                    Beat = Range(0f, 1f),
                    BeatRate = Range(3f, 5f),
                    Switch = Range(0.5f, 3f),
                    Offset = count > 1 ? i / (float)count : 0f,
                };
                birds.Add(bird);
            }
        }

        public FlightPattern Pattern { get; }

        public IReadOnlyList<Bird> Birds => birds;

        /// <summary>True once they've flown the pattern and turned away.</summary>
        public bool IsLeaving => stay <= 0f;

        /// <summary>The middle of the group.</summary>
        public Vector3 Centre
        {
            get
            {
                Vector3 sum = Vector3.zero;
                foreach (Bird bird in birds)
                {
                    sum += bird.Position;
                }
                return birds.Count > 0 ? sum / birds.Count : anchor;
            }
        }

        /// <summary>True when every bird is more than <paramref name="distance"/> from <paramref name="viewer"/> on its way out.</summary>
        public bool IsGone(Vector3 viewer, float distance)
        {
            if (!IsLeaving)
            {
                return false;
            }
            float squared = distance * distance;
            foreach (Bird bird in birds)
            {
                if ((bird.Position - viewer).sqrMagnitude < squared)
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>Moves every bird on by <paramref name="deltaTime"/> seconds.</summary>
        public void Step(float deltaTime)
        {
            if (deltaTime <= 0f)
            {
                return;
            }
            time += deltaTime;
            stay -= deltaTime;
            anchor += drift * deltaTime;
            Vector3 lead = birds.Count > 0 ? birds[0].Position : anchor;
            Vector3 leadHeading = birds.Count > 0 && birds[0].Velocity.sqrMagnitude > 0.01f ? birds[0].Velocity.normalized : -exit;
            for (int i = 0; i < birds.Count; i++)
            {
                Bird bird = birds[i];
                Vector3 goal = GoalOf(i, bird, lead, leadHeading);
                Vector3 wanted = goal - bird.Position;
                float cruise = IsLeaving ? speed * 1.25f : speed;
                Vector3 desired = wanted.sqrMagnitude > 0.01f ? wanted.normalized * cruise : bird.Velocity;

                // Apart from close neighbours and along with them.
                Vector3 apart = Vector3.zero;
                Vector3 along = Vector3.zero;
                int near = 0;
                for (int j = 0; j < birds.Count; j++)
                {
                    if (j == i)
                    {
                        continue;
                    }
                    Vector3 offset = bird.Position - birds[j].Position;
                    float distance = offset.magnitude;
                    if (distance < Separation * 3f)
                    {
                        along += birds[j].Velocity;
                        near++;
                        if (distance < Separation && distance > 1e-3f)
                        {
                            apart += offset / distance * (Separation - distance);
                        }
                    }
                }
                if (near > 0 && Pattern != FlightPattern.Vee)
                {
                    desired = Vector3.Lerp(desired, along / near, Pattern == FlightPattern.Swirl ? 0.35f : 0.15f);
                }
                desired += apart * 3f;

                // Turn towards it smoothly; climb and dive gently.
                bird.Velocity = Vector3.Lerp(bird.Velocity, desired, 1f - Mathf.Exp(-TurnRate * deltaTime));
                bird.Velocity.y = Mathf.Clamp(bird.Velocity.y, -3f, 3f);
                bird.Position += bird.Velocity * deltaTime;

                // Flap a while, glide a while (circling birds mostly glide).
                bird.Switch -= deltaTime;
                if (bird.Switch <= 0f)
                {
                    bool glide = bird.BeatRate > 0f && Range(0f, 1f) < (Pattern == FlightPattern.Circle ? 0.8f : 0.35f);
                    bird.BeatRate = glide ? 0f : Range(3f, 5f);
                    bird.Switch = glide ? Range(1.5f, 4f) : Range(1f, 3f);
                }
                bird.Beat += bird.BeatRate * deltaTime;
                birds[i] = bird;
            }
        }

        // Where a bird is steering to now.
        private Vector3 GoalOf(int index, Bird bird, Vector3 lead, Vector3 leadHeading)
        {
            if (IsLeaving)
            {
                return bird.Position + exit * 50f + Vector3.up * 2f;
            }
            switch (Pattern)
            {
                case FlightPattern.Circle:
                {
                    // Each on its own circle round the anchor, a little apart in height.
                    float angle = time * speed / radius + bird.Offset * Mathf.PI * 2f;
                    float lift = (bird.Offset - 0.5f) * 8f + Mathf.Sin(time * 0.2f + bird.Offset * 6f) * 3f;
                    return anchor + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * (radius * (0.8f + bird.Offset * 0.4f)) + Vector3.up * lift;
                }
                case FlightPattern.Swirl:
                {
                    // A point folding round the anchor; the flock chases it and turns as one.
                    float t = time * speed / radius;
                    var swirl = new Vector3(Mathf.Sin(t * 0.9f), 0.25f * Mathf.Sin(t * 1.7f + 1f), Mathf.Sin(t * 0.6f + 2f) * Mathf.Cos(t * 0.35f));
                    return anchor + swirl * radius + new Vector3(Mathf.Sin(bird.Offset * 40f), 0f, Mathf.Cos(bird.Offset * 40f)) * 2.5f;
                }
                case FlightPattern.Vee:
                {
                    if (index == 0)
                    {
                        return anchor + exit * 400f + Vector3.up * Mathf.Sin(time * 0.3f) * 2f;
                    }
                    // Alternate sides, each a step further back and out.
                    int rank = (index + 1) / 2;
                    float side = index % 2 == 0 ? 1f : -1f;
                    Vector3 right = Vector3.Cross(Vector3.up, leadHeading).normalized;
                    return lead - leadHeading * (rank * 2.6f) + right * (side * rank * 2.2f);
                }
                default:
                    // Straight over, past the anchor and on.
                    return anchor + exit * 400f + new Vector3(Mathf.Sin(time * 0.4f + bird.Offset * 5f), 0.3f * Mathf.Sin(time * 0.3f), Mathf.Cos(time * 0.5f)) * 6f;
            }
        }

        private float Range(float min, float max)
        {
            return min + (float)random.NextDouble() * (max - min);
        }

        private static Vector3 Flat(Vector3 direction)
        {
            direction.y = 0f;
            return direction.sqrMagnitude > 1e-6f ? direction.normalized : Vector3.forward;
        }
    }
}
