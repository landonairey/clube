using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// The title page's background (GL480, GL512): a small fixed-seed area of the real world, generated and meshed by the
    /// real pipeline (<see cref="WorldView"/> on <c>MenuWorldConfig</c>: a 16 m square whose sealed outer faces cut it open like
    /// a cake), so the layers, grass and trees are the game's own. Once it has loaded, the ores are stamped into its cut face
    /// on purpose (<see cref="MenuOres"/>: gold at the bottom, then silver, chalcopyrite, malachite and tin near the top and
    /// on the surface) with a stump beside them; a campfire and the player's figure (<see cref="KeeperFigure"/>, mining pose)
    /// stand on the grass. The camera looks down at it from the front, shifted so the block sits right of the menu's column.
    /// </summary>
    public class MenuDiorama : MonoBehaviour
    {
        // The block is 16 m square (centred on the origin, the cut face at -z); its ground is about 9 m up.
        private const float FrameHeight = 12f;

        [Tooltip("The camera that looks at the diorama.")]
        [SerializeField]
        private Camera view;

        [Tooltip("The real world's view of the small fixed area (on MenuWorldConfig).")]
        [SerializeField]
        private WorldView world;

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
        private bool dressed;

        private void Awake()
        {
            block = world.transform;
            BuildLight();
        }

        private void LateUpdate()
        {
            if (!dressed && world.IsReady && !world.IsLoading)
            {
                dressed = true;
                Dress();
            }
            block.localRotation = Quaternion.Euler(0f, Mathf.Sin(Time.time * 0.12f) * sway, 0f);
            if (view != null && !Mathf.Approximately(view.aspect, lastAspect))
            {
                Frame();
            }
            if (fireLight == null)
            {
                return;
            }
            float flicker = Mathf.PerlinNoise(Time.time * 6f, 0.3f);
            fireLight.intensity = 1.6f + flicker * 1.2f;
            for (int i = 0; i < flames.Length; i++)
            {
                float s = 0.75f + Mathf.PerlinNoise(Time.time * 5f, i * 3.1f) * 0.5f;
                flames[i].localScale = new Vector3(1f, s, 1f);
            }
        }

        // Once the real terrain is in: ores in the cut face, a stump, then the campfire and the miner on the grass.
        private void Dress()
        {
            var ores = new MenuOres(world.World, world.Config.Materials);
            float ground = ores.SurfaceAt(0f, 0f);
            ores.StampAll(ground);
            Vector3 tin = ores.TinOnSurface;
            BuildCampfire(new Vector3(-1.2f, ores.SurfaceAt(-1.2f, -2.4f), -2.4f));
            BuildMiner(new Vector3(tin.x, ores.SurfaceAt(tin.x, tin.z + 1.1f), tin.z + 1.1f));
        }

        // Points the camera down at the block from the front, then slides it so the block sits right of the menu.
        private void Frame()
        {
            lastAspect = view.aspect;
            var centre = new Vector3(0f, FrameHeight * 0.65f, 0f);
            Vector3 from = Quaternion.Euler(24f, -22f, 0f) * Vector3.back;
            float halfHeight = Mathf.Tan(view.fieldOfView * 0.5f * Mathf.Deg2Rad);
            // Far enough for the block (and its trees) to fill about two thirds of the screen's height.
            float distance = FrameHeight * 1.35f / halfHeight;
            view.transform.position = transform.TransformPoint(centre + from * distance);
            view.transform.LookAt(transform.TransformPoint(centre));
            float halfWidth = halfHeight * view.aspect * distance;
            view.transform.position -= view.transform.right * halfWidth * shiftRight;
            view.clearFlags = CameraClearFlags.SolidColor;
            view.backgroundColor = new Color(0.1f, 0.12f, 0.15f);
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

        private void BuildMiner(Vector3 at)
        {
            var miner = new GameObject("Miner");
            miner.transform.SetParent(block, false);
            miner.transform.localPosition = at;
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
    }
}
