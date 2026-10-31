using System;
using System.Collections.Generic;
using System.IO;
using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>One saved game in the saves folder: its file and its header.</summary>
    public sealed class SaveEntry
    {
        public SaveEntry(string path, SaveHeader header)
        {
            Path = path;
            Header = header;
        }

        public string Path { get; }

        public SaveHeader Header { get; }

        /// <summary>The save's name; for a save from before names (version 1), its file's name.</summary>
        public string Name => !string.IsNullOrEmpty(Header.SaveName) ? Header.SaveName : System.IO.Path.GetFileNameWithoutExtension(Path);
    }

    /// <summary>
    /// The saved games (GL205, GL207, GL208; S4): one file per save in the player's data folder
    /// (<see cref="Application.persistentDataPath"/>, on Windows under AppData\LocalLow), named after the save
    /// (<see cref="SaveNames.FileNameFor"/>). Save and Exit writes one; the main menu's load page lists them.
    /// </summary>
    public static class SaveSlots
    {
        public static string Folder => Path.Combine(Application.persistentDataPath, "Saves");

        /// <summary>The file a save of this name is kept in.</summary>
        public static string PathFor(string saveName)
        {
            return Path.Combine(Folder, SaveNames.FileNameFor(saveName));
        }

        /// <summary>True when a save of this name exists, so saving under it replaces it.</summary>
        public static bool Exists(string saveName)
        {
            return File.Exists(PathFor(saveName));
        }

        /// <summary>Every readable save, newest first.</summary>
        public static List<SaveEntry> List()
        {
            var saves = new List<SaveEntry>();
            if (!Directory.Exists(Folder))
            {
                return saves;
            }
            foreach (string path in Directory.GetFiles(Folder, "*" + SaveNames.Extension))
            {
                SaveHeader header = GameSaveFile.ReadHeader(path);
                if (header != null)
                {
                    saves.Add(new SaveEntry(path, header));
                }
            }
            saves.Sort((a, b) => b.Header.SavedAt.CompareTo(a.Header.SavedAt));
            return saves;
        }

        /// <summary>A whole save; null, with the reason in <paramref name="problem"/>, when it can't be read.</summary>
        public static GameSave Read(string path, out string problem)
        {
            problem = null;
            try
            {
                return GameSaveFile.Read(path);
            }
            catch (Exception exception) when (exception is IOException || exception is InvalidDataException || exception is UnauthorizedAccessException)
            {
                problem = exception.Message;
                UnityEngine.Debug.LogWarning($"Couldn't read the save at {path}: {exception}");
                return null;
            }
        }

        /// <summary>
        /// Writes a save under its name (<see cref="SaveHeader.SaveName"/>), replacing one of the same name; false,
        /// with the reason in <paramref name="problem"/>, when it couldn't be written.
        /// </summary>
        public static bool Write(GameSave save, out string problem)
        {
            problem = null;
            string path = PathFor(save.Header.SaveName);
            try
            {
                GameSaveFile.Write(path, save);
                return true;
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                problem = exception.Message;
                UnityEngine.Debug.LogWarning($"Couldn't write the save to {path}: {exception}");
                return false;
            }
        }

        /// <summary>
        /// Deletes a save's file (GL230); only files in the saves folder. False, with the reason in
        /// <paramref name="problem"/>, when it couldn't be deleted.
        /// </summary>
        public static bool Delete(string path, out string problem)
        {
            problem = null;
            try
            {
                string full = Path.GetFullPath(path);
                if (!string.Equals(Path.GetDirectoryName(full), Path.GetFullPath(Folder), StringComparison.OrdinalIgnoreCase))
                {
                    problem = "Not in the saves folder.";
                    return false;
                }
                File.Delete(full);
                return true;
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                problem = exception.Message;
                UnityEngine.Debug.LogWarning($"Couldn't delete the save {path}: {exception}");
                return false;
            }
        }
    }
}
