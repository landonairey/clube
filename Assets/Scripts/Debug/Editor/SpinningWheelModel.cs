using System.Collections.Generic;
using System.IO;
using Clube.Core;
using Clube.Game;
using UnityEditor;
using UnityEngine;

namespace Clube.Debug.Editor
{
    /// <summary>
    /// Builds the spinning wheel (GL833): <i>Clube → Models → Build spinning wheel</i> writes its low-poly model, a
    /// placeable prefab, the item, and the recipe that makes it at the workbench (10 boards of any wood, 5 copper
    /// ingots, 5 wool from any animal), and offers the recipe at the workbench. The fabric workshops' yards then show
    /// this model (<c>YardStations</c>), the same one the player builds. Run again after changing the shape.
    /// </summary>
    public static class SpinningWheelModel
    {
        private const string ModelName = "Spinning Wheel";
        private const string Prefab = "Assets/Prefabs/Stations/Spinning Wheel.prefab";
        private const string ItemPath = "Assets/Config/Items/Spinning wheel.asset";
        private const string RecipePath = "Assets/Config/Recipes/Build spinning wheel.asset";
        private const string SourceItem = "Assets/Config/Items/Sleeping roll.asset";
        private const string SourceRecipe = "Assets/Config/Recipes/Build workbench.asset";
        private const string BenchPrefab = "Assets/Prefabs/Stations/Workbench.prefab";
        private const string WoodMaterial = "Assets/Materials/Stations/Wood.mat";
        private const string ClothMaterial = "Assets/Materials/Stations/Cloth.mat";
        private const string WoolMaterial = "Assets/Materials/Stations/Wool.mat";
        private static readonly Color WoolColour = new Color(0.88f, 0.85f, 0.76f);
        private const string Items = "Assets/Config/Items/";

        private const int Boards = 10, Ingots = 5, Wool = 5;
        private const int UnlockLevel = 4;

        [MenuItem("Clube/Models/Build spinning wheel")]
        public static void Build()
        {
            Directory.CreateDirectory("Assets/Models");
            LowPolyModels.Save(ModelName, MakeMesh());
            MakeWoolMaterial();
            AssetDatabase.SaveAssets();
            ItemDefinition item = MakeItem(MakePrefab());
            Recipe recipe = MakeRecipe(item);
            OfferAtWorkbench(recipe);
            AssetDatabase.SaveAssets();
            UnityEngine.Debug.Log("Built the spinning wheel: model, prefab, item and its workbench recipe. Rebuild the item catalog, bake icons, and add the recipe to each scene's journal.");
        }

