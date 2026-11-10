using System.Collections.Generic;
using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// Crumbs flying off a hit (GL273): small low-poly chunks of the material the tool works, in its colour (a hidden ore's
    /// as the stone it's shown as), splashing out of the struck face in a wide cone (GL460), falling, bouncing on the ground
    /// under them and shrinking away within a second or two; rock throws a few sparks now and then, and ore crushed on a
    /// boulder throws fragments of the ore (GL459). Rock breaks into angular chips, soil into clods, wood and leaves into flat
    /// flakes; a picked bush sheds leaves. More crumbs the more the hit took. A pool of a few dozen, reused.
    /// </summary>
    [RequireComponent(typeof(PlayerToolUser))]
    public class HitDebris : MonoBehaviour
    {
        private const int PoolSize = 64;
        private static readonly int TintId = Shader.PropertyToID("_Tint");
        private static readonly Color LeafGreen = new Color(0.26f, 0.42f, 0.15f);

        [Tooltip("The Clube/Held Item material (tinted per crumb).")]
        [SerializeField]
        private Material material;

        [Tooltip("Gravity on the crumbs, metres a second squared.")]
        [SerializeField, Min(0f)]
        private float gravity = 12f;

        [Tooltip("Sparks: the torch flame material, glowing. Empty for none.")]
        [SerializeField]
        private Material sparkMaterial;

        [Tooltip("Chance a hit on rock or ore throws sparks (GL460, GL498; renamed so old scene values no longer override it).")]
        [SerializeField, Range(0f, 1f)]
        private float rockSparkChance = 0.7f;

        [Tooltip("Chance a crush on the boulder throws a few sparks (GL459).")]
        [SerializeField, Range(0f, 1f)]
        private float boulderSparkChance = 0.85f;

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
            public bool Spark;
        }

        private readonly List<Crumb> crumbs = new List<Crumb>();
        private readonly Mesh[] shapes = new Mesh[3];
        private MaterialPropertyBlock tint;
        private PlayerToolUser tools;
        private BoulderBashing bashing;
        private PlayerController player;
        private Transform holder;
        private int next;

        private void Awake()
        {
            tools = GetComponent<PlayerToolUser>();
            bashing = GetComponent<BoulderBashing>();
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
            if (bashing != null)
            {
                bashing.OreCrushed += OnOreCrushed;
            }
        }

        private void OnDisable()
        {
            tools.Struck -= OnStruck;
            tools.BushPicked -= OnBushPicked;
            if (bashing != null)
            {
                bashing.OreCrushed -= OnOreCrushed;
            }
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
            if (result.Reached <= 0 || tools.WorldView == null)
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
            Vector3 at = tools.WorldView.transform.TransformPoint(tools.ImpactCentre);
            // Sparks now and then off rock (GL460).
            Burst(at, Earthy(hit.DebugColor), shape, Mathf.Clamp(8 + took * 3, 8, 22), OutwardFrom(at), hit.Kind == MaterialKind.Rock ? rockSparkChance : 0f);
        }

        // Ore crushed on a boulder (GL459): fragments of the ore fly off the boulder, with sparks now and then.
        private void OnOreCrushed(Vector3 at, VoxelMaterial ore)
        {
            Color colour = ore != null ? Earthy(ore.DebugColor) : new Color(0.5f, 0.5f, 0.52f);
            Burst(at, colour, 0, 18, OutwardFrom(at), boulderSparkChance);
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
                Vector3 at = tools.WorldView.transform.TransformPoint(tools.ImpactCentre);
                Burst(at, LeafGreen, 2, 10, OutwardFrom(at), 0f);
            }
        }

        // Out of the struck face: from the impact towards the player's head, which is on the open side of it.
        private Vector3 OutwardFrom(Vector3 at)
        {
            Transform head = player != null && player.Head != null ? player.Head : transform;
            Vector3 outward = head.position - at;
            return outward.sqrMagnitude > 1e-4f ? outward.normalized : Vector3.up;
        }

        // Crumbs splash out of the face in a wide cone all round its outward direction (GL460: never in a huddle), each with
        // its own floor below it; with sparkChance, a few sparks fly too.
        private void Burst(Vector3 at, Color colour, int shape, int count, Vector3 outward, float sparkChance)
        {
            EnsurePool();
            if (holder == null)
            {
                return;
            }
            Vector3 side = Vector3.Cross(outward, Mathf.Abs(outward.y) > 0.9f ? Vector3.right : Vector3.up).normalized;
            Vector3 other = Vector3.Cross(outward, side);
            for (int i = 0; i < count; i++)
            {
                // Evenly round the cone, jittered, so they spread out rather than bunch.
                float angle = (i + Random.value * 0.8f) / count * Mathf.PI * 2f;
                Vector3 across = side * Mathf.Cos(angle) + other * Mathf.Sin(angle);
                Vector3 velocity = outward * Random.Range(1.2f, 2.6f) + across * Random.Range(1.5f, 3.4f) + Vector3.up * Random.Range(1.8f, 3.6f);
                Launch(crumbs[next], at + outward * 0.05f + across * 0.04f, velocity, colour, shape, false);
                next = (next + 1) % crumbs.Count;
            }
            if (sparkChance > 0f && Random.value < sparkChance && sparkMaterial != null)
            {
                int sparkCount = Random.Range(6, 12);
                for (int i = 0; i < sparkCount; i++)
                {
                    Vector3 across = Random.insideUnitSphere;
                    Vector3 velocity = (outward * Random.Range(1.5f, 3f) + across * 2.2f + Vector3.up * Random.Range(0.5f, 1.8f)) * 1.3f;
                    Launch(crumbs[next], at + outward * 0.05f, velocity, Color.white, 0, true);
                    next = (next + 1) % crumbs.Count;
                }
            }
        }

        private void Launch(Crumb crumb, Vector3 start, Vector3 velocity, Color colour, int shape, bool spark)
        {
            crumb.Velocity = velocity;
            crumb.Spin = Random.insideUnitSphere * 540f;
            crumb.Size = spark ? Random.Range(0.012f, 0.02f) : Random.Range(0.025f, shape == 1 ? 0.05f : 0.065f);
            crumb.Age = 0f;
            crumb.Life = spark ? Random.Range(0.25f, 0.45f) : Random.Range(1f, 1.7f);
            crumb.Alive = true;
            crumb.Spark = spark;
            crumb.Floor = FloorUnder(start) + crumb.Size * 0.4f;
            crumb.Object.transform.SetPositionAndRotation(start, Random.rotation);
            crumb.Object.transform.localScale = Vector3.one * crumb.Size;
            crumb.Object.GetComponent<MeshFilter>().sharedMesh = shapes[shape];
            if (spark)
            {
                crumb.Renderer.sharedMaterial = sparkMaterial;
                crumb.Renderer.SetPropertyBlock(null);
            }
            else
            {
                crumb.Renderer.sharedMaterial = material;
                // Each crumb a little lighter or darker, as the ground's texture is.
                tint.SetColor(TintId, colour * Random.Range(0.8f, 1.15f));
                crumb.Renderer.SetPropertyBlock(tint);
            }
            crumb.Object.SetActive(true);
        }

        // The ground below a point, never above it: the first collider under it that isn't the player (a downward ray once
        // stopped on the player's own capsule or an overhang, and the crumbs settled in mid-air in a huddle).
        private float FloorUnder(Vector3 start)
        {
            int found = Physics.RaycastNonAlloc(start, Vector3.down, floorHits, 6f, ~0, QueryTriggerInteraction.Ignore);
            float floor = start.y - 1.5f;
            float nearest = float.MaxValue;
            for (int i = 0; i < found; i++)
            {
                RaycastHit hit = floorHits[i];
                if (hit.collider.transform.IsChildOf(transform) || hit.distance >= nearest)
                {
                    continue;
                }
                nearest = hit.distance;
                floor = hit.point.y;
            }
            return Mathf.Min(floor, start.y - 0.02f);
        }

        private readonly RaycastHit[] floorHits = new RaycastHit[8];

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
                    if (crumb.Spark)
                    {
                        // A spark goes out where it lands.
                        crumb.Alive = false;
                        crumb.Object.SetActive(false);
                        continue;
                    }
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

        // An irregular low-poly lump about a unit across: an octahedron with its corners pushed in and out. Also the
        // leaves and splinters of a felling (FellingBurst).
        internal static Mesh Chunk(string name, Vector3 scale, int seed)
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
