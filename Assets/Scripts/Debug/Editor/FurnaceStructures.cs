using System.Collections.Generic;
using System.IO;
using Clube.Core;
using Clube.Game;
using UnityEditor;
using UnityEngine;

namespace Clube.Debug.Editor
{
    /// <summary>
    /// Builds the furnaces from the ground (GL98), as the campfire is (GL85): <i>Clube → Models → Build
    /// furnaces</i> turns the furnace prefab into the Stone furnace, a <see cref="TerrainStructure"/> of
    /// loose stone (cobbles) in the shape of <see cref="LowPolyModels.FurnaceParts"/>, and makes the Clay
    /// furnace beside it: the same shape in clay, hotter, with its own item, icon and recipe. Run again
    /// after changing the shape; it updates what's there.
    /// </summary>
    public static class FurnaceStructures
    {
        private const string StonePrefab = "Assets/Prefabs/Stations/Furnace.prefab";
        private const string ClayPrefab = "Assets/Prefabs/Stations/Clay Furnace.prefab";
        private const string StoneItem = "Assets/Config/Items/Furnace.asset";
        private const string ClayItem = "Assets/Config/Items/Clay furnace.asset";
        private const string StoneRecipe = "Assets/Config/Recipes/Build furnace.asset";
        private const string ClayRecipe = "Assets/Config/Recipes/Build clay furnace.asset";
        private const string StoneIcon = "Assets/Textures/Items/furnace.png";
        private const string ClayIcon = "Assets/Textures/Items/clay furnace.png";
        private const string StoneLook = "Assets/Materials/Stations/Furnace.mat";
        private const string ClayLook = "Assets/Materials/Stations/Clay.mat";

        // What each costs, a little over what it gives back when mined away (206 samples).
        private const int Cost = 210;
        private const float ClayHeat = 1.5f;
        private const int ClayUnlockLevel = 5;

        [MenuItem("Clube/Models/Build furnaces")]
        public static void Build()
        {
            LowPolyModels.BuildFurnaceStamp();
            var stamp = AssetDatabase.LoadAssetAtPath<UnityEngine.Mesh>("Assets/Models/Furnace stamp.asset");
            var looseStone = AssetDatabase.LoadAssetAtPath<VoxelMaterial>("Assets/Config/Materials/Loose stone.asset");
            var clay = AssetDatabase.LoadAssetAtPath<VoxelMaterial>("Assets/Config/Materials/Clay.asset");
            var clayItem = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/Config/Items/Clay.asset");
            var glow = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Stations/Glow.mat");

            Configure(StonePrefab, "Stone furnace", 1f, looseStone, AssetDatabase.LoadAssetAtPath<Material>(StoneLook), stamp);
            Rename(StoneItem, "Stone furnace");
            SetCost(StoneRecipe, null, Cost, -1);

            if (!File.Exists(ClayPrefab))
            {
                AssetDatabase.CopyAsset(StonePrefab, ClayPrefab);
            }
            Configure(ClayPrefab, "Clay furnace", ClayHeat, clay, ClayMaterial(), stamp);
            ItemDefinition clayFurnace = ClayFurnaceItem();
            ClayFurnaceRecipe();
            SetCost(ClayRecipe, clayItem, Cost, ClayUnlockLevel, clayFurnace);
            AssetDatabase.SaveAssets();
            UnityEngine.Debug.Log($"Built the stone and clay furnaces ({LowPolyModels.FurnaceParts().Count} samples each).");
        }

        // The prefab as a structure of one material: no model of its own (the preview child shows where
        // the samples go), a glow inside, and a collider in its mouth to use it by.
        private static void Configure(string path, string name, float heat, VoxelMaterial material, Material look, UnityEngine.Mesh stamp)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                Object.DestroyImmediate(root.GetComponent<MeshRenderer>());
                Object.DestroyImmediate(root.GetComponent<MeshFilter>());

                var box = Get<BoxCollider>(root);
                box.center = new Vector3(0f, 0.35f, 0.75f);
                box.size = new Vector3(0.75f, 0.7f, 0.3f);

                var station = new SerializedObject(root.GetComponent<StationObject>());
                station.FindProperty("displayName").stringValue = name;
                station.FindProperty("heat").floatValue = heat;
                station.ApplyModifiedPropertiesWithoutUndo();

                Transform preview = Child(root, "Placement preview");
                Get<MeshFilter>(preview.gameObject).sharedMesh = stamp;
                var previewRenderer = Get<MeshRenderer>(preview.gameObject);
                previewRenderer.sharedMaterial = look;

                Transform glow = Child(root, "Glow");
                glow.localPosition = new Vector3(0f, 0.35f, 0f);
                var light = Get<Light>(glow.gameObject);
                light.type = LightType.Point;
                light.color = new Color(1f, 0.42f, 0.14f);
                light.intensity = 1.6f;
                light.range = 3f;

