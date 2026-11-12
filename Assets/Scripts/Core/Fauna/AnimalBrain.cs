using UnityEngine;

namespace Clube.Core
{
    /// <summary>What an animal is doing (GL606).</summary>
    public enum AnimalState
    {
        /// <summary>Standing with its head down, eating.</summary>
        Graze,

        /// <summary>Walking to another spot in the herd's range.</summary>
        Wander,

        /// <summary>Walking back to the herd, to rest there.</summary>
        Gather,

        /// <summary>Lying down with the herd.</summary>
        Rest,

        /// <summary>Running from the player or from a hit.</summary>
        Flee,

        Dead,
    }

    /// <summary>What an animal knows this tick: the hour, where it and its herd are, and where the player is.</summary>
    public struct AnimalSenses
    {
        /// <summary>Hour of the day, 0-24.</summary>
        public float Hour;

        /// <summary>Where it stands, on the map (x, z).</summary>
        public Vector2 Position;

        /// <summary>Where the herd lives: wandering stays within the species' range of it.</summary>
        public Vector2 Home;

        /// <summary>The middle of the herd's living animals: where they gather to rest.</summary>
        public Vector2 HerdCentre;

        /// <summary>True when the player is about; <see cref="Threat"/> is where.</summary>
        public bool HasThreat;

        public Vector2 Threat;
    }

    /// <summary>
    /// One animal's mind (GL606): its state, timers and decisions, with no GameObject, so it can be tested. Each tick it
    /// reads <see cref="AnimalSenses"/> and leaves a <see cref="Heading"/> and <see cref="Speed"/> for whatever moves the
    /// body (<c>Animal</c>, Game). By day (its species' active hours) it grazes and wanders within the herd's range; out of
    /// them it walks to the herd's middle and lies down. The player coming nearer than its skittishness, or a hit, makes it
    /// run; a hit also costs health (GL607), and at none it is dead.
    /// </summary>
    public sealed class AnimalBrain
    {
        /// <summary>Seconds it keeps running after the player is out of its sight.</summary>
        public const float FleeSeconds = 5f;

        /// <summary>A resting animal notices the player this much nearer than a grazing one.</summary>
        public const float RestingAlertness = 0.6f;

        private readonly System.Random random;
        private float timer;
        private Vector2 target;
        private Vector2 fleeFrom;
        private float swerve;
        private float swerveTimer;

        public AnimalBrain(AnimalSpecies species, int seed)
        {
            Species = species;
            Health = species.Health;
            random = new System.Random(seed);
            State = AnimalState.Graze;
            timer = Between(1f, 6f);
        }

        public AnimalSpecies Species { get; }

        public AnimalState State { get; private set; }

        public float Health { get; private set; }

        public bool IsDead => State == AnimalState.Dead;

        /// <summary>Which way to move on the map (x, z), a unit vector, or zero to stand.</summary>
        public Vector2 Heading { get; private set; }

        /// <summary>How fast to move along <see cref="Heading"/>, in metres a second.</summary>
        public float Speed { get; private set; }

        /// <summary>True while it lies down: resting, or dead.</summary>
        public bool IsLying => State == AnimalState.Rest || State == AnimalState.Dead;

        /// <summary>
        /// A hit of so much damage from a place (GL607): it dies at no health, and otherwise runs from where the hit came.
        /// True when this hit killed it.
        /// </summary>
        public bool Hit(float damage, Vector2 from)
        {
            if (IsDead)
            {
                return false;
            }
            Health -= Mathf.Max(0f, damage);
            if (Health <= 0f)
            {
                Health = 0f;
                State = AnimalState.Dead;
                Stand();
                return true;
            }
            StartFleeing(from);
            return false;
        }

        /// <summary>The body couldn't go where it was headed (water, a drop): it turns aside, or gives the walk up.</summary>
        public void Blocked()
        {
            if (State == AnimalState.Flee)
            {
                swerve = random.Next(2) == 0 ? 80f : -80f;
                swerveTimer = 1f;
            }
            else if (State == AnimalState.Wander || State == AnimalState.Gather)
            {
                // Gathering animals rest where they are stopped; the others eat.
                State = State == AnimalState.Gather ? AnimalState.Rest : AnimalState.Graze;
                timer = Between(2f, 6f);
                Stand();
            }
        }

