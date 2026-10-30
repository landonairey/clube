using System;
using System.IO;
using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// The one save slot (GL205, first pass of S4): a file in the player's data folder
    /// (<see cref="Application.persistentDataPath"/>, on Windows under AppData\LocalLow), written by Save and
    /// Exit and read by the main menu's Load.
    /// </summary>
    public static class SaveSlot
    {
        private const string FileName = "Saves/world.clubesave";

        public static string Path => System.IO.Path.Combine(Application.persistentDataPath, FileName);

        public static bool Exists => File.Exists(Path);

        /// <summary>The save's header (mode, seed, when), or null when there's no readable save.</summary>
        public static SaveHeader ReadHeader()
        {
            return Exists ? GameSaveFile.ReadHeader(Path) : null;
        }

        /// <summary>The whole save; null, with the reason in <paramref name="problem"/>, when it can't be read.</summary>
        public static GameSave Read(out string problem)
        {
            problem = null;
            try
            {
                return GameSaveFile.Read(Path);
            }
            catch (Exception exception) when (exception is IOException || exception is InvalidDataException || exception is UnauthorizedAccessException)
            {
                problem = exception.Message;
                UnityEngine.Debug.LogWarning($"Couldn't read the save at {Path}: {exception}");
                return null;
            }
        }

        /// <summary>Writes the save; false, with the reason in <paramref name="problem"/>, when it couldn't be written.</summary>
        public static bool Write(GameSave save, out string problem)
        {
            problem = null;
            try
            {
                GameSaveFile.Write(Path, save);
                return true;
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                problem = exception.Message;
                UnityEngine.Debug.LogWarning($"Couldn't write the save to {Path}: {exception}");
                return false;
            }
        }
    }
}
