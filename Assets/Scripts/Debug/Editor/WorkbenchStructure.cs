using System.Collections.Generic;
using System.IO;
using Clube.Core;
using Clube.Game;
using UnityEditor;
using UnityEngine;

namespace Clube.Debug.Editor
{
    /// <summary>
    /// Builds the workbench (GL452), the upgrade from the stump: <i>Clube → Models → Build workbench</i> makes a
    /// <see cref="TerrainStructure"/> of Oak boards samples in the shape of a table (a two-course top on four legs), a
    /// worktop station that makes every stump recipe, its item and icon, and its recipe at the stump (100 oak boards).
    /// Like the furnaces it is real terrain, so a careless swing breaks it. Run again after changing the shape.
    /// </summary>
    public static class WorkbenchStructure
    {
        private const string FurnacePrefab = "Assets/Prefabs/Stations/Furnace.prefab";
        private const string Prefab = "Assets/Prefabs/Stations/Workbench.prefab";
        private const string FurnaceItem = "Assets/Config/Items/Furnace.asset";
        private const string Item = "Assets/Config/Items/Workbench.asset";
        private const string FurnaceRecipe = "Assets/Config/Recipes/Build furnace.asset";
        private const string BuildRecipe = "Assets/Config/Recipes/Build workbench.asset";
        private const string Icon = "Assets/Textures/Items/workbench.png";
        private const string Look = "Assets/Materials/Stations/Workbench.mat";
        private const string Boards = "Assets/Config/Items/Oak boards.asset";
        private const string BoardMaterial = "Assets/Config/Materials/Oak boards.asset";

        private const int Cost = 100;
        private const int UnlockLevel = 3;

        // The table in samples (0.25 m): 6 long (x), 3 deep (z), the top's two courses on legs two courses tall.
        private const int MinX = -3, MaxX = 2, MinZ = -1, MaxZ = 1, LegHeight = 2, TopCourses = 2;

        [MenuItem("Clube/Models/Build workbench")]
        public static void Build()
        {
            var material = AssetDatabase.LoadAssetAtPath<VoxelMaterial>(BoardMaterial);
            var boards = AssetDatabase.LoadAssetAtPath<ItemDefinition>(Boards);
            if (material == null || boards == null)
            {
                UnityEngine.Debug.LogError("Build workbench: Oak boards material or item missing.");
                return;
            }
            if (!File.Exists(Prefab))
            {
                AssetDatabase.CopyAsset(FurnacePrefab, Prefab);
            }
            ItemDefinition item = MakeItem();
            MakeRecipe(boards, item);
            AssetDatabase.SaveAssets();
            // After the recipe, so the bench lists it with the other worktop recipes.
            ConfigurePrefab(material);
            AssetDatabase.SaveAssets();
            UnityEngine.Debug.Log($"Built the workbench ({Parts().Count} samples, built for {Cost} oak boards).");
        }

        /// <summary>The table's samples, as offsets from the first air sample above the ground at its centre.</summary>
        internal static List<Vector3Int> Parts()
        {
            var parts = new List<Vector3Int>();
            for (int y = 0; y < LegHeight + TopCourses; y++)
            {
                for (int z = MinZ; z <= MaxZ; z++)
                {
                    for (int x = MinX; x <= MaxX; x++)
                    {
                        bool top = y >= LegHeight;
                        bool leg = (x == MinX || x == MaxX) && (z == MinZ || z == MaxZ);
                        if (top || leg)
                        {
                            parts.Add(new Vector3Int(x, y, z));
                        }
                    }
                }
            }
            return parts;
        }

