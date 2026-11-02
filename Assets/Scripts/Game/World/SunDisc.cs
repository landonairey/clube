using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// Draws the sun (GL277) in place of the skybox's: a round ball of light far out along the sun's direction, facing the
    /// camera, in the sun's colour (deep at sunrise and sunset), by <c>Clube/Sun</c>. The ground and the low-poly clouds hide
    /// it; where a cloud is in front, the cloud shader draws the sun's 12-sided outline shining through, from the globals
    /// this sets (<c>_SunShineDir</c>, <c>_SunShineColor</c>, <c>_SunShineRadius</c>). <see cref="DayNightCycle"/> tells it
    /// where the sun is each frame.
    /// </summary>
    public class SunDisc : MonoBehaviour
    {
        private static readonly int ColorId = Shader.PropertyToID("_SunColor");
        private static readonly int StrengthId = Shader.PropertyToID("_Strength");
        private static readonly int CoreId = Shader.PropertyToID("_Core");
        private static readonly int ShineDirId = Shader.PropertyToID("_SunShineDir");
        private static readonly int ShineColorId = Shader.PropertyToID("_SunShineColor");
        private static readonly int ShineRadiusId = Shader.PropertyToID("_SunShineRadius");

        [Tooltip("The Clube/Sun material.")]
        [SerializeField]
        private Material material;

        [Tooltip("How wide the sun's glow is, in degrees; the bright ball is the middle share of it.")]
        [SerializeField, Range(2f, 30f)]
        private float glowSize = 16f;

        [Tooltip("How wide the bright ball is, in degrees.")]
        [SerializeField, Range(0.5f, 6f)]
        private float size = 4.5f;

        [Tooltip("How far out it's drawn, as a share of the camera's far plane: behind the moon.")]
        [SerializeField, Range(0.5f, 0.99f)]
        private float distanceShare = 0.93f;

        private GameObject disc;
        private Material instance;
        private Vector3 direction = Vector3.up;

        /// <summary>
        /// Puts the sun at a direction, in a colour (sRGB), as strong as <paramref name="strength"/> (0 gone under the
        /// horizon, 1 up).
        /// </summary>
        public void Show(Vector3 sunDirection, Color colour, float strength)
        {
            if (instance == null)
            {
                if (material == null)
                {
                    return;
                }
                instance = new Material(material) { name = material.name + " (sun)" };
                disc = new GameObject("Sun");
                disc.transform.SetParent(transform, false);
                disc.AddComponent<MeshFilter>().sharedMesh = BuildQuad();
                var renderer = disc.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = instance;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            }
            direction = sunDirection.normalized;
            Color linear = colour.linear;
            instance.SetColor(ColorId, linear);
            instance.SetFloat(StrengthId, strength);
            instance.SetFloat(CoreId, size / glowSize);
            disc.SetActive(strength > 0.001f);
            // For the clouds: where the sun is and how big, to let its outline shine through them.
            Shader.SetGlobalVector(ShineDirId, direction);
            Shader.SetGlobalVector(ShineColorId, new Vector4(linear.r, linear.g, linear.b, strength));
            Shader.SetGlobalFloat(ShineRadiusId, Mathf.Tan(size * 0.5f * Mathf.Deg2Rad));
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
            t.rotation = Quaternion.LookRotation(direction, Mathf.Abs(direction.y) > 0.999f ? Vector3.forward : Vector3.up);
            float width = 2f * distance * Mathf.Tan(glowSize * 0.5f * Mathf.Deg2Rad);
            t.localScale = new Vector3(width, width, width);
        }

        private void OnDisable()
        {
            Shader.SetGlobalVector(ShineColorId, Vector4.zero);
        }

        private void OnDestroy()
        {
            if (instance != null)
            {
                Destroy(instance);
            }
        }

        private static Mesh BuildQuad()
        {
            var mesh = new Mesh { name = "Sun disc" };
            mesh.vertices = new[] { new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f) };
            mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };
            mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one);
            return mesh;
        }
    }
}
