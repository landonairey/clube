using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// The title page's background (GL480): a block of the player's parcel cut open like a cake, built in code from low-poly
    /// boxes so its cut face is clean. Grass, dirt, stone and deep rock show as ragged layers on the face, with gold ore at the
    /// bottom, then a silver node, chalcopyrite, malachite and tin ore near the top; on the grass stand a tree, a campfire and
    /// a stump, and the player's figure (<see cref="KeeperFigure"/>, mining pose) swings at a tin node poking out of the
    /// ground. The camera looks down at it from the front, shifted so the block sits right of the menu's column. One mesh
    /// and a handful of parts, cheap enough for the menu's 60 fps cap.
    /// </summary>
    public class MenuDiorama : MonoBehaviour
    {
        // The block: x across, z deep (the cut face is at -z), y up from its bottom.
        private const float Width = 10f;
        private const float Depth = 6f;
        private const float Height = 7f;
        private const float Cell = 0.5f;

        private static readonly Color Grass = new Color(0.36f, 0.55f, 0.22f);
        private static readonly Color Dirt = new Color(0.45f, 0.31f, 0.19f);
        private static readonly Color Stone = new Color(0.52f, 0.5f, 0.47f);
        private static readonly Color Rock = new Color(0.33f, 0.32f, 0.33f);
        private static readonly Color Gold = new Color(0.95f, 0.76f, 0.2f);
        private static readonly Color Silver = new Color(0.82f, 0.84f, 0.88f);
        private static readonly Color Chalcopyrite = new Color(0.66f, 0.6f, 0.3f);
        private static readonly Color Tarnish = new Color(0.38f, 0.36f, 0.68f);
        private static readonly Color Malachite = new Color(0.12f, 0.6f, 0.38f);
        private static readonly Color Tin = new Color(0.3f, 0.24f, 0.2f);
        private static readonly Color Bark = new Color(0.38f, 0.26f, 0.16f);
        private static readonly Color Leaves = new Color(0.25f, 0.48f, 0.2f);

        [Tooltip("The camera that looks at the diorama.")]
        [SerializeField]
        private Camera view;

        [Tooltip("The Clube/Low Poly material (vertex colours).")]
        [SerializeField]
        private Material material;

        [Tooltip("The campfire's model, copied without its station parts.")]
        [SerializeField]
        private GameObject campfirePrefab;

        [Tooltip("How far the block sits right of the screen's middle, as a share of half the screen.")]
        [SerializeField, Range(0f, 1f)]
        private float shiftRight = 0.38f;

        [Tooltip("Degrees a minute the block turns back and forth, so it never sits quite still.")]
        [SerializeField, Min(0f)]
        private float sway = 4f;

        private Transform block;
        private Light fireLight;
        private Transform[] flames;
        private float lastAspect;

        private void Awake()
        {
            block = new GameObject("Parcel block").transform;
            block.SetParent(transform, false);
            var mesh = new LowPolyMeshBuilder();
            var random = new System.Random(480);
            BuildGround(mesh, random);
            BuildOres(mesh, random);
            BuildTree(mesh, new Vector3(-3.1f, Height, 1.2f));
            BuildStump(mesh, new Vector3(3.6f, Height, 1.8f));
            Part("Ground", block, Vector3.zero, mesh.Make("Parcel block"));
            BuildCampfire(new Vector3(-0.6f, Height, 1.4f));
            BuildMiner();
            BuildLight();
        }

        private void LateUpdate()
        {
            block.localRotation = Quaternion.Euler(0f, Mathf.Sin(Time.time * 0.12f) * sway, 0f);
            if (view != null && !Mathf.Approximately(view.aspect, lastAspect))
            {
                Frame();
            }
            float flicker = Mathf.PerlinNoise(Time.time * 6f, 0.3f);
            if (fireLight != null)
            {
                fireLight.intensity = 1.6f + flicker * 1.2f;
            }
            for (int i = 0; i < flames.Length; i++)
            {
                float s = 0.75f + Mathf.PerlinNoise(Time.time * 5f, i * 3.1f) * 0.5f;
                flames[i].localScale = new Vector3(1f, s, 1f);
            }
        }

        // Points the camera down at the block from the front, then slides it so the block sits right of the menu.
        private void Frame()
        {
            lastAspect = view.aspect;
            var centre = new Vector3(0f, Height * 0.55f, 0f);
            Vector3 from = Quaternion.Euler(24f, -22f, 0f) * Vector3.back;
            float halfHeight = Mathf.Tan(view.fieldOfView * 0.5f * Mathf.Deg2Rad);
            // Far enough for the block (and its tree) to fill about two thirds of the screen's height.
            float distance = Height * 1.35f / halfHeight;
            view.transform.position = transform.TransformPoint(centre + from * distance);
            view.transform.LookAt(transform.TransformPoint(centre));
            float halfWidth = halfHeight * view.aspect * distance;
            view.transform.position -= view.transform.right * halfWidth * shiftRight;
            view.clearFlags = CameraClearFlags.SolidColor;
            view.backgroundColor = new Color(0.1f, 0.12f, 0.15f);
        }

        // Columns of boxes, each split into grass, dirt, stone and rock at ragged heights.
        private static void BuildGround(LowPolyMeshBuilder mesh, System.Random random)
        {
            int across = Mathf.RoundToInt(Width / Cell);
            int deep = Mathf.RoundToInt(Depth / Cell);
            for (int x = 0; x < across; x++)
            {
                for (int z = 0; z < deep; z++)
                {
                    float px = -Width * 0.5f + (x + 0.5f) * Cell;
                    float pz = -Depth * 0.5f + (z + 0.5f) * Cell;
                    float top = Height + Wave(px, pz, 0.15f, 0f) + Jitter(random, 0.06f);
                    float dirt = top - 0.3f;
                    float stone = dirt - 1.4f + Wave(px, pz, 0.35f, 2f) + Jitter(random, 0.12f);
                    float rock = 2.6f + Wave(px, pz, 0.4f, 5f) + Jitter(random, 0.12f);
                    Column(mesh, px, pz, 0f, rock, Shade(Rock, random));
                    Column(mesh, px, pz, rock, stone, Shade(Stone, random));
                    Column(mesh, px, pz, stone, dirt, Shade(Dirt, random));
                    Column(mesh, px, pz, dirt, top, Shade(Grass, random));
                }
            }
        }

        private static void Column(LowPolyMeshBuilder mesh, float x, float z, float from, float to, Color color)
        {
            if (to > from)
            {
                mesh.Block(new Vector3(x, from, z), new Vector3(Cell, to - from, Cell), color);
            }
        }

        // Ore nodes as clusters of crystals set into the cut face (and the tin into the top), deepest first.
        private static void BuildOres(LowPolyMeshBuilder mesh, System.Random random)
        {
            float face = -Depth * 0.5f;
            Node(mesh, random, new Vector3(1.2f, 0.9f, face), 0.9f, Gold, 9);
            Node(mesh, random, new Vector3(-2.6f, 1.6f, face), 0.7f, Gold, 6);
            Node(mesh, random, new Vector3(-0.8f, 3.2f, face), 0.8f, Silver, 8);
            Node(mesh, random, new Vector3(3.0f, 3.9f, face), 0.7f, Chalcopyrite, 7);
            Node(mesh, random, new Vector3(-3.6f, 4.6f, face), 0.6f, Malachite, 6);
            Node(mesh, random, new Vector3(0.9f, 4.9f, face), 0.5f, Malachite, 4);
            Node(mesh, random, new Vector3(-1.9f, 6.2f, face), 0.5f, Tin, 5);
            // The tin node the miner works, breaking through the grass beside the cut.
            Node(mesh, random, new Vector3(2.2f, Height + 0.1f, -1.6f), 0.45f, Tin, 6);
        }

        private static void Node(LowPolyMeshBuilder mesh, System.Random random, Vector3 centre, float radius, Color color, int crystals)
        {
            for (int i = 0; i < crystals; i++)
            {
                var offset = new Vector3(Jitter(random, radius), Jitter(random, radius * 0.7f), Jitter(random, 0.12f));
                float size = radius * (0.25f + (float)random.NextDouble() * 0.3f);
                // Chalcopyrite's tarnish: every other crystal blue-purple, so it doesn't read as gold.
                Color tint = color == Chalcopyrite && i % 2 == 1 ? Tarnish : color;
                mesh.Diamond(centre + offset, new Vector3(size, size * 1.3f, size), Shade(tint, random));
            }
        }

        private static void BuildTree(LowPolyMeshBuilder mesh, Vector3 foot)
        {
            mesh.Prism(foot - Vector3.up * 0.2f, foot + Vector3.up * 2.6f, 0.22f, 0.14f, 7, Bark);
            mesh.Prism(foot + new Vector3(0f, 1.8f, 0f), foot + new Vector3(0.6f, 2.5f, 0.1f), 0.08f, 0.05f, 5, Bark);
            mesh.Diamond(foot + new Vector3(0f, 3.1f, 0f), new Vector3(1.2f, 1f, 1.2f), Leaves);
            mesh.Diamond(foot + new Vector3(0.7f, 2.7f, 0.3f), new Vector3(0.8f, 0.7f, 0.8f), Leaves * 0.9f);
            mesh.Diamond(foot + new Vector3(-0.6f, 2.6f, -0.2f), new Vector3(0.75f, 0.65f, 0.75f), Leaves * 1.1f);
            mesh.Diamond(foot + new Vector3(0.1f, 3.8f, 0.2f), new Vector3(0.7f, 0.6f, 0.7f), Leaves * 1.05f);
        }

        private static void BuildStump(LowPolyMeshBuilder mesh, Vector3 foot)
        {
            mesh.Prism(foot - Vector3.up * 0.1f, foot + Vector3.up * 0.55f, 0.36f, 0.32f, 8, Bark);
            mesh.Prism(foot + Vector3.up * 0.55f, foot + Vector3.up * 0.57f, 0.3f, 0.3f, 8, new Color(0.78f, 0.62f, 0.42f));
        }

        private void BuildCampfire(Vector3 at)
        {
            Transform fire = new GameObject("Campfire").transform;
            fire.SetParent(block, false);
            fire.localPosition = at;
            if (campfirePrefab != null)
            {
                ModelCopy.Copy(campfirePrefab, fire, "Model", false, 1.1f);
            }
            var flame = new LowPolyMeshBuilder();
            flame.Diamond(new Vector3(0f, 0.3f, 0f), new Vector3(0.14f, 0.32f, 0.14f), new Color(1f, 0.55f, 0.1f));
            flame.Diamond(new Vector3(0f, 0.28f, 0f), new Vector3(0.08f, 0.22f, 0.08f), new Color(1f, 0.85f, 0.3f));
            Mesh flameMesh = flame.Make("Menu flame");
            flames = new Transform[3];
            for (int i = 0; i < flames.Length; i++)
            {
                float angle = i * 120f * Mathf.Deg2Rad;
                flames[i] = Part("Flame", fire, new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 0.1f, flameMesh);
            }
            fireLight = new GameObject("Fire light").AddComponent<Light>();
            fireLight.transform.SetParent(fire, false);
            fireLight.transform.localPosition = new Vector3(0f, 0.6f, -0.3f);
            fireLight.type = LightType.Point;
            fireLight.color = new Color(1f, 0.6f, 0.25f);
            fireLight.range = 4f;
        }

        private void BuildMiner()
        {
            var miner = new GameObject("Miner");
            miner.transform.SetParent(block, false);
            miner.transform.localPosition = new Vector3(2.2f, Height, -0.55f);
            miner.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            miner.transform.localScale = Vector3.one * 0.9f;
            var figure = miner.AddComponent<KeeperFigure>();
            figure.Build(ShopKind.Mining, material, 7);
            figure.Pose = KeeperPose.Mine;
            foreach (Collider body in miner.GetComponentsInChildren<Collider>())
            {
                Destroy(body);
            }
        }

        private void BuildLight()
        {
            var sun = new GameObject("Diorama light").AddComponent<Light>();
            sun.transform.SetParent(transform, false);
            sun.transform.localRotation = Quaternion.Euler(42f, 35f, 0f);
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.95f, 0.86f);
            sun.intensity = 1.2f;
            sun.shadows = LightShadows.Soft;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.36f, 0.38f, 0.42f);
        }

        private Transform Part(string name, Transform parent, Vector3 at, Mesh mesh)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            return go.transform;
        }

        private static float Wave(float x, float z, float amount, float seed)
        {
            return (Mathf.PerlinNoise(x * 0.35f + seed, z * 0.35f + seed * 1.7f) - 0.5f) * 2f * amount;
        }

        private static float Jitter(System.Random random, float amount)
        {
            return ((float)random.NextDouble() * 2f - 1f) * amount;
        }

        private static Color Shade(Color color, System.Random random)
        {
            float s = 0.92f + (float)random.NextDouble() * 0.14f;
            return new Color(color.r * s, color.g * s, color.b * s, 1f);
        }
    }
}
