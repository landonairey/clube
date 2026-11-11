using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// A bridge where the trade road crosses a river (GL570): two cobblestone footings at the banks, maple boards across on two
    /// stringers, and the parcel fence's posts and rails along each side. One low-poly mesh in vertex colours, with box
    /// colliders for the deck, the rails and the footings so the deck is walkable and the rails stop you.
    /// </summary>
    public static class RoadBridge
    {
        public const float Width = 2.2f;
        private const float BoardWidth = 0.22f;
        private const float BoardThickness = 0.08f;
        private const float FootingLength = 1f;
        private const float FootingDepth = 1.6f;
        private const float RailHeight = 0.9f;
        private const float PostSpacing = 2f;

        private static readonly Color Cobble = new Color(0.52f, 0.5f, 0.47f);
        private static readonly Color Board = new Color(0.66f, 0.42f, 0.26f);
        private static readonly Color Stringer = new Color(0.45f, 0.28f, 0.17f);

        /// <summary>Builds the bridge under <paramref name="parent"/> and returns it.</summary>
        public static GameObject Build(RouteBridges.Span span, Material material, Transform parent, int seed)
        {
            Vector3 flat = new Vector3(span.To.x - span.From.x, 0f, span.To.y - span.From.y);
            float length = flat.magnitude;
            var root = new GameObject("Road bridge");
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(new Vector3(span.From.x, 0f, span.From.y), Quaternion.LookRotation(flat / length, Vector3.up));
            // Local frame: x across, z along, y world height.
            float Top(float z) => Mathf.Lerp(span.FromHeight, span.ToHeight, z / length);
            Vector3 along = new Vector3(0f, span.ToHeight - span.FromHeight, length);
            float deckLength = along.magnitude;
            Quaternion slope = Quaternion.LookRotation(along / deckLength, Vector3.up);
            Vector3 middle = new Vector3(0f, Top(length * 0.5f), length * 0.5f);
            var mesh = new LowPolyMeshBuilder();
            var random = new System.Random(seed);
            float F() => (float)random.NextDouble();

            // Stringers, and the boards along them.
            foreach (float x in new[] { -0.7f, 0.7f })
            {
                mesh.Box(middle + Vector3.down * 0.14f + Vector3.right * x, new Vector3(0.14f, 0.16f, deckLength), slope, Stringer);
            }
            int boards = Mathf.RoundToInt(Width / BoardWidth);
            for (int i = 0; i < boards; i++)
            {
                float x = (i + 0.5f) / boards * Width - Width * 0.5f;
                float shade = 0.88f + 0.12f * F();
                mesh.Box(middle + Vector3.down * (BoardThickness * 0.5f) + Vector3.right * x, new Vector3(BoardWidth - 0.02f, BoardThickness, deckLength), slope, Board * shade);
            }

            // Footings: a cobble block under each end, down into the bank, with loose cobbles at its foot.
            for (int end = 0; end < 2; end++)
            {
                Footing(end, length, span, out Vector3 centre, out Vector3 size);
                mesh.Box(centre, size, Quaternion.identity, Cobble * 0.92f);
                float inward = end == 0 ? 1f : -1f;
                for (int k = 0; k < 7; k++)
                {
                    float cx = (F() - 0.5f) * (Width + 0.2f);
                    float cz = centre.z + inward * (FootingLength * 0.5f + 0.05f + F() * 0.2f);
                    float stone = 0.22f + 0.16f * F();
                    mesh.Box(new Vector3(cx, centre.y + size.y * 0.5f - 0.1f - F() * 0.3f, cz), new Vector3(stone, stone * 0.8f, stone), Quaternion.Euler(F() * 30f, F() * 90f, F() * 30f), Cobble * (0.8f + 0.3f * F()));
                }
            }

            // Side rails: the parcel fence's posts and two rails on each edge.
            int posts = Mathf.Max(1, Mathf.RoundToInt(length / PostSpacing));
            foreach (float x in new[] { -Width * 0.5f + 0.06f, Width * 0.5f - 0.06f })
            {
                Vector3 previous = default;
                for (int i = 0; i <= posts; i++)
                {
                    float z = length * i / posts;
                    var foot = new Vector3(x, Top(z), z);
                    mesh.Block(foot + Vector3.down * 0.15f, new Vector3(0.12f, RailHeight + 0.15f, 0.12f), ParcelBorders.PostWood * (0.85f + 0.15f * F()));
                    if (i > 0)
                    {
                        AddRail(mesh, previous, foot, RailHeight, ParcelBorders.RailWood);
                        AddRail(mesh, previous, foot, RailHeight * 0.5f, ParcelBorders.RailWood * 0.95f);
                    }
                    previous = foot;
                }
            }

            root.AddComponent<MeshFilter>().sharedMesh = mesh.Make("Road bridge");
            root.AddComponent<MeshRenderer>().sharedMaterial = material;

            // Colliders: the deck, a tall thin wall along each rail, and the footings.
            AddBox(root.transform, middle + Vector3.down * 0.1f, new Vector3(Width, 0.2f, deckLength), slope);
            foreach (float x in new[] { -Width * 0.5f + 0.06f, Width * 0.5f - 0.06f })
            {
                AddBox(root.transform, middle + new Vector3(x, 0.45f, 0f), new Vector3(0.14f, 1f, deckLength), slope);
            }
            for (int end = 0; end < 2; end++)
            {
                Footing(end, length, span, out Vector3 centre, out Vector3 size);
                AddBox(root.transform, centre, size, Quaternion.identity);
            }
            return root;
        }

        // A footing's centre and size, in the bridge's local frame: its top just under the boards, its foot in the bank.
        private static void Footing(int end, float length, RouteBridges.Span span, out Vector3 centre, out Vector3 size)
        {
            float z = end == 0 ? FootingLength * 0.5f : length - FootingLength * 0.5f;
            float deck = end == 0 ? span.FromHeight : span.ToHeight;
            float top = deck - 0.17f;
            float bottom = deck - FootingDepth;
            centre = new Vector3(0f, (top + bottom) * 0.5f, z);
            size = new Vector3(Width + 0.3f, top - bottom, FootingLength);
        }

        private static void AddRail(LowPolyMeshBuilder mesh, Vector3 from, Vector3 to, float over, Color tint)
        {
            Vector3 a = from + Vector3.up * over, b = to + Vector3.up * over;
            Vector3 d = b - a;
            mesh.Box((a + b) * 0.5f, new Vector3(0.06f, 0.09f, d.magnitude), Quaternion.LookRotation(d.normalized, Vector3.up), tint);
        }

        // A box collider on a child, in the bridge's local frame.
        private static void AddBox(Transform parent, Vector3 centre, Vector3 size, Quaternion turn)
        {
            var child = new GameObject("Collider");
            child.transform.SetParent(parent, false);
            child.transform.localPosition = centre;
            child.transform.localRotation = turn;
            child.AddComponent<BoxCollider>().size = size;
        }
    }
}
