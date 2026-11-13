using System.Collections.Generic;
using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// The trade cart's looks (GL477): a two-wheeled hand cart in low-poly wood with two of the game's wooden crates on its
    /// bed (real storage, so they can be looted), pulled by a carter walking between the shafts. Placed along the
    /// <see cref="CartRoute"/> each frame by <see cref="TradeCart"/>: wheels turn with the distance, the bed tilts with
    /// the slope, and the carter walks while it moves. The origin is under the axle, the front (+z) where the carter pulls.
    /// </summary>
    public class TradeCartModel : MonoBehaviour
    {
        private const float WheelRadius = 0.45f;
        public const float CrateSize = 0.8f;

        // Metres from the axle to the carter, and from the axle to where the bed's ends meet the ground for tilting.
        public const float CarterAhead = 2.1f;
        public const float TiltReach = 1.1f;

        private static readonly Color Wood = new Color(0.5f, 0.35f, 0.2f);
        private static readonly Color DarkWood = new Color(0.36f, 0.24f, 0.14f);
        private static readonly Color Iron = new Color(0.3f, 0.31f, 0.33f);

        private readonly List<Transform> wheels = new List<Transform>();
        private readonly List<StorageChest> crates = new List<StorageChest>();
        private Transform body;
        private Transform carter;
        private FigureRig carterFigure;
        private float lastDistance = float.NaN;
        private float wheelTurn;

        /// <summary>The bed and wheels as one piece, which tilts with the slope (and tips, GL583).</summary>
        public Transform Bed => body;

        /// <summary>The crates on the bed.</summary>
        public IReadOnlyList<StorageChest> Crates => crates;

        /// <summary>Builds the cart, its crates (from <paramref name="cratePrefab"/>) and the carter.</summary>
        public void Build(Material material, GameObject cratePrefab, int seed)
        {
            body = new GameObject("Cart").transform;
            body.SetParent(transform, false);
            Part("Bed", body, Vector3.zero, BedMesh(), material);
            var bodyCollider = body.gameObject.AddComponent<BoxCollider>();
            bodyCollider.center = new Vector3(0f, 0.72f, 0f);
            bodyCollider.size = new Vector3(1.3f, 0.12f, 2.2f);
            Mesh wheel = WheelMesh();
            foreach (float side in new[] { -0.74f, 0.74f })
            {
                wheels.Add(Part("Wheel", body, new Vector3(side, WheelRadius, 0f), wheel, material));
            }
            if (cratePrefab != null)
            {
                foreach (float along in new[] { -0.5f, 0.5f })
                {
                    GameObject crate = Instantiate(cratePrefab, body);
                    crate.name = "Cart crate";
                    crate.transform.localPosition = new Vector3(0f, 0.78f, along);
                    crate.transform.localRotation = Quaternion.Euler(0f, along < 0f ? 180f : 0f, 0f);
                    Fit(crate);
                    if (crate.TryGetComponent(out PlacedObject placed))
                    {
                        placed.Fixed = true;
                        placed.Item = null;
                    }
                    if (crate.TryGetComponent(out StorageChest chest))
                    {
                        chest.MadeOf(1f);
                        crates.Add(chest);
                    }
                }
            }
            carter = new GameObject("Carter").transform;
            carter.SetParent(transform, false);
            carterFigure = carter.gameObject.AddComponent<FigureRig>();
            carterFigure.Build(FigurePreset.Carter, material, seed);
        }

        /// <summary>Scales a crate so its footprint is <paramref name="side"/> metres: to sit on a cart's bed or a deck.</summary>
        public static void Fit(GameObject crate, float side = CrateSize)
        {
            Renderer[] renderers = crate.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                return;
            }
            Bounds bounds = renderers[0].bounds;
            foreach (Renderer r in renderers)
            {
                bounds.Encapsulate(r.bounds);
            }
            float size = Mathf.Max(bounds.size.x, bounds.size.z);
            if (size > 1e-3f)
            {
                crate.transform.localScale *= side / size;
            }
        }

        private static Transform Part(string name, Transform parent, Vector3 at, Mesh mesh, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            return go.transform;
        }

        private static Mesh BedMesh()
        {
            var model = new LowPolyMeshBuilder();
            // The bed, its low sides and the axle under it.
            model.Box(new Vector3(0f, 0.72f, 0f), new Vector3(1.3f, 0.08f, 2.2f), Quaternion.identity, Wood);
            foreach (float side in new[] { -0.62f, 0.62f })
            {
                model.Box(new Vector3(side, 0.9f, 0f), new Vector3(0.06f, 0.28f, 2.2f), Quaternion.identity, DarkWood);
            }
            model.Box(new Vector3(0f, 0.9f, -1.07f), new Vector3(1.3f, 0.28f, 0.06f), Quaternion.identity, DarkWood);
            model.Prism(new Vector3(-0.8f, WheelRadius, 0f), new Vector3(0.8f, WheelRadius, 0f), 0.04f, 0.04f, 6, Iron);
            model.Box(new Vector3(0f, 0.58f, 0f), new Vector3(0.12f, 0.2f, 0.12f), Quaternion.identity, DarkWood);
            // The shafts out the front, ending at the carter's hands.
            foreach (float side in new[] { -0.36f, 0.36f })
            {
                model.Prism(new Vector3(side * 1.4f, 0.72f, 1.0f), new Vector3(side, 0.82f, CarterAhead + 0.2f), 0.035f, 0.03f, 5, DarkWood);
            }
            model.Prism(new Vector3(-0.36f, 0.82f, CarterAhead + 0.15f), new Vector3(0.36f, 0.82f, CarterAhead + 0.15f), 0.03f, 0.03f, 5, DarkWood);
            return model.Make("Trade cart");
        }

        private static Mesh WheelMesh()
        {
            var model = new LowPolyMeshBuilder();
            model.Prism(new Vector3(-0.05f, 0f, 0f), new Vector3(0.05f, 0f, 0f), WheelRadius, WheelRadius, 10, DarkWood);
            model.Prism(new Vector3(-0.09f, 0f, 0f), new Vector3(0.09f, 0f, 0f), 0.09f, 0.09f, 6, Iron);
            // Spokes, so the turning shows.
            model.Box(Vector3.zero, new Vector3(0.12f, WheelRadius * 1.9f, 0.06f), Quaternion.identity, Wood);
            model.Box(Vector3.zero, new Vector3(0.12f, 0.06f, WheelRadius * 1.9f), Quaternion.identity, Wood);
            return model.Make("Trade cart wheel");
        }

        /// <summary>
        /// Puts the cart <paramref name="distance"/> metres along <paramref name="route"/> (from the trading post's end),
        /// heading towards the post when <paramref name="towardsPost"/>. <paramref name="local"/> turns route space (the
        /// world's local metres) into the scene.
        /// </summary>
        public void Place(CartRoute route, float distance, bool towardsPost, bool moving, Transform local)
        {
            float ahead = towardsPost ? -1f : 1f;
            Vector3 axle = route.At(distance);
            Vector3 front = route.At(distance + ahead * TiltReach);
            Vector3 back = route.At(distance - ahead * TiltReach);
            Vector3 carterAt = route.At(distance + ahead * CarterAhead);
            PlaceAt(local.TransformPoint(axle), local.TransformPoint(carterAt), local.TransformDirection(front - back), 0f, moving, distance);
        }

        /// <summary>
        /// Puts the cart in the scene (world space) with its axle at <paramref name="axle"/> and the carter at
        /// <paramref name="carterAt"/>, the bed lying along <paramref name="bedForward"/> and tipped back by
        /// <paramref name="tipDegrees"/> (its front up, GL583). <paramref name="distance"/> is how far it has gone, for the wheels.
        /// </summary>
        public void PlaceAt(Vector3 axle, Vector3 carterAt, Vector3 bedForward, float tipDegrees, bool moving, float distance)
        {
            Vector3 flatForward = carterAt - axle;
            flatForward.y = 0f;
            transform.position = axle;
            if (bedForward.sqrMagnitude > 1e-6f && flatForward.sqrMagnitude > 1e-6f)
            {
                transform.rotation = Quaternion.LookRotation(flatForward);
                body.rotation = Quaternion.LookRotation(bedForward) * Quaternion.Euler(-tipDegrees, 0f, 0f);
            }
            carter.position = carterAt;
            carter.rotation = transform.rotation;
            carterFigure.Pose = moving ? FigureAction.Walk : FigureAction.Stand;
            if (!float.IsNaN(lastDistance))
            {
                wheelTurn += Mathf.Abs(distance - lastDistance) / WheelRadius * Mathf.Rad2Deg;
            }
            lastDistance = distance;
            foreach (Transform wheel in wheels)
            {
                wheel.localRotation = Quaternion.Euler(wheelTurn, 0f, 0f);
            }
        }
    }
}
