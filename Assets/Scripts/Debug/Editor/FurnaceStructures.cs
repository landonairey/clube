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
    /// furnace beside it: the same shape in clay, hotter, with its own item, icon and recipe, and the Brick
    /// furnace (GL229) in bricks, hotter again. Run again
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
        private const string BrickPrefab = "Assets/Prefabs/Stations/Brick Furnace.prefab";
        private const string BrickItem = "Assets/Config/Items/Brick furnace.asset";
        private const string BrickRecipe = "Assets/Config/Recipes/Build brick furnace.asset";
        private const string BrickIcon = "Assets/Textures/Items/brick furnace.png";
        private const string BrickLook = "Assets/Materials/Stations/Brick.mat";

        private const float ClayHeat = 1.5f;
        private const int ClayUnlockLevel = 5;

        // The brick furnace outdoes the clay one (GL229): hotter, so faster on the same fuel.
        private const float BrickHeat = 2.25f;
        private const int BrickUnlockLevel = 6;

        [MenuItem("Clube/Models/Build furnaces")]
        public static void Build()
        {
            LowPolyModels.BuildFurnaceStamp();
            // What each costs: a little over what it gives back when mined away, rounded up to a 5.
            int cost = (LowPolyModels.FurnaceParts().Count + 4) / 5 * 5 + 5;
            var stamp = AssetDatabase.LoadAssetAtPath<UnityEngine.Mesh>("Assets/Models/Furnace stamp.asset");
            var looseStone = AssetDatabase.LoadAssetAtPath<VoxelMaterial>("Assets/Config/Materials/Loose stone.asset");
            var clay = AssetDatabase.LoadAssetAtPath<VoxelMaterial>("Assets/Config/Materials/Clay.asset");
            var clayItem = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/Config/Items/Clay.asset");
            var brick = AssetDatabase.LoadAssetAtPath<VoxelMaterial>("Assets/Config/Materials/Brick.asset");
            var brickItem = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/Config/Items/Brick.asset");

            Configure(StonePrefab, "Stone furnace", 1f, looseStone, AssetDatabase.LoadAssetAtPath<Material>(StoneLook), stamp);
            Rename(StoneItem, "Stone furnace");
            SetCost(StoneRecipe, null, cost, -1);

            CopyIfMissing(StonePrefab, ClayPrefab);
            Configure(ClayPrefab, "Clay furnace", ClayHeat, clay, Look(ClayLook, new Color(0.68f, 0.48f, 0.34f)), stamp);
            ItemDefinition clayFurnace = FurnaceItem(ClayItem, "clay-furnace", "Clay furnace", ClayIcon, new Color(1.25f, 0.9f, 0.68f), ClayPrefab);
            CopyIfMissing(StoneRecipe, ClayRecipe);
            SetCost(ClayRecipe, clayItem, cost, ClayUnlockLevel, clayFurnace);

            // The brick furnace (GL229): the clay one's copy, so it smelts the same recipes, built of bricks.
            CopyIfMissing(ClayPrefab, BrickPrefab);
            Configure(BrickPrefab, "Brick furnace", BrickHeat, brick, Look(BrickLook, new Color(0.62f, 0.27f, 0.2f)), stamp);
            ItemDefinition brickFurnace = FurnaceItem(BrickItem, "brick-furnace", "Brick furnace", BrickIcon, new Color(1.3f, 0.7f, 0.6f), BrickPrefab);
            CopyIfMissing(ClayRecipe, BrickRecipe);
            SetCost(BrickRecipe, brickItem, cost, BrickUnlockLevel, brickFurnace);
            AssetDatabase.SaveAssets();
            UnityEngine.Debug.Log($"Built the stone, clay and brick furnaces ({LowPolyModels.FurnaceParts().Count} samples each, built for {cost}).");
        }

        private static void CopyIfMissing(string from, string to)
        {
            if (!File.Exists(to))
            {
                AssetDatabase.CopyAsset(from, to);
            }
        }

        // The prefab as a structure of one material: no model of its own (the preview child shows where
        // the samples go), a glow inside, and a collider round its body to use it by.
        private static void Configure(string path, string name, float heat, VoxelMaterial material, Material look, UnityEngine.Mesh stamp)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                Object.DestroyImmediate(root.GetComponent<MeshRenderer>());
                Object.DestroyImmediate(root.GetComponent<MeshFilter>());

                var box = Get<BoxCollider>(root);
                // The whole body (GL143), so the player can use it from any side and tools still break it.
                box.center = new Vector3(0f, 0.85f, 0f);
                box.size = new Vector3(1.3f, 1.7f, 1.3f);

                var station = new SerializedObject(root.GetComponent<StationObject>());
                station.FindProperty("displayName").stringValue = name;
                station.FindProperty("heat").floatValue = heat;
                station.ApplyModifiedPropertiesWithoutUndo();

                Transform preview = Child(root, "Placement preview");
                Get<MeshFilter>(preview.gameObject).sharedMesh = stamp;
                var previewRenderer = Get<MeshRenderer>(preview.gameObject);
                previewRenderer.sharedMaterial = look;

                Transform glow = Child(root, "Glow");
                glow.localPosition = new Vector3(0f, 0.2f, 0f);
                var light = Get<Light>(glow.gameObject);
                light.type = LightType.Point;
                light.color = new Color(1f, 0.42f, 0.14f);
                light.intensity = 1.6f;
                light.range = 3f;

                var structure = Get<TerrainStructure>(root);
                var serialized = new SerializedObject(structure);
                SerializedProperty parts = serialized.FindProperty("parts");
                List<(Vector3Int Offset, float Density)> offsets = LowPolyModels.FurnaceParts();
                parts.arraySize = offsets.Count;
                for (int i = 0; i < offsets.Count; i++)
                {
                    SerializedProperty part = parts.GetArrayElementAtIndex(i);
                    part.FindPropertyRelative("Offset").vector3IntValue = offsets[i].Offset;
                    part.FindPropertyRelative("Density").floatValue = offsets[i].Density;
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

        // A furnace's build-preview colour, made from the stone one.
        private static Material Look(string path, Color color)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(AssetDatabase.LoadAssetAtPath<Material>(StoneLook));
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", color);
            EditorUtility.SetDirty(material);
            return material;
        }

        // A furnace's item: the stone one's copy, its own id, name, prefab and a tinted icon.
        private static ItemDefinition FurnaceItem(string path, string id, string name, string icon, Color tint, string prefab)
        {
            CopyIfMissing(StoneItem, path);
            TintIcon(icon, tint);
            var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);
            var serialized = new SerializedObject(item);
            serialized.FindProperty("id").stringValue = id;
            serialized.FindProperty("displayName").stringValue = name;
            serialized.FindProperty("icon").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Texture2D>(icon);
            serialized.FindProperty("prefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(prefab);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return item;
        }

        // The stone furnace's icon, tinted, written to its own file.
        private static void TintIcon(string path, Color tint)
        {
            var source = new Texture2D(2, 2);
            source.LoadImage(File.ReadAllBytes(StoneIcon));
            Color[] pixels = source.GetPixels();
            for (int i = 0; i < pixels.Length; i++)
            {
                Color pixel = pixels[i];
                pixels[i] = new Color(Mathf.Clamp01(pixel.r * tint.r), Mathf.Clamp01(pixel.g * tint.g), Mathf.Clamp01(pixel.b * tint.b), pixel.a);
            }
            source.SetPixels(pixels);
            File.WriteAllBytes(path, source.EncodeToPNG());
            Object.DestroyImmediate(source);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            var stoneImporter = (TextureImporter)AssetImporter.GetAtPath(StoneIcon);
            importer.alphaIsTransparency = stoneImporter.alphaIsTransparency;
            importer.mipmapEnabled = stoneImporter.mipmapEnabled;
            importer.textureCompression = stoneImporter.textureCompression;
            importer.SaveAndReimport();
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