        public void Tick(float deltaTime, in AnimalSenses senses)
        {
            if (IsDead)
            {
                return;
            }
            float alert = Species.Skittishness * (State == AnimalState.Rest ? RestingAlertness : 1f);
            bool near = senses.HasThreat && (senses.Threat - senses.Position).sqrMagnitude < alert * alert;
            if (near)
            {
                StartFleeing(senses.Threat);
            }
            timer -= deltaTime;
            switch (State)
            {
                case AnimalState.Flee:
                    Flee(deltaTime, senses);
                    break;
                case AnimalState.Graze:
                case AnimalState.Wander:
                    if (!Species.IsActiveAt(senses.Hour))
                    {
                        State = AnimalState.Gather;
                        break;
                    }
                    Roam(senses);
                    break;
                default:
                    if (Species.IsActiveAt(senses.Hour))
                    {
                        State = AnimalState.Graze;
                        timer = Between(1f, 4f);
                        Stand();
                        break;
                    }
                    Settle(senses);
                    break;
            }
        }

        private void StartFleeing(Vector2 from)
        {
            State = AnimalState.Flee;
            fleeFrom = from;
            timer = FleeSeconds;
        }

        private void Flee(float deltaTime, in AnimalSenses senses)
        {
            if (senses.HasThreat)
            {
                fleeFrom = senses.Threat;
            }
            if (timer <= 0f)
            {
                State = AnimalState.Graze;
                timer = Between(2f, 5f);
                Stand();
                return;
            }
            Vector2 away = senses.Position - fleeFrom;
            if (away.sqrMagnitude < 1e-4f)
            {
                away = Vector2.up;
            }
            swerveTimer -= deltaTime;
            if (swerveTimer > 0f)
            {
                float radians = swerve * Mathf.Deg2Rad, cos = Mathf.Cos(radians), sin = Mathf.Sin(radians);
                away = new Vector2(away.x * cos - away.y * sin, away.x * sin + away.y * cos);
            }
            Heading = away.normalized;
            Speed = Species.RunSpeed;
        }

        // Eats for a while, then walks to another spot near home; one that strayed walks back first (the herd keeps together).
        private void Roam(in AnimalSenses senses)
        {
            float range = Species.Range;
            if (State == AnimalState.Graze)
            {
                Stand();
                bool strayed = (senses.Position - senses.Home).sqrMagnitude > range * range;
                if (timer <= 0f || strayed)
                {
                    Vector2 around = strayed ? senses.Home : Vector2.Lerp(senses.Home, senses.HerdCentre, 0.5f);
                    float angle = Between(0f, Mathf.PI * 2f), far = Mathf.Sqrt(Between(0f, 1f)) * range * (strayed ? 0.4f : 0.8f);
                    target = around + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * far;
                    State = AnimalState.Wander;
                    timer = 40f;
                }
                return;
            }
            if (WalkTo(senses.Position, target, 0.5f) || timer <= 0f)
            {
                State = AnimalState.Graze;
                timer = Between(4f, 12f);
                Stand();
            }
        }

        // Walks to the herd's middle and lies down within a few lengths of it.
        private void Settle(in AnimalSenses senses)
        {
            float close = Mathf.Max(1.5f, Species.Length * 1.5f + Species.Herd.y * 0.3f);
            if (State == AnimalState.Rest)
            {
                Stand();
                if ((senses.Position - senses.HerdCentre).sqrMagnitude > close * close * 9f)
                {
                    State = AnimalState.Gather;
                }
                return;
            }
            State = AnimalState.Gather;
            if (WalkTo(senses.Position, senses.HerdCentre, close))
            {
                State = AnimalState.Rest;
                Stand();
            }
        }

        private bool WalkTo(Vector2 from, Vector2 to, float within)
        {
            Vector2 gap = to - from;
            if (gap.sqrMagnitude <= within * within)
            {
                return true;
            }
            Heading = gap.normalized;
            Speed = Species.WalkSpeed;
            return false;
        }

        private void Stand()
        {
            Heading = Vector2.zero;
            Speed = 0f;
        }

        private float Between(float from, float to)
        {
            return from + (float)random.NextDouble() * (to - from);
        }
    }
}
