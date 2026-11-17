using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// The low-poly models of a settlement's lights (GL716), by <see cref="SettlementLightKind.Id"/>, in vertex colours,
    /// origin at the foot: a torch on a post, a 2 x 2 m bonfire in a ring of stones, an oil lantern hung from a post's arm.
    /// The flame and its light are added by <see cref="SettlementLamp"/>. Built once and shared.
    /// </summary>
    public static class SettlementLightMeshes
    {
        private static readonly Color Timber = new Color(0.45f, 0.32f, 0.2f);
        private static readonly Color Charred = new Color(0.18f, 0.13f, 0.1f);
        private static readonly Color Stone = new Color(0.5f, 0.49f, 0.47f);
        private static readonly Color Iron = new Color(0.22f, 0.23f, 0.25f);
        private static readonly Color Glow = new Color(1f, 0.78f, 0.4f);

        private static Mesh torch, bonfire, lantern;

        /// <summary>The model of a kind of light, or null for one with none.</summary>
        public static Mesh For(string kind)
        {
            switch (kind)
            {
                case "torch": return torch != null ? torch : torch = Torch();
                case "bonfire": return bonfire != null ? bonfire : bonfire = Bonfire();
                case "lantern": return lantern != null ? lantern : lantern = Lantern();
                default: return null;
            }
        }

        /// <summary>Where a lantern's glass hangs on its post (the flame's place), from the foot.</summary>
        public static readonly Vector3 LanternGlass = new Vector3(0.62f, 2.3f, 0f);

        // A rough post with an iron cup at the top holding a pitch-soaked head; the flame burns at 1.75 m.
        private static Mesh Torch()
        {
            var model = new LowPolyMeshBuilder();
            model.Prism(new Vector3(0f, -0.3f, 0f), new Vector3(0f, 1.5f, 0f), 0.07f, 0.055f, 6, Timber);
            model.Prism(new Vector3(0f, 1.45f, 0f), new Vector3(0f, 1.62f, 0f), 0.06f, 0.1f, 6, Iron);
            model.Prism(new Vector3(0f, 1.55f, 0f), new Vector3(0f, 1.7f, 0f), 0.075f, 0.06f, 6, Charred);
            return model.Make("Settlement torch");
        }

        // A ring of stones two metres across round a cone of logs, charred where they meet.
        private static Mesh Bonfire()
        {
            var model = new LowPolyMeshBuilder();
            const int Stones = 12;
            for (int i = 0; i < Stones; i++)
            {
                float angle = i * Mathf.PI * 2f / Stones;
                var at = new Vector3(Mathf.Cos(angle) * 0.85f, 0.1f, Mathf.Sin(angle) * 0.85f);
                model.Blob(at, new Vector3(0.18f, 0.13f, 0.15f), Quaternion.Euler(0f, angle * Mathf.Rad2Deg + i * 17f, 0f), Stone * (0.85f + 0.05f * (i % 4)));
            }
            const int Logs = 7;
            for (int i = 0; i < Logs; i++)
            {
                float angle = i * Mathf.PI * 2f / Logs + 0.3f;
                var foot = new Vector3(Mathf.Cos(angle) * 0.6f, 0f, Mathf.Sin(angle) * 0.6f);
                model.Prism(foot, new Vector3(0f, 1.1f, 0f) + foot * 0.12f, 0.08f, 0.05f, 5, i % 2 == 0 ? Timber : Timber * 0.8f);
            }
            model.Blob(new Vector3(0f, 0.08f, 0f), new Vector3(0.55f, 0.08f, 0.55f), Quaternion.identity, Charred);
            return model.Make("Settlement bonfire");
        }

        // A squared post with an arm out to one side, a chain and an iron lantern with its glass lit.
        private static Mesh Lantern()
        {
            var model = new LowPolyMeshBuilder();
            model.Box(new Vector3(0f, 1.15f, 0f), new Vector3(0.14f, 3.1f, 0.14f), Quaternion.identity, Timber);
            model.Box(new Vector3(0f, 2.68f, 0f), new Vector3(0.22f, 0.06f, 0.22f), Quaternion.identity, Timber * 0.85f);
            model.Box(new Vector3(0.36f, 2.62f, 0f), new Vector3(0.72f, 0.08f, 0.08f), Quaternion.identity, Timber * 0.9f);
            // An iron brace under the arm.
            model.Box(new Vector3(0.18f, 2.45f, 0f), new Vector3(0.42f, 0.03f, 0.03f), Quaternion.Euler(0f, 0f, 40f), Iron);
            Vector3 glass = LanternGlass;
            model.Box(new Vector3(glass.x, 2.52f, 0f), new Vector3(0.02f, 0.14f, 0.02f), Quaternion.identity, Iron);
            model.Prism(glass + new Vector3(0f, 0.12f, 0f), glass + new Vector3(0f, 0.2f, 0f), 0.13f, 0.03f, 4, Iron);
            model.Prism(glass + new Vector3(0f, -0.12f, 0f), glass + new Vector3(0f, 0.12f, 0f), 0.09f, 0.09f, 4, Glow);
            model.Prism(glass + new Vector3(0f, -0.16f, 0f), glass + new Vector3(0f, -0.12f, 0f), 0.12f, 0.12f, 4, Iron);
            return model.Make("Settlement lantern");
        }
    }
}
