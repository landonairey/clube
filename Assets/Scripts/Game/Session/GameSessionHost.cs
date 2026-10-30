using System;
using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// Runs the menu's game in the world scene (GL203, GL205): gives the world the session's seed before it
    /// starts, puts a loaded save back (the edited chunks before the world streams, then the player, the
    /// clock, drying clay and what was built, once the player is switched on), and gathers it all into a
    /// <see cref="GameSave"/> for Save and Exit. Played straight from the Editor (no session) it changes
    /// nothing, and saving still works.
    /// </summary>
    [RequireComponent(typeof(WorldView))]
    public class GameSessionHost : MonoBehaviour, IWorldConfigOverride
    {
        [Tooltip("Every item, to find the saved ones by id.")]
        [SerializeField]
        private ItemCatalog catalog;

        [Tooltip("The player, whose state is saved; may start switched off (the lab's camera toggle switches it on).")]
        [SerializeField]
        private PlayerSaveState player;

        [SerializeField]
        private GameClock clock;

        [SerializeField]
        private DroppedGroundDrying drying;

        private WorldView worldView;

        // A loaded save still to be put back on the player and the built objects.
        private GameSave restoring;

        /// <summary>The game mode saved with the game: the session's, or World when played from the Editor.</summary>
        public GameMode Mode => GameSession.IsActive ? GameSession.Mode : GameMode.World;

        public void Apply(WorldConfig playCopy)
        {
            if (GameSession.IsActive)
            {
                playCopy.Terrain.Seed = GameSession.Seed;
            }
        }

        private void Awake()
        {
            worldView = GetComponent<WorldView>();
        }

        private void Start()
        {
            GameSave save = GameSession.IsActive ? GameSession.Loaded : null;
            if (save == null || worldView.World == null)
            {
                return;
            }
            // Before the world streams its first chunk (WorldView.Update), so edited ones aren't generated.
            WorldSaving.Restore(worldView.World, save.Chunks);
            if (clock != null)
            {
                clock.Restore(save.Days);
            }
            if (drying != null && worldView.Config.Materials != null)
            {
                foreach (DryingSave sample in save.Drying)
                {
                    drying.Drying.Restore(sample.Sample, worldView.Config.Materials.Get(sample.Material), sample.ReadyAt);
                }
            }
            restoring = save;
            GameSession.ForgetLoaded();
        }

        private void Update()
        {
            // Stations find the player's skills, and "? ore" comes from the player's knowledge: wait for the player.
            if (restoring == null || (player != null && (!player.isActiveAndEnabled || !player.IsReady)))
            {
                return;
            }
            var items = new ItemLookup(catalog, player != null ? player.Ores : null);
            MaterialRegistry materials = worldView.Config.Materials;
            if (player != null && restoring.Player != null)
            {
                player.Restore(restoring.Player, items, materials);
            }
            PlacedObjectSaving.Restore(restoring.Placed, items, materials);
            restoring = null;
        }

        /// <summary>The world's name (GL206): the session's, or the default when played from the Editor.</summary>
        public string WorldName => GameSession.IsActive ? GameSession.WorldName : SaveNames.DefaultWorldName;

        /// <summary>The name a save gets when the player doesn't type one (GL207): world, mode and the time now.</summary>
        public string DefaultSaveName => SaveNames.DefaultSaveName(WorldName, Mode.ToString(), DateTime.Now);

        /// <summary>Everything a save keeps, as it is now, under a name.</summary>
        public GameSave Capture(string saveName)
        {
            var save = new GameSave
            {
                Header = new SaveHeader
                {
                    SaveName = saveName,
                    WorldName = WorldName,
                    Mode = Mode.ToString(),
                    Seed = worldView.Config.Terrain.Seed,
                    SavedAt = DateTime.UtcNow,
                    Build = BuildInfo.Version,
                },
                Days = clock != null ? clock.Days : 0d,
                Placed = PlacedObjectSaving.Capture(),
            };
            if (worldView.World != null)
            {
                save.Chunks = WorldSaving.Capture(worldView.World);
            }
            if (player != null && player.IsReady)
            {
                save.Player = player.Capture();
            }
            if (drying != null)
            {
                foreach ((Vector3Int sample, VoxelMaterial material, double readyAt) in drying.Drying.Waiting)
                {
                    save.Drying.Add(new DryingSave { Sample = sample, Material = material.Id, ReadyAt = readyAt });
                }
            }
            return save;
        }

        /// <summary>
        /// Writes the game as a save of this name, replacing one of the same name (GL207); an empty name takes
        /// <see cref="DefaultSaveName"/>. False, with the reason, when it couldn't.
        /// </summary>
        public bool Save(string saveName, out string problem)
        {
            if (restoring != null)
            {
                // Still putting a save back: saving now would lose the part not yet restored.
                problem = "The game is still loading.";
                return false;
            }
            string name = SaveNames.Clean(saveName);
            return SaveSlots.Write(Capture(name.Length > 0 ? name : DefaultSaveName), out problem);
        }
    }
}
