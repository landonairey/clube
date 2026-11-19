using System;
using System.Collections;
using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// The bounded map a scene is playing (GL598, GL601): once the world has a <see cref="BoundedMapGenerator"/> it
    /// surveys the map (<see cref="ResourceSurvey"/>) and plans its settlements, roads and water routes
    /// (<see cref="WorldPlan"/>), once per generated map, and sets the player down by the capital
    /// (<see cref="MapStart"/>). The world map, the ground markers and later the settlements read the map from here.
    /// GL762: the survey and plan are seconds of main-thread work, run a slice a frame so the load screen keeps drawing.
    /// </summary>
    public class MapSession : MonoBehaviour, IColumnHold
    {
        // Fewest trees rooted on the starting parcel (GL745).
        private const int StartParcelTrees = 3;

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

        // Milliseconds of each frame the survey and plan take, behind the load screen (GL762).
        private const float BuildMilliseconds = 100f;

        private BoundedMapGenerator built;
        private int waitedFrames;
        private SlicedWork building;
        private BoundedMapGenerator buildingFor;

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

        /// <summary>How far the survey and plan have got, 0 to 1.</summary>
        public float BuildProgress => building != null ? building.Progress : 0f;

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
            if (worldView != null)
            {
                worldView.AddHold(this);
            }
        }

        private void OnDisable()
        {
            if (Current == this)
            {
                Current = null;
            }
            if (worldView != null)
            {
                worldView.RemoveHold(this);
            }
            StopBuilding();
        }

        /// <summary>
        /// GL653: until a bounded map is surveyed and planned nobody knows where its roads and settlements go, so none of
        /// its ground is shown; planned, <see cref="MapRoads"/> and <see cref="MapSettlements"/> hold what they will write.
        /// </summary>
        public bool Holds(Vector2Int column)
        {
            return worldView != null && worldView.Generator is BoundedMapGenerator && !IsReady;
        }

        /// <summary>Surveys and plans again on the next frame, even though the map is the same.</summary>
        public void Rebuild()
        {
            built = null;
            StopBuilding();
        }

        private void StopBuilding()
        {
            building?.Dispose();
            building = null;
            buildingFor = null;
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
                StopBuilding();
                return;
            }
            if (generator == built)
            {
                return;
            }
            if (buildingFor != null && buildingFor != generator)
            {
                // The world was regenerated under the survey: it starts again on the new map.
                StopBuilding();
            }
            if (!IsBuilding)
            {
                // A frame for the message to show before the work starts.
                IsBuilding = true;
                waitedFrames = 0;
                return;
            }
            if (waitedFrames++ < 1)
            {
                return;
            }
            if (building == null)
            {
                building = new SlicedWork(BuildSteps(generator));
                buildingFor = generator;
            }
            if (building.Run(BuildMilliseconds))
            {
                UnityEngine.Debug.Log($"Map session: longest step of the survey and plan {building.LongestStepMilliseconds:0} ms", this);
                StopBuilding();
            }
        }

        private IEnumerator BuildSteps(BoundedMapGenerator generator)
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            TerrainSettings terrain = Terrain;
            ResourceSurvey survey = null;
            yield return new SlicedWork.Part(
                ResourceSurvey.BuildSteps(terrain, generator, worldView.Ores, worldView.Trees, surveyCellSize, done => survey = done), 0f, SurveyShare);
            // GL634: the road classes' slope limits are shares of the slope the player can walk, the controller's own limit.
            var rules = new WorldPlanRules();
            CharacterController walker = player != null ? player.GetComponentInChildren<CharacterController>(true) : null;
            if (walker == null)
            {
                walker = FindAnyObjectByType<CharacterController>(FindObjectsInactive.Include);
            }
            if (walker != null)
            {
                rules.SetWalkableSlope(walker.slopeLimit);
            }
            WorldPlan plan = WorldPlanner.Build(survey, terrain.Seed, rules);
            // The plan is from the survey's 40 m cells: its roads are put on the real ground, and signposts on them (GL631, GL632).
            yield return new SlicedWork.Part(WorldRoads.FinishSteps(plan, generator), SurveyShare, 1f);
            Survey = survey;
            Plan = plan;
            Map = generator;
            built = generator;
            IsBuilding = false;
            LastBuildSeconds = (float)watch.Elapsed.TotalSeconds;
            Version++;

            Vector2 at = MapStart.Pick(Plan, Survey);
            // GL744: the player starts on a parcel that can be theirs, beside the home settlement rather than on its locked land.
            PlayerParcels parcels = player != null ? player.GetComponent<PlayerParcels>() : null;
            if (parcels != null)
            {
                MapLayout layout = generator.Layout;
                System.Func<float, float, bool> wet = SettlementWriter.WetOf(generator.Water);
                at = MapStart.FamilyParcel(Plan, at, parcels.SizeInChunks * worldView.World.Grid.ChunkWorldSize.x,
                    (x, z) => layout.WalksFreely(x, z) && (wet == null || !wet(x, z)), generator.Height);
            }
            StartPosition = worldView.transform.TransformPoint(new Vector3(at.x, generator.Height(at.x, at.y), at.y));
            PlantStartParcel(at);
            PlacePlayer();
            BindLimit();
            UnityEngine.Debug.Log(
                $"Map session: seed {terrain.Seed}, {Plan.Settlements.Count} settlements, {Plan.Roads.Count} roads, {Plan.WaterRoutes.Count} water routes; "
                + $"survey and plan {LastBuildSeconds:0.0} s ({Survey.Timings}; {Plan.Timings}); start {StartPosition}", this);
            Rebuilt?.Invoke();
        }

        // The share of the build the survey is, for the load screen's progress: the roads take the rest.
        private const float SurveyShare = 0.35f;

        // GL745: the starting parcel has a couple of trees, whatever the seed gave it.
        private void PlantStartParcel(Vector2 at)
        {
            PlayerParcels parcels = player != null ? player.GetComponent<PlayerParcels>() : null;
            if (parcels == null || worldView.Trees == null)
            {
                return;
            }
            float size = parcels.SizeInChunks * worldView.World.Grid.ChunkWorldSize.x;
            var corner = new Vector2(Mathf.Floor(at.x / size) * size, Mathf.Floor(at.y / size) * size);
            worldView.Trees.EnsureTrees(new Rect(corner, new Vector2(size, size)), StartParcelTrees);
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
                // GL787: a loaded game's saved position wins over the map's start.
                player.SetDownAtStart(StartPosition);
            }
            else if (!player.PlacedFromSave)
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

    }
}