        // Submeshes: 0 wood, 1 wool and cord. A treadle spinning wheel: a bench on four splayed legs, the big spoked wheel
        // upright at one end on two uprights, the maiden posts with a distaff of wool at the other, a drive cord between,
        // and a treadle board on the ground linked to the wheel. Front (the spinner's side) is +z.
        private static Mesh MakeMesh()
        {
            var model = new LowPolyModels.ModelBuilder(2);
            const float benchY = 0.5f;
            model.Box(0, new Vector3(0.08f, benchY, 0f), new Vector3(1.0f, 0.07f, 0.24f));
            foreach (float x in new[] { -0.4f, 0.4f })
            {
                foreach (float z in new[] { -0.1f, 0.1f })
                {
                    float tiltX = z > 0f ? -8f : 8f;
                    float tiltZ = x > 0f ? 8f : -8f;
                    model.Box(0, new Vector3(x + 0.08f, benchY * 0.5f - 0.02f, z * 1.25f), new Vector3(0.06f, benchY + 0.04f, 0.06f), Quaternion.Euler(tiltX, 0f, tiltZ));
                }
            }

            // The wheel: a twelve-sided rim, six spokes, a hub and axle, held between two uprights.
            Vector2 centre = new Vector2(-0.28f, 0.98f);
            const float radius = 0.36f;
            for (int k = 0; k < 12; k++)
            {
                float angle = k * 30f;
                float rad = angle * Mathf.Deg2Rad;
                Vector3 at = new Vector3(centre.x + radius * Mathf.Cos(rad), centre.y + radius * Mathf.Sin(rad), 0f);
                model.Box(0, at, new Vector3(0.05f, 0.2f, 0.07f), Quaternion.Euler(0f, 0f, angle));
            }
            for (int k = 0; k < 6; k++)
            {
                model.Box(0, new Vector3(centre.x, centre.y, 0f), new Vector3(2f * radius - 0.04f, 0.03f, 0.03f), Quaternion.Euler(0f, 0f, k * 30f));
            }
            model.Box(0, new Vector3(centre.x, centre.y, 0f), new Vector3(0.11f, 0.11f, 0.1f));
            model.Box(0, new Vector3(centre.x, centre.y, 0f), new Vector3(0.035f, 0.035f, 0.3f));
            foreach (float z in new[] { -0.125f, 0.125f })
            {
                float top = centre.y + 0.03f;
                float bottom = benchY + 0.03f;
                model.Box(0, new Vector3(centre.x, (top + bottom) * 0.5f, z), new Vector3(0.06f, top - bottom, 0.04f));
            }

            // The maiden posts and the flyer between them, a tilted distaff topped with wool.
            foreach (float z in new[] { -0.07f, 0.07f })
            {
                model.Box(0, new Vector3(0.44f, benchY + 0.2f, z), new Vector3(0.05f, 0.4f, 0.04f));
            }
            model.Box(0, new Vector3(0.44f, benchY + 0.3f, 0f), new Vector3(0.06f, 0.06f, 0.2f));
            model.Box(0, new Vector3(0.5f, benchY + 0.55f, 0f), new Vector3(0.03f, 0.55f, 0.03f), Quaternion.Euler(0f, 0f, -10f));
            model.Box(1, new Vector3(0.54f, benchY + 0.86f, 0f), new Vector3(0.13f, 0.17f, 0.13f), Quaternion.Euler(0f, 20f, -10f));
            model.Box(1, new Vector3(0.44f, benchY + 0.3f, 0f), new Vector3(0.09f, 0.09f, 0.12f));

            // The drive cord from the top of the wheel to the flyer.
            Vector3 from = new Vector3(centre.x, centre.y + radius, 0.025f);
            Vector3 to = new Vector3(0.44f, benchY + 0.3f, 0.025f);
            Vector3 along = to - from;
            float slope = Mathf.Atan2(along.y, along.x) * Mathf.Rad2Deg;
            model.Box(1, (from + to) * 0.5f, new Vector3(along.magnitude, 0.012f, 0.012f), Quaternion.Euler(0f, 0f, slope));

            // The treadle on the ground and its rod up to the wheel's crank.
            model.Box(0, new Vector3(-0.05f, 0.05f, 0.14f), new Vector3(0.55f, 0.04f, 0.14f), Quaternion.Euler(0f, 0f, 4f));
            model.Box(0, new Vector3(centre.x + 0.1f, 0.45f, 0.14f), new Vector3(0.025f, 0.8f, 0.025f), Quaternion.Euler(0f, 0f, 6f));
            return model.Build(ModelName);
        }

        // Undyed wool: the cloth material in a cream colour.
        private static void MakeWoolMaterial()
        {
            if (!File.Exists(WoolMaterial))
            {
                AssetDatabase.CopyAsset(ClothMaterial, WoolMaterial);
            }
            var material = AssetDatabase.LoadAssetAtPath<Material>(WoolMaterial);
            material.SetColor("_BaseColor", WoolColour);
            EditorUtility.SetDirty(material);
        }