        private static void ConfigurePrefab(VoxelMaterial material)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(Prefab);
            try
            {
                // No fire: a bench only works while used.
                if (root.TryGetComponent(out StationFire fire))
                {
                    Object.DestroyImmediate(fire);
                }
                Transform glow = root.transform.Find("Glow");
                if (glow != null)
                {
                    Object.DestroyImmediate(glow.gameObject);
                }

                float voxel = 0.25f;
                var size = new Vector3((MaxX - MinX + 1) * voxel, (LegHeight + TopCourses) * voxel, (MaxZ - MinZ + 1) * voxel);
                var centre = new Vector3((MinX + MaxX) * 0.5f * voxel, size.y * 0.5f, 0f);
                var box = root.GetComponent<BoxCollider>();
                box.center = centre;
                box.size = size;

                var station = new SerializedObject(root.GetComponent<StationObject>());
                station.FindProperty("displayName").stringValue = "Workbench";
                station.FindProperty("kind").intValue = (int)StationKind.Worktop;
                station.FindProperty("burnsFuel").boolValue = false;
                station.FindProperty("heat").floatValue = 1f;
                SerializedProperty recipes = station.FindProperty("recipes");
                List<Recipe> worktop = WorktopRecipes();
                recipes.arraySize = worktop.Count;
                for (int i = 0; i < worktop.Count; i++)
                {
                    recipes.GetArrayElementAtIndex(i).objectReferenceValue = worktop[i];
                }
                station.ApplyModifiedPropertiesWithoutUndo();

                // The build preview: a board-coloured box where the table goes.
                Transform preview = root.transform.Find("Placement preview");
                preview.localPosition = centre;
                preview.localScale = size;
                preview.GetComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<UnityEngine.Mesh>("Cube.fbx");
                preview.GetComponent<MeshRenderer>().sharedMaterial = LookMaterial();

                var structure = new SerializedObject(root.GetComponent<TerrainStructure>());
                SerializedProperty parts = structure.FindProperty("parts");
                List<Vector3Int> offsets = Parts();
                parts.arraySize = offsets.Count;
                for (int i = 0; i < offsets.Count; i++)
                {
                    SerializedProperty part = parts.GetArrayElementAtIndex(i);
                    part.FindPropertyRelative("Offset").vector3IntValue = offsets[i];
                    part.FindPropertyRelative("Density").floatValue = 0.95f;
                    part.FindPropertyRelative("Material").objectReferenceValue = material;
                }
                structure.FindProperty("sampleDensity").floatValue = 0.95f;
                structure.FindProperty("placementPreview").objectReferenceValue = preview.gameObject;
                structure.FindProperty("glowsOn").objectReferenceValue = null;
                structure.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, Prefab);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // Every recipe made at a worktop: the bench makes what the stump makes.
        private static List<Recipe> WorktopRecipes()
        {
            var found = new List<Recipe>();
            foreach (string guid in AssetDatabase.FindAssets("t:Recipe", new[] { "Assets/Config/Recipes" }))
            {
                var recipe = AssetDatabase.LoadAssetAtPath<Recipe>(AssetDatabase.GUIDToAssetPath(guid));
                if (recipe != null && recipe.Station == StationKind.Worktop)
                {
                    found.Add(recipe);
                }
            }
            found.Sort((a, b) => a.UnlockLevel != b.UnlockLevel ? a.UnlockLevel.CompareTo(b.UnlockLevel) : string.CompareOrdinal(a.name, b.name));
            return found;
        }

        private static Material LookMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(Look);
            if (material == null)
            {
                material = new Material(AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Stations/Furnace.mat"));
                AssetDatabase.CreateAsset(material, Look);
            }
            material.SetColor("_BaseColor", new Color(0.62f, 0.45f, 0.28f));
            EditorUtility.SetDirty(material);
            return material;
        }

        private static ItemDefinition MakeItem()
        {
            if (!File.Exists(Item))
            {
                AssetDatabase.CopyAsset(FurnaceItem, Item);
            }
            PaintIcon();
            var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(Item);
            var serialized = new SerializedObject(item);
            serialized.FindProperty("id").stringValue = "workbench";
            serialized.FindProperty("displayName").stringValue = "Workbench";
            serialized.FindProperty("icon").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Texture2D>(Icon);
            serialized.FindProperty("prefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(Prefab);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return item;
        }

        // A plain icon: a plank-topped table on four legs, outlined.
        private static void PaintIcon()
        {
            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var wood = new Color(0.66f, 0.47f, 0.28f);
            var dark = new Color(0.36f, 0.24f, 0.13f);
            var outline = new Color(0.12f, 0.08f, 0.05f);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool top = x >= 12 && x <= 115 && y >= 66 && y <= 86;
                    bool topEdge = top && (x <= 14 || x >= 113 || y <= 68 || y >= 84);
                    bool plankLine = top && (y == 76 || x == 46 || x == 81) && !topEdge;
                    bool leg = y >= 22 && y < 66 && ((x >= 20 && x <= 31) || (x >= 96 && x <= 107));
                    bool legEdge = leg && (x == 20 || x == 31 || x == 96 || x == 107 || y <= 24);
                    bool rail = y >= 40 && y <= 46 && x > 31 && x < 96;
                    Color colour = Color.clear;
                    if (topEdge || legEdge || plankLine)
                    {
                        colour = outline;
                    }
                    else if (top)
                    {
                        colour = wood;
                    }
                    else if (leg || rail)
                    {
                        colour = dark;
                    }
                    texture.SetPixel(x, y, colour);
                }
            }
            File.WriteAllBytes(Icon, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(Icon);
            var importer = (TextureImporter)AssetImporter.GetAtPath(Icon);
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        private static void MakeRecipe(ItemDefinition boards, ItemDefinition item)
        {
            if (!File.Exists(BuildRecipe))
            {
                AssetDatabase.CopyAsset(FurnaceRecipe, BuildRecipe);
            }
            var recipe = new SerializedObject(AssetDatabase.LoadAssetAtPath<Recipe>(BuildRecipe));
            SerializedProperty inputs = recipe.FindProperty("inputs");
            inputs.arraySize = 1;
            inputs.GetArrayElementAtIndex(0).FindPropertyRelative("Item").objectReferenceValue = boards;
            inputs.GetArrayElementAtIndex(0).FindPropertyRelative("Count").intValue = Cost;
            recipe.FindProperty("output").FindPropertyRelative("Item").objectReferenceValue = item;
            recipe.FindProperty("output").FindPropertyRelative("Count").intValue = 1;
            recipe.FindProperty("station").intValue = (int)StationKind.Worktop;
            recipe.FindProperty("unlockSkill").intValue = (int)SkillType.Crafting;
            recipe.FindProperty("unlockLevel").intValue = UnlockLevel;
            recipe.FindProperty("experience").floatValue = 50f;
            recipe.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
