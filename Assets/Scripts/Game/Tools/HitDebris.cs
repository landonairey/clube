using System.Collections.Generic;
using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// Crumbs flying off a hit (GL273): small low-poly chunks of the material the tool works, in its colour (a hidden ore's
    /// as the stone it's shown as), thrown back towards the player and up, falling, bouncing on the ground under them and
    /// shrinking away within a second or two. Rock breaks into angular chips, soil into clods, wood and leaves into flat
    /// flakes; a picked bush sheds leaves. More crumbs the more the hit took. A pool of a few dozen, reused.
    /// </summary>
    [RequireComponent(typeof(PlayerToolUser))]
    public class HitDebris : MonoBehaviour
    {
        private const int PoolSize = 48;
        private static readonly int TintId = Shader.PropertyToID("_Tint");
        private static readonly Color LeafGreen = new Color(0.26f, 0.42f, 0.15f);

        [Tooltip("The Clube/Held Item material (tinted per crumb).")]
        [SerializeField]
        private Material material;

        [Tooltip("Gravity on the crumbs, metres a second squared.")]
        [SerializeField, Min(0f)]
        private float gravity = 12f;

        private sealed class Crumb
        {
            public GameObject Object;
            public MeshRenderer Renderer;
            public Vector3 Velocity;
            public Vector3 Spin;
            public float Floor;
            public float Size;
            public float Age;
            public float Life;
            public bool Alive;
        }

        private readonly List<Crumb> crumbs = new List<Crumb>();
        private readonly Mesh[] shapes = new Mesh[3];
        private MaterialPropertyBlock tint;
        private PlayerToolUser tools;
        private PlayerController player;
        private Transform holder;
        private int next;

        private void Awake()
        {
            tools = GetComponent<PlayerToolUser>();
            player = GetComponent<PlayerController>();
            tint = new MaterialPropertyBlock();
            shapes[0] = Chunk("Chip", new Vector3(1f, 0.8f, 0.9f), 1);
            shapes[1] = Chunk("Clod", new Vector3(1f, 0.9f, 1f), 2);
            shapes[2] = Chunk("Flake", new Vector3(1.2f, 0.25f, 1f), 3);
        }

        private void OnEnable()
        {
            tools.Struck += OnStruck;
            tools.BushPicked += OnBushPicked;
        }

        private void OnDisable()
        {
            tools.Struck -= OnStruck;
            tools.BushPicked -= OnBushPicked;
            foreach (Crumb crumb in crumbs)
            {
                crumb.Alive = false;
                crumb.Object.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            if (holder != null)
            {
                Destroy(holder.gameObject);
            }
            foreach (Mesh mesh in shapes)
            {
                Destroy(mesh);
            }
        }

        private void OnStruck(StrikeResult result)
        {
            int took = Mathf.Max(result.Removed, result.Broken);
            if (took <= 0 || tools.WorldView == null)
            {
                return;
            }
            VoxelMaterial hit = tools.HitMaterial;
            MaterialRegistry registry = tools.WorldView.Config.Materials;
            if (registry != null)
            {
                hit = registry.Shown(hit);
            }
            if (hit == null)
            {
                return;
            }
            int shape = hit.Kind == MaterialKind.Rock ? 0 : hit.Kind == MaterialKind.Soil ? 1 : 2;
            Burst(tools.WorldView.transform.TransformPoint(tools.ImpactCentre), Earthy(hit.DebugColor), shape, Mathf.Clamp(4 + took * 2, 5, 14));
        }

        // A material's debug colour toned down towards how its texture looks: a little greyer and darker.
        private static Color Earthy(Color colour)
        {
            float grey = colour.r * 0.3f + colour.g * 0.59f + colour.b * 0.11f;
            return Color.Lerp(colour, new Color(grey, grey, grey), 0.3f) * 0.75f;
        }

        private void OnBushPicked(Bush bush)
        {
            if (tools.WorldView != null)
            {
                Burst(tools.WorldView.transform.TransformPoint(tools.ImpactCentre), LeafGreen, 2, 10);
            }
        }

        private void Burst(Vector3 at, Color colour, int shape, int count)
        {
            EnsurePool();
            Transform head = player != null && player.Head != null ? player.Head : transform;
            Vector3 back = -head.forward;
            back.y = 0f;
            back = back.sqrMagnitude > 1e-4f ? back.normalized : Vector3.forward;
            for (int i = 0; i < count; i++)
            {
                Crumb crumb = crumbs[next];
                next = (next + 1) % crumbs.Count;
                Vector3 spread = Random.insideUnitSphere;
                crumb.Velocity = back * Random.Range(0.6f, 1.8f) + Vector3.up * Random.Range(1.4f, 3.2f) + spread * 1.2f;
                crumb.Spin = Random.insideUnitSphere * 540f;
                crumb.Size = Random.Range(0.025f, shape == 1 ? 0.05f : 0.065f);
                crumb.Age = 0f;
                crumb.Life = Random.Range(1f, 1.7f);
                crumb.Alive = true;
                Vector3 start = at + spread * 0.06f + back * 0.04f;
                crumb.Floor = Physics.Raycast(start + Vector3.up * 0.5f, Vector3.down, out RaycastHit ground, 6f, ~0, QueryTriggerInteraction.Ignore)
                    ? ground.point.y + crumb.Size * 0.4f
                    : at.y - 1.5f;
                crumb.Object.transform.SetPositionAndRotation(start, Random.rotation);
                crumb.Object.transform.localScale = Vector3.one * crumb.Size;
                crumb.Object.GetComponent<MeshFilter>().sharedMesh = shapes[shape];
                // Each crumb a little lighter or darker, as the ground's texture is.
                tint.SetColor(TintId, colour * Random.Range(0.8f, 1.15f));
                crumb.Renderer.SetPropertyBlock(tint);
                crumb.Object.SetActive(true);
            }
        }

        private void Update()
        {
            float step = Time.deltaTime;
            foreach (Crumb crumb in crumbs)
            {
                if (!crumb.Alive)
                {
                    continue;
                }
                crumb.Age += step;
                if (crumb.Age >= crumb.Life)
                {
                    crumb.Alive = false;
                    crumb.Object.SetActive(false);
                    continue;
                }
                Transform t = crumb.Object.transform;
                crumb.Velocity += Vector3.down * gravity * step;
                Vector3 position = t.position + crumb.Velocity * step;
                if (position.y < crumb.Floor)
                {
                    // Bounce, losing most of it; settle once slow.
                    position.y = crumb.Floor;
                    crumb.Velocity = new Vector3(crumb.Velocity.x * 0.5f, -crumb.Velocity.y * 0.3f, crumb.Velocity.z * 0.5f);
                    crumb.Spin *= 0.5f;
                    if (crumb.Velocity.y < 0.4f)
                    {
                        crumb.Velocity = Vector3.zero;
                        crumb.Spin = Vector3.zero;
                    }
                }
                t.position = position;
                t.rotation = Quaternion.Euler(crumb.Spin * step) * t.rotation;
                // Shrinks away over its last third.
                float left = Mathf.Clamp01((crumb.Life - crumb.Age) / (crumb.Life * 0.33f));
                t.localScale = Vector3.one * crumb.Size * left;
            }
        }

        private void EnsurePool()
        {
            if (holder != null || material == null)
            {
                return;
            }
            holder = new GameObject("Hit debris").transform;
            for (int i = 0; i < PoolSize; i++)
            {
                var piece = new GameObject("Crumb");
                piece.transform.SetParent(holder, false);
                piece.AddComponent<MeshFilter>().sharedMesh = shapes[0];
                var renderer = piece.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
                piece.SetActive(false);
                crumbs.Add(new Crumb { Object = piece, Renderer = renderer });
            }
        }

        // An irregular low-poly lump about a unit across: an octahedron with its corners pushed in and out.
        private static Mesh Chunk(string name, Vector3 scale, int seed)
        {
            var random = new System.Random(seed);
            Vector3[] corners =
            {
                Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back,
            };
            for (int i = 0; i < corners.Length; i++)
            {
                corners[i] = Vector3.Scale(corners[i] * (0.35f + 0.25f * (float)random.NextDouble()), scale);
            }
            int[] faces = { 2, 0, 4, 2, 4, 1, 2, 1, 5, 2, 5, 0, 3, 4, 0, 3, 1, 4, 3, 5, 1, 3, 0, 5 };
            var vertices = new Vector3[faces.Length];
            var normals = new Vector3[faces.Length];
            var colors = new Color[faces.Length];
            var triangles = new int[faces.Length];
            for (int f = 0; f < faces.Length; f += 3)
            {
                Vector3 a = corners[faces[f]], b = corners[faces[f + 1]], c = corners[faces[f + 2]];
                Vector3 normal = Vector3.Cross(b - a, c - a).normalized;
                if (Vector3.Dot(normal, a + b + c) < 0f)
                {
                    (b, c) = (c, b);
                    normal = -normal;
                }
                vertices[f] = a;
                vertices[f + 1] = b;
                vertices[f + 2] = c;
                for (int k = 0; k < 3; k++)
                {
                    normals[f + k] = normal;
                    colors[f + k] = Color.white;
                    triangles[f + k] = f + k;
                }
            }
            var mesh = new Mesh { name = name, vertices = vertices, normals = normals, colors = colors, triangles = triangles };
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
