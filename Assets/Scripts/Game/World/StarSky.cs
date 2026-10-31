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

        private GameObject sphere;
        private Material instance;

        /// <summary>
        /// Turns the stars: <paramref name="x"/>, <paramref name="y"/> and <paramref name="pole"/> are world directions
        /// of right ascension 0 and 90 on the celestial equator and of the celestial pole. <paramref name="night"/> is
        /// how dark it is, 0 by day to 1 at night.
        /// </summary>
        public void Show(Vector3 x, Vector3 y, Vector3 pole, float night)
        {
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

        private void LateUpdate()
        {
            Camera view = Camera.main;
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
