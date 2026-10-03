using System;
using System.IO;
using Clube.Core;
using UnityEditor;
using UnityEngine;

namespace Clube.Debug.Editor
{
    /// <summary>
    /// Saves a chunk's surface as a grayscale PNG heightmap (K29) under
    /// <c>Assets/Heightmaps/</c>, imported ready for the heightmap generator: readable,
    /// uncompressed, linear, with no mipmaps. One pixel per sample column.
    /// </summary>
    internal static class HeightmapPngExporter
    {
        private const string Folder = "Assets/Heightmaps";

        /// <returns>The asset path of the saved PNG.</returns>
        public static string Export(ChunkView view)
        {
            Chunk chunk = view.Chunk;
            WorldConfig config = view.Config;
            Vector3Int samples = chunk.SampleCount;
            float chunkHeight = chunk.VoxelCount.y * config.VoxelSize;

            float[] heights = HeightmapExport.ColumnSurfaceHeights(chunk, config.IsoLevel, config.VoxelSize);
            byte[] grey = HeightmapExport.ToGrayscale(heights, chunkHeight);

            // Texture rows run from the bottom up, like the heights (x fastest, then z).
            var pixels = new Color32[grey.Length];
            for (int i = 0; i < grey.Length; i++)
            {
                pixels[i] = new Color32(grey[i], grey[i], grey[i], 255);
            }

            var texture = new Texture2D(samples.x, samples.z, TextureFormat.RGBA32, mipChain: false, linear: true);
            byte[] png;
            try
            {
                texture.SetPixels32(pixels);
                png = texture.EncodeToPNG();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }

            Directory.CreateDirectory(Folder);
            string path = $"{Folder}/{view.name}_{config.Terrain.Generator}_{DateTime.Now:yyyyMMdd_HHmmss}.png";
            File.WriteAllBytes(path, png);
            AssetDatabase.ImportAsset(path);

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Default;
            importer.isReadable = true;
            importer.sRGBTexture = false;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.filterMode = FilterMode.Point;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.SaveAndReimport();
            return path;
        }
    }
}
