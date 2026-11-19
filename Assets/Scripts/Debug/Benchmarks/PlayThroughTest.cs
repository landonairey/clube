using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Clube.Core;
using Clube.Game;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;

namespace Clube.Debug
{
    /// <summary>
    /// Plays the built game from the menu to a reloaded save and back (GL772), to show that a player build works from start
    /// to finish: run the exe with <c>-playthrough</c>. It presses real keys through the input system, and writes
    /// <c>playthrough.txt</c> and a few pictures beside the exe, with every check and every error the game logged.
    /// </summary>
    public sealed class PlayThroughTest : MonoBehaviour
    {
        private const string Flag = "-playthrough";
        private const string SaveName = "zz playthrough test";
        private const int Seed = 1;
        private const float LoadTimeout = 420f;
        private const int MostErrorsListed = 30;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void StartIfAsked()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), Flag) >= 0 && FindAnyObjectByType<PlayThroughTest>() == null)
            {
                var go = new GameObject("Play-through test");
                DontDestroyOnLoad(go);
                go.AddComponent<PlayThroughTest>();
            }
        }

        private readonly StringBuilder report = new StringBuilder();
        private readonly List<string> errors = new List<string>();
        private readonly HashSet<string> errorsSeen = new HashSet<string>();
        private int errorCount;
        private int checks;
        private int failures;
        private Vector3 savedPosition;
        private float savedCoins;
        private ItemDefinition savedItem;
        private int savedItemCount;
        private Vector3Int dugSample;

        private string Folder => Path.GetDirectoryName(Application.dataPath);

        private IEnumerator Start()
        {
            Application.logMessageReceived += OnLog;
            Application.runInBackground = true;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            if (Keyboard.current == null)
            {
                InputSystem.AddDevice<Keyboard>();
            }
            report.AppendLine($"Play-through, version {BuildInfo.Version}, {(Application.isEditor ? "Editor" : "player")}, scripting backend {ScriptingBackend()}");

            yield return Step("Main menu", TheMenu());
            yield return Step("Tutorial start", TheTutorial());
            yield return Step("New game, arriving by cart", NewGame());
            yield return Step("Walking and jumping", Walking());
            yield return Step("Screens", Screens());
            yield return Step("Items and digging", ItemsAndDigging());
            yield return Step("Time passing", TimePassing());
            yield return Step("Saving", Saving());
            yield return Step("Loading the save", LoadingTheSave());
            yield return Step("Back to the menu", BackToTheMenu());

            report.AppendLine($"{errorCount} errors logged" + (errors.Count > 0 ? ":" : "."));
            foreach (string error in errors)
            {
                report.AppendLine("  " + error);
            }
            report.Insert(0, $"RESULT: {(failures == 0 && errorCount == 0 ? "PASS" : "FAIL")}, {checks - failures} of {checks} checks passed, {errorCount} errors logged\n");
            File.WriteAllText(Path.Combine(Folder, "playthrough.txt"), report.ToString());
            Application.logMessageReceived -= OnLog;
            GameScenes.Quit();
        }

        private static string ScriptingBackend()
        {
#if ENABLE_IL2CPP
            return "IL2CPP";
#else
            return "Mono";
#endif
        }

        private void OnLog(string message, string stackTrace, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert)
            {
                return;
            }
            errorCount++;
            string firstLine = message.Split('\n')[0];
            if (errors.Count < MostErrorsListed && errorsSeen.Add(firstLine))
            {
                string where = stackTrace.Split('\n')[0];
                errors.Add($"{type}: {firstLine}" + (where.Length > 0 ? $" ({where})" : ""));
            }
        }

        // Runs one part of the test; an exception in it fails the part, and the next part still runs.
        private IEnumerator Step(string name, IEnumerator body)
        {
            int errorsBefore = errorCount;
            float began = Time.realtimeSinceStartup;
            report.AppendLine($"== {name}");
            while (true)
            {
                object next;
                try
                {
                    if (!body.MoveNext())
                    {
                        break;
                    }
                    next = body.Current;
                }
                catch (Exception e)
                {
                    Check(false, $"stopped by {e.GetType().Name}: {e.Message}");
                    break;
                }
                yield return next;
            }
            report.AppendLine($"   {Time.realtimeSinceStartup - began:0} s, {errorCount - errorsBefore} errors logged");
            File.WriteAllText(Path.Combine(Folder, "playthrough.txt"), report.ToString());
        }

        private void Check(bool passed, string what)
        {
            checks++;
            failures += passed ? 0 : 1;
            report.AppendLine($"   {(passed ? "ok  " : "FAIL")} {what}");
        }

        private static IEnumerator Seconds(float seconds)
        {
            yield return new WaitForSecondsRealtime(seconds);
        }

        private static IEnumerator Press(Key key)
        {
            InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(key));
            yield return null;
            yield return null;
            InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
            yield return null;
            yield return null;
        }

        private IEnumerator Picture(string name)
        {
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(Folder, $"playthrough-{name}.png"));
            yield return null;
            yield return null;
        }

        // Waits until a new map session is ready and the player stands on it.
        private IEnumerator TheGameLoaded(MapSession before)
        {
            float began = Time.realtimeSinceStartup;
            bool ready = false;
            while (!ready && Time.realtimeSinceStartup - began < LoadTimeout)
            {
                yield return Seconds(0.25f);
                MapSession session = MapSession.Current;
                PlayerSpawn spawn = FindAnyObjectByType<PlayerSpawn>();
                ready = session != null && !ReferenceEquals(session, before) && session.IsReady && spawn != null && spawn.IsPlaced;
            }
            Check(ready, ready ? $"the map opened after {Time.realtimeSinceStartup - began:0} s" : "the map never opened");
        }

        private IEnumerator TheMenu()
        {
            yield return Seconds(2f);
            Check(SceneManager.GetActiveScene().name == "MainMenu", $"the game opens on the menu (scene {SceneManager.GetActiveScene().name})");
            Check(FindAnyObjectByType<MainMenu>() != null, "the menu is running");
            yield return Picture("menu");
        }

        private IEnumerator TheTutorial()
        {
            GameSession.StartNew(GameMode.Demo, Seed, "Playthrough", true);
            GameScenes.LoadGame();
            yield return TheGameLoaded(null);
            yield return Seconds(15f);
            Check(GameSession.Tutorial, "the tutorial is under way");
            PlayerController player = FindAnyObjectByType<PlayerController>();
            Check(player != null && player.IsInMenu, "the crew lead asks for the character's name");
            yield return Picture("tutorial");
            GameScenes.LoadMainMenu();
            yield return TheMenuAgain();
        }

        private IEnumerator TheMenuAgain()
        {
            float began = Time.realtimeSinceStartup;
            while (SceneManager.GetActiveScene().name != "MainMenu" && Time.realtimeSinceStartup - began < 60f)
            {
                yield return null;
            }
            Check(SceneManager.GetActiveScene().name == "MainMenu", "back on the menu");
            yield return Seconds(2f);
        }

        private IEnumerator NewGame()
        {
            GameSession.StartNew(GameMode.Demo, Seed, "Playthrough");
            GameScenes.LoadGame();
            yield return TheGameLoaded(null);
            float began = Time.realtimeSinceStartup;
            yield return Seconds(2f);
            while (ArrivalCutscene.IsPlaying && Time.realtimeSinceStartup - began < 180f)
            {
                yield return Seconds(0.5f);
            }
            Check(!ArrivalCutscene.IsPlaying, $"the arrival by cart finished after {Time.realtimeSinceStartup - began:0} s");
            yield return Seconds(5f);
            yield return Picture("arrived");
        }

        private IEnumerator Walking()
        {
            PlayerController player = FindAnyObjectByType<PlayerController>();
            Check(player != null, "the player is there");
            Vector3 from = player.transform.position;
            float began = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - began < 6f)
            {
                InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(Key.W));
                yield return null;
            }
            InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
            yield return Seconds(0.5f);
            float walked = Vector3.Distance(from, player.transform.position);
            Check(walked > 2f, $"W walks the player forward ({walked:0.0} m in 6 s)");
            float ground = player.transform.position.y;
            InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(Key.Space));
            float highest = ground;
            for (int i = 0; i < 40; i++)
            {
                yield return null;
                highest = Mathf.Max(highest, player.transform.position.y);
            }
            InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
            yield return Seconds(1f);
            Check(highest > ground + 0.2f, $"Space jumps ({highest - ground:0.00} m)");
        }

        private IEnumerator Screens()
        {
            WorldMapScreen map = FindAnyObjectByType<WorldMapScreen>();
            FieldLogScreen log = FindAnyObjectByType<FieldLogScreen>();
            InventoryScreen inventory = FindAnyObjectByType<InventoryScreen>();
            PauseMenu pause = FindAnyObjectByType<PauseMenu>();

            yield return Press(Key.M);
            yield return Seconds(1f);
            Check(map != null && map.IsOpen, "M opens the map");
            yield return Picture("map");
            yield return Press(Key.M);
            yield return Seconds(0.5f);
            Check(map != null && !map.IsOpen, "M closes the map");

            yield return Press(Key.Q);
            yield return Seconds(0.7f);
            Check(log != null && log.IsOpen, "Q opens the field log");
            yield return Picture("field-log");
            yield return Press(Key.Q);
            yield return Seconds(0.5f);
            Check(log != null && !log.IsOpen, "Q closes the field log");

            yield return Press(Key.E);
            yield return Seconds(0.7f);
            Check(inventory != null && inventory.IsOpen, "E opens the inventory");
            yield return Picture("inventory");
            yield return Press(Key.E);
            yield return Seconds(0.5f);
            Check(inventory != null && !inventory.IsOpen, "E closes the inventory");

            yield return Press(Key.Escape);
            yield return Seconds(0.7f);
            Check(pause != null && pause.IsOpen, "Esc opens the pause menu");
            yield return Picture("pause");
            yield return Press(Key.Escape);
            yield return Seconds(0.5f);
            Check(pause != null && !pause.IsOpen, "Esc closes the pause menu");
        }

        private IEnumerator ItemsAndDigging()
        {
            PlayerInventory inventory = FindAnyObjectByType<PlayerInventory>();
            GameSessionHost host = FindAnyObjectByType<GameSessionHost>();
            savedItem = host.Catalog.Items[0];
            int before = inventory.Inventory.Count(savedItem);
            inventory.GiveOrDrop(savedItem, 1);
            savedItemCount = inventory.Inventory.Count(savedItem);
            Check(savedItemCount == before + 1, $"an item goes into the inventory ({savedItem.name})");

            WorldView view = MapSession.Current.WorldView;
            Vector3 local = view.transform.InverseTransformPoint(inventory.transform.position + inventory.transform.forward * 3f);
            float ground = MapSession.Current.Map.Height(local.x, local.z);
            dugSample = view.World.Grid.WorldToNearestSample(new Vector3(local.x, ground - 0.5f, local.z));
            view.World.SetDensity(dugSample, 0f);
            yield return Seconds(1f);
            Check((view.World.GetDensity(dugSample) ?? 1f) < 0.5f, "the ground can be dug");
        }

        private IEnumerator TimePassing()
        {
            Time.timeScale = 12f;
            yield return Seconds(15f);
            Time.timeScale = 1f;
            yield return Seconds(2f);
            Check(FindAnyObjectByType<PlayerController>() != null, "three game minutes pass at twelve times the speed");
            yield return Picture("later");
        }

        private IEnumerator Saving()
        {
            GameSessionHost host = FindAnyObjectByType<GameSessionHost>();
            savedPosition = FindAnyObjectByType<PlayerController>().transform.position;
            savedCoins = FindAnyObjectByType<PlayerWallet>().Wallet.Coins;
            bool saved = host.Save(SaveName, out string problem);
            Check(saved, saved ? "the game saves" : $"the game did not save: {problem}");
            Check(SaveSlots.Exists(SaveName), "the save is on disk");
            yield return Seconds(1f);
        }

        private IEnumerator LoadingTheSave()
        {
            GameSave save = SaveSlots.Read(SaveSlots.PathFor(SaveName), out string problem);
            Check(save != null, save != null ? "the save reads back" : $"the save did not read back: {problem}");
            if (save == null)
            {
                yield break;
            }
            MapSession before = MapSession.Current;
            GameSession.StartLoaded(save);
            GameScenes.LoadGame();
            yield return TheGameLoaded(before);
            yield return Seconds(8f);
            Vector3 position = FindAnyObjectByType<PlayerController>().transform.position;
            float moved = Vector3.Distance(savedPosition, position);
            Check(moved < 5f, $"the player is back where the game was saved ({moved:0.0} m off: saved at {savedPosition}, now at {position})");
            float coins = FindAnyObjectByType<PlayerWallet>().Wallet.Coins;
            Check(Mathf.Abs(coins - savedCoins) < 0.01f, $"the coins came back ({coins:0} of {savedCoins:0})");
            int count = FindAnyObjectByType<PlayerInventory>().Inventory.Count(savedItem);
            Check(count == savedItemCount, $"the items came back ({count} of {savedItemCount})");
            World world = MapSession.Current.WorldView.World;
            float began = Time.realtimeSinceStartup;
            while (!world.GetDensity(dugSample).HasValue && Time.realtimeSinceStartup - began < 60f)
            {
                yield return Seconds(0.5f);
            }
            Check((world.GetDensity(dugSample) ?? 1f) < 0.5f, "the dug ground stayed dug");
            yield return Picture("reloaded");
        }

        private IEnumerator BackToTheMenu()
        {
            GameScenes.LoadMainMenu();
            yield return TheMenuAgain();
            bool deleted = SaveSlots.Delete(SaveSlots.PathFor(SaveName), out string problem);
            Check(deleted, deleted ? "the test's save is deleted" : $"the test's save was not deleted: {problem}");
        }
    }
}
