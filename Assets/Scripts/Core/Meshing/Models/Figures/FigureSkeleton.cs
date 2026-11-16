using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// Where a figure's joints are (GL608), in metres at height 1, the origin between the feet and +z ahead: what
    /// <see cref="FigureMeshes"/> shapes the parts around and the rig hangs them from. Hips carry the legs and the torso;
    /// the torso carries the head and the arms; each limb bends once (elbow, knee).
    /// </summary>
    public static class FigureSkeleton
    {
        /// <summary>The hips above the ground when standing: thigh plus shin.</summary>
        public const float HipHeight = ThighLength + ShinLength;

        /// <summary>The hips above the ground when seated.</summary>
        public const float SeatHeight = 0.47f;

        public const float ThighLength = 0.4f;
        public const float ShinLength = 0.44f;
        public const float UpperArmLength = 0.27f;

        /// <summary>The neck's top above the hips, where the head turns.</summary>
        public const float NeckHeight = 0.56f;

        /// <summary>The top of a bare head above the ground at height 1.</summary>
        public const float Tall = 1.76f;

        /// <summary>The palm in the forearm's frame (the forearm hangs along -y): where a held prop goes.</summary>
        public static readonly Vector3 HandSocket = new Vector3(0f, -0.29f, 0.03f);

        /// <summary>A shoulder in the torso's frame; <paramref name="side"/> is -1 for the left, 1 for the right.</summary>
        public static Vector3 Shoulder(float build, int side)
        {
            return new Vector3(side * (0.2f * build + 0.045f), 0.45f, 0f);
        }

        /// <summary>A hip joint in the hips' frame.</summary>
        public static Vector3 Hip(float build, int side)
        {
            return new Vector3(side * 0.092f * build, 0f, 0f);
        }
    }
}
