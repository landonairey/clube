using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// GL719: the air's haze, which washes distant land out toward the fog colour so distance reads as scale. It owns the
    /// scene fog's mode and density (the day-night cycle owns its colour): exponential squared, so the land near the
    /// viewer keeps its colours and the haze builds with distance, set by <see cref="HalfDistance"/>, where land is half
    /// lost in it. Every world shader takes the scene fog (the terrain, coarse and far terrain, trees, stand-ins, water).
    /// </summary>
    public sealed class AirHaze : MonoBehaviour
    {
        public const float MinHalfDistance = 1000f;
        public const float MaxHalfDistance = 40000f;

        [Tooltip("Metres at which land is half lost in the haze: a peak about 1 km off keeps its colours, the next a few km off is muted, the lowland far below more so.")]
        [SerializeField, Range(MinHalfDistance, MaxHalfDistance)]
        private float halfDistance = 7000f;

        private bool saved;
        private bool sceneFog;
        private FogMode sceneMode;
        private float sceneDensity;

        public float HalfDistance
        {
            get => halfDistance;
            set => halfDistance = Mathf.Clamp(value, MinHalfDistance, MaxHalfDistance);
        }

        /// <summary>The exponential-squared fog density that hides half of what lies <paramref name="halfDistance"/> away.</summary>
        public static float DensityFor(float halfDistance)
        {
            return Mathf.Sqrt(Mathf.Log(2f)) / Mathf.Max(1f, halfDistance);
        }

        /// <summary>How much of what lies <paramref name="distance"/> away the haze hides, 0 to 1 (Unity's exponential squared fog).</summary>
        public static float HazeAt(float distance, float halfDistance)
        {
            float x = DensityFor(halfDistance) * distance;
            return 1f - Mathf.Exp(-x * x);
        }

        private void OnEnable()
        {
            sceneFog = RenderSettings.fog;
            sceneMode = RenderSettings.fogMode;
            sceneDensity = RenderSettings.fogDensity;
            saved = true;
            Apply();
        }

        private void OnDisable()
        {
            if (!saved)
            {
                return;
            }
            RenderSettings.fog = sceneFog;
            RenderSettings.fogMode = sceneMode;
            RenderSettings.fogDensity = sceneDensity;
            saved = false;
        }

        private void Update()
        {
            Apply();
        }

        private void Apply()
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = DensityFor(halfDistance);
        }
    }
}
