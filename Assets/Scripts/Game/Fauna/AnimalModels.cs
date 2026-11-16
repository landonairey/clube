using System.Collections.Generic;
using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// An animal's built body (GL604): the parts that move and the sizes its poses need. <see cref="Pose"/> sets the
    /// whole stance from a few numbers, so the walk, the graze, lying down and the carcass are one piece of code.
    /// </summary>
    public sealed class AnimalRig
    {
        public Transform Model;
        public Transform Head;
        public Transform[] Legs;

        /// <summary>Height of the legs' hinges, of the trunk's middle and half the trunk's width, in metres.</summary>
        public float LegTop, TrunkMiddle, HalfWidth;

        /// <summary>
        /// Sets the stance. <paramref name="stride"/> is the walk's phase in radians and <paramref name="swing"/> how far
        /// the legs swing, in degrees; <paramref name="headDip"/> lowers the head, in degrees; <paramref name="lie"/> (0-1)
        /// folds the legs and settles the body on the ground; <paramref name="onSide"/> (0-1) rolls it onto its side, the
        /// carcass; <paramref name="lift"/> raises the body, in metres (a hop when hit).
        /// </summary>
        public void Pose(float stride, float swing, float headDip, float lie, float onSide, float lift)
        {
            float settle = -lie * LegTop * 0.8f * (1f - onSide);
            Model.localPosition = new Vector3(TrunkMiddle * onSide, settle + HalfWidth * onSide + lift, 0f);
            Model.localRotation = Quaternion.Euler(0f, 0f, 90f * onSide);
            Head.localRotation = Quaternion.Euler(headDip, 0f, 0f);
            for (int i = 0; i < Legs.Length; i++)
            {
                // Diagonal pairs step together.
                float walk = Mathf.Sin(stride + (i == 0 || i == 3 ? 0f : Mathf.PI)) * swing;
                Legs[i].localRotation = Quaternion.Euler(Mathf.Lerp(walk, -82f, lie * (1f - onSide)), 0f, 0f);
            }
        }
    }

    /// <summary>
    /// Builds the animals' low-poly bodies in code (GL604), in the faceted look of the figures to come (GL608): a trunk, a
    /// head on its neck and four legs, each a few flat-shaded shapes from <see cref="LowPolyMeshBuilder"/>, shaped by the
    /// species' size and its <see cref="AnimalLook"/> (ears, horns and antlers, a hump, wool, a long neck). The head and
    /// legs are their own parts so they can swing. Meshes are made once per species and shared by its animals.
    /// </summary>
    public static class AnimalModels
    {
        private sealed class Meshes
        {
            public Mesh Trunk, Head, Leg;
        }

        private static readonly Dictionary<string, Meshes> Cache = new Dictionary<string, Meshes>();

        /// <summary>Builds a species' body under <paramref name="root"/> (its origin on the ground, facing +Z).</summary>
        public static AnimalRig Build(Transform root, AnimalSpecies species, Material material)
        {
            AnimalLook look = AnimalLooks.For(species.Id);
            Sizes s = Sizes.Of(species, look);
            if (!Cache.TryGetValue(species.Id, out Meshes meshes) || meshes.Trunk == null)
            {
                meshes = new Meshes { Trunk = Trunk(species, look, s), Head = Head(species, look, s), Leg = Leg(species, look, s) };
                Cache[species.Id] = meshes;
            }
            var rig = new AnimalRig { LegTop = s.LegTop, TrunkMiddle = s.Middle, HalfWidth = s.HalfWidth };
            rig.Model = new GameObject("Model").transform;
            rig.Model.SetParent(root, false);
            Part("Trunk", rig.Model, Vector3.zero, meshes.Trunk, material);
            rig.Head = Part("Head", rig.Model, s.NeckBase, meshes.Head, material);
            rig.Legs = new Transform[4];
            for (int i = 0; i < 4; i++)
            {
                var at = new Vector3((i % 2 == 0 ? -1f : 1f) * s.HalfWidth * 0.55f, s.LegTop, (i < 2 ? 1f : -1f) * s.TrunkLength * 0.32f);
                rig.Legs[i] = Part("Leg " + i, rig.Model, at, meshes.Leg, material);
            }
            return rig;
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

        // The measures the parts share, in metres.
        private struct Sizes
        {
            public float TrunkLength, HalfWidth, HalfDepth, Middle, LegLength, LegTop, HeadLength;
            public Vector3 NeckBase;

            public static Sizes Of(AnimalSpecies species, AnimalLook look)
            {
                var s = new Sizes();
                float height = species.ShoulderHeight;
                s.TrunkLength = species.Length * look.Trunk;
                s.HalfWidth = s.TrunkLength * look.Girth * 0.5f;
                s.LegLength = height * look.Legs;
                // The trunk fills what the legs leave of the height, and a little of the legs.
                s.HalfDepth = (height - s.LegLength * 0.85f) * 0.5f;
                s.Middle = height - s.HalfDepth;
                s.LegTop = s.LegLength * 1.08f;
                s.HeadLength = species.Length * look.Head;
                s.NeckBase = new Vector3(0f, height - s.HalfDepth * 0.75f, s.TrunkLength * 0.4f);
                return s;
            }
        }

        private static Mesh Trunk(AnimalSpecies species, AnimalLook look, Sizes s)
        {
            var b = new LowPolyMeshBuilder();
            var middle = new Vector3(0f, s.Middle, 0f);
            b.Blob(middle, new Vector3(s.HalfWidth, s.HalfDepth, s.TrunkLength * 0.5f), Quaternion.identity, look.Coat, 7, 5);
            if (look.Woolly)
            {
                // Lumps of fleece over the back and flanks.
                Color pale = Color.Lerp(look.Coat, Color.white, 0.12f), shade = Color.Lerp(look.Coat, Color.black, 0.08f);
                for (int i = 0; i < 7; i++)
                {
                    float along = (i / 6f - 0.5f) * s.TrunkLength * 0.75f, side = (i % 3 - 1) * s.HalfWidth * 0.55f;
                    var at = new Vector3(side, s.Middle + s.HalfDepth * (i % 3 == 1 ? 0.45f : 0.15f), along);
                    b.Blob(at, new Vector3(s.HalfWidth * 0.75f, s.HalfDepth * 0.75f, s.TrunkLength * 0.24f), Quaternion.Euler(0f, i * 40f, 0f),
                        i % 2 == 0 ? pale : shade, 5, 3);
                }
            }
            if (look.Hump > 0f)
            {
                b.Blob(new Vector3(0f, s.Middle + s.HalfDepth * (0.35f + look.Hump * 0.5f), s.TrunkLength * 0.2f),
                    new Vector3(s.HalfWidth * 0.9f, s.HalfDepth * (0.6f + look.Hump * 0.5f), s.TrunkLength * 0.3f), Quaternion.Euler(-12f, 0f, 0f),
                    Color.Lerp(look.Coat, Color.black, 0.12f), 6, 4);
            }
            if (look.Shaggy)
            {
                Color skirt = Color.Lerp(look.Coat, Color.black, 0.2f);
                for (int side = -1; side <= 1; side += 2)
                {
                    b.Blob(new Vector3(side * s.HalfWidth * 0.7f, s.Middle - s.HalfDepth * 0.75f, 0f),
                        new Vector3(s.HalfWidth * 0.5f, s.HalfDepth * 0.7f, s.TrunkLength * 0.46f), Quaternion.identity, skirt, 5, 4);
                }
            }
            var rump = new Vector3(0f, s.Middle + s.HalfDepth * 0.35f, -s.TrunkLength * 0.47f);
            if (look.Tail <= 0f)
            {
                float tuft = Mathf.Max(s.HalfWidth * 0.3f, species.Length * 0.05f);
                b.Blob(rump + new Vector3(0f, 0f, -tuft * 0.5f), new Vector3(tuft, tuft, tuft), Quaternion.identity, Color.Lerp(look.Coat, Color.white, 0.35f), 5, 3);
            }
            else
            {
                float length = species.Length * look.Tail;
                b.Prism(rump, rump + new Vector3(0f, -length, -length * 0.3f), length * 0.09f + 0.01f, length * 0.05f + 0.008f, 4, look.Skin);
            }
            return b.Make(species.Name + " trunk");
        }

        private static Mesh Leg(AnimalSpecies species, AnimalLook look, Sizes s)
        {
            var b = new LowPolyMeshBuilder();
            float radius = s.LegLength * look.LegThickness;
            Color upper = look.Woolly ? look.Coat : look.Skin;
            b.Prism(Vector3.zero, new Vector3(0f, -s.LegTop * 0.5f, 0f), radius * 1.25f, radius, 5, upper);
            b.Prism(new Vector3(0f, -s.LegTop * 0.5f, 0f), new Vector3(0f, -s.LegTop, 0f), radius, radius * 0.75f, 5, look.Skin);
            return b.Make(species.Name + " leg");
        }

        // The neck and head, about the neck's base: the part that dips to graze.
        private static Mesh Head(AnimalSpecies species, AnimalLook look, Sizes s)
        {
            var b = new LowPolyMeshBuilder();
            float h = s.HeadLength, rise = look.NeckRise * Mathf.Deg2Rad;
            var along = new Vector3(0f, Mathf.Sin(rise), Mathf.Cos(rise));
            Vector3 neckEnd = along * (species.Length * look.Neck);
            bool longWool = look.Woolly && look.Neck >= 0.3f;
            float neckBase = Mathf.Min(s.HalfDepth * 0.6f, h * 0.7f) * (longWool ? 1.1f : 1f), neckTop = h * (longWool ? 0.36f : 0.28f);
            b.Prism(-along * (s.HalfDepth * 0.4f), neckEnd, neckBase, neckTop, 6, look.Coat);

            // The head hangs a little nose-down from the neck's end.
            Quaternion tilt = Quaternion.Euler(18f, 0f, 0f);
            Vector3 centre = neckEnd + tilt * new Vector3(0f, h * 0.05f, h * 0.28f);
            Color face = look.Woolly ? look.Skin : look.Coat;
            b.Blob(centre, new Vector3(h * 0.3f, h * 0.32f, h * 0.48f), tilt, face, 6, 4);
            b.Blob(centre + tilt * new Vector3(0f, -h * 0.07f, h * 0.42f), new Vector3(h * 0.2f, h * 0.19f, h * 0.24f), tilt, look.Skin, 5, 3);
            if (look.Woolly)
            {
                b.Blob(centre + tilt * new Vector3(0f, h * 0.26f, -h * 0.12f), new Vector3(h * 0.3f, h * 0.2f, h * 0.3f), tilt,
                    Color.Lerp(look.Coat, Color.white, 0.12f), 5, 3);
            }

            float ear = h * look.Ears;
            for (int side = -1; side <= 1; side += 2)
            {
                if (look.EarsUp)
                {
                    Vector3 root = centre + tilt * new Vector3(side * h * 0.15f, h * 0.25f, -h * 0.25f);
                    b.Diamond(root + tilt * new Vector3(side * ear * 0.08f, ear * 0.5f, -ear * 0.1f), new Vector3(h * 0.09f, ear * 0.5f, h * 0.05f), look.Coat);
                }
                else
                {
                    Vector3 root = centre + tilt * new Vector3(side * h * 0.27f, h * 0.16f, -h * 0.3f);
                    b.Diamond(root + tilt * new Vector3(side * ear * 0.45f, 0f, 0f), new Vector3(ear * 0.5f, h * 0.05f, h * 0.1f), look.Coat);
                }
                Horns(b, look, centre, tilt, h, side);
            }
            if (look.Beard)
            {
                Vector3 chin = centre + tilt * new Vector3(0f, -h * 0.25f, h * 0.2f);
                b.Prism(chin, chin + tilt * new Vector3(0f, -h * 0.6f, -h * 0.05f), h * 0.13f, h * 0.03f, 4, Color.Lerp(look.Coat, Color.black, 0.2f));
            }
            return b.Make(species.Name + " head");
        }

        // One side's horn or antler, as tapering sticks from point to point (in head lengths about the head's middle).
        private static void Horns(LowPolyMeshBuilder b, AnimalLook look, Vector3 centre, Quaternion tilt, float h, int side)
        {
            Vector3 P(float x, float y, float z) => centre + tilt * new Vector3(side * x * h, y * h, z * h);
            void Stick(Vector3 from, Vector3 to, float thick, float thin) => b.Prism(from, to, thick * h, thin * h, 4, look.Horn);
            switch (look.Horns)
            {
                case AnimalHorns.Goat:
                    Stick(P(0.12f, 0.26f, -0.12f), P(0.18f, 0.72f, -0.42f), 0.07f, 0.05f);
                    Stick(P(0.18f, 0.72f, -0.42f), P(0.22f, 0.8f, -0.85f), 0.05f, 0.015f);
                    break;
                case AnimalHorns.Cow:
                    Stick(P(0.22f, 0.24f, -0.18f), P(0.58f, 0.34f, -0.14f), 0.07f, 0.05f);
                    Stick(P(0.58f, 0.34f, -0.14f), P(0.68f, 0.66f, -0.08f), 0.05f, 0.015f);
                    break;
                case AnimalHorns.Yak:
                    Stick(P(0.22f, 0.24f, -0.18f), P(0.85f, 0.3f, -0.12f), 0.08f, 0.06f);
                    Stick(P(0.85f, 0.3f, -0.12f), P(1.08f, 0.9f, 0f), 0.06f, 0.015f);
                    break;
                case AnimalHorns.Bison:
                    Stick(P(0.26f, 0.2f, -0.12f), P(0.5f, 0.26f, -0.12f), 0.08f, 0.06f);
                    Stick(P(0.5f, 0.26f, -0.12f), P(0.5f, 0.56f, -0.1f), 0.06f, 0.015f);
                    break;
                case AnimalHorns.Antlers:
                    Stick(P(0.12f, 0.28f, -0.18f), P(0.36f, 0.9f, -0.42f), 0.05f, 0.04f);
                    Stick(P(0.36f, 0.9f, -0.42f), P(0.46f, 1.5f, -0.3f), 0.04f, 0.012f);
                    Stick(P(0.36f, 0.9f, -0.42f), P(0.42f, 1.25f, 0.02f), 0.035f, 0.012f);
                    Stick(P(0.2f, 0.5f, -0.26f), P(0.22f, 0.8f, 0.05f), 0.03f, 0.012f);
                    break;
                case AnimalHorns.GreatAntlers:
                    Stick(P(0.14f, 0.28f, -0.18f), P(0.6f, 1f, -0.5f), 0.06f, 0.05f);
                    Stick(P(0.6f, 1f, -0.5f), P(0.8f, 1.9f, -0.62f), 0.05f, 0.012f);
                    Stick(P(0.14f, 0.28f, -0.18f), P(0.3f, 0.72f, 0.26f), 0.04f, 0.012f);
                    Stick(P(0.37f, 0.64f, -0.34f), P(0.56f, 1.02f, 0.08f), 0.04f, 0.012f);
                    Stick(P(0.6f, 1f, -0.5f), P(0.74f, 1.44f, -0.06f), 0.04f, 0.012f);
                    Stick(P(0.7f, 1.45f, -0.56f), P(0.96f, 1.78f, -0.2f), 0.035f, 0.012f);
                    break;
            }
        }
    }
}
