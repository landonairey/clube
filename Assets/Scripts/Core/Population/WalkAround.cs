using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// A figure's step towards a point, going round someone standing in the way (GL740: "have them go around you"):
    /// it bends off its line as it comes near, keeps to the side it is already on, and never steps inside
    /// the clearance. Flat (x, z); plain maths so it can be tested.
    /// </summary>
    public static class WalkAround
    {
        // Metres ahead of the clearance at which a figure starts to bend off its line.
        private const float LookAhead = 1.5f;

        /// <summary>True when a point is too close to the one to go round to be stood on: a figure counts it as reached from beside them.</summary>
        public static bool IsBlocked(Vector2 point, Vector2 other, float clearance)
        {
            return (point - other).sqrMagnitude < clearance * clearance;
        }

        /// <summary>Where a figure at <paramref name="at"/> is after a step of <paramref name="stride"/> metres towards <paramref name="target"/>.</summary>
        public static Vector2 Step(Vector2 at, Vector2 target, Vector2 other, float clearance, float stride)
        {
            Vector2 line = target - at;
            float left = line.magnitude;
            if (left < 1e-4f)
            {
                return at;
            }
            Vector2 heading = line / left;
            Vector2 toOther = other - at;
            float ahead = Vector2.Dot(toOther, heading);
            var side = new Vector2(-heading.y, heading.x);
            float across = Vector2.Dot(toOther, side);
            Vector2 direction = heading;
            if (ahead > 0f && ahead < clearance + LookAhead && Mathf.Abs(across) < clearance)
            {
                // Away from them, on the side the figure is already on.
                direction = (heading * 0.4f - side * (across > 0f ? 1f : -1f)).normalized;
            }
            Vector2 next = at + direction * Mathf.Min(stride, left);
            Vector2 away = next - other;
            float distance = away.magnitude;
            if (distance >= clearance)
            {
                return next;
            }
            // Slide round the edge of the clearance rather than through it.
            away = distance > 1e-4f ? away / distance : -heading;
            return other + away * clearance;
        }
    }
}
