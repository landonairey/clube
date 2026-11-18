using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// A settlement's name board (GL717: "a larger board panel that is suspended from chains from the post that the
    /// settlement name is written in larger font size"): a stout post and a board lettered on both faces, hung by two
    /// chains either from a short bracket so it hangs centred in front of the post, or from a longer arm off the post's
    /// side. The board and its chains hang from one pivot that swings in the wind (<see cref="SignboardSway"/>), so the
    /// chains move with the board they hold. Low-poly vertex colours; a collider on the post.
    /// </summary>
    public static class Signboard
    {
        private const float PostHeight = 3.1f;
        private const float BoardHeight = 0.55f;
        private const float BoardThickness = 0.06f;
        private const float ChainLength = 0.36f;
        private const float LetterSize = 0.034f;

        private static readonly Color BoardWood = new Color(0.62f, 0.47f, 0.3f);
        private static readonly Color Rim = new Color(0.4f, 0.28f, 0.17f);
        private static readonly Color Iron = new Color(0.2f, 0.2f, 0.22f);

        /// <summary>
        /// Builds a name board under <paramref name="parent"/>, standing at its origin, its board's faces looking along
        /// the parent's z (turn the root to face the road's travellers).
        /// </summary>
        /// <param name="sideArm">True for a board off a long arm at the post's side; false for one hung centred in front of the post.</param>
        public static GameObject Build(string name, bool sideArm, Material material, Material textMaterial, Transform parent, int seed)
        {
            var random = new System.Random(seed);
            float F() => (float)random.NextDouble();
            var root = new GameObject($"Signboard ({name})");
            root.transform.SetParent(parent, false);

            // The post and its arm, still.
            var frame = new LowPolyMeshBuilder();
            Color postWood = ParcelBorders.PostWood * (0.85f + 0.2f * F());
            postWood.a = 1f;
            frame.Box(new Vector3(0f, PostHeight * 0.5f - 0.4f, 0f), new Vector3(0.16f, PostHeight + 0.8f, 0.16f), Quaternion.identity, postWood);
            frame.Box(new Vector3(0f, PostHeight + 0.03f, 0f), new Vector3(0.22f, 0.06f, 0.22f), Quaternion.identity, postWood * 0.85f);
            float width = Mathf.Clamp(0.5f + Signpost.TextWidth(name, LetterSize) * 1.05f, 1.1f, 4f);
            float armY = PostHeight - 0.18f;
            Vector3 hang;
            if (sideArm)
            {
                // A long arm out to the side and a brace under it; the board hangs under the arm's outer part.
                float arm = width + 0.35f;
                frame.Box(new Vector3(arm * 0.5f, armY, 0f), new Vector3(arm, 0.1f, 0.1f), Quaternion.identity, postWood * 0.92f);
                frame.Box(new Vector3(0.3f, armY - 0.3f, 0f), new Vector3(0.7f, 0.07f, 0.07f), Quaternion.Euler(0f, 0f, 42f), postWood * 0.88f);
                hang = new Vector3(0.25f + width * 0.5f, armY - 0.05f, 0f);
            }
            else
            {
                // A short bracket out of the post's front and a crossbar on it as wide as the board, so the board hangs
                // centred in front of the post.
                float reach = 0.4f;
                frame.Box(new Vector3(0f, armY, reach * 0.5f), new Vector3(0.1f, 0.1f, reach), Quaternion.identity, postWood * 0.92f);
                frame.Box(new Vector3(0f, armY, reach), new Vector3(width + 0.1f, 0.08f, 0.08f), Quaternion.identity, postWood * 0.9f);
                frame.Box(new Vector3(0f, armY - 0.22f, reach * 0.4f), new Vector3(0.06f, 0.06f, 0.5f), Quaternion.Euler(-40f, 0f, 0f), Iron);
                hang = new Vector3(0f, armY - 0.04f, reach);
            }
            root.AddComponent<MeshFilter>().sharedMesh = frame.Make("Signboard post");
            root.AddComponent<MeshRenderer>().sharedMaterial = material;
            var box = root.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, PostHeight * 0.5f, 0f);
            box.size = new Vector3(0.2f, PostHeight, 0.2f);

            // The pivot the chains hang from; board and chains swing on it together.
            var pivot = new GameObject("Swinging board").transform;
            pivot.SetParent(root.transform, false);
            pivot.localPosition = hang;
            var board = new LowPolyMeshBuilder();
            float chainX = width * 0.5f - 0.12f;
            for (int side = -1; side <= 1; side += 2)
            {
                // A hook into the arm, then links turned about turn.
                board.Box(new Vector3(side * chainX, -0.015f, 0f), new Vector3(0.03f, 0.05f, 0.03f), Quaternion.identity, Iron);
                const int Links = 5;
                for (int k = 0; k < Links; k++)
                {
                    float y = -0.05f - (k + 0.5f) * (ChainLength - 0.05f) / Links;
                    Quaternion turn = Quaternion.Euler(0f, k % 2 == 0 ? 0f : 90f, 0f);
                    board.Box(new Vector3(side * chainX, y, 0f), new Vector3(0.035f, (ChainLength - 0.05f) / Links + 0.012f, 0.01f), turn, Iron);
                }
                // An eye bolted into the board's top.
                board.Box(new Vector3(side * chainX, -ChainLength - 0.01f, 0f), new Vector3(0.04f, 0.04f, BoardThickness + 0.01f), Quaternion.identity, Iron);
            }
            float boardY = -ChainLength - BoardHeight * 0.5f;
            Color wood = BoardWood * (0.85f + 0.2f * F());
            wood.a = 1f;
            board.Box(new Vector3(0f, boardY, 0f), new Vector3(width, BoardHeight, BoardThickness), Quaternion.Euler(0f, 0f, (F() - 0.5f) * 1.5f), wood);
            // A rim of darker wood round the panel's face, both sides.
            for (int face = -1; face <= 1; face += 2)
            {
                float z = face * (BoardThickness * 0.5f + 0.006f);
                board.Box(new Vector3(0f, boardY + BoardHeight * 0.5f - 0.03f, z), new Vector3(width, 0.05f, 0.012f), Quaternion.identity, Rim);
                board.Box(new Vector3(0f, boardY - BoardHeight * 0.5f + 0.03f, z), new Vector3(width, 0.05f, 0.012f), Quaternion.identity, Rim);
                board.Box(new Vector3(-width * 0.5f + 0.03f, boardY, z), new Vector3(0.05f, BoardHeight, 0.012f), Quaternion.identity, Rim);
                board.Box(new Vector3(width * 0.5f - 0.03f, boardY, z), new Vector3(0.05f, BoardHeight, 0.012f), Quaternion.identity, Rim);
            }
            pivot.gameObject.AddComponent<MeshFilter>().sharedMesh = board.Make("Signboard");
            pivot.gameObject.AddComponent<MeshRenderer>().sharedMaterial = material;
            float faceZ = BoardThickness * 0.5f + 0.014f;
            Signpost.Text(pivot, name, new Vector3(0f, boardY, -faceZ), 0f, textMaterial, LetterSize);
            Signpost.Text(pivot, name, new Vector3(0f, boardY, faceZ), 180f, textMaterial, LetterSize);
            pivot.gameObject.AddComponent<SignboardSway>().Seed = F() * 100f;
            return root;
        }
    }

    /// <summary>
    /// GL717: "make the board sway in the wind and have the chains move with the connection to the board". Swings its
    /// object about its own x (the line of the chains' hooks): a slow swing with gusts, a few degrees either way.
    /// </summary>
    public sealed class SignboardSway : MonoBehaviour
    {
        /// <summary>Puts this board's swing out of step with its neighbours'.</summary>
        public float Seed;

        [Tooltip("The widest swing, in degrees.")]
        [SerializeField]
        private float swing = 9f;

        private void Update()
        {
            float t = Time.time + Seed;
            float gust = 0.45f + 0.55f * Mathf.PerlinNoise(t * 0.15f, Seed);
            float angle = swing * gust * (Mathf.Sin(t * 1.7f) * 0.8f + Mathf.Sin(t * 3.1f + 1.3f) * 0.2f);
            transform.localRotation = Quaternion.Euler(angle, 0f, angle * 0.15f);
        }
    }
}
