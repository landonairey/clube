using System.IO;
using Clube.Core;
using Clube.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Clube.Debug.Editor
{
    /// <summary>
    /// Builds the tutorial's map (GL514): the heightmap of the work fields and the stone mountain round them
    /// (<see cref="CompanyWork.Height"/>, written as a 16-bit RAW file), its <see cref="WorldConfig"/> (a copy of
    /// WorldLab's on the Heightmap generator, without trees or water), and the Tutorial scene (a copy of WorldLab
    /// without the town and the travellers). Run again after changing the map's shape to rewrite the heightmap.
    /// </summary>
    public static class TutorialMapTools
    {
        private const string HeightmapPath = "Assets/Config/TutorialHeightmap.bytes";
        private const string ConfigPath = "Assets/Config/TutorialWorldConfig.asset";
        private const string ScenePath = "Assets/Scenes/Tutorial.unity";
        private const string WorldLabPath = "Assets/Scenes/WorldLab.unity";
        private const int Side = 128;
        private const float Lowest = 4f;
        private const float Range = 88f;

        [MenuItem("Clube/Tutorial/Write heightmap")]
        public static void WriteHeightmap()
        {
            var bytes = new byte[Side * Side * 2];
            for (int z = 0; z < Side; z++)
            {
                for (int x = 0; x < Side; x++)
                {
                    float height = (CompanyWork.Height(x, z) - Lowest) / Range;
                    int value = Mathf.Clamp(Mathf.RoundToInt(height * 65535f), 0, 65535);
                    int i = (x + z * Side) * 2;
                    bytes[i] = (byte)(value & 0xff);
                    bytes[i + 1] = (byte)(value >> 8);
                }
            }
            File.WriteAllBytes(HeightmapPath, bytes);
            AssetDatabase.ImportAsset(HeightmapPath);
        }

        [MenuItem("Clube/Tutorial/Build map")]
        public static void BuildMap()
        {
            WriteHeightmap();
            if (AssetDatabase.LoadAssetAtPath<WorldConfig>(ConfigPath) == null)
            {
                AssetDatabase.CopyAsset("Assets/Config/WorldLabWorldConfig.asset", ConfigPath);
            }
            var config = new SerializedObject(AssetDatabase.LoadAssetAtPath<WorldConfig>(ConfigPath));
            config.FindProperty("fixedAreaSize").floatValue = 200f;
            config.FindProperty("worldHeightInChunks").intValue = 24;
            SerializedProperty terrain = config.FindProperty("terrain");
            terrain.FindPropertyRelative("generator").intValue = (int)TerrainGeneratorType.Heightmap;
            terrain.FindPropertyRelative("shapeOwner").intValue = (int)TerrainGeneratorType.Heightmap;
            terrain.FindPropertyRelative("surfaceLevel").floatValue = Lowest + Range * 0.5f;
            terrain.FindPropertyRelative("amplitude").floatValue = Range * 0.5f;
            terrain.FindPropertyRelative("heightmapRaw").objectReferenceValue = AssetDatabase.LoadAssetAtPath<TextAsset>(HeightmapPath);
            terrain.FindPropertyRelative("rawIs16Bit").boolValue = true;
            terrain.FindPropertyRelative("heightmapUnitsPerPixel").floatValue = 1f;
            terrain.FindPropertyRelative("trees.chance").floatValue = 0f;
            terrain.FindPropertyRelative("trees.fieldChance").floatValue = 0f;
            terrain.FindPropertyRelative("startArea.enabled").boolValue = false;
            terrain.FindPropertyRelative("layers.rockChance").floatValue = 0f;
            terrain.FindPropertyRelative("water.enabled").boolValue = false;
            config.ApplyModifiedPropertiesWithoutUndo();
            // Assigning the heightmap resets the level, amplitude and scale (OnValidate): set them again.
            config.Update();
            terrain.FindPropertyRelative("surfaceLevel").floatValue = Lowest + Range * 0.5f;
            terrain.FindPropertyRelative("amplitude").floatValue = Range * 0.5f;
            terrain.FindPropertyRelative("heightmapUnitsPerPixel").floatValue = 1f;
            config.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();

            if (!File.Exists(ScenePath))
            {
                AssetDatabase.CopyAsset(WorldLabPath, ScenePath);
            }
            var scene = EditorSceneManager.OpenScene(ScenePath);
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == "Town" || root.name == "Travellers" || root.name == "Merchant Site")
                {
                    Object.DestroyImmediate(root);
                    continue;
                }
                WorldView world = root.GetComponent<WorldView>();
                if (world != null)
                {
                    var view = new SerializedObject(world);
                    view.FindProperty("config").objectReferenceValue = AssetDatabase.LoadAssetAtPath<WorldConfig>(ConfigPath);
                    view.ApplyModifiedPropertiesWithoutUndo();
                }
                MiningCorpTutorial tutorial = root.GetComponent<MiningCorpTutorial>();
                if (tutorial != null)
                {
                    var fields = new SerializedObject(tutorial);
                    SetItems(fields.FindProperty("supplies"), "Torch", "Copper ingot", "Coffee", "Oak boards");
                    SetItems(fields.FindProperty("valuables"), "Gold ingot", "Silver ingot");
                    fields.ApplyModifiedPropertiesWithoutUndo();
                }
                if (root.GetComponent<PlayerController>() != null)
                {
                    if (root.GetComponent<PlayerName>() == null)
                    {
                        root.AddComponent<PlayerName>();
                    }
                    // All the land is the company's (GL516): no parcels, so nothing says "you don't own this land".
                    foreach (MonoBehaviour parcels in new MonoBehaviour[] { root.GetComponent<ParcelBorders>(), root.GetComponent<PlayerParcels>() })
                    {
                        if (parcels != null)
                        {
                            Object.DestroyImmediate(parcels);
                        }
                    }
                }
            }
            EditorSceneManager.SaveScene(scene);
            AddToBuild();
        }

        [MenuItem("Clube/Tutorial/Take the old tutorial out of WorldLab")]
        public static void CleanWorldLab()
        {
            var scene = EditorSceneManager.OpenScene(WorldLabPath);
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.GetComponent<MiningCorpTutorial>() != null)
                {
                    Object.DestroyImmediate(root);
                    continue;
                }
                if (root.GetComponent<PlayerController>() != null && root.GetComponent<PlayerName>() == null)
                {
                    root.AddComponent<PlayerName>();
                }
            }
            EditorSceneManager.SaveScene(scene);
        }

        private static void SetItems(SerializedProperty list, params string[] names)
        {
            list.arraySize = names.Length;
            for (int i = 0; i < names.Length; i++)
            {
                list.GetArrayElementAtIndex(i).objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<ItemDefinition>($"Assets/Config/Items/{names[i]}.asset");
            }
        }

        // The tutorial scene goes right after the main menu.
        private static void AddToBuild()
        {
            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (scenes.Exists(s => s.path == ScenePath))
            {
                return;
            }
            scenes.Insert(Mathf.Min(1, scenes.Count), new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
