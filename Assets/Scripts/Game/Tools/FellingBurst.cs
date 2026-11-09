using System.Collections.Generic;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// What a felled tree throws up as it lands (GL469): leaves flung up out of the dust that tumble and shrink away in about
    /// a second, and splinters for the wood that shattered past the woodcutter's cap (GL468). Small tinted flakes and chips
    /// drawn with the held-item material, like <see cref="HitDebris"/>; it removes itself when the last one is gone.
    /// </summary>
    public class FellingBurst : MonoBehaviour
    {
        private static readonly int TintId = Shader.PropertyToID("_Tint");
        private static readonly Color[] LeafColours =
        {
            new Color(0.26f, 0.42f, 0.15f), new Color(0.34f, 0.5f, 0.18f), new Color(0.2f, 0.34f, 0.12f), new Color(0.45f, 0.48f, 0.16f),
        };
        private static readonly Color SplinterColour = new Color(0.55f, 0.42f, 0.28f);
        private const float Gravity = 7f;
        private const float Drag = 1.6f;

        private static Mesh leafShape;
        private static Mesh splinterShape;

        private sealed class Piece
        {
            public Transform Transform;
            public Vector3 Velocity;
            public Vector3 Spin;
            public float Size;
            public float Age;
            public float Life;
        }

        private readonly List<Piece> pieces = new List<Piece>();

        /// <summary>
        /// Throws <paramref name="leaves"/> leaves and <paramref name="splinters"/> splinters up from a box round
        /// <paramref name="centre"/> (in <paramref name="parent"/>'s space) that's <paramref name="length"/> along
        /// <paramref name="along"/> and <paramref name="width"/> across.
        /// </summary>
        public static FellingBurst Spawn(
            Transform parent, Vector3 centre, Vector3 along, float length, float width, int leaves, int splinters, Material material)
        {
            if (material == null || leaves + splinters <= 0)
            {
                return null;
            }
            leafShape = leafShape != null ? leafShape : HitDebris.Chunk("Leaf", new Vector3(1.2f, 0.12f, 0.8f), 11);
            splinterShape = splinterShape != null ? splinterShape : HitDebris.Chunk("Splinter", new Vector3(0.35f, 0.3f, 1.6f), 12);
            var burst = new GameObject("Felling burst").AddComponent<FellingBurst>();
            burst.transform.SetParent(parent, false);
            burst.transform.localPosition = centre;
            Vector3 across = Vector3.Cross(Vector3.up, along).normalized;
            var tint = new MaterialPropertyBlock();
            for (int i = 0; i < leaves + splinters; i++)
            {
                bool leaf = i < leaves;
                Vector3 offset = along * Random.Range(-0.5f, 0.5f) * length + across * Random.Range(-0.5f, 0.5f) * width
                                 + Vector3.up * Random.Range(0f, 1f);
                Vector3 outward = (offset - Vector3.up * offset.y).normalized;
                var velocity = new Vector3(Random.Range(-1.5f, 1.5f), 0f, Random.Range(-1.5f, 1.5f)) + outward * Random.Range(0.5f, 2f);
                velocity.y = leaf ? Random.Range(3.5f, 7.5f) : Random.Range(2.5f, 5.5f);
                var piece = new GameObject(leaf ? "Leaf" : "Splinter");
                piece.transform.SetParent(burst.transform, false);
                piece.transform.localPosition = offset;
                piece.transform.localRotation = Random.rotationUniform;
                float size = leaf ? Random.Range(0.18f, 0.32f) : Random.Range(0.12f, 0.22f);
                piece.transform.localScale = Vector3.one * size;
                piece.AddComponent<MeshFilter>().sharedMesh = leaf ? leafShape : splinterShape;
                var meshRenderer = piece.AddComponent<MeshRenderer>();
                meshRenderer.sharedMaterial = material;
                meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                tint.SetColor(TintId, leaf ? LeafColours[Random.Range(0, LeafColours.Length)] : SplinterColour);
                meshRenderer.SetPropertyBlock(tint);
                burst.pieces.Add(new Piece
                {
                    Transform = piece.transform,
                    Velocity = velocity,
                    Spin = Random.insideUnitSphere * (leaf ? 720f : 540f),
                    Size = size,
                    Life = leaf ? Random.Range(0.8f, 1.4f) : Random.Range(0.6f, 1.1f),
                });
            }
            return burst;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            for (int i = pieces.Count - 1; i >= 0; i--)
            {
                Piece piece = pieces[i];
                piece.Age += dt;
                if (piece.Age >= piece.Life)
                {
                    Destroy(piece.Transform.gameObject);
                    pieces.RemoveAt(i);
                    continue;
                }
                // Air slows them quickly, so they hang a moment at the top while they shrink away.
                piece.Velocity += Vector3.down * Gravity * dt;
                piece.Velocity -= piece.Velocity * Mathf.Min(1f, Drag * dt);
                piece.Transform.localPosition += piece.Velocity * dt;
                piece.Transform.localRotation *= Quaternion.Euler(piece.Spin * dt);
                float left = 1f - piece.Age / piece.Life;
                piece.Transform.localScale = Vector3.one * (piece.Size * Mathf.Sqrt(left));
            }
            if (pieces.Count == 0)
            {
                Destroy(gameObject);
            }
        }
    }
}
