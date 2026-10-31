using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// A sphere of radius 1 seen from inside, for drawing things on the sky around the camera (GL252, GL253): the
    /// stars and the clouds. <see cref="Follow"/> keeps it centred on the camera, just inside the far plane, so it
    /// covers every direction the camera looks.
    /// </summary>
    public static class SkySphere
    {
        /// <summary>A sphere of <paramref name="rings"/> rings of quads from pole to pole.</summary>
        public static Mesh Build(string name, int rings = 24, int segments = 48)
        {
            var vertices = new Vector3[(rings + 1) * (segments + 1)];
            for (int ring = 0; ring <= rings; ring++)
            {
                float polar = Mathf.PI * ring / rings;
                for (int segment = 0; segment <= segments; segment++)
                {
                    float around = 2f * Mathf.PI * segment / segments;
                    vertices[ring * (segments + 1) + segment] = new Vector3(
                        Mathf.Sin(polar) * Mathf.Cos(around), Mathf.Cos(polar), Mathf.Sin(polar) * Mathf.Sin(around));
                }
            }
            var triangles = new int[rings * segments * 6];
            int index = 0;
            for (int ring = 0; ring < rings; ring++)
            {
                for (int segment = 0; segment < segments; segment++)
                {
                    int a = ring * (segments + 1) + segment;
                    int b = a + segments + 1;
                    // Seen from the centre each direction crosses the sphere once, so the shaders draw both sides.
                    triangles[index++] = a;
                    triangles[index++] = a + 1;
                    triangles[index++] = b;
                    triangles[index++] = a + 1;
                    triangles[index++] = b + 1;
                    triangles[index++] = b;
                }
            }
            var mesh = new Mesh { name = name, vertices = vertices, triangles = triangles };
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 2f);
            return mesh;
        }

        /// <summary>Makes an object holding the sphere with this material, under <paramref name="parent"/>.</summary>
        public static GameObject Create(string name, Transform parent, Material material)
        {
            var sphere = new GameObject(name);
            sphere.transform.SetParent(parent, false);
            sphere.AddComponent<MeshFilter>().sharedMesh = Build(name);
            var renderer = sphere.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            return sphere;
        }

        /// <summary>Centres the sphere on the camera with a radius of <paramref name="share"/> of its far plane.</summary>
        public static void Follow(Transform sphere, Camera view, float share)
        {
            float radius = view.farClipPlane * share;
            sphere.position = view.transform.position;
            sphere.rotation = Quaternion.identity;
            sphere.localScale = Vector3.one * radius;
        }

        /// <summary>Destroys the sphere and its mesh.</summary>
        public static void Destroy(GameObject sphere)
        {
            if (sphere == null)
            {
                return;
            }
            MeshFilter filter = sphere.GetComponent<MeshFilter>();
            if (filter != null && filter.sharedMesh != null)
            {
                Object.Destroy(filter.sharedMesh);
            }
            Object.Destroy(sphere);
        }
    }
}
