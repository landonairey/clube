using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// The rules of the player's slide (GL219, GL247, GL872), pure so they can be tested: when one starts, how fast, how
    /// it slows or speeds up on a slope and when it ends. A slide starts from the speed the player carries (a sprint, or
    /// the run-up of a jump out of an earlier slide), never from the crouch walk, so chaining jumps and slides downhill
    /// keeps the speed, up to <see cref="Tuning.TopSpeed"/>.
    /// </summary>
    public static class SlideMotion
    {
        /// <summary>A slide never goes faster than this many times its start (downhill).</summary>
        public const float StartGrowth = 1.6f;

        /// <summary>Ground falling at least this steeply (metres per metre) is downhill: there a slide's distance isn't counted.</summary>
        public const float DownhillRise = -0.05f;

        /// <summary>Without Sprint held, a slide still starts when the carried speed is at least this share of a sprint (a chained slide).</summary>
        public const float CarriedSprintShare = 0.9f;

        /// <summary>The player's speeds and the slide's limits, in metres per second and metres per metre.</summary>
        public struct Tuning
        {
            public float WalkSpeed;
            public float SprintSpeed;
            public float CrouchSpeed;
            /// <summary>A slide from a sprint starts this much faster.</summary>
            public float Boost;
            /// <summary>Slowing on level ground, metres per second squared.</summary>
            public float Drag;
            public float Gravity;
            /// <summary>The steepest rise a slide starts up or keeps going up.</summary>
            public float SteepestRise;
            /// <summary>The longest a slide goes on level ground or uphill, in metres.</summary>
            public float MaxDistance;
            /// <summary>The fastest any slide goes, chained or not.</summary>
            public float TopSpeed;
        }

        /// <summary>
        /// True when a slide starts this frame: Crouch held (pressed now, or still held on landing), on or just off the
        /// ground, faster than a walk, sprinting or carrying a sprint's speed, on ground that isn't too steep a rise.
        /// <paramref name="spent"/> is true after a slide ended on the ground with Crouch still held, so holding it doesn't
        /// start slide after slide on its own.
        /// </summary>
        public static bool Starts(in Tuning t, bool crouchHeld, bool spent, bool nearGround, bool sprinting, float carriedSpeed, float rise)
        {
            return crouchHeld && !spent && nearGround && carriedSpeed > t.WalkSpeed
                && (sprinting || carriedSpeed >= t.SprintSpeed * CarriedSprintShare)
                && rise <= t.SteepestRise;
        }

        /// <summary>The speed a slide starts at: a sprint's boost, or the carried speed when that's faster, never past the top speed.</summary>
        public static float StartSpeed(in Tuning t, float carriedSpeed)
        {
            return Mathf.Min(t.TopSpeed, Mathf.Max(carriedSpeed, t.SprintSpeed * t.Boost));
        }

        /// <summary>
        /// One step of a slide: it slows by its drag, less downhill (or speeds up), more uphill, never past
        /// <see cref="StartGrowth"/> times its start or the top speed.
        /// </summary>
        public static float Step(in Tuning t, float speed, float startSpeed, float rise, float deltaTime)
        {
            float slowing = t.Drag + t.Gravity * rise;
            float top = Mathf.Min(t.TopSpeed, startSpeed * StartGrowth);
            return Mathf.Min(top, speed - slowing * deltaTime);
        }

        /// <summary>The distance a step adds towards the slide's limit: none downhill, so a slide down a slope goes on while Crouch is held.</summary>
        public static float CountedDistance(float speed, float rise, float deltaTime)
        {
            return rise <= DownhillRise ? 0f : Mathf.Max(0f, speed) * deltaTime;
        }

        /// <summary>
        /// True when the slide ends: down to a crouch walk or its level distance used up. A rise doesn't end it on the spot
        /// (a bump in the voxel ground did, dropping a fast slide to the crouch walk): <see cref="Step"/> slows it uphill.
        /// </summary>
        public static bool Ends(in Tuning t, float speed, float distance)
        {
            return speed <= t.CrouchSpeed || distance >= t.MaxDistance;
        }
    }
}
