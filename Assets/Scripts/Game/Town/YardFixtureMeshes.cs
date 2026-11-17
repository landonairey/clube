using System;
using System.Collections.Generic;
using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// The low-poly models of a work yard's fixtures (GL713, GL714): an anvil on its stump, a tool rack, a spinning wheel,
    /// a loom, a furnace and the rest, by <see cref="YardFixture.Kind"/>, and the canvas tarp on posts over a yard. Each
    /// model is built once and shared; a kind with no model shows nothing. Feet at y = 0, the worker's side towards +z.
    /// </summary>
    public static class YardFixtureMeshes
    {
        private static readonly Color Wood = new Color(0.45f, 0.31f, 0.18f);
        private static readonly Color DarkWood = new Color(0.32f, 0.21f, 0.12f);
        private static readonly Color Iron = new Color(0.22f, 0.22f, 0.24f);
        private static readonly Color Stone = new Color(0.5f, 0.48f, 0.45f);
        private static readonly Color Wool = new Color(0.88f, 0.85f, 0.76f);
        private static readonly Color Canvas = new Color(0.82f, 0.76f, 0.62f);
        private static readonly Color Hide = new Color(0.6f, 0.45f, 0.3f);

        // Each kind's model, a row each: a new fixture is a row here and a row in the building catalog.
        private static readonly Dictionary<string, Action<LowPolyMeshBuilder>> Models = new Dictionary<string, Action<LowPolyMeshBuilder>>
        {
            { "anvil", Anvil },
            { "tool-rack", ToolRack },
            { "spinning-wheel", SpinningWheel },
            { "loom", Loom },
            { "furnace", Furnace },
            { "ore-pile", OrePile },
            { "trough", Trough },
            { "hide-frame", HideFrame },
            { "tub", Tub },
            { "sawhorse", Sawhorse },
            { "workbench", Workbench },
            { "log-pile", LogPile },
        };

        private static readonly Dictionary<string, Mesh> Cache = new Dictionary<string, Mesh>();

        /// <summary>The model of a fixture kind, or null for a kind with none.</summary>
        public static Mesh For(string kind)
        {
            if (kind == null || !Models.TryGetValue(kind, out Action<LowPolyMeshBuilder> build))
            {
                return null;
            }
            if (!Cache.TryGetValue(kind, out Mesh mesh) || mesh == null)
            {
                var model = new LowPolyMeshBuilder();
                build(model);
                mesh = model.Make($"Yard {kind}");
                Cache[kind] = mesh;
            }
            return mesh;
        }

        /// <summary>
        /// GL714: a canvas tarp over a yard, in the building's frame: from the front wall at <paramref name="eaves"/> high,
        /// sloping down to two posts <paramref name="depth"/> metres out, <paramref name="width"/> across.
        /// </summary>
        public static Mesh Tarp(float width, float depth, float eaves, float front)
        {
            var model = new LowPolyMeshBuilder();
            float w = width * 0.5f - 0.15f;
            float low = eaves - 0.5f;
            Vector3 a = new Vector3(-w, eaves, front), b = new Vector3(w, eaves, front);
            Vector3 c = new Vector3(w, low, front + depth), d = new Vector3(-w, low, front + depth);
            // A little sag in the middle: two panels meeting below the straight line.
            Vector3 m1 = new Vector3(-w, (eaves + low) * 0.5f - 0.12f, front + depth * 0.5f), m2 = new Vector3(w, (eaves + low) * 0.5f - 0.12f, front + depth * 0.5f);
            model.Box(Vector3.Lerp(a, m2, 0.5f) + Vector3.up * 0.01f, new Vector3(width - 0.3f, 0.03f, depth * 0.52f), Quaternion.Euler(Mathf.Atan2(eaves - m1.y, depth * 0.5f) * Mathf.Rad2Deg, 0f, 0f), Canvas);
            model.Box(Vector3.Lerp(m1, c, 0.5f), new Vector3(width - 0.3f, 0.03f, depth * 0.52f), Quaternion.Euler(Mathf.Atan2(m1.y - low, depth * 0.5f) * Mathf.Rad2Deg, 0f, 0f), Canvas * 0.95f);
            // The posts holding the outer edge, and a pole along it.
            foreach (float x in new[] { -w, w })
            {
                model.Prism(new Vector3(x, 0f, front + depth), new Vector3(x, low + 0.05f, front + depth), 0.06f, 0.05f, 6, DarkWood);
            }
            model.Prism(new Vector3(-w - 0.1f, low, front + depth), new Vector3(w + 0.1f, low, front + depth), 0.04f, 0.04f, 6, Wood);
            return model.Make("Yard tarp");
        }

        private static void Anvil(LowPolyMeshBuilder m)
        {
            m.Prism(Vector3.zero, new Vector3(0f, 0.5f, 0f), 0.24f, 0.22f, 8, DarkWood);
            m.Box(new Vector3(0f, 0.58f, 0f), new Vector3(0.18f, 0.16f, 0.14f), Quaternion.identity, Iron);
            m.Box(new Vector3(0f, 0.7f, 0f), new Vector3(0.42f, 0.09f, 0.18f), Quaternion.identity, Iron);
            m.Wedge(new Vector3(0.2f, 0.7f, 0f), new Vector3(0.38f, 0.7f, 0f), 0.12f, 0.07f, 0.01f, Iron);
        }

        private static void ToolRack(LowPolyMeshBuilder m)
        {
            foreach (float x in new[] { -0.55f, 0.55f })
            {
                m.Box(new Vector3(x, 0.75f, 0f), new Vector3(0.08f, 1.5f, 0.08f), Quaternion.identity, DarkWood);
            }
            m.Box(new Vector3(0f, 1.3f, 0f), new Vector3(1.25f, 0.08f, 0.1f), Quaternion.identity, Wood);
            m.Box(new Vector3(0f, 0.3f, 0f), new Vector3(1.25f, 0.06f, 0.08f), Quaternion.identity, Wood);
            // Hammers, tongs and a file hung from the bar.
            for (int i = 0; i < 4; i++)
            {
                float x = -0.4f + i * 0.27f;
                m.Box(new Vector3(x, 1.0f, 0.07f), new Vector3(0.03f, 0.55f, 0.03f), Quaternion.Euler(0f, 0f, i % 2 == 0 ? 4f : -4f), Wood);
                m.Box(new Vector3(x, 0.74f, 0.07f), new Vector3(i % 2 == 0 ? 0.14f : 0.05f, 0.06f, 0.06f), Quaternion.identity, Iron);
            }
        }

        // GL714: "a low poly spinning wheel": a slanted bench on three legs, the big wheel on two uprights with its spokes,
        // a treadle, and the flyer and bobbin with a tuft of wool.
        private static void SpinningWheel(LowPolyMeshBuilder m)
        {
            Quaternion bench = Quaternion.Euler(0f, 0f, 8f);
            m.Box(new Vector3(0f, 0.42f, 0f), new Vector3(0.9f, 0.06f, 0.22f), bench, Wood);
            m.Prism(new Vector3(-0.38f, 0f, 0f), new Vector3(-0.36f, 0.38f, 0f), 0.03f, 0.03f, 5, DarkWood);
            m.Prism(new Vector3(0.36f, 0f, -0.1f), new Vector3(0.34f, 0.48f, -0.06f), 0.03f, 0.03f, 5, DarkWood);
            m.Prism(new Vector3(0.36f, 0f, 0.1f), new Vector3(0.34f, 0.48f, 0.06f), 0.03f, 0.03f, 5, DarkWood);
            // The uprights and the wheel, its axle across the bench.
            Vector3 hub = new Vector3(-0.15f, 0.82f, 0f);
            foreach (float z in new[] { -0.08f, 0.08f })
            {
                m.Box(new Vector3(-0.15f, 0.62f, z), new Vector3(0.05f, 0.42f, 0.03f), Quaternion.identity, DarkWood);
            }
            m.Prism(hub + new Vector3(0f, 0f, -0.06f), hub + new Vector3(0f, 0f, 0.06f), 0.05f, 0.05f, 6, DarkWood);
            const int Rim = 12;
            const float Radius = 0.32f;
            for (int i = 0; i < Rim; i++)
            {
                float a0 = i * Mathf.PI * 2f / Rim, a1 = (i + 1) * Mathf.PI * 2f / Rim;
                Vector3 p0 = hub + new Vector3(Mathf.Cos(a0), Mathf.Sin(a0), 0f) * Radius, p1 = hub + new Vector3(Mathf.Cos(a1), Mathf.Sin(a1), 0f) * Radius;
                m.Prism(p0, p1, 0.022f, 0.022f, 4, Wood);
                if (i % 2 == 0)
                {
                    m.Prism(hub, p0, 0.01f, 0.01f, 4, Wood);
                }
            }
            // The treadle and its rod to the crank.
            m.Box(new Vector3(-0.15f, 0.05f, 0.12f), new Vector3(0.35f, 0.03f, 0.14f), Quaternion.Euler(-10f, 0f, 0f), Wood);
            m.Prism(new Vector3(-0.15f, 0.06f, 0.08f), hub + new Vector3(0.05f, 0f, 0.08f), 0.01f, 0.01f, 4, DarkWood);
            // The flyer on its post at the high end, a bobbin of wool, and the drive band.
            m.Box(new Vector3(0.3f, 0.6f, 0f), new Vector3(0.05f, 0.2f, 0.05f), Quaternion.identity, DarkWood);
            m.Prism(new Vector3(0.22f, 0.72f, 0f), new Vector3(0.42f, 0.72f, 0f), 0.025f, 0.02f, 6, Wood);
            m.Blob(new Vector3(0.32f, 0.72f, 0f), new Vector3(0.06f, 0.045f, 0.045f), Quaternion.identity, Wool, 6, 3);
            m.Prism(hub + new Vector3(0f, Radius, 0f), new Vector3(0.3f, 0.74f, 0f), 0.006f, 0.006f, 3, Wool);
        }

        private static void Loom(LowPolyMeshBuilder m)
        {
            foreach (float x in new[] { -0.6f, 0.6f })
            {
                m.Box(new Vector3(x, 0.7f, -0.2f), new Vector3(0.07f, 1.4f, 0.07f), Quaternion.identity, DarkWood);
                m.Box(new Vector3(x, 0.4f, 0.15f), new Vector3(0.07f, 0.8f, 0.07f), Quaternion.identity, DarkWood);
                m.Box(new Vector3(x, 0.75f, -0.02f), new Vector3(0.06f, 0.06f, 0.42f), Quaternion.identity, Wood);
            }
            m.Box(new Vector3(0f, 1.38f, -0.2f), new Vector3(1.3f, 0.08f, 0.08f), Quaternion.identity, Wood);
            m.Prism(new Vector3(-0.6f, 0.78f, 0.15f), new Vector3(0.6f, 0.78f, 0.15f), 0.05f, 0.05f, 6, Wood);
            // The warp and the cloth woven so far.
            m.Box(new Vector3(0f, 1.08f, -0.2f), new Vector3(1.05f, 0.56f, 0.01f), Quaternion.identity, Wool);
            m.Box(new Vector3(0f, 0.8f, -0.03f), new Vector3(1.05f, 0.01f, 0.34f), Quaternion.Euler(-35f, 0f, 0f), new Color(0.55f, 0.32f, 0.3f));
        }

        private static void Furnace(LowPolyMeshBuilder m)
        {
            m.Prism(Vector3.zero, new Vector3(0f, 0.9f, 0f), 0.5f, 0.38f, 8, Stone);
            m.Prism(new Vector3(0f, 0.9f, 0f), new Vector3(0f, 1.5f, 0f), 0.2f, 0.16f, 6, Stone * 0.85f);
            m.Box(new Vector3(0f, 0.3f, 0.4f), new Vector3(0.3f, 0.3f, 0.12f), Quaternion.identity, new Color(0.9f, 0.45f, 0.1f));
        }

        private static void OrePile(LowPolyMeshBuilder m)
        {
            for (int i = 0; i < 7; i++)
            {
                float a = i * 2.4f;
                m.Diamond(new Vector3(Mathf.Cos(a) * 0.25f * (i % 3) * 0.5f, 0.1f + (i == 0 ? 0.1f : 0f), Mathf.Sin(a) * 0.2f * (i % 3) * 0.5f),
                    new Vector3(0.16f, 0.12f, 0.14f), Color.Lerp(Stone, new Color(0.5f, 0.32f, 0.2f), (i % 4) * 0.25f));
            }
        }

        private static void Trough(LowPolyMeshBuilder m)
        {
            m.Box(new Vector3(0f, 0.2f, 0f), new Vector3(1.4f, 0.06f, 0.45f), Quaternion.identity, Wood);
            foreach (float z in new[] { -0.2f, 0.2f })
            {
                m.Box(new Vector3(0f, 0.35f, z), new Vector3(1.4f, 0.3f, 0.05f), Quaternion.identity, Wood);
            }
            foreach (float x in new[] { -0.68f, 0.68f })
            {
                m.Box(new Vector3(x, 0.25f, 0f), new Vector3(0.05f, 0.5f, 0.45f), Quaternion.identity, DarkWood);
            }
            m.Box(new Vector3(0f, 0.42f, 0f), new Vector3(1.3f, 0.02f, 0.36f), Quaternion.identity, new Color(0.25f, 0.4f, 0.5f));
        }

        private static void HideFrame(LowPolyMeshBuilder m)
        {
            foreach (float x in new[] { -0.6f, 0.6f })
            {
                m.Box(new Vector3(x, 0.8f, 0f), new Vector3(0.07f, 1.6f, 0.07f), Quaternion.Euler(-8f, 0f, 0f), DarkWood);
            }
            m.Box(new Vector3(0f, 1.5f, -0.1f), new Vector3(1.3f, 0.07f, 0.07f), Quaternion.identity, Wood);
            m.Box(new Vector3(0f, 0.3f, 0.05f), new Vector3(1.3f, 0.07f, 0.07f), Quaternion.identity, Wood);
            m.Box(new Vector3(0f, 0.9f, -0.03f), new Vector3(1.0f, 1.0f, 0.02f), Quaternion.Euler(-8f, 0f, 0f), Hide);
        }

        private static void Tub(LowPolyMeshBuilder m)
        {
            m.Prism(Vector3.zero, new Vector3(0f, 0.6f, 0f), 0.36f, 0.42f, 10, Wood);
            m.Prism(new Vector3(0f, 0.15f, 0f), new Vector3(0f, 0.18f, 0f), 0.38f, 0.38f, 10, Iron);
            m.Prism(new Vector3(0f, 0.5f, 0f), new Vector3(0f, 0.53f, 0f), 0.42f, 0.42f, 10, Iron);
            m.Prism(new Vector3(0f, 0.55f, 0f), new Vector3(0f, 0.58f, 0f), 0.39f, 0.39f, 10, new Color(0.35f, 0.28f, 0.18f));
        }

        private static void Sawhorse(LowPolyMeshBuilder m)
        {
            foreach (float x in new[] { -0.45f, 0.45f })
            {
                m.Box(new Vector3(x, 0.32f, -0.12f), new Vector3(0.05f, 0.7f, 0.05f), Quaternion.Euler(-18f, 0f, 0f), DarkWood);
                m.Box(new Vector3(x, 0.32f, 0.12f), new Vector3(0.05f, 0.7f, 0.05f), Quaternion.Euler(18f, 0f, 0f), DarkWood);
            }
            m.Box(new Vector3(0f, 0.66f, 0f), new Vector3(1.1f, 0.08f, 0.1f), Quaternion.identity, Wood);
            m.Prism(new Vector3(-0.7f, 0.78f, 0f), new Vector3(0.7f, 0.78f, 0f), 0.11f, 0.1f, 7, new Color(0.55f, 0.4f, 0.25f));
        }

        private static void Workbench(LowPolyMeshBuilder m)
        {
            m.Box(new Vector3(0f, 0.78f, 0f), new Vector3(1.4f, 0.08f, 0.6f), Quaternion.identity, Wood);
            foreach (float x in new[] { -0.62f, 0.62f })
            {
                foreach (float z in new[] { -0.24f, 0.24f })
                {
                    m.Box(new Vector3(x, 0.37f, z), new Vector3(0.07f, 0.74f, 0.07f), Quaternion.identity, DarkWood);
                }
            }
            m.Box(new Vector3(0.3f, 0.86f, 0f), new Vector3(0.3f, 0.08f, 0.2f), Quaternion.identity, Stone);
            m.Box(new Vector3(-0.35f, 0.85f, 0.05f), new Vector3(0.22f, 0.04f, 0.04f), Quaternion.Euler(0f, 30f, 0f), Iron);
        }

        private static void LogPile(LowPolyMeshBuilder m)
        {
            for (int row = 0; row < 3; row++)
            {
                for (int i = 0; i < 3 - row; i++)
                {
                    float z = (i - (2 - row) * 0.5f) * 0.24f;
                    float y = 0.12f + row * 0.21f;
                    m.Prism(new Vector3(-0.6f, y, z), new Vector3(0.6f, y, z), 0.12f, 0.12f, 7, i % 2 == 0 ? Wood : DarkWood);
                }
            }
        }
    }
}
