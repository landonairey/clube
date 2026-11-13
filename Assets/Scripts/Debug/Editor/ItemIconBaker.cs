using System.Collections.Generic;
using System.IO;
using Clube.Core;
using Clube.Game;
using UnityEditor;
using UnityEngine;

namespace Clube.Debug.Editor
{
    /// <summary>
    /// Bakes every item's inventory icon from its 3D model (GL612): <i>Clube → Items → Bake icons from models</i> goes
    /// through the item catalog, asks <see cref="ItemIconModels"/> for each item's model, has
    /// <see cref="ItemIconRenderer"/> take its picture, writes it to <c>Assets/Textures/Items/</c> and links it to the
    /// item, where the inventory reads it as before. A new item gets its icon by running the menu; nothing is drawn by
    /// hand. Cached: each picture's file remembers what it was taken of (the model's shape and colours, its texture,
    /// the look of the renderer), and an item whose model hasn't changed is skipped.
    /// </summary>
    public static class ItemIconBaker
    {
        /// <summary>Pixels along an icon's side.</summary>
        public const int Size = 128;

        // Raise when the renderer's lights, shaders or framing change, so every icon is taken again.
        private const string Look = "v4";
        private const string Folder = "Assets/Textures/Items";
        private const string CatalogPath = "Assets/Config/Items/ItemCatalog.asset";
        private const string RegistryPath = "Assets/Config/Materials/MaterialRegistry.asset";

        [MenuItem("Clube/Items/Bake icons from models")]
        public static void BakeChanged()
        {
            Bake(false);
        }

        [MenuItem("Clube/Items/Bake icons from models (all again)")]
        public static void BakeAll()
        {
            Bake(true);
        }

        /// <summary>Where an item's baked icon is kept.</summary>
        public static string PathOf(ItemDefinition item)
        {
            return $"{Folder}/{item.name.ToLowerInvariant()}.png";
        }

        /// <summary>
        /// Bakes the icons whose models changed (all of them when <paramref name="force"/>) and returns how many were
        /// written.
        /// </summary>
        public static int Bake(bool force)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<ItemCatalog>(CatalogPath);
            var materials = AssetDatabase.LoadAssetAtPath<MaterialRegistry>(RegistryPath);
            if (catalog == null || materials == null)
            {
                UnityEngine.Debug.LogError($"Item icons: no catalog at {CatalogPath} or no materials at {RegistryPath}.");
                return 0;
            }
            Directory.CreateDirectory(Folder);
            int written = 0, kept = 0, painted = 0;
            using (var renderer = new ItemIconRenderer())
            {
                foreach (ItemDefinition item in catalog.Items)
                {
                    if (item == null)
                    {
                        continue;
                    }
                    ItemIconModel model = ItemIconModels.For(item, materials);
                    if (model.IsEmpty)
                    {
                        painted++;
                        continue;
                    }
                    string path = PathOf(item);
                    string key = KeyOf(model);
                    var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                    if (!force && importer != null && importer.userData == key && item.Icon == AssetDatabase.LoadAssetAtPath<Texture2D>(path))
                    {
                        kept++;
                        continue;
                    }
                    Texture2D picture = renderer.Render(model, Size);
                    if (picture == null)
                    {
                        UnityEngine.Debug.LogWarning($"Item icons: nothing to draw for {item.DisplayName}.");
                        continue;
                    }
                    Write(path, picture, key);
                    Link(item, path);
                    // Only the item: saving everything would also write whatever else is unsaved in the Editor.
                    AssetDatabase.SaveAssetIfDirty(item);
                    written++;
                }
            }
            UnityEngine.Debug.Log($"Item icons: {written} baked, {kept} unchanged, {painted} without a model (painted by hand).");
            return written;
        }

        private static void Write(string path, Texture2D picture, string key)
        {
            File.WriteAllBytes(path, picture.EncodeToPNG());
            Object.DestroyImmediate(picture);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.userData = key;
            importer.SaveAndReimport();
        }

        private static void Link(ItemDefinition item, string path)
        {
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (item.Icon == icon)
            {
                return;
            }
            var serialized = new SerializedObject(item);
            serialized.FindProperty("icon").objectReferenceValue = icon;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        // What a picture was taken of, in a line: the same line means the same picture.
        private static string KeyOf(ItemIconModel model)
        {
            var parts = new List<string> { Look, Size.ToString(), Round(model.Turn.eulerAngles), Round(model.View.eulerAngles), ColorUtility.ToHtmlStringRGBA(model.Tint) };
            if (model.Prefab != null)
            {
                parts.Add(AssetDatabase.GetAssetDependencyHash(AssetDatabase.GetAssetPath(model.Prefab)).ToString());
            }
            if (model.Mesh != null)
            {
                parts.Add(HashOf(model.Mesh).ToString("x8"));
            }
            if (model.Albedo != null)
            {
                parts.Add(AssetDatabase.GetAssetDependencyHash(AssetDatabase.GetAssetPath(model.Albedo)).ToString());
            }
            return string.Join("|", parts);
        }

        private static string Round(Vector3 v)
        {
            return $"{Mathf.RoundToInt(v.x)},{Mathf.RoundToInt(v.y)},{Mathf.RoundToInt(v.z)}";
        }

        // The model's corners, colours and surfaces, so a change to the code that builds it takes the picture again.
        private static uint HashOf(UnityEngine.Mesh mesh)
        {
            uint hash = 2166136261;
            void Mix(float value)
            {
                hash = (hash ^ (uint)Mathf.RoundToInt(value * 4096f)) * 16777619;
            }
            foreach (Vector3 v in mesh.vertices)
            {
                Mix(v.x);
                Mix(v.y);
                Mix(v.z);
            }
            foreach (Color c in mesh.colors)
            {
                Mix(c.r);
                Mix(c.g);
                Mix(c.b);
                Mix(c.a);
            }
            foreach (Vector2 uv in mesh.uv)
            {
                Mix(uv.x);
                Mix(uv.y);
            }
            return hash;
        }
    }
}
