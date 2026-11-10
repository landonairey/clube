using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// The night sky's stars (GL253): a sphere round the camera drawn by <c>Clube/Stars</c>, a few thousand stars of
    /// different brightness and colour and a faint Milky Way, fixed on the celestial sphere. <see cref="DayNightCycle"/>
    /// turns it about the celestial pole once a sidereal day (<see cref="Clube.Core.SkyPositions.SiderealAngle"/>), so
    /// the stars wheel round the pole star through the night and rise a little earlier each night, and fades them in as
    /// the daylight goes. Drawn after the sky and before the moon, without depth, so the ground hides them.
    /// </summary>
    public class StarSky : MonoBehaviour
    {
        private static readonly int StarXId = Shader.PropertyToID("_StarX");
        private static readonly int StarYId = Shader.PropertyToID("_StarY");
        private static readonly int StarZId = Shader.PropertyToID("_StarZ");
        private static readonly int NightId = Shader.PropertyToID("_Night");

        [Tooltip("The Clube/Stars material.")]
        [SerializeField]
        private Material material;

        [Tooltip("How far out the stars are drawn, as a share of the camera's far plane: behind the moon.")]
        [SerializeField, Range(0.5f, 0.99f)]
        private float distanceShare = 0.95f;

        [Tooltip("Seconds between shooting stars at night: the least and the most (GL495).")]
        [SerializeField]
        private Vector2 shootingStarGap = new Vector2(60f, 180f);

        private const float StreakSeconds = 0.8f;

        private GameObject sphere;
        private Material instance;
        private float night;
        private LineRenderer streak;
        private float nextStreakAt = -1f;
        private float streakStart;
        private Vector3 streakFrom;
        private Vector3 streakTo;

        /// <summary>
        /// Turns the stars: <paramref name="x"/>, <paramref name="y"/> and <paramref name="pole"/> are world directions
        /// of right ascension 0 and 90 on the celestial equator and of the celestial pole. <paramref name="night"/> is
        /// how dark it is, 0 by day to 1 at night.
        /// </summary>
        public void Show(Vector3 x, Vector3 y, Vector3 pole, float night)
        {
            this.night = night;
            if (instance == null)
            {
                if (material == null)
                {
                    return;
                }
                instance = new Material(material) { name = material.name + " (stars)" };
                sphere = SkySphere.Create("Stars", transform, instance);
            }
            instance.SetVector(StarXId, x);
            instance.SetVector(StarYId, y);
            instance.SetVector(StarZId, pole);
            instance.SetFloat(NightId, night);
            sphere.SetActive(night > 0.01f);
        }

        // One pooled streak across the sky, now and then, on dark nights only (GL495).
        private void UpdateShootingStar(Camera view)
        {
            if (nextStreakAt < 0f)
            {
                nextStreakAt = Time.time + Random.Range(shootingStarGap.x, shootingStarGap.y);
            }
            if (streak != null && streak.enabled)
            {
                float t = (Time.time - streakStart) / StreakSeconds;
                if (t >= 1f || night < 0.5f)
                {
                    streak.enabled = false;
                    return;
                }
                streak.SetPosition(0, Vector3.Lerp(streakFrom, streakTo, Mathf.Clamp01(t - 0.25f)));
                streak.SetPosition(1, Vector3.Lerp(streakFrom, streakTo, t));
                streak.startColor = new Color(1f, 1f, 1f, 0f);
                streak.endColor = new Color(1f, 0.97f, 0.9f, Mathf.Sin(t * Mathf.PI));
                return;
            }
            if (night < 0.85f || Time.time < nextStreakAt)
            {
                return;
            }
            nextStreakAt = Time.time + Random.Range(shootingStarGap.x, shootingStarGap.y);
            if (streak == null)
            {
                Shader shader = Shader.Find("Sprites/Default");
                if (shader == null)
                {
                    return;
                }
                var go = new GameObject("Shooting star");
                go.transform.SetParent(transform, false);
                streak = go.AddComponent<LineRenderer>();
                streak.positionCount = 2;
                streak.useWorldSpace = true;
                streak.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                streak.receiveShadows = false;
                streak.sharedMaterial = new Material(shader) { name = "Shooting star" };
            }
            float radius = Mathf.Min(view.farClipPlane * 0.5f, 400f);
            Vector3 start = Quaternion.Euler(-Random.Range(25f, 65f), Random.value * 360f, 0f) * Vector3.forward;
            Vector3 along = Quaternion.AngleAxis(Random.value * 360f, start) * Vector3.Cross(start, Vector3.up).normalized;
            streakFrom = view.transform.position + start * radius;
            streakTo = view.transform.position + (start + along * 0.5f).normalized * radius;
            streakStart = Time.time;
            streak.widthMultiplier = radius * 0.004f;
            streak.enabled = true;
        }

        private void LateUpdate()
        {
            Camera view = Camera.main;
            if (view != null)
            {
                UpdateShootingStar(view);
            }
            if (sphere != null && sphere.activeSelf && view != null)
            {
                SkySphere.Follow(sphere.transform, view, distanceShare);
            }
        }

        private void OnDestroy()
        {
            SkySphere.Destroy(sphere);
            if (instance != null)
            {
                Destroy(instance);
            }
        }
    }
}
