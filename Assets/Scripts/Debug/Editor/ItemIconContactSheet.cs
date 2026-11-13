using System.IO;
using Clube.Core;
using UnityEditor;
using UnityEngine;

namespace Clube.Debug.Editor
{
    /// <summary>
    /// One picture of every item's icon (GL612), each in a slot-coloured square in catalog order, to look the whole set
    /// over at once: <i>Clube → Items → Write icon contact sheet</i> writes <c>Temp/item icons.png</c>.
    /// </summary>
    public static class ItemIconContactSheet
    {
        private const string CatalogPath = "Assets/Config/Items/ItemCatalog.asset";
        private const int Cell = 96;
        private const int Gap = 6;
        private static readonly Color Slot = new Color(0.2f, 0.2f, 0.22f, 1f);
        private static readonly Color Sheet = new Color(0.1f, 0.1f, 0.11f, 1f);

        [MenuItem("Clube/Items/Write icon contact sheet")]
        public static void WriteToTemp()
        {
            string path = Path.GetFullPath("Temp/item icons.png");
            Write(path, 12);
            EditorUtility.RevealInFinder(path);
        }

        /// <summary>Writes the sheet as a PNG, <paramref name="columns"/> icons to a row.</summary>
        public static void Write(string path, int columns)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<ItemCatalog>(CatalogPath);
            int count = catalog.Items.Count;
            int rows = (count + columns - 1) / columns;
            int width = columns * (Cell + Gap) + Gap, height = rows * (Cell + Gap) + Gap;
            var sheet = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var pixels = new Color[width * height];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = Sheet;
            }
            for (int index = 0; index < count; index++)
            {
                ItemDefinition item = catalog.Items[index];
                Color[] icon = item != null && item.Icon != null ? Read(item.Icon) : null;
                int left = Gap + index % columns * (Cell + Gap);
                int bottom = height - (index / columns + 1) * (Cell + Gap);
                for (int y = 0; y < Cell; y++)
                {
                    for (int x = 0; x < Cell; x++)
                    {
                        Color over = icon != null ? icon[y * Cell + x] : Color.clear;
                        Color mixed = Color.Lerp(Slot, over, over.a);
                        mixed.a = 1f;
                        pixels[(bottom + y) * width + left + x] = mixed;
                    }
                }
            }
            sheet.SetPixels(pixels);
            sheet.Apply();
            File.WriteAllBytes(path, sheet.EncodeToPNG());
            Object.DestroyImmediate(sheet);
        }

        // An icon's pixels at the cell's size, whether or not its texture is readable.
        private static Color[] Read(Texture icon)
        {
            RenderTexture previous = RenderTexture.active;
            RenderTexture target = RenderTexture.GetTemporary(Cell, Cell, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Graphics.Blit(icon, target);
            RenderTexture.active = target;
            var read = new Texture2D(Cell, Cell, TextureFormat.RGBA32, false);
            read.ReadPixels(new Rect(0, 0, Cell, Cell), 0, 0);
            read.Apply();
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(target);
            Color[] colours = read.GetPixels();
            Object.DestroyImmediate(read);
            return colours;
        }
    }
}
