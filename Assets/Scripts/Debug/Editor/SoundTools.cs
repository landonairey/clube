using System.Collections.Generic;
using System.IO;
using Clube.Core;
using Clube.Game;
using UnityEditor;
using UnityEngine;

namespace Clube.Debug.Editor
{
    /// <summary>
    /// *Clube → Audio → Generate sound effects* (GL271, GL272): writes the game's own sound effects with
    /// <see cref="SoundSynth"/> into <c>Assets/Audio/Generated/</c> (footsteps, tool hits, breaking and gathering, bird
    /// calls, wind and leaf loops), then gives them to the player's sounds (<c>Assets/Config/Audio/PlayerSounds.asset</c>:
    /// footsteps, a hit sound for every tool by its kind, breaks and pick-ups) and to every <see cref="AmbientSounds"/> in
    /// the open scene, and turns sound effects on. The crafting, selling and building sounds keep their clips. Run it again
    /// after changing the synthesis; the clips are rewritten in place.
    /// </summary>
    public static class SoundTools
    {
        public const string Folder = "Assets/Audio/Generated";
        private const string PlayerSoundsPath = "Assets/Config/Audio/PlayerSounds.asset";
        private const int Variants = 4;

        [MenuItem("Clube/Audio/Generate sound effects")]
        public static void Generate()
        {
            Directory.CreateDirectory(Folder);
            var written = new Dictionary<string, List<string>>();
            void Write(string set, int variant, float[] samples)
            {
                string path = $"{Folder}/{set} {variant + 1}.wav";
                File.WriteAllBytes(path, SoundSynth.ToWav(samples));
                if (!written.TryGetValue(set, out List<string> paths))
                {
                    paths = new List<string>();
                    written.Add(set, paths);
                }
                paths.Add(path);
            }

            foreach (StepSurface surface in System.Enum.GetValues(typeof(StepSurface)))
            {
                for (int v = 0; v < Variants; v++)
                {
                    Write($"Step {surface}", v, SoundSynth.Footstep(surface, 100 + (int)surface * 10 + v));
                }
            }
            foreach (HitSound kind in System.Enum.GetValues(typeof(HitSound)))
            {
                for (int v = 0; v < 3; v++)
                {
                    Write($"Hit {kind}", v, SoundSynth.Hit(kind, 200 + (int)kind * 10 + v));
                }
            }
            for (int v = 0; v < 3; v++)
            {
                Write("Crumble", v, SoundSynth.Crumble(300 + v));
                Write("Thud", v, SoundSynth.Thud(310 + v));
                Write("Rustle", v, SoundSynth.Rustle(320 + v));
            }
            for (int v = 0; v < 2; v++)
            {
                Write("Pickup", v, SoundSynth.Pickup(330 + v));
            }
            for (int v = 0; v < 8; v++)
            {
                Write("Bird", v, SoundSynth.Chirp(400 + v));
            }
            Write("Wind loop", 0, SoundSynth.WindLoop(14f, 500));
            Write("Leaves loop", 0, SoundSynth.LeavesLoop(9f, 510));
            AssetDatabase.Refresh();

            AudioClip[] Clips(string set)
            {
                var clips = new List<AudioClip>();
                foreach (string path in written[set])
                {
                    clips.Add(AssetDatabase.LoadAssetAtPath<AudioClip>(path));
                }
                return clips.ToArray();
            }

            var sounds = AssetDatabase.LoadAssetAtPath<PlayerSounds>(PlayerSoundsPath);
            if (sounds != null)
            {
                var so = new SerializedObject(sounds);
                so.FindProperty("effectsOn").boolValue = true;
                SetClips(so, "stoneSteps", Clips("Step Stone"));
                SetClips(so, "dirtSteps", Clips("Step Dirt"));
                SetClips(so, "grassSteps", Clips("Step Grass"));
                SetClips(so, "claySteps", Clips("Step Clay"));
                SetClips(so, "otherToolHits", Clips("Hit Knock"));
                SetClips(so, "chiselHits", Clips("Hit Tap"));
                SetClips(so, "soilHits", Clips("Hit Scrape"));
                SetClips(so, "leafHits", Clips("Rustle"));
                SetClips(so, "rockBreaks", Clips("Crumble"));
                SetClips(so, "soilBreaks", Clips("Thud"));
                SetClips(so, "plantBreaks", Clips("Rustle"));
                SetClips(so, "gathered", Clips("Pickup"));

                // A hit sound for every tool, by its kind.
                var tools = new List<ToolDefinition>();
                foreach (string guid in AssetDatabase.FindAssets("t:ToolDefinition", new[] { "Assets/Config/Items" }))
                {
                    tools.Add(AssetDatabase.LoadAssetAtPath<ToolDefinition>(AssetDatabase.GUIDToAssetPath(guid)));
                }
                SerializedProperty hits = so.FindProperty("toolHits");
                hits.arraySize = tools.Count;
                for (int i = 0; i < tools.Count; i++)
                {
                    SerializedProperty entry = hits.GetArrayElementAtIndex(i);
                    entry.FindPropertyRelative("tool").objectReferenceValue = tools[i];
                    AudioClip[] clips = Clips($"Hit {HitOf(tools[i])}");
                    SetClips(entry.FindPropertyRelative("sound"), clips);
                }
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(sounds);
            }

            foreach (AmbientSounds ambient in Object.FindObjectsByType<AmbientSounds>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var so = new SerializedObject(ambient);
                so.FindProperty("sounds").objectReferenceValue = sounds;
                so.FindProperty("windLoop").objectReferenceValue = Clips("Wind loop")[0];
                so.FindProperty("leavesLoop").objectReferenceValue = Clips("Leaves loop")[0];
                AudioClip[] calls = Clips("Bird");
                SerializedProperty birds = so.FindProperty("birdCalls");
                birds.arraySize = calls.Length;
                for (int i = 0; i < calls.Length; i++)
                {
                    birds.GetArrayElementAtIndex(i).objectReferenceValue = calls[i];
                }
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(ambient);
            }
            AssetDatabase.SaveAssets();
            UnityEngine.Debug.Log($"Sound effects: {written.Count} sets written to {Folder}.");
        }

        /// <summary>The hit sound a tool makes, by its kind and what its head is made of.</summary>
        public static HitSound HitOf(ToolDefinition tool)
        {
            string name = tool.DisplayName.ToLowerInvariant();
            switch (HeldToolMeshes.ShapeOf(tool))
            {
                case HeldShape.Hand:
                    return HitSound.Slap;
                case HeldShape.Hatchet:
                case HeldShape.HandAxe:
                    return HitSound.Chop;
                case HeldShape.Shovel:
                    return HitSound.Scrape;
                case HeldShape.Chisel:
                    return HitSound.Tap;
                default:
                    return name.Contains("stone") ? HitSound.Knock : HitSound.Clink;
            }
        }

        private static void SetClips(SerializedObject so, string set, AudioClip[] clips)
        {
            SetClips(so.FindProperty(set), clips);
        }

        private static void SetClips(SerializedProperty set, AudioClip[] clips)
        {
            SerializedProperty array = set.FindPropertyRelative("clips");
            array.arraySize = clips.Length;
            for (int i = 0; i < clips.Length; i++)
            {
                array.GetArrayElementAtIndex(i).objectReferenceValue = clips[i];
            }
        }
    }
}