                var structure = Get<TerrainStructure>(root);
                var serialized = new SerializedObject(structure);
                SerializedProperty parts = serialized.FindProperty("parts");
                List<Vector3Int> offsets = LowPolyModels.FurnaceParts();
                parts.arraySize = offsets.Count;
                for (int i = 0; i < offsets.Count; i++)
                {
                    SerializedProperty part = parts.GetArrayElementAtIndex(i);
                    part.FindPropertyRelative("Offset").vector3IntValue = offsets[i];
                    part.FindPropertyRelative("Material").objectReferenceValue = material;
                }
                serialized.FindProperty("sampleDensity").floatValue = 0.85f;
                serialized.FindProperty("placementPreview").objectReferenceValue = preview.gameObject;
                serialized.FindProperty("glowsOn").objectReferenceValue = null;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // The component on the object, added when it has none (GetComponent's missing result isn't null to ?? in the Editor).
        private static T Get<T>(GameObject target) where T : Component
        {
            return target.TryGetComponent(out T found) ? found : target.AddComponent<T>();
        }

        private static Transform Child(GameObject root, string name)
        {
            Transform child = root.transform.Find(name);
            if (child == null)
            {
                child = new GameObject(name).transform;
                child.SetParent(root.transform, false);
            }
            return child;
        }

        private static void Rename(string itemPath, string name)
        {
            var item = new SerializedObject(AssetDatabase.LoadAssetAtPath<ItemDefinition>(itemPath));
            item.FindProperty("displayName").stringValue = name;
            item.ApplyModifiedPropertiesWithoutUndo();
        }

        // A warm clay colour for the clay furnace's build preview, made from the stone one.
        private static Material ClayMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(ClayLook);
            if (material == null)
            {
                material = new Material(AssetDatabase.LoadAssetAtPath<Material>(StoneLook));
                AssetDatabase.CreateAsset(material, ClayLook);
            }
            material.SetColor("_BaseColor", new Color(0.68f, 0.48f, 0.34f));
            EditorUtility.SetDirty(material);
            return material;
        }

        // The clay furnace's item: the stone one's copy, its own id, name, prefab and a clay-tinted icon.
        private static ItemDefinition ClayFurnaceItem()
        {
            if (!File.Exists(ClayItem))
            {
                AssetDatabase.CopyAsset(StoneItem, ClayItem);
            }
            TintIcon();
            var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(ClayItem);
            var serialized = new SerializedObject(item);
            serialized.FindProperty("id").stringValue = "clay-furnace";
            serialized.FindProperty("displayName").stringValue = "Clay furnace";
            serialized.FindProperty("icon").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Texture2D>(ClayIcon);
            serialized.FindProperty("prefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(ClayPrefab);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return item;
        }

        private static void TintIcon()
        {
            var source = new Texture2D(2, 2);
            source.LoadImage(File.ReadAllBytes(StoneIcon));
            var tint = new Color(1.25f, 0.9f, 0.68f);
            Color[] pixels = source.GetPixels();
            for (int i = 0; i < pixels.Length; i++)
            {
                Color pixel = pixels[i];
                pixels[i] = new Color(Mathf.Clamp01(pixel.r * tint.r), Mathf.Clamp01(pixel.g * tint.g), Mathf.Clamp01(pixel.b * tint.b), pixel.a);
            }
            source.SetPixels(pixels);
            File.WriteAllBytes(ClayIcon, source.EncodeToPNG());
            Object.DestroyImmediate(source);
            AssetDatabase.ImportAsset(ClayIcon);
            var importer = (TextureImporter)AssetImporter.GetAtPath(ClayIcon);
            var stoneImporter = (TextureImporter)AssetImporter.GetAtPath(StoneIcon);
            importer.alphaIsTransparency = stoneImporter.alphaIsTransparency;
            importer.mipmapEnabled = stoneImporter.mipmapEnabled;
            importer.textureCompression = stoneImporter.textureCompression;
            importer.SaveAndReimport();
        }

        private static Recipe ClayFurnaceRecipe()
        {
            if (!File.Exists(ClayRecipe))
            {
                AssetDatabase.CopyAsset(StoneRecipe, ClayRecipe);
            }
            return AssetDatabase.LoadAssetAtPath<Recipe>(ClayRecipe);
        }

        // A build recipe's single input and count, its output and the Crafting level it unlocks at
        // (null or -1 to leave them).
        private static void SetCost(string path, ItemDefinition input, int count, int unlockLevel, ItemDefinition output = null)
        {
            var recipe = new SerializedObject(AssetDatabase.LoadAssetAtPath<Recipe>(path));
            SerializedProperty inputs = recipe.FindProperty("inputs");
            inputs.arraySize = 1;
            SerializedProperty first = inputs.GetArrayElementAtIndex(0);
            if (input != null)
            {
                first.FindPropertyRelative("Item").objectReferenceValue = input;
            }
            first.FindPropertyRelative("Count").intValue = count;
            if (output != null)
            {
                recipe.FindProperty("output").FindPropertyRelative("Item").objectReferenceValue = output;
            }
            if (unlockLevel >= 0)
            {
                recipe.FindProperty("unlockLevel").intValue = unlockLevel;
                recipe.FindProperty("experience").floatValue = 50f;
            }
            recipe.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
