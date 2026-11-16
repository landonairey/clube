using System.Collections.Generic;
using System.Globalization;
using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// A wooden signpost by the road (GL632: "natural looking sign posts pointing to directions of nearby towns"): a
    /// rough post leaning a little, and one board per arm, pointed at its far end, turned the way the road leaves and
    /// lettered on both faces with a settlement's name and how far it is. Hand-made looking: no two boards quite level
    /// or quite square to the post. One low-poly mesh in vertex colours, a collider on the post.
    /// </summary>
    public static class Signpost
    {
        private const float PostHeight = 2.55f;
        private const float BoardHeight = 0.2f;
        private const float BoardThickness = 0.045f;
        private const float TopBoard = 2.3f;
        private const float BoardStep = 0.25f;
        private const float LetterSize = 0.0165f;

        private static readonly Color Ink = new Color(0.13f, 0.08f, 0.04f);
        private static readonly Color BoardWood = new Color(0.72f, 0.58f, 0.4f);

        private static Material sharedText;

        /// <summary>Builds a signpost under <paramref name="parent"/>, standing at its origin.</summary>
        /// <param name="post">The planned post: its arms' directions are (x, z) in the parent's frame.</param>
        public static GameObject Build(PlannedSignpost post, Material material, Material textMaterial, Transform parent, int seed)
        {
            var random = new System.Random(seed);
            float F() => (float)random.NextDouble();
            var root = new GameObject(post.AtSettlement ? "Signpost (settlement)" : "Signpost (junction)");
            root.transform.SetParent(parent, false);
            var mesh = new LowPolyMeshBuilder();

            // The post: six-sided, tapering, leaning a few degrees, a rough cap cut on a slant.
            Quaternion lean = Quaternion.Euler((F() - 0.5f) * 7f, F() * 360f, (F() - 0.5f) * 7f);
            Vector3 top = lean * Vector3.up * PostHeight;
            Color postWood = ParcelBorders.PostWood * (0.85f + 0.2f * F());
            mesh.Prism(Vector3.down * 0.5f, top, 0.085f, 0.065f, 6, postWood);
            mesh.Box(top + Vector3.up * 0.02f, new Vector3(0.17f, 0.05f, 0.17f), lean * Quaternion.Euler(8f, 20f, 0f), postWood * 0.85f);

            // Arms: the nearest place on top. Two arms the same way sit one under the other.
            var arms = new List<SignArm>(post.Arms);
            arms.Sort((a, b) => a.Metres.CompareTo(b.Metres));
            for (int i = 0; i < arms.Count; i++)
            {
                SignArm arm = arms[i];
                string words = $"{arm.Name}  {Distance(arm.Metres)}";
                float length = Mathf.Clamp(0.34f + words.Length * 0.052f, 0.9f, 1.9f);
                float height = TopBoard - i * BoardStep;
                Vector3 at = top * (height / PostHeight);
                var way = new Vector3(arm.Direction.x, 0f, arm.Direction.y);
                // Nailed on by hand: a little off the true direction, a little off level.
                Quaternion turn = Quaternion.LookRotation(way, Vector3.up) * Quaternion.Euler((F() - 0.5f) * 7f, (F() - 0.5f) * 8f, (F() - 0.5f) * 4f);
                Color wood = BoardWood * (0.8f + 0.25f * F());
                wood.a = 1f;
                // The board starts a hand's width behind the post and ends in a point.
                float back = 0.14f, point = 0.16f;
                mesh.Placement = Matrix4x4.TRS(at, turn, Vector3.one);
                mesh.Box(new Vector3(0f, 0f, (length - point - back) * 0.5f), new Vector3(BoardThickness, BoardHeight, length - point + back), Quaternion.identity, wood);
                mesh.Wedge(new Vector3(0f, 0f, length - point), new Vector3(0f, 0f, length), BoardThickness, BoardHeight, 0.02f, wood * 0.94f);
                mesh.Placement = Matrix4x4.identity;

                var holder = new GameObject("Arm").transform;
                holder.SetParent(root.transform, false);
                holder.localPosition = at;
                holder.localRotation = turn;
                float centre = (length - point) * 0.5f + 0.04f;
                float face = BoardThickness * 0.5f + 0.004f;
                Text(holder, words, new Vector3(-face, 0f, centre), 90f, textMaterial);
                Text(holder, words, new Vector3(face, 0f, centre), -90f, textMaterial);
            }

            root.AddComponent<MeshFilter>().sharedMesh = mesh.Make("Signpost");
            root.AddComponent<MeshRenderer>().sharedMaterial = material;
            var box = root.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, PostHeight * 0.5f, 0f);
            box.size = new Vector3(0.2f, PostHeight, 0.2f);
            return root;
        }

        /// <summary>A distance as a sign gives it: "350 m", "1.2 km".</summary>
        public static string Distance(float metres)
        {
            if (metres < 950f)
            {
                return $"{Mathf.Max(10, Mathf.RoundToInt(metres / 10f) * 10)} m";
            }
            return (metres / 1000f).ToString("0.0", CultureInfo.InvariantCulture) + " km";
        }

        // Words on a board's face: a TextMesh facing -z at yaw 0, in the shared sign-text material (hidden behind things).
        private static void Text(Transform parent, string words, Vector3 at, float yaw, Material textMaterial)
        {
            var go = new GameObject("Words");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            var text = go.AddComponent<TextMesh>();
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.font = font;
            text.text = words;
            text.fontSize = 64;
            text.characterSize = LetterSize;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.color = Ink;
            if (sharedText == null)
            {
                sharedText = textMaterial != null ? new Material(textMaterial) : new Material(font.material);
                sharedText.name = "Signpost words";
                sharedText.mainTexture = font.material.mainTexture;
            }
            go.GetComponent<MeshRenderer>().sharedMaterial = sharedText;
        }
    }
}
