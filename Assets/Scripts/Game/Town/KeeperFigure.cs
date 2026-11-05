using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>What a shopkeeper's body is doing (GL404-GL406), which <see cref="KeeperFigure"/> animates.</summary>
    public enum KeeperPose
    {
        Stand,
        Walk,
        Sit,
        Lie,
        Hammer,
        Stoke,
        Sweep,
        Drink,
        Examine,
        Read,
        Whittle,
    }

    /// <summary>
    /// A shopkeeper's low-poly body (GL404, first pass of NP1): blocky legs, torso, arms and head on joints, dressed by trade
    /// (the blacksmith's apron, the miner's cap, the merchant's hat, the woodsman's red shirt, the prospector's robe), and
    /// a prop in the right hand for the chores (hammer, broom, mug, book, knife). Poses are made in code by turning the
    /// joints each frame: no rig or animation clips. The figure's origin is between its feet, facing +z.
    /// </summary>
    public class KeeperFigure : MonoBehaviour
    {
        private const float HipHeight = 0.86f;
        private const float SeatHeight = 0.47f;

        private static readonly Color Skin = new Color(0.8f, 0.6f, 0.46f);
        private static readonly Color Trousers = new Color(0.27f, 0.24f, 0.22f);
        private static readonly Color Boots = new Color(0.2f, 0.14f, 0.1f);
        private static readonly Color Wood = new Color(0.45f, 0.31f, 0.18f);
        private static readonly Color Iron = new Color(0.3f, 0.31f, 0.33f);

        private Transform body;
        private Transform hips;
        private Transform torso;
        private Transform head;
        private Transform armLeft;
        private Transform armRight;
        private Transform legLeft;
        private Transform legRight;
        private GameObject prop;
        private float phase;

        /// <summary>The pose being shown.</summary>
        public KeeperPose Pose { get; set; }

        /// <summary>Builds the body for a trade, in a material for vertex-coloured models.</summary>
        public void Build(ShopKind kind, Material material, int seed)
        {
            var random = new System.Random(seed);
            phase = (float)random.NextDouble() * 10f;
            Look look = LookOf(kind);
            body = new GameObject("Body").transform;
            body.SetParent(transform, false);
            hips = Part("Hips", body, new Vector3(0f, HipHeight, 0f), null, material);
            legLeft = Part("Leg L", hips, new Vector3(-0.1f, 0f, 0f), Leg(look), material);
            legRight = Part("Leg R", hips, new Vector3(0.1f, 0f, 0f), Leg(look), material);
            torso = Part("Torso", hips, Vector3.zero, Torso(look), material);
            head = Part("Head", torso, new Vector3(0f, 0.64f, 0f), Head(look), material);
            armLeft = Part("Arm L", torso, new Vector3(-0.29f, 0.58f, 0f), Arm(look), material);
            armRight = Part("Arm R", torso, new Vector3(0.29f, 0.58f, 0f), Arm(look), material);
            Mesh propMesh = Prop(kind);
            if (propMesh != null)
            {
                prop = Part("Prop", armRight, new Vector3(0f, -0.56f, 0.03f), propMesh, material).gameObject;
            }
            // Something to look at for E: the whole figure, turning with it when it lies down.
            var capsule = body.gameObject.AddComponent<CapsuleCollider>();
            capsule.center = new Vector3(0f, 0.9f, 0f);
            capsule.height = 1.8f;
            capsule.radius = 0.3f;
        }

        private static Transform Part(string name, Transform parent, Vector3 at, Mesh mesh, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at;
            if (mesh != null)
            {
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterial = material;
            }
            return go.transform;
        }

        /// <summary>
        /// Lays the figure on its back along <paramref name="along"/> (head that way), its middle over <paramref name="centre"/>
        /// (world positions), or stands it back up when <paramref name="lie"/> is false.
        /// </summary>
        public void LieAlong(bool lie, Vector3 centre, Vector3 along)
        {
            if (body == null)
            {
                return;
            }
            if (!lie)
            {
                body.localPosition = Vector3.zero;
                body.localRotation = Quaternion.identity;
                return;
            }
            Vector3 up = transform.parent != null ? transform.parent.up : Vector3.up;
            body.rotation = Quaternion.LookRotation(up, along);
            body.position = centre - along.normalized * 0.88f + up * 0.24f;
        }

        private void Update()
        {
            if (body == null)
            {
                return;
            }
            float t = Time.time + phase;
            Reset();
            switch (Pose)
            {
                case KeeperPose.Walk:
                {
                    float swing = Mathf.Sin(t * 7f);
                    legLeft.localRotation = Quaternion.Euler(swing * 28f, 0f, 0f);
                    legRight.localRotation = Quaternion.Euler(-swing * 28f, 0f, 0f);
                    armLeft.localRotation = Quaternion.Euler(-swing * 22f, 0f, 0f);
                    armRight.localRotation = Quaternion.Euler(swing * 22f, 0f, 0f);
                    hips.localPosition = new Vector3(0f, HipHeight + Mathf.Abs(Mathf.Cos(t * 7f)) * 0.04f, 0f);
                    break;
                }
                case KeeperPose.Sit:
                    Sit();
                    break;
                case KeeperPose.Lie:
                    // Asleep: still, breathing slowly.
                    torso.localScale = new Vector3(1f, 1f, 1f + Mathf.Sin(t * 1.4f) * 0.04f);
                    head.localRotation = Quaternion.Euler(-10f, 0f, 0f);
                    break;
                case KeeperPose.Hammer:
                {
                    // Up slowly, down hard on the anvil.
                    float beat = Mathf.Repeat(t * 0.9f, 1f);
                    float up = beat < 0.75f ? beat / 0.75f : 1f - (beat - 0.75f) / 0.25f;
                    armRight.localRotation = Quaternion.Euler(-40f - up * 110f, 0f, 0f);
                    armLeft.localRotation = Quaternion.Euler(-45f, 0f, 10f);
                    torso.localRotation = Quaternion.Euler(10f + (1f - up) * 6f, 0f, 0f);
                    head.localRotation = Quaternion.Euler(20f, 0f, 0f);
                    break;
                }
                case KeeperPose.Stoke:
                {
                    float push = Mathf.Sin(t * 2.2f) * 0.5f + 0.5f;
                    armLeft.localRotation = Quaternion.Euler(-60f - push * 15f, 0f, 0f);
                    armRight.localRotation = Quaternion.Euler(-60f - push * 15f, 0f, 0f);
                    torso.localRotation = Quaternion.Euler(12f + push * 8f, 0f, 0f);
                    break;
                }
                case KeeperPose.Sweep:
                {
                    float stroke = Mathf.Sin(t * 3f);
                    armLeft.localRotation = Quaternion.Euler(-40f, 0f, -10f);
                    armRight.localRotation = Quaternion.Euler(-30f, 0f, 0f);
                    torso.localRotation = Quaternion.Euler(12f, stroke * 18f, 0f);
                    legLeft.localRotation = Quaternion.Euler(Mathf.Sin(t * 3.5f) * 14f, 0f, 0f);
                    legRight.localRotation = Quaternion.Euler(-Mathf.Sin(t * 3.5f) * 14f, 0f, 0f);
                    break;
                }
                case KeeperPose.Drink:
                {
                    Sit();
                    // A long sip every few seconds, head back.
                    float cycle = Mathf.Repeat(t, 6f);
                    float sip = cycle < 1f ? cycle : cycle < 2.5f ? 1f : cycle < 3.2f ? 1f - (cycle - 2.5f) / 0.7f : 0f;
                    armRight.localRotation = Quaternion.Euler(-30f - sip * 125f, 0f, -sip * 20f);
                    head.localRotation = Quaternion.Euler(-sip * 25f, 0f, 0f);
                    armLeft.localRotation = Quaternion.Euler(-20f, 0f, 8f);
                    break;
                }
                case KeeperPose.Examine:
                {
                    torso.localRotation = Quaternion.Euler(24f, 0f, 0f);
                    head.localRotation = Quaternion.Euler(22f, Mathf.Sin(t * 0.6f) * 25f, 0f);
                    armRight.localRotation = Quaternion.Euler(-55f + Mathf.Sin(t * 1.3f) * 10f, 0f, 0f);
                    armLeft.localRotation = Quaternion.Euler(-35f, 0f, 0f);
                    break;
                }
                case KeeperPose.Read:
                    armRight.localRotation = Quaternion.Euler(-70f, 0f, -18f);
                    armLeft.localRotation = Quaternion.Euler(-70f, 0f, 18f);
                    head.localRotation = Quaternion.Euler(28f + Mathf.Sin(t * 0.4f) * 3f, 0f, 0f);
                    break;
                case KeeperPose.Whittle:
                    Sit();
                    armLeft.localRotation = Quaternion.Euler(-55f, 0f, 15f);
                    armRight.localRotation = Quaternion.Euler(-50f + Mathf.Sin(t * 5f) * 12f, 0f, -15f);
                    head.localRotation = Quaternion.Euler(30f, 0f, 0f);
                    break;
                default:
                    // Standing: a little sway and breath, now and then a glance about.
                    torso.localRotation = Quaternion.Euler(Mathf.Sin(t * 1.1f) * 1.5f, 0f, 0f);
                    head.localRotation = Quaternion.Euler(0f, Mathf.Sin(t * 0.37f) * 20f, 0f);
                    armLeft.localRotation = Quaternion.Euler(0f, 0f, -4f);
                    armRight.localRotation = Quaternion.Euler(0f, 0f, 4f);
                    break;
            }
            if (prop != null)
            {
                prop.SetActive(PropShown(Pose));
            }
        }

        private void Reset()
        {
            hips.localPosition = new Vector3(0f, HipHeight, 0f);
            hips.localRotation = Quaternion.identity;
            torso.localRotation = Quaternion.identity;
            torso.localScale = Vector3.one;
            head.localRotation = Quaternion.identity;
            armLeft.localRotation = Quaternion.identity;
            armRight.localRotation = Quaternion.identity;
            legLeft.localRotation = Quaternion.identity;
            legRight.localRotation = Quaternion.identity;
        }

        // Seated: hips down on the seat, legs out in front.
        private void Sit()
        {
            hips.localPosition = new Vector3(0f, SeatHeight, 0f);
            legLeft.localRotation = Quaternion.Euler(-85f, -6f, 0f);
            legRight.localRotation = Quaternion.Euler(-85f, 6f, 0f);
        }

        // The prop is out only for the chore it belongs to.
        private static bool PropShown(KeeperPose pose)
        {
            return pose == KeeperPose.Hammer || pose == KeeperPose.Sweep || pose == KeeperPose.Drink || pose == KeeperPose.Read
                || pose == KeeperPose.Whittle;
        }

        // How a trade dresses.
        private struct Look
        {
            public Color Shirt;
            public Color Apron;
            public bool HasApron;
            public Color Hair;
            public Color Hat;
            public int HatStyle;
            public bool Beard;
            public bool Robe;
        }

        private static Look LookOf(ShopKind kind)
        {
            switch (kind)
            {
                case ShopKind.Blacksmith:
                    return new Look { Shirt = new Color(0.52f, 0.5f, 0.46f), Apron = new Color(0.28f, 0.18f, 0.12f), HasApron = true, Hair = new Color(0.2f, 0.14f, 0.1f), Beard = true };
                case ShopKind.Mining:
                    return new Look { Shirt = new Color(0.66f, 0.52f, 0.28f), Hair = new Color(0.3f, 0.2f, 0.12f), Hat = new Color(0.42f, 0.3f, 0.18f), HatStyle = 1, Beard = true };
                case ShopKind.Lumber:
                    return new Look { Shirt = new Color(0.66f, 0.18f, 0.16f), Hair = new Color(0.5f, 0.3f, 0.15f), Hat = new Color(0.2f, 0.28f, 0.22f), HatStyle = 1, Beard = true };
                case ShopKind.Prospector:
                    return new Look { Shirt = new Color(0.24f, 0.32f, 0.55f), Hair = new Color(0.85f, 0.85f, 0.82f), Hat = new Color(0.2f, 0.24f, 0.4f), HatStyle = 2, Robe = true };
                default:
                    return new Look { Shirt = new Color(0.25f, 0.48f, 0.3f), Apron = new Color(0.82f, 0.78f, 0.66f), HasApron = true, Hair = new Color(0.12f, 0.1f, 0.08f), Hat = new Color(0.55f, 0.2f, 0.18f), HatStyle = 2 };
            }
        }

        // A leg hanging from the hip: trouser, boot with the toe forward (a robe covers the legs to the boots).
        private static Mesh Leg(Look look)
        {
            var model = new LowPolyMeshBuilder();
            model.Box(new Vector3(0f, -0.39f, 0f), new Vector3(0.17f, 0.78f, 0.19f), Quaternion.identity, look.Robe ? look.Shirt * 0.9f : Trousers);
            model.Box(new Vector3(0f, -0.8f, 0.04f), new Vector3(0.18f, 0.1f, 0.27f), Quaternion.identity, Boots);
            return model.Make("Keeper leg");
        }

        // The torso from the hip up: tunic, belt, and an apron or the robe's skirt.
        private static Mesh Torso(Look look)
        {
            var model = new LowPolyMeshBuilder();
            model.Box(new Vector3(0f, 0.32f, 0f), new Vector3(0.44f, 0.64f, 0.26f), Quaternion.identity, look.Shirt);
            model.Box(new Vector3(0f, 0.06f, 0f), new Vector3(0.46f, 0.07f, 0.28f), Quaternion.identity, Boots);
            if (look.HasApron)
            {
                model.Box(new Vector3(0f, 0.12f, 0.14f), new Vector3(0.36f, 0.72f, 0.03f), Quaternion.identity, look.Apron);
            }
            if (look.Robe)
            {
                model.Box(new Vector3(0f, -0.25f, 0f), new Vector3(0.48f, 0.5f, 0.3f), Quaternion.identity, look.Shirt * 0.92f);
            }
            return model.Make("Keeper torso");
        }

        // The head on the neck: face, hair or hat, nose, maybe a beard.
        private static Mesh Head(Look look)
        {
            var model = new LowPolyMeshBuilder();
            model.Box(new Vector3(0f, 0.04f, 0f), new Vector3(0.1f, 0.08f, 0.1f), Quaternion.identity, Skin * 0.95f);
            model.Box(new Vector3(0f, 0.2f, 0f), new Vector3(0.25f, 0.27f, 0.25f), Quaternion.identity, Skin);
            model.Box(new Vector3(0f, 0.19f, 0.135f), new Vector3(0.05f, 0.07f, 0.04f), Quaternion.identity, Skin * 0.92f);
            model.Box(new Vector3(-0.06f, 0.24f, 0.127f), new Vector3(0.035f, 0.025f, 0.01f), Quaternion.identity, new Color(0.1f, 0.08f, 0.07f));
            model.Box(new Vector3(0.06f, 0.24f, 0.127f), new Vector3(0.035f, 0.025f, 0.01f), Quaternion.identity, new Color(0.1f, 0.08f, 0.07f));
            if (look.Beard)
            {
                model.Box(new Vector3(0f, 0.1f, 0.1f), new Vector3(0.24f, 0.12f, 0.08f), Quaternion.identity, look.Hair);
            }
            switch (look.HatStyle)
            {
                case 1:
                    // A close cap.
                    model.Box(new Vector3(0f, 0.35f, 0f), new Vector3(0.27f, 0.08f, 0.27f), Quaternion.identity, look.Hat);
                    break;
                case 2:
                    // A brimmed hat.
                    model.Box(new Vector3(0f, 0.34f, 0f), new Vector3(0.4f, 0.03f, 0.4f), Quaternion.identity, look.Hat);
                    model.Box(new Vector3(0f, 0.42f, 0f), new Vector3(0.24f, 0.14f, 0.24f), Quaternion.identity, look.Hat * 0.9f);
                    break;
                default:
                    model.Box(new Vector3(0f, 0.32f, -0.02f), new Vector3(0.27f, 0.06f, 0.27f), Quaternion.identity, look.Hair);
                    break;
            }
            model.Box(new Vector3(0f, 0.22f, -0.12f), new Vector3(0.26f, 0.2f, 0.04f), Quaternion.identity, look.Hair);
            return model.Make("Keeper head");
        }

        // An arm hanging from the shoulder: sleeve, then the hand.
        private static Mesh Arm(Look look)
        {
            var model = new LowPolyMeshBuilder();
            model.Box(new Vector3(0f, -0.24f, 0f), new Vector3(0.12f, 0.48f, 0.13f), Quaternion.identity, look.Shirt * 0.95f);
            model.Box(new Vector3(0f, -0.53f, 0f), new Vector3(0.1f, 0.11f, 0.11f), Quaternion.identity, Skin);
            return model.Make("Keeper arm");
        }

        // The tool of the trade, held in the right hand with the arm pointing down: along -y is along the arm.
        private static Mesh Prop(ShopKind kind)
        {
            var model = new LowPolyMeshBuilder();
            switch (kind)
            {
                case ShopKind.Blacksmith:
                    // A hammer: handle forward out of the fist, head at its end.
                    model.Prism(new Vector3(0f, 0f, -0.05f), new Vector3(0f, 0f, 0.3f), 0.016f, 0.014f, 5, Wood);
                    model.Box(new Vector3(0f, 0f, 0.32f), new Vector3(0.06f, 0.14f, 0.06f), Quaternion.identity, Iron);
                    break;
                case ShopKind.Wares:
                    // A broom reaching down and forward to the ground.
                    model.Prism(new Vector3(0f, 0.3f, -0.1f), new Vector3(0f, -0.75f, 0.45f), 0.018f, 0.018f, 5, Wood);
                    model.Box(new Vector3(0f, -0.82f, 0.5f), new Vector3(0.3f, 0.12f, 0.08f), Quaternion.Euler(-28f, 0f, 0f), new Color(0.75f, 0.65f, 0.38f));
                    break;
                case ShopKind.Mining:
                    // A mug of beer, foam on top.
                    model.Prism(new Vector3(0f, -0.03f, 0.05f), new Vector3(0f, 0.09f, 0.05f), 0.045f, 0.045f, 7, new Color(0.5f, 0.36f, 0.22f));
                    model.Prism(new Vector3(0f, 0.09f, 0.05f), new Vector3(0f, 0.11f, 0.05f), 0.042f, 0.04f, 7, new Color(0.95f, 0.92f, 0.82f));
                    break;
                case ShopKind.Prospector:
                    // A small closed book.
                    model.Box(new Vector3(0f, -0.02f, 0.08f), new Vector3(0.05f, 0.2f, 0.15f), Quaternion.identity, new Color(0.45f, 0.15f, 0.12f));
                    model.Box(new Vector3(0.005f, -0.02f, 0.08f), new Vector3(0.042f, 0.19f, 0.14f), Quaternion.identity, new Color(0.88f, 0.84f, 0.72f));
                    break;
                case ShopKind.Lumber:
                    // A stick being whittled with a knife.
                    model.Prism(new Vector3(0f, 0f, -0.05f), new Vector3(0f, 0f, 0.25f), 0.012f, 0.01f, 5, Wood);
                    break;
                default:
                    return null;
            }
            return model.Make($"{kind} prop");
        }
    }
}
