using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// Sets a figure's <see cref="FigureDetail"/> from how far the camera is (GL615): full within
    /// <see cref="ShadowDistance"/>, no shadow to <see cref="FarDistance"/>, one standing mesh beyond. Added to every
    /// <see cref="FigureRig"/> as it is built; looks a few times a second, each figure at its own moment.
    /// </summary>
    public class FigureLod : MonoBehaviour
    {
        /// <summary>Metres from the camera within which a figure casts a shadow.</summary>
        public static float ShadowDistance = 30f;

        /// <summary>Metres from the camera past which a figure is one standing mesh.</summary>
        public static float FarDistance = 60f;

        /// <summary>False draws every figure in full, whatever the distance (to compare costs).</summary>
        public static bool Enabled = true;

        private const float Interval = 0.4f;

        private static Camera viewer;

        private FigureRig rig;
        private float next;

        /// <summary>The detail a figure gets at a distance from the camera.</summary>
        public static FigureDetail DetailAt(float distance)
        {
            return !Enabled || distance <= ShadowDistance ? FigureDetail.Full
                : distance <= FarDistance ? FigureDetail.NoShadow
                : FigureDetail.Far;
        }

        private void Awake()
        {
            rig = GetComponent<FigureRig>();
            next = Time.time + Random.value * Interval;
        }

        private void Update()
        {
            if (Time.time < next || rig == null)
            {
                return;
            }
            next = Time.time + Interval;
            if (viewer == null || !viewer.isActiveAndEnabled)
            {
                viewer = Camera.main;
            }
            if (viewer != null)
            {
                rig.Detail = DetailAt(Vector3.Distance(viewer.transform.position, transform.position));
            }
        }
    }
}
