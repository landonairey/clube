using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// Draws the moon (GL248): a disc far out along the moon's direction, facing the camera, shaded by the
    /// <c>Clube/Moon</c> shader as a ball lit from the sun's side, so its phase is whatever the angle between them makes
    /// it. Pale by day, bright by night; the ground in front of it hides it. <see cref="DayNightCycle"/> tells it where
    /// the moon and sun are each frame.
    /// </summary>
    public class MoonDisc : MonoBehaviour
    {
        private static readonly int SunDirectionId = Shader.PropertyToID("_SunDirection");
        private static readonly int DaylightId = Shader.PropertyToID("_Daylight");

        [Tooltip("The Clube/Moon material.")]
        [SerializeField]
        private Material material;

        [Tooltip("How wide the moon looks, in degrees (the real one is about 0.5).")]
        [SerializeField, Range(0.2f, 6f)]
        private float angularSize = 1.8f;

        [Tooltip("How far out it's drawn, as a share of the camera's far plane.")]
        [SerializeField, Range(0.5f, 0.99f)]
        private float distanceShare = 0.9f;

        private GameObject disc;
        private MeshRenderer discRenderer;
        private Material instance;
        private Vector3 direction = Vector3.down;

        /// <summary>Puts the moon at a direction, lit from the sun's, with the daylight 0-1 (pale by day).</summary>
        public void Show(Vector3 moonDirection, Vector3 sunDirection, float daylight)
        {
            if (instance == null)
            {
                Build();
                if (instance == null)
                {
                    return;
                }
            }
            direction = moonDirection.normalized;
            instance.SetVector(SunDirectionId, sunDirection.normalized);
            instance.SetFloat(DaylightId, daylight);
            // Under the horizon it's out of sight anyway; leave it off then.
            disc.SetActive(direction.y > -0.05f);
        }

        private void Build()
        {
            if (material == null)
            {
                return;
            }
            instance = new Material(material) { name = material.name + " (moon)" };
            disc = new GameObject("Moon");
            disc.transform.SetParent(transform, false);
            disc.AddComponent<MeshFilter>().sharedMesh = BuildQuad();
            discRenderer = disc.AddComponent<MeshRenderer>();
            discRenderer.sharedMaterial = instance;
            discRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            discRenderer.receiveShadows = false;
            discRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            discRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        }

        private static Mesh BuildQuad()
        {
            var mesh = new Mesh { name = "Moon disc" };
            mesh.vertices = new[] { new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f) };
            mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };
            mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one);
            return mesh;
        }

        private void LateUpdate()
        {
            Camera view = Camera.main;
            if (disc == null || !disc.activeSelf || view == null)
            {
                return;
            }
            float distance = view.farClipPlane * distanceShare;
            Transform t = disc.transform;
            t.position = view.transform.position + direction * distance;
            // Facing the camera, its up kept towards the sky's up so the lit side turns as it should.
            t.rotation = Quaternion.LookRotation(direction, Mathf.Abs(direction.y) > 0.999f ? Vector3.forward : Vector3.up);
            float size = 2f * distance * Mathf.Tan(angularSize * 0.5f * Mathf.Deg2Rad);
            t.localScale = new Vector3(size, size, size);
        }

        private void OnDestroy()
        {
            if (instance != null)
            {
                Destroy(instance);
            }
        }
    }
}