        private static GameObject MakePrefab()
        {
            var root = new GameObject(ModelName);
            try
            {
                root.AddComponent<MeshFilter>().sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>($"Assets/Models/{ModelName}.asset");
                root.AddComponent<MeshRenderer>().sharedMaterials = new[]
                {
                    AssetDatabase.LoadAssetAtPath<Material>(WoodMaterial),
                    AssetDatabase.LoadAssetAtPath<Material>(WoolMaterial),
                };
                var box = root.AddComponent<BoxCollider>();
                box.center = new Vector3(0.1f, 0.68f, 0f);
                box.size = new Vector3(1.2f, 1.36f, 0.4f);
                root.AddComponent<PlacedObject>();
                return PrefabUtility.SaveAsPrefabAsset(root, Prefab);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static ItemDefinition MakeItem(GameObject prefab)
        {
            if (!File.Exists(ItemPath))
            {
                AssetDatabase.CopyAsset(SourceItem, ItemPath);
            }
            var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(ItemPath);
            var serialized = new SerializedObject(item);
            serialized.FindProperty("id").stringValue = "spinning-wheel";
            serialized.FindProperty("displayName").stringValue = "Spinning wheel";
            serialized.FindProperty("iconShape").intValue = 0;
            serialized.FindProperty("prefab").objectReferenceValue = prefab;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return item;
        }

        private static Recipe MakeRecipe(ItemDefinition item)
        {
            if (!File.Exists(RecipePath))
            {
                AssetDatabase.CopyAsset(SourceRecipe, RecipePath);
            }
            ItemDefinition oak = Load("Oak boards"), maple = Load("Maple boards"), copper = Load("Copper ingot");
            ItemDefinition sheep = Load("Sheep wool"), goat = Load("Goat wool"), alpaca = Load("Alpaca wool"), yak = Load("Yak wool");
            var recipe = new SerializedObject(AssetDatabase.LoadAssetAtPath<Recipe>(RecipePath));
            SerializedProperty inputs = recipe.FindProperty("inputs");
            inputs.arraySize = 3;
            SetInput(inputs, 0, oak, Boards);
            SetInput(inputs, 1, copper, Ingots);
            SetInput(inputs, 2, sheep, Wool);
            recipe.FindProperty("output").FindPropertyRelative("Item").objectReferenceValue = item;
            recipe.FindProperty("output").FindPropertyRelative("Count").intValue = 1;
            recipe.FindProperty("station").intValue = (int)StationKind.Worktop;
            recipe.FindProperty("unlockSkill").intValue = (int)SkillType.Crafting;
            recipe.FindProperty("unlockLevel").intValue = UnlockLevel;
            recipe.FindProperty("experience").floatValue = 60f;
            SetSwaps(recipe, "swapInput", "swapFor", oak, new[] { maple });
            SetSwaps(recipe, "swapInput2", "swapFor2", sheep, new[] { goat, alpaca, yak });
            recipe.ApplyModifiedPropertiesWithoutUndo();
            return AssetDatabase.LoadAssetAtPath<Recipe>(RecipePath);
        }

        private static ItemDefinition Load(string name)
        {
            var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>($"{Items}{name}.asset");
            if (item == null)
            {
                throw new FileNotFoundException($"Item '{name}' is missing.");
            }
            return item;
        }

        private static void SetInput(SerializedProperty inputs, int index, ItemDefinition item, int count)
        {
            SerializedProperty entry = inputs.GetArrayElementAtIndex(index);
            entry.FindPropertyRelative("Item").objectReferenceValue = item;
            entry.FindPropertyRelative("Count").intValue = count;
        }

        private static void SetSwaps(SerializedObject recipe, string inputField, string listField, ItemDefinition input, ItemDefinition[] others)
        {
            recipe.FindProperty(inputField).objectReferenceValue = input;
            SerializedProperty list = recipe.FindProperty(listField);
            list.arraySize = others.Length;
            for (int i = 0; i < others.Length; i++)
            {
                list.GetArrayElementAtIndex(i).objectReferenceValue = others[i];
            }
        }

        // The workbench lists the stump's recipes; this one is made there, not at the stump.
        private static void OfferAtWorkbench(Recipe recipe)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(BenchPrefab);
            try
            {
                var station = root.GetComponent<StationObject>();
                var serialized = new SerializedObject(station);
                SerializedProperty list = serialized.FindProperty("recipes");
                for (int i = 0; i < list.arraySize; i++)
                {
                    if (list.GetArrayElementAtIndex(i).objectReferenceValue == recipe)
                    {
                        return;
                    }
                }
                list.arraySize++;
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = recipe;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, BenchPrefab);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>Adds the recipe to every <see cref="PlayerJournal"/> in the open scene, so the crafting journal lists it.</summary>
        public static void AddToJournals(Recipe recipe, IEnumerable<PlayerJournal> journals)
        {
            foreach (PlayerJournal journal in journals)
            {
                var serialized = new SerializedObject(journal);
                SerializedProperty list = serialized.FindProperty("recipes");
                bool listed = false;
                for (int i = 0; i < list.arraySize; i++)
                {
                    listed |= list.GetArrayElementAtIndex(i).objectReferenceValue == recipe;
                }
                if (!listed)
                {
                    list.arraySize++;
                    list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = recipe;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(journal);
                }
            }
        }
    }
}
