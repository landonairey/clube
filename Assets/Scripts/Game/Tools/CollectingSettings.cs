using System;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>How a hit takes the ground (GL96), switchable in play to compare them.</summary>
    public enum CollectMode
    {
        /// <summary>Today's: the reached voxels' solid corners take damage over several hits and come away whole.</summary>
        VoxelHits,

        /// <summary>An area-of-impact sphere: each hit lowers every sample inside by the impact amount.</summary>
        SphereGradual,

        /// <summary>An area-of-impact sphere: each hit takes every solid sample inside at once.</summary>
        SphereWhole,
    }

    /// <summary>
    /// The collecting test's settings (GL96): which <see cref="CollectMode"/> the player's tools use,
    /// and for the sphere modes how big a sphere the hand and the tools reach and how much a hit
    /// takes from each sample inside.
    /// </summary>
    [Serializable]
    public class CollectingSettings
    {
        public const float MaxRadius = 2f;

        [Tooltip("How a hit takes the ground.")]
        public CollectMode Mode = CollectMode.SphereGradual;

        [Tooltip("Sphere modes: the hand takes only the voxel under the dot (GL106), not a sphere.")]
        public bool HandTakesAimedVoxel = true;

        [Tooltip("Sphere modes: the hand's area of impact, radius in metres.")]
        [Range(0.05f, MaxRadius)]
        public float HandRadius = 0.3f;

        [Tooltip("Sphere modes: a tool's area of impact, radius in metres.")]
        [Range(0.05f, MaxRadius)]
        public float ToolRadius = 0.6f;

        [Tooltip("Gradual sphere: how much density a hand hit takes from each sample (a whole sample is 1).")]
        [Range(0.01f, 1f)]
        public float HandAmount = 0.2f;

        [Tooltip("Gradual sphere: how much density a tool hit takes from each sample.")]
        [Range(0.01f, 1f)]
        public float ToolAmount = 1f;

        public bool UsesSphere => Mode != CollectMode.VoxelHits;
    }
}
