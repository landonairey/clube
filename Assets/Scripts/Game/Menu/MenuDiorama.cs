using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// The title page's background (GL480, GL512): a small fixed-seed area of the real world, generated and meshed by the
    /// real pipeline (<see cref="WorldView"/> on <c>MenuWorldConfig</c>: a 64 m square (2 x 2 parcels) whose sealed outer faces cut it open like
    /// a cake), so the layers, grass and trees are the game's own. Once it has loaded, the ores are stamped into its cut face
    /// on purpose (<see cref="MenuOres"/>: gold at the bottom, then silver, chalcopyrite, malachite and tin near the top and
    /// on the surface); a campfire and the player's figure (<see cref="KeeperFigure"/>, mining pose)
    /// stand on the grass. The camera looks down at it from the front, shifted so the block sits right of the menu's column.
    /// </summary>
    public class MenuDiorama : MonoBehaviour
    {
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

        [Tooltip("Degrees the camera swings round the block back and forth, so it never sits quite still.")]
        [SerializeField, Min(0f)]
        private float sway = 4f;

        [Tooltip("Degrees the camera looks down at the block (GL535: lower than top-down).")]
        [SerializeField, Range(5f, 80f)]
        private float cameraPitch = 28f;

        private Transform block;
        private Light fireLight;
        private FlameCluster flame;
        private bool dressed;
        private float frameHeight = 26f;
        private float frameCentreY = 36f;

        [Tooltip("The campfire's flames (the prefab's own flame material).")]
        [SerializeField]
        private Material flameMaterial;

        [Tooltip("The campfire's smoke (the Drying steam material).")]
        [SerializeField]
        private Material smokeMaterial;

        private void Awake()
        {
            block = world.transform;
            // The scene has no listener of its own, so the menu clicks (UiClick) were never heard (GL518).
            if (view != null && view.GetComponent<AudioListener>() == null && FindAnyObjectByType<AudioListener>() == null)
            {
                view.gameObject.AddComponent<AudioListener>();
            }
            BuildLight();
        }

        private void LateUpdate()
        {
            if (!dressed && world.IsReady && !world.IsLoading)
            {
                dressed = true;
                Dress();
            }
            // The camera swings round the block rather than the block turning: the terrain shader's textures and ore glints
            // are in world space, so a turning block made them slide over the ground and the ore nodes look fixed.
            if (view != null)
            {
                Frame();
            }
            if (fireLight == null)
            {
                return;
            }
            float flicker = Mathf.PerlinNoise(Time.time * 6f, 0.3f);
            fireLight.intensity = 1.6f + flicker * 1.2f;
            flame?.Animate(1f);
        }

        // Once the real terrain is in: ores in the cut face, then the campfire and the miner on the grass.
        private void Dress()
        {
            WorldConfig config = world.Config;
            float top = config.WorldHeightInChunks * config.ChunkSize.y * config.VoxelSize;
            float half = config.FixedAreaSize * 0.5f;
            float scale = half / 16f;
            var ores = new MenuOres(world.World, config.Materials, top, half);
            // The ground's height range over the block sets the framing (the spot is a mountain's edge, so it tilts).
            float low = float.MaxValue, high = float.MinValue;
            for (float x = -half + 2f; x <= half - 2f; x += 4f * scale)
            {
                for (float z = -half + 2f; z <= half - 2f; z += 4f * scale)
                {
                    float h = ores.SurfaceAt(x, z);
                    low = Mathf.Min(low, h);
                    high = Mathf.Max(high, h);
                }
            }
            frameCentreY = low + (high - low) * 0.5f + 1f;
            frameHeight = Mathf.Max(22f * scale, (high - low) * 0.7f + 14f);
            UnityEngine.Debug.Log($"MenuDiorama: ground {low:0.0}-{high:0.0} m, framing {frameCentreY:0.0} +- {frameHeight:0.0}");
            ores.StripSoilFromSkirt();
            ores.StampAll();
            Vector3 tin = ores.TinOnSurface;
            Vector2 fire = new Vector2(-1.5f * scale, -half + 5.5f);
            BuildCampfire(new Vector3(fire.x, ores.SurfaceAt(fire.x, fire.y), fire.y));
            BuildMiner(new Vector3(tin.x, ores.SurfaceAt(tin.x, tin.z + 2.2f), tin.z + 2.2f));
        }

        // Points the camera down at the block from the front, then slides it so the block sits right of the menu.
        private void Frame()
        {
            var centre = new Vector3(0f, frameCentreY, 0f);
            Vector3 from = Quaternion.Euler(cameraPitch, -22f + Mathf.Sin(Time.time * 0.12f) * sway, 0f) * Vector3.back;
            float halfHeight = Mathf.Tan(view.fieldOfView * 0.5f * Mathf.Deg2Rad);
            // Far enough for the block (and its trees) to fill about two thirds of the screen's height.
            float distance = frameHeight * 1.35f / halfHeight;
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
            // The game's own fire while a campfire works (StationFire): the dancing flame cluster and the smoke, with the
            // campfire prefab's own materials and sizes.
            if (flameMaterial != null)
            {
                flame = new FlameCluster(fire, new Vector3(0f, 0.08f, 0f), 0.4f, flameMaterial);
            }
            if (smokeMaterial != null)
            {
                TorchFlame.MakeSmoke(fire, new Vector3(0f, 0.08f + 0.4f * 0.6f, 0f), smokeMaterial);
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
