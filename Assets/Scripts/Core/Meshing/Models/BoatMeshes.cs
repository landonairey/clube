using System.Collections.Generic;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// The low-poly models of the vessels (GL618), built from a <see cref="VesselClass"/> row's measures and fittings: a
    /// hull with a pointed bow and a flat stern, a deck inside its bulwarks, then thwarts and oars, masts with square
    /// sails, a deckhouse, a wheelhouse, a stack and hatches as the row asks. The origin is on the waterline amidships,
    /// the bow towards +z. Where the crew stand and the crates sit comes from here too, so they fit any class.
    /// </summary>
    public static class BoatMeshes
    {
        private const int Stations = 10;

        private static readonly Color Rope = new Color(0.62f, 0.55f, 0.4f);
        private static readonly Color Soot = new Color(0.12f, 0.12f, 0.13f);

        /// <summary>The whole vessel as one mesh.</summary>
        public static Mesh Build(VesselClass vessel)
        {
            var model = new LowPolyMeshBuilder();
            Hull(model, vessel);
            Fittings(model, vessel);
            return model.Make(vessel.Name);
        }

        /// <summary>True for a boat with no deck over its hull: its people sit on thwarts.</summary>
        public static bool IsOpen(VesselClass vessel)
        {
            return vessel.Masts == 0 && !vessel.Deckhouse && !vessel.Wheelhouse;
        }

        /// <summary>The deck's (an open boat's floor's) height over the waterline.</summary>
        public static float DeckHeight(VesselClass vessel)
        {
            return IsOpen(vessel) ? Mathf.Min(0.06f, vessel.Freeboard * 0.3f) : vessel.Freeboard - Mathf.Min(0.35f, vessel.Freeboard * 0.3f);
        }

        /// <summary>Half the hull's width at a place along it, 0 the stern to 1 the bow.</summary>
        public static float HalfBeam(VesselClass vessel, float along)
        {
            float share = along < 0.25f ? Mathf.Lerp(0.72f, 1f, along / 0.25f)
                : along < 0.58f ? 1f : Mathf.Sqrt(Mathf.Max(0f, 1f - Mathf.Pow((along - 0.58f) / 0.42f, 2f)));
            return vessel.Beam * 0.5f * share;
        }

        /// <summary>Where the hands stand (or sit, in an open boat), the helmsman first: on the deck, in the model's own metres.</summary>
        public static List<Vector3> CrewSpots(VesselClass vessel)
        {
            var spots = new List<Vector3>();
            float deck = DeckHeight(vessel);
            float side = vessel.Beam * 0.22f;
            for (int i = 0; i < vessel.Crew; i++)
            {
                // The helm aft, then forward along the sides in turn.
                float z = i == 0 ? -0.36f * vessel.Length : Mathf.Lerp(0.3f, -0.2f, vessel.Crew > 2 ? (i - 1) / (float)(vessel.Crew - 2) : 0f) * vessel.Length;
                if (vessel.Deckhouse && i == 0)
                {
                    z = -0.22f * vessel.Length;
                }
                float x = i == 0 ? 0f : (i % 2 == 0 ? side : -side);
                spots.Add(new Vector3(IsOpen(vessel) ? 0f : x, deck, z));
            }
            return spots;
        }

        /// <summary>Where the crates sit amidships, on the deck.</summary>
        public static List<Vector3> CrateSpots(VesselClass vessel, float crateSize)
        {
            var spots = new List<Vector3>();
            float deck = DeckHeight(vessel);
            bool pairs = vessel.Beam * 0.6f > crateSize * 2.2f;
            int rows = pairs ? (vessel.Crates + 1) / 2 : vessel.Crates;
            float pitch = crateSize * 1.15f;
            for (int i = 0; i < vessel.Crates; i++)
            {
                int row = pairs ? i / 2 : i;
                float z = (row - (rows - 1) * 0.5f) * pitch + (IsOpen(vessel) ? 0.1f : -0.02f) * vessel.Length;
                float x = pairs ? (i % 2 == 0 ? -1f : 1f) * (vessel.Wheelhouse ? vessel.Beam * 0.3f : crateSize * 0.58f) : 0f;
                spots.Add(new Vector3(x, deck, z));
            }
            return spots;
        }

        private static float Sheer(VesselClass vessel, float along)
        {
            float bow = Mathf.Max(0f, along - 0.5f) * 2f, stern = Mathf.Max(0f, 0.3f - along) / 0.3f;
            return vessel.Freeboard * (1f + 0.3f * bow * bow + 0.08f * stern);
        }

        private static float Keel(VesselClass vessel, float along)
        {
            float rise = along > 0.7f ? Mathf.Pow((along - 0.7f) / 0.3f, 2f) : along < 0.15f ? 0.4f * (0.15f - along) / 0.15f : 0f;
            return -vessel.Draught * (1f - rise);
        }

        private static void Hull(LowPolyMeshBuilder model, VesselClass vessel)
        {
            float deck = DeckHeight(vessel);
            var rings = new Vector3[Stations + 1][];
            for (int s = 0; s <= Stations; s++)
            {
                float along = s / (float)Stations;
                float z = (along - 0.5f) * vessel.Length;
                float half = HalfBeam(vessel, along), sheer = Sheer(vessel, along), keel = Keel(vessel, along);
                float inner = half * 0.92f, floor = Mathf.Min(deck + (sheer - vessel.Freeboard), sheer - 0.02f);
                rings[s] = new[]
                {
                    new Vector3(-half, sheer, z), new Vector3(-half * 0.72f, keel * 0.55f, z), new Vector3(0f, keel, z),
                    new Vector3(half * 0.72f, keel * 0.55f, z), new Vector3(half, sheer, z),
                    // The bulwark's inner foot each side: the deck's edge.
                    new Vector3(-inner, floor, z), new Vector3(inner, floor, z),
                };
            }
            Color hull = vessel.Hull, below = vessel.Hull * 0.72f, deckColor = Color.Lerp(vessel.Hull, new Color(0.62f, 0.5f, 0.34f), 0.75f);
            below.a = 1f;
            for (int s = 0; s < Stations; s++)
            {
                Vector3[] a = rings[s], b = rings[s + 1];
                var inside = new Vector3(0f, vessel.Freeboard * 0.3f, (a[0].z + b[0].z) * 0.5f);
                model.Quad(a[0], b[0], b[1], a[1], inside, hull);
                model.Quad(a[1], b[1], b[2], a[2], inside, below);
                model.Quad(a[2], b[2], b[3], a[3], inside, below);
                model.Quad(a[3], b[3], b[4], a[4], inside, hull);
                // The deck, seen from above, and the bulwarks' inner faces, seen from the deck.
                model.Quad(a[5], b[5], b[6], a[6], inside + Vector3.down * 10f, deckColor);
                model.Quad(a[0], b[0], b[5], a[5], new Vector3(-vessel.Beam, inside.y, inside.z), vessel.Trim);
                model.Quad(a[4], b[4], b[6], a[6], new Vector3(vessel.Beam, inside.y, inside.z), vessel.Trim);
            }
            // The flat stern.
            Vector3[] stern = rings[0];
            var ahead = new Vector3(0f, 0f, stern[0].z + 1f);
            model.Quad(stern[0], stern[1], stern[3], stern[4], ahead, hull);
            model.Triangle(stern[1], stern[2], stern[3], ahead, below);
            model.Quad(stern[0], stern[4], stern[6], stern[5], new Vector3(0f, 0f, stern[0].z - 1f), vessel.Trim);
        }

        private static void Fittings(LowPolyMeshBuilder model, VesselClass vessel)
        {
            float deck = DeckHeight(vessel), length = vessel.Length, beam = vessel.Beam;
            Color dark = vessel.Trim * 0.8f;
            dark.a = 1f;
            if (IsOpen(vessel))
            {
                // Thwarts to sit on, and the oars over the sides.
                foreach (float z in new[] { -0.36f, -0.12f, 0.2f })
                {
                    float half = HalfBeam(vessel, z + 0.5f) * 0.92f;
                    model.Box(new Vector3(0f, vessel.Freeboard * 0.72f, z * length), new Vector3(half * 2f, 0.04f, 0.22f), Quaternion.identity, vessel.Trim);
                }
            }
            if (vessel.Oars)
            {
                foreach (float side in new[] { -1f, 1f })
                {
                    var lock_ = new Vector3(side * beam * 0.5f, vessel.Freeboard + 0.03f, -0.1f * length);
                    model.Prism(lock_ + new Vector3(-side * 0.5f, 0.12f, 0f), lock_ + new Vector3(side * 1.5f, -vessel.Freeboard - 0.1f, -0.25f), 0.025f, 0.03f, 5, Rope);
                    model.Box(lock_ + new Vector3(side * 1.6f, -vessel.Freeboard - 0.14f, -0.27f), new Vector3(0.36f, 0.03f, 0.14f), Quaternion.Euler(0f, 0f, side * -30f), Rope);
                }
            }
            // The rudder and its tiller.
            model.Box(new Vector3(0f, -vessel.Draught * 0.3f, -0.5f * length - 0.06f), new Vector3(0.06f, vessel.Draught + vessel.Freeboard * 0.6f, 0.3f + length * 0.01f), Quaternion.identity, dark);
            for (int m = 0; m < vessel.Masts; m++)
            {
                float z = (vessel.Masts == 1 ? 0.1f : m == 0 ? 0.2f : -0.14f) * length;
                float height = Mathf.Min(12f, length * (vessel.Wheelhouse ? 0.3f : 0.72f)) * (m == 0 ? 1f : 0.85f);
                float wide = Mathf.Min(beam * 1.5f, height * 0.75f);
                model.Prism(new Vector3(0f, deck, z), new Vector3(0f, deck + height, z), 0.06f + length * 0.006f, 0.04f, 6, vessel.Trim);
                model.Prism(new Vector3(-wide * 0.5f, deck + height * 0.92f, z), new Vector3(wide * 0.5f, deck + height * 0.92f, z), 0.04f, 0.04f, 5, vessel.Trim);
                // The sail, bellied a little by the wind from astern.
                float top = deck + height * 0.9f, foot = deck + height * 0.3f;
                var inside = new Vector3(0f, (top + foot) * 0.5f, z - 2f);
                var belly = new Vector3(0f, (top + foot) * 0.5f, z + wide * 0.12f);
                Vector3 tl = new Vector3(-wide * 0.48f, top, z), tr = new Vector3(wide * 0.48f, top, z);
                Vector3 bl = new Vector3(-wide * 0.48f, foot, z), br = new Vector3(wide * 0.48f, foot, z);
                foreach (Vector3 from in new[] { inside, belly + (belly - inside) })
                {
                    model.Triangle(tl, tr, belly, from, vessel.Sail);
                    model.Triangle(tr, br, belly, from, vessel.Sail * 0.94f);
                    model.Triangle(br, bl, belly, from, vessel.Sail * 0.9f);
                    model.Triangle(bl, tl, belly, from, vessel.Sail * 0.96f);
                }
            }
            if (vessel.Masts > 0)
            {
                model.Prism(new Vector3(0f, Sheer(vessel, 0.95f), 0.44f * length), new Vector3(0f, Sheer(vessel, 1f) + 0.25f * vessel.Freeboard, 0.5f * length + beam * 0.5f), 0.05f, 0.03f, 5, vessel.Trim);
            }
            if (vessel.Deckhouse)
            {
                float z = (vessel.Wheelhouse ? -0.39f : -0.33f) * length, depth = (vessel.Wheelhouse ? 0.13f : 0.16f) * length;
                float wide = HalfBeam(vessel, z / length + 0.5f) * 1.25f, tall = Mathf.Min(2.2f, 1.2f + beam * 0.16f);
                model.Block(new Vector3(0f, deck, z), new Vector3(wide, tall, depth), vessel.Trim);
                model.Block(new Vector3(0f, deck + tall, z), new Vector3(wide + 0.3f, 0.1f, depth + 0.3f), dark);
                if (vessel.Stack)
                {
                    model.Prism(new Vector3(0f, deck + tall, z - depth * 0.15f), new Vector3(0f, deck + tall + 1.9f, z - depth * 0.2f), 0.42f, 0.38f, 8, Soot);
                    model.Prism(new Vector3(0f, deck + tall + 1.1f, z - depth * 0.18f), new Vector3(0f, deck + tall + 1.4f, z - depth * 0.19f), 0.44f, 0.44f, 8, vessel.Hull);
                }
            }
            if (vessel.Wheelhouse)
            {
                float z = 0.36f * length, wide = HalfBeam(vessel, 0.86f) * 1.5f;
                model.Block(new Vector3(0f, deck, z), new Vector3(wide, 1.5f, 0.09f * length), vessel.Trim);
                model.Block(new Vector3(0f, deck + 1.5f, z + 0.2f), new Vector3(wide * 0.8f, 1.3f, 0.06f * length), vessel.Trim);
                model.Block(new Vector3(0f, deck + 2.8f, z + 0.2f), new Vector3(wide * 0.8f + 0.3f, 0.1f, 0.06f * length + 0.3f), dark);
                // The row of hatches down the long deck between the houses.
                for (int h = 0; h < 7; h++)
                {
                    model.Block(new Vector3(0f, deck, Mathf.Lerp(-0.27f, 0.27f, h / 6f) * length), new Vector3(beam * 0.3f, 0.18f, 0.055f * length), Soot * 2.2f);
                }
            }
        }
    }
}
