using System;
using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// The bounded map a scene is playing (GL598, GL601): once the world has a <see cref="BoundedMapGenerator"/> it
    /// surveys the map (<see cref="ResourceSurvey"/>) and plans its settlements, roads and water routes
    /// (<see cref="WorldPlan"/>), once per generated map, and sets the player down by the capital
    /// (<see cref="MapStart"/>). The world map, the ground markers and later the settlements read the map from here.
    /// The survey runs its height jobs from the main thread, so it stalls a few seconds behind a message.
    /// </summary>
    public class MapSession : MonoBehaviour
    {
        [SerializeField]
        private WorldView worldView;

        [Tooltip("Set down by the capital once the map is planned (and again after a regenerate).")]
        [SerializeField]
        private PlayerSpawn player;

        [Tooltip("The lab's fly camera, moved over the start as well (optional).")]
        [SerializeField]
        private Transform flyCamera;

        [Tooltip("Side of the survey's cells in metres.")]
        [SerializeField, Range(20f, 80f)]
        private float surveyCellSize = 40f;

        private BoundedMapGenerator built;
        private int waitedFrames;

        /// <summary>Raised when a map has been surveyed and planned (at start, and after each regenerate).</summary>
        public event Action Rebuilt;

        /// <summary>The scene's map session, if it has one.</summary>
        public static MapSession Current { get; private set; }

        public WorldView WorldView => worldView;

        /// <summary>The map's generator; null until the first map is ready.</summary>
        public BoundedMapGenerator Map { get; private set; }

        public MapLayout Layout => Map != null ? Map.Layout : null;
        public ResourceSurvey Survey { get; private set; }
        public WorldPlan Plan { get; private set; }

        /// <summary>True once <see cref="Survey"/> and <see cref="Plan"/> describe the world's current map.</summary>
        public bool IsReady => built != null && worldView != null && ReferenceEquals(worldView.Generator, built);

        /// <summary>True from the frame before the survey starts until it is done.</summary>
        public bool IsBuilding { get; private set; }

        /// <summary>Counts the maps built, so views know when to redraw.</summary>
        public int Version { get; private set; }

        /// <summary>Where the player starts (world space, on the ground).</summary>
        public Vector3 StartPosition { get; private set; }

        /// <summary>Seconds the last survey and plan took.</summary>
        public float LastBuildSeconds { get; private set; }

        /// <summary>The world's terrain settings (the Play mode copy).</summary>
        public TerrainSettings Terrain => worldView != null && worldView.Config != null ? worldView.Config.Terrain : null;

        private void OnEnable()
        {
            Current = this;
        }

        private void OnDisable()
        {
            if (Current == this)
            {
                Current = null;
            }
        }

        /// <summary>Surveys and plans again on the next frame, even though the map is the same.</summary>
        public void Rebuild()
        {
            built = null;
        }

        private void Update()
        {
            if (worldView == null || !worldView.IsReady)
            {
                return;
            }
            var generator = worldView.Generator as BoundedMapGenerator;
            if (generator == null)
            {
                Map = null;
                built = null;
                IsBuilding = false;
                return;
            }
            if (generator == built)
            {
                return;
            }
            if (!IsBuilding)
            {
                // A frame for the message to show before the stall.
                IsBuilding = true;
                waitedFrames = 0;
                return;
            }
            if (waitedFrames++ < 1)
            {
                return;
            }
            Build(generator);
        }

        private void Build(BoundedMapGenerator generator)
        {
            float start = Time.realtimeSinceStartup;
            TerrainSettings terrain = Terrain;
            Survey = ResourceSurvey.Build(terrain, generator, worldView.Ores, worldView.Trees, surveyCellSize);
            Plan = WorldPlanner.Build(Survey, terrain.Seed);
            Map = generator;
            built = generator;
            IsBuilding = false;
            LastBuildSeconds = Time.realtimeSinceStartup - start;
            Version++;

            Vector2 at = MapStart.Pick(Plan, Survey);
            StartPosition = worldView.transform.TransformPoint(new Vector3(at.x, generator.Height(at.x, at.y), at.y));
            PlacePlayer();
            BindLimit();
            UnityEngine.Debug.Log(
                $"Map session: seed {terrain.Seed}, {Plan.Settlements.Count} settlements, {Plan.Roads.Count} roads, {Plan.WaterRoutes.Count} water routes; "
                + $"survey and plan {LastBuildSeconds:0.0} s ({Survey.Timings}); start {StartPosition}", this);
            Rebuilt?.Invoke();
        }

        private void PlacePlayer()
        {
            if (flyCamera != null)
            {
                flyCamera.position = StartPosition + new Vector3(0f, 12f, 0f);
            }
            if (player == null)
            {
                return;
            }
            if (player.isActiveAndEnabled)
            {
                player.SetDownOver(StartPosition);
            }
            else
            {
                // Whatever enables the player spawns it at its own x and z.
                player.transform.position = StartPosition;
            }
        }

        // The player is held inside the map's edges (GL640); the limit rides on the player and reads this session.
        private void BindLimit()
        {
            if (player == null || player.GetComponent<PlayerController>() == null)
            {
                return;
            }
            if (!player.TryGetComponent(out MapBoundaryLimit limit))
            {
                limit = player.gameObject.AddComponent<MapBoundaryLimit>();
            }
            limit.Bind(this);
        }

        private void OnGUI()
        {
            if (!IsBuilding)
            {
                return;
            }
            const float width = 360f, height = 48f;
            var box = new Rect((Screen.width - width) * 0.5f, Screen.height * 0.4f, width, height);
            GUI.Box(box, GUIContent.none);
            var style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 18 };
            GUI.Label(box, "Surveying the map…", style);
        }
    }
}
