using System.Collections.Generic;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>The meshes of one figure's parts, each in its joint's frame; left and right limbs share theirs.</summary>
    public sealed class FigureParts
    {
        public Mesh Pelvis;
        public Mesh Torso;
        public Mesh Head;
        public Mesh UpperArm;
        public Mesh Forearm;
        public Mesh Thigh;
        public Mesh Shin;

        /// <summary>Every part once.</summary>
        public Mesh[] All => new[] { Pelvis, Torso, Head, UpperArm, Forearm, Thigh, Shin };

        /// <summary>Triangles drawn for the whole figure: both arms and both legs counted.</summary>
        public int Triangles => Count(Pelvis) + Count(Torso) + Count(Head) + 2 * (Count(UpperArm) + Count(Forearm) + Count(Thigh) + Count(Shin));

        private static int Count(Mesh mesh)
        {
            return (int)(mesh.GetIndexCount(0) / 3);
        }
    }

    /// <summary>
    /// Builds a low-poly person from a <see cref="FigureLook"/> (GL608): flat-shaded, tapered six-sided limbs and torso
    /// with bevelled joints, a faceted head with a hint of a face, hair or a hat, mitten hands and wedge feet, and the
    /// outfit's pieces as colour blocks and a few added shapes. No GameObjects: the rig (<c>FigureRig</c>) hangs the parts
    /// on <see cref="FigureSkeleton"/>'s joints. Looks that match share one set of meshes.
    /// </summary>
    public static class FigureMeshes
    {
        private const int Sides = 6;
        private static readonly Quaternion Upright = Quaternion.Euler(-90f, 0f, 0f);
        private static readonly Color Eye = new Color(0.1f, 0.08f, 0.07f);
        private static readonly Dictionary<string, FigureParts> Shared = new Dictionary<string, FigureParts>();

        /// <summary>The parts for a look, built once for every figure that looks the same.</summary>
        public static FigureParts For(FigureLook look)
        {
            string key = look.MeshKey();
            if (!Shared.TryGetValue(key, out FigureParts parts) || parts.Torso == null)
            {
                parts = Build(look);
                Shared[key] = parts;
            }
            return parts;
        }

        /// <summary>Builds a look's parts afresh.</summary>
        public static FigureParts Build(FigureLook look)
        {
            return new FigureParts
            {
                Pelvis = Pelvis(look), Torso = Torso(look), Head = Head(look), UpperArm = UpperArm(look), Forearm = Forearm(look),
                Thigh = Thigh(look), Shin = Shin(look),
            };
        }

        // The hips: the seat of the trousers, and whatever hangs from the waist (robe, coat skirts, ragged hem).
        private static Mesh Pelvis(FigureLook look)
        {
            float b = look.Build;
            var model = new LowPolyMeshBuilder();
            Color cloth = look.Has(FigureOutfit.Robe) ? Shade(look.Shirt, 0.9f) : look.Trousers;
            Rings(model, cloth, (-0.13f, 0.11f * b, 0.085f, 0f), (-0.04f, 0.17f * b, 0.115f, 0f), (0.04f, 0.165f * b, 0.11f, 0f));
            if (look.Has(FigureOutfit.Robe))
            {
                Rings(model, Shade(look.Shirt, 0.92f), (-0.56f, 0.25f * b, 0.21f, 0f), (-0.2f, 0.2f * b, 0.16f, 0f), (0.03f, 0.172f * b, 0.118f, 0f));
            }
            if (look.Has(FigureOutfit.Coat))
            {
                // Skirts to the knee with a trimmed hem.
                Rings(model, Shade(look.Shirt, 0.95f), (-0.4f, 0.24f * b, 0.19f, 0f), (0.03f, 0.175f * b, 0.12f, 0f));
                Rings(model, look.Accent, (-0.44f, 0.245f * b, 0.195f, 0f), (-0.4f, 0.245f * b, 0.195f, 0f));
            }
            if (look.Has(FigureOutfit.Patched))
            {
                // A ragged hem: torn tails of the shirt hanging over the hips.
                Color torn = Shade(look.Shirt, 0.85f);
                for (int i = 0; i < Sides; i++)
                {
                    float a0 = Mathf.PI * 2f * i / Sides, a1 = Mathf.PI * 2f * (i + 1) / Sides, mid = (a0 + a1) * 0.5f;
                    Vector3 left = new Vector3(Mathf.Cos(a0) * 0.175f * b, 0.04f, Mathf.Sin(a0) * 0.12f);
                    Vector3 right = new Vector3(Mathf.Cos(a1) * 0.175f * b, 0.04f, Mathf.Sin(a1) * 0.12f);
                    Vector3 tip = new Vector3(Mathf.Cos(mid) * 0.19f * b, -0.1f - (i % 2) * 0.06f, Mathf.Sin(mid) * 0.135f);
                    model.Triangle(left, right, tip, Vector3.zero, torn);
                    model.Triangle(left, tip, right, tip * 2f, torn);
                }
            }
            return model.Make("Figure pelvis");
        }

        // The torso from the hips up: waist, chest, sloping shoulders, neck; then belt, apron, pack, cloak and the rest.
        private static Mesh Torso(FigureLook look)
        {
            float b = look.Build;
            var model = new LowPolyMeshBuilder();
            Rings(model, look.Shirt, (0f, 0.165f * b, 0.11f, 0f), (0.1f, 0.16f * b, 0.105f, 0f), (0.3f, 0.2f * b, 0.13f, 0.01f),
                (0.44f, 0.21f * b, 0.12f, 0f), (0.52f, 0.09f, 0.075f, 0f));
            Rings(model, Shade(look.Skin, 0.95f), (0.5f, 0.058f, 0.058f, 0f), (0.58f, 0.052f, 0.052f, 0f));
            if (look.Has(FigureOutfit.Belt))
            {
                Rings(model, look.Boots, (0.02f, 0.175f * b, 0.12f, 0f), (0.08f, 0.172f * b, 0.117f, 0f));
                model.Box(new Vector3(0f, 0.05f, 0.118f), new Vector3(0.05f, 0.05f, 0.015f), Quaternion.identity, new Color(0.7f, 0.6f, 0.3f));
            }
            if (look.Has(FigureOutfit.Apron))
            {
                // A bib on the chest and a skirt to the knee, a little off the body.
                model.Quad(new Vector3(-0.1f * b, 0.42f, 0.128f), new Vector3(0.1f * b, 0.42f, 0.128f), new Vector3(0.15f * b, 0.1f, 0.122f),
                    new Vector3(-0.15f * b, 0.1f, 0.122f), Vector3.zero, look.Accent);
                model.Quad(new Vector3(-0.15f * b, 0.1f, 0.122f), new Vector3(0.15f * b, 0.1f, 0.122f), new Vector3(0.17f * b, -0.36f, 0.15f),
                    new Vector3(-0.17f * b, -0.36f, 0.15f), Vector3.zero, Shade(look.Accent, 0.93f));
                model.Quad(new Vector3(-0.15f * b, 0.1f, 0.12f), new Vector3(0.15f * b, 0.1f, 0.12f), new Vector3(0.17f * b, -0.36f, 0.148f),
                    new Vector3(-0.17f * b, -0.36f, 0.148f), new Vector3(0f, 0f, 1f), Shade(look.Accent, 0.8f));
            }
            if (look.Has(FigureOutfit.Coat))
            {
                // A cream cravat, two rows of buttons and a trimmed waist.
                model.Blob(new Vector3(0f, 0.44f, 0.11f), new Vector3(0.07f, 0.03f, 0.06f), Upright, new Color(0.92f, 0.9f, 0.82f), 5, 3);
                for (int i = 0; i < 3; i++)
                {
                    float y = 0.34f - i * 0.1f;
                    model.Box(new Vector3(-0.06f, y, 0.128f), new Vector3(0.035f, 0.035f, 0.02f), Quaternion.identity, look.Accent);
                    model.Box(new Vector3(0.06f, y, 0.128f), new Vector3(0.035f, 0.035f, 0.02f), Quaternion.identity, look.Accent);
                }
                Rings(model, look.Accent, (0.03f, 0.178f * b, 0.123f, 0f), (0.07f, 0.176f * b, 0.121f, 0f));
            }
            if (look.Has(FigureOutfit.Patched))
            {
                model.Box(new Vector3(-0.08f * b, 0.33f, 0.132f), new Vector3(0.11f, 0.11f, 0.015f), Quaternion.Euler(0f, 0f, 12f), look.Accent);
                model.Box(new Vector3(0.09f * b, 0.17f, 0.115f), new Vector3(0.09f, 0.1f, 0.015f), Quaternion.Euler(0f, 0f, -8f), Shade(look.Accent, 0.85f));
                model.Box(new Vector3(0.04f, 0.36f, -0.125f), new Vector3(0.13f, 0.1f, 0.015f), Quaternion.Euler(0f, 0f, 6f), look.Accent);
            }
            if (look.Has(FigureOutfit.Scarf))
            {
                Rings(model, look.Accent, (0.45f, 0.14f, 0.115f, 0.01f), (0.49f, 0.125f, 0.105f, 0.01f), (0.54f, 0.085f, 0.08f, 0f));
                model.Box(new Vector3(0.06f, 0.36f, 0.125f), new Vector3(0.06f, 0.16f, 0.02f), Quaternion.Euler(0f, 0f, 8f), Shade(look.Accent, 0.9f));
            }
            if (look.Has(FigureOutfit.Backpack))
            {
                // A pack on the back with a bedroll across the top and straps over the shoulders.
                Rings(model, look.Accent, (0.1f, 0.11f, 0.06f, -0.17f), (0.14f, 0.14f, 0.085f, -0.19f), (0.38f, 0.13f, 0.08f, -0.19f),
                    (0.43f, 0.09f, 0.05f, -0.17f));
                model.Prism(new Vector3(-0.17f, 0.47f, -0.17f), new Vector3(0.17f, 0.47f, -0.17f), 0.05f, 0.05f, 6, Shade(look.Accent, 1.25f));
                for (int side = -1; side <= 1; side += 2)
                {
                    model.Box(new Vector3(side * 0.12f * b, 0.33f, 0.124f), new Vector3(0.035f, 0.24f, 0.015f), Quaternion.identity, Shade(look.Accent, 0.8f));
                }
            }
            if (look.Has(FigureOutfit.Cloak))
            {
                // A collar round the shoulders and a cloak down the back to the calves.
                Color cloak = Shade(look.Shirt, 0.72f);
                Rings(model, cloak, (0.4f, 0.235f * b, 0.15f, 0f), (0.47f, 0.2f * b, 0.13f, 0f), (0.54f, 0.1f, 0.09f, 0f));
                Rings(model, cloak, (-0.5f, 0.27f * b, 0.035f, -0.2f), (0f, 0.24f * b, 0.04f, -0.15f), (0.44f, 0.2f * b, 0.04f, -0.1f));
            }
            return model.Make("Figure torso");
        }

        // The head on the neck: skull and jaw, eyes and a nose, hair or a hat, maybe a beard.
        private static Mesh Head(FigureLook look)
        {
            var model = new LowPolyMeshBuilder { Placement = Matrix4x4.Scale(Vector3.one * look.HeadSize) };
            model.Blob(new Vector3(0f, 0.18f, 0f), new Vector3(0.15f, 0.16f, 0.17f), Upright, look.Skin);
            model.Blob(new Vector3(0f, 0.08f, 0.025f), new Vector3(0.105f, 0.105f, 0.08f), Upright, Shade(look.Skin, 0.97f), 6, 3);
            model.Prism(new Vector3(0f, 0.17f, 0.125f), new Vector3(0f, 0.125f, 0.175f), 0.03f, 0.014f, 4, Shade(look.Skin, 0.9f));
            model.Box(new Vector3(-0.058f, 0.2f, 0.13f), new Vector3(0.03f, 0.036f, 0.02f), Quaternion.identity, Eye);
            model.Box(new Vector3(0.058f, 0.2f, 0.13f), new Vector3(0.03f, 0.036f, 0.02f), Quaternion.identity, Eye);
            if (look.Has(FigureOutfit.Beard))
            {
                model.Blob(new Vector3(0f, 0.065f, 0.06f), new Vector3(0.12f, 0.1f, 0.1f), Upright, look.Hair, 6, 3);
            }
            if (look.HatStyle != FigureHat.Hood)
            {
                // Hair over the crown and down the back; the face shows under its front edge.
                model.Blob(new Vector3(0f, 0.235f, -0.035f), new Vector3(0.158f, 0.165f, 0.135f), Upright, look.Hair);
            }
            switch (look.HatStyle)
            {
                case FigureHat.Cap:
                    Rings(model, look.Hat, (0.27f, 0.165f, 0.172f, -0.01f), (0.34f, 0.15f, 0.155f, -0.015f), (0.385f, 0.09f, 0.095f, -0.02f));
                    model.Box(new Vector3(0f, 0.275f, 0.18f), new Vector3(0.2f, 0.018f, 0.11f), Quaternion.Euler(12f, 0f, 0f), Shade(look.Hat, 0.85f));
                    break;
                case FigureHat.Brimmed:
                    model.Prism(new Vector3(0f, 0.275f, -0.01f), new Vector3(0f, 0.295f, -0.01f), 0.28f, 0.26f, 8, look.Hat);
                    Rings(model, Shade(look.Hat, 0.9f), (0.29f, 0.16f, 0.165f, -0.01f), (0.4f, 0.125f, 0.13f, -0.01f), (0.43f, 0.07f, 0.075f, -0.01f));
                    break;
                case FigureHat.Hood:
                    // Over the top, down the sides and back, a point at the crown, a cowl on the shoulders.
                    model.Blob(new Vector3(0f, 0.2f, -0.055f), new Vector3(0.185f, 0.18f, 0.2f), Upright, look.Hat);
                    model.Prism(new Vector3(0f, 0.36f, -0.08f), new Vector3(0f, 0.43f, -0.16f), 0.07f, 0.01f, 5, Shade(look.Hat, 0.9f));
                    Rings(model, Shade(look.Hat, 0.9f), (-0.06f, 0.17f, 0.14f, -0.01f), (0.04f, 0.13f, 0.12f, -0.02f));
                    break;
                case FigureHat.TopHat:
                    model.Prism(new Vector3(0f, 0.28f, -0.01f), new Vector3(0f, 0.3f, -0.01f), 0.24f, 0.23f, 8, look.Hat);
                    Rings(model, look.Hat, (0.3f, 0.135f, 0.14f, -0.01f), (0.56f, 0.15f, 0.155f, -0.01f));
                    Rings(model, look.Accent, (0.305f, 0.142f, 0.147f, -0.01f), (0.35f, 0.144f, 0.149f, -0.01f));
                    break;
            }
            return model.Make("Figure head");
        }

        // An upper arm hanging from the shoulder, its top rounded off.
        private static Mesh UpperArm(FigureLook look)
        {
            float w = Limb(look);
            var model = new LowPolyMeshBuilder();
            Color sleeve = Shade(look.Shirt, 0.94f);
            Rings(model, sleeve, (-FigureSkeleton.UpperArmLength - 0.02f, 0.05f * w, 0.052f * w, 0f), (-0.04f, 0.066f * w, 0.07f * w, 0f),
                (0.035f, 0.058f * w, 0.062f * w, 0f), (0.07f, 0.025f, 0.03f, 0f));
            if (look.Has(FigureOutfit.Patched))
            {
                model.Box(new Vector3(0.062f * w, -0.14f, 0f), new Vector3(0.015f, 0.09f, 0.08f), Quaternion.identity, look.Accent);
            }
            return model.Make("Figure upper arm");
        }

        // A forearm from the elbow, and a mitten hand with a thumb: large, so it reads from afar.
        private static Mesh Forearm(FigureLook look)
        {
            float w = Limb(look);
            var model = new LowPolyMeshBuilder();
            Color sleeve = look.Has(FigureOutfit.BareArms) ? look.Skin : Shade(look.Shirt, 0.94f);
            Rings(model, sleeve, (-0.23f, 0.04f * w, 0.042f * w, 0f), (-0.03f, 0.054f * w, 0.056f * w, 0f), (0.03f, 0.04f * w, 0.042f * w, 0f));
            if (look.Has(FigureOutfit.Coat))
            {
                Rings(model, look.Accent, (-0.235f, 0.05f * w, 0.052f * w, 0f), (-0.19f, 0.052f * w, 0.054f * w, 0f));
            }
            else if (look.Has(FigureOutfit.BareArms))
            {
                // A rolled sleeve at the elbow.
                Rings(model, Shade(look.Shirt, 0.94f), (-0.05f, 0.06f * w, 0.062f * w, 0f), (0.03f, 0.056f * w, 0.058f * w, 0f));
            }
            model.Blob(new Vector3(0f, -0.285f, 0.005f), new Vector3(0.045f, 0.062f, 0.07f), Upright, look.Skin, 5, 3);
            model.Prism(new Vector3(0f, -0.26f, 0.035f), new Vector3(0f, -0.31f, 0.07f), 0.02f, 0.012f, 4, Shade(look.Skin, 0.93f));
            return model.Make("Figure forearm");
        }

        // A thigh hanging from the hip.
        private static Mesh Thigh(FigureLook look)
        {
            float w = Limb(look);
            var model = new LowPolyMeshBuilder();
            Color cloth = look.Has(FigureOutfit.Robe) ? Shade(look.Shirt, 0.9f) : look.Trousers;
            Rings(model, cloth, (-FigureSkeleton.ThighLength - 0.02f, 0.06f * w, 0.064f * w, 0f), (-0.05f, 0.084f * w, 0.092f * w, 0f),
                (0.04f, 0.06f * w, 0.07f * w, 0f));
            return model.Make("Figure thigh");
        }

        // A shin from the knee, in a boot with the toe forward.
        private static Mesh Shin(FigureLook look)
        {
            float w = Limb(look);
            float sole = -FigureSkeleton.ShinLength;
            var model = new LowPolyMeshBuilder();
            Color cloth = look.Has(FigureOutfit.Robe) ? Shade(look.Shirt, 0.9f) : Shade(look.Trousers, 0.95f);
            Rings(model, cloth, (-0.26f, 0.05f * w, 0.052f * w, 0f), (-0.04f, 0.066f * w, 0.07f * w, 0f), (0.03f, 0.052f * w, 0.056f * w, 0f));
            Rings(model, look.Boots, (sole, 0.062f * w, 0.066f * w, 0f), (sole + 0.03f, 0.066f * w, 0.07f * w, 0f), (-0.22f, 0.062f * w, 0.064f * w, 0f));
            // The foot: a wedge from the heel to a low toe, flat on the ground.
            float half = 0.062f * w;
            Vector3 heelL = new Vector3(-half, sole, 0.02f), heelR = new Vector3(half, sole, 0.02f);
            Vector3 toeL = new Vector3(-half * 0.8f, sole, 0.19f), toeR = new Vector3(half * 0.8f, sole, 0.19f);
            Vector3 topL = new Vector3(-half, sole + 0.09f, 0.04f), topR = new Vector3(half, sole + 0.09f, 0.04f);
            Vector3 tipL = new Vector3(-half * 0.8f, sole + 0.035f, 0.19f), tipR = new Vector3(half * 0.8f, sole + 0.035f, 0.19f);
            Vector3 inside = new Vector3(0f, sole + 0.03f, 0.1f);
            Color boot = Shade(look.Boots, 0.9f);
            model.Quad(heelL, heelR, toeR, toeL, inside, boot);
            model.Quad(topL, topR, tipR, tipL, inside, look.Boots);
            model.Quad(toeL, toeR, tipR, tipL, inside, boot);
            model.Quad(heelL, toeL, tipL, topL, inside, boot);
            model.Quad(heelR, toeR, tipR, topR, inside, boot);
            return model.Make("Figure shin");
        }

        // Limbs thicken with the build, but less than the trunk does.
        private static float Limb(FigureLook look)
        {
            return 0.5f + look.Build * 0.5f;
        }

        // Rings bottom to top: height, radius across x, radius across z, and how far forward the ring's centre is.
        private static void Rings(LowPolyMeshBuilder model, Color color, params (float y, float rx, float rz, float z)[] rings)
        {
            var centres = new Vector3[rings.Length];
            var radii = new Vector2[rings.Length];
            for (int i = 0; i < rings.Length; i++)
            {
                centres[i] = new Vector3(0f, rings[i].y, rings[i].z);
                radii[i] = new Vector2(rings[i].rx, rings[i].rz);
            }
            model.Loft(centres, radii, Sides, color);
        }

        // A colour made darker or lighter, staying opaque.
        private static Color Shade(Color color, float by)
        {
            return new Color(Mathf.Clamp01(color.r * by), Mathf.Clamp01(color.g * by), Mathf.Clamp01(color.b * by), 1f);
        }
    }
}
