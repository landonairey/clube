using Clube.Core;
using UnityEditor;
using UnityEngine;

namespace Clube.Debug.Editor
{
    /// <summary>
    /// *Clube → Sky → Build cloud noise* (GL252): bakes <see cref="CloudNoise"/> into the repeating 3D texture the
    /// <c>Clube/Clouds</c> shader samples, <c>Assets/Textures/Sky/Cloud noise.asset</c>. Run it again after changing the
    /// noise; the clouds' material keeps pointing at the same asset.
    /// </summary>
    public static class SkyTextureTools
    {
        public const string CloudNoisePath = "Assets/Textures/Sky/Cloud noise.asset";
        private const int Size = 64;
        private const int Seed = 4127;

        [MenuItem("Clube/Sky/Build cloud noise")]
        public static void BuildCloudNoise()
        {
            byte[] bytes = CloudNoise.Build(Size, Seed);
            var texture = new Texture3D(Size, Size, Size, TextureFormat.RGBA32, false)
            {
                name = "Cloud noise",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
                anisoLevel = 0,
            };
            texture.SetPixelData(bytes, 0);
            texture.Apply(false, false);

            if (!AssetDatabase.IsValidFolder("Assets/Textures/Sky"))
            {
                AssetDatabase.CreateFolder("Assets/Textures", "Sky");
            }
            var existing = AssetDatabase.LoadAssetAtPath<Texture3D>(CloudNoisePath);
            if (existing != null)
            {
                EditorUtility.CopySerialized(texture, existing);
                Object.DestroyImmediate(texture);
                EditorUtility.SetDirty(existing);
            }
            else
            {
                AssetDatabase.CreateAsset(texture, CloudNoisePath);
            }
            AssetDatabase.SaveAssets();
            UnityEngine.Debug.Log($"Cloud noise: {Size}³ written to {CloudNoisePath}.");
        }
    }
}
