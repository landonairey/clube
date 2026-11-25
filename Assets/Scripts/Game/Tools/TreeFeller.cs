using System.Collections.Generic;
using Clube.Core;
using Unity.Profiling;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// Felling (GL344): after every chop on a tree, asks <see cref="TreeFelling"/> whether a limb came off. If one did, it's
    /// meshed exactly as the terrain draws it (<see cref="LimbMeshJob"/>: the world's mesh job on just its samples, with the
    /// world's settings and materials), taken out of the voxels, and handed to a <see cref="FallingLimb"/> that tips it over
    /// to the player's right, snapped to the nearest of x and z so it lands on the grid, and puts it back as voxels when it
    /// has landed.
    /// </summary>
    /// <remarks>
    /// Felling a big crown is a lot of work (90,000 samples to search, mesh and take out), once a single frame's spike of
    /// up to two seconds (2026-11-24b backlog, item 9). Each step now runs a few milliseconds a frame or in a job: the search
    /// (<see cref="SeveredSearch"/>), the mesh, and the removal (<see cref="LimbRemoval"/>, under the standing mesh).
    /// </remarks>
    public class TreeFeller : MonoBehaviour
    {
        [SerializeField]
        private WorldView worldView;

        [SerializeField]
        private PlayerToolUser toolUser;

        [Tooltip("The view the fall direction is taken from (the player's camera); the main camera when empty.")]
        [SerializeField]
        private Transform view;

        [Tooltip("Material for the dust plume where a limb lands (a soft particle material).")]
        [SerializeField]
        private Material dustMaterial;

        [Tooltip("Colour of the dust plume.")]
        [SerializeField]
        private Color dustColour = new Color(0.62f, 0.55f, 0.45f, 0.75f);

        [Tooltip("Material for the leaves and splinters a landing throws up (GL469): the Clube/Held Item material, tinted per piece.")]
        [SerializeField]
        private Material burstMaterial;

        [Tooltip("Where landed leaves go to decay (GL350); on World.")]
        [SerializeField]
        private FallenLeaves fallenLeaves;

        [Tooltip("The skills whose Woodcutting level caps the wood a felled piece keeps (GL468); found on this object when empty.")]
        [SerializeField]
        private PlayerSkills skills;

        [Tooltip("Milliseconds a frame may spend finding what a chop cut off and taking it out of the voxels; the rest goes on next frame, so felling a big crown never stalls one.")]
        [SerializeField, Min(0.5f)]
        private float frameMilliseconds = 3f;

        private TreeMaterials trees;
        private World treesFor;

        // Where felled wood landed (GL349): wood cut off again from these just drops, with no second fall.
        private readonly HashSet<Vector3Int> landedWood = new HashSet<Vector3Int>();

        // Chops waiting to be looked at, oldest first; only the first is worked on.
        private readonly Queue<Felling> fellings = new Queue<Felling>();

        // The last limb's voxels being taken out: the next search waits for it, or it would find the rest of that limb
        // hanging and fell it twice.
        private LimbRemoval lastRemoval;

        // Felling's main-thread time in the profiler (and to ProfilerRecorder), per frame.
        private static readonly ProfilerMarker WorkMarker = new ProfilerMarker("TreeFeller.Work");

        /// <summary>Raised when a limb comes off and starts to fall.</summary>
        public event System.Action<FelledLimb> Felled;

        /// <summary>Most wood, in volume, a felled piece keeps at the player's Woodcutting level (GL468).</summary>
        public float WoodCap => FellingYield.WoodCap(skills != null ? skills.Skills.Level(SkillType.Woodcutting) : 0);

        /// <summary>Chops still being looked at (a big crown takes a few frames).</summary>
        public int Pending => fellings.Count;

        // One chop on its way: searched a slice a frame, then meshed in a job, then falling.
        private sealed class Felling
        {
            public SeveredSearch Search;
            public LimbMeshJob Mesh;
            public Vector3Int Fall;
            public float WoodCap;
            public System.Action<List<Vector3Int>> Landed;
        }

        private void Awake()
        {
            if (skills == null)
            {
                skills = GetComponent<PlayerSkills>();
            }
        }

        private void OnEnable()
        {
            if (toolUser != null)
            {
                toolUser.Chopped += OnChopped;
            }
        }

        private void OnDisable()
        {
            if (toolUser != null)
            {
                toolUser.Chopped -= OnChopped;
            }
            // Nothing is left half done: whatever was cut off still falls.
            FinishAll();
        }

        private void OnChopped(VoxelBox box)
        {
            TryFell(box);
        }

        /// <summary>
        /// Looks at what a chop on <paramref name="box"/> cut off; whatever did comes off and falls towards
        /// <paramref name="fall"/> (a unit step along x or z), or to the right of where the player looks now. True when the
        /// chop was taken: the search runs a few milliseconds a frame (<see cref="SeveredSearch"/>), so a limb starts to fall
        /// a frame or more later, and a big crown later still.
        /// </summary>
        /// <remarks>
        /// GL672: <paramref name="woodCap"/> overrides the player's Woodcutting cap (a settlement's lumberjack fells at the
        /// highest level: no wood lost); <paramref name="landed"/> is told where the piece's wood came to rest.
        /// </remarks>
        public bool TryFell(VoxelBox box, Vector3Int? fall = null, float? woodCap = null, System.Action<List<Vector3Int>> landed = null)
        {
            if (worldView == null || !worldView.IsReady)
            {
                return false;
            }
            World world = worldView.World;
            if (trees == null || treesFor != world)
            {
                trees = TreeMaterials.From(worldView.Config.Terrain.Trees);
                treesFor = world;
                landedWood.Clear();
            }
            fellings.Enqueue(new Felling
            {
                Search = new SeveredSearch(world, box.MinSample, box.MaxSample, trees, worldView.Config.IsoLevel),
                Fall = fall ?? FallDirection(),
                WoodCap = woodCap ?? WoodCap,
                Landed = landed,
            });
            if (!isActiveAndEnabled)
            {
                // No frames to spread it over.
                FinishAll();
            }
            else
            {
                Work(frameMilliseconds);
            }
            return true;
        }

        private void Update()
        {
            Work(frameMilliseconds);
        }

        // Works on the waiting chops for about the given time.
        private void Work(double milliseconds)
        {
            using ProfilerMarker.AutoScope scope = WorkMarker.Auto();
            var watch = System.Diagnostics.Stopwatch.StartNew();
            while (fellings.Count > 0)
            {
                double left = milliseconds - watch.Elapsed.TotalMilliseconds;
                if (left <= 0.0 || !Advance(fellings.Peek(), left, false))
                {
                    return;
                }
                fellings.Dequeue();
            }
        }

        // Every waiting chop, to the end, now.
        private void FinishAll()
        {
            while (fellings.Count > 0)
            {
                Advance(fellings.Peek(), double.PositiveInfinity, true);
                fellings.Dequeue();
            }
            if (worldView != null && worldView.World == treesFor)
            {
                lastRemoval?.Finish();
            }
        }

        // Takes a chop further; true once it's done with (it fell, dropped, or cut nothing off).
        private bool Advance(Felling felling, double milliseconds, bool now)
        {
            if (worldView == null || worldView.World != treesFor)
            {
                felling.Mesh?.Cancel();
                return true;
            }
            if (felling.Mesh == null)
            {
                if (lastRemoval != null && !lastRemoval.IsDone)
                {
                    if (!now)
                    {
                        return false;
                    }
                    lastRemoval.Finish();
                }
                if (!felling.Search.Step(milliseconds))
                {
                    return false;
                }
                FelledLimb limb = felling.Search.Limb;
                if (limb == null)
                {
                    return true;
                }
                if (HasFallen(limb))
                {
                    Drop(felling, limb);
                    return true;
                }
                felling.Mesh = new LimbMeshJob(limb, worldView.World.Grid, worldView.Config.MeshSettings);
            }
            if (!now && !felling.Mesh.IsCompleted)
            {
                return false;
            }
            StartFall(felling, felling.Search.Limb, felling.Mesh.Complete(), felling.Mesh.Origin, now);
            return true;
        }

        // Already lying down (on an old leaf pile, say): it just drops onto the ground, as it is (GL349). It was capped when it
        // fell, so it isn't again (GL468).
        private void Drop(Felling felling, FelledLimb limb)
        {
            World world = worldView.World;
            float iso = worldView.Config.IsoLevel;
            TreeFelling.Remove(world, limb);
            var wood = new List<Vector3Int>();
            var leaves = new List<Vector3Int>();
            TreeFelling.Land(world, limb, Vector3Int.zero, trees, iso, leafPacking: 1f, landedWood: wood, landedLeaves: leaves);
            OnLanded(wood, leaves);
            felling.Landed?.Invoke(wood);
        }

        // Meshed whole, so the whole piece falls; wood past the cap is gone once it lands (GL468). Its voxels are taken out
        // a slice a frame while the mesh stands in for them; it tips once they're gone.
        private void StartFall(Felling felling, FelledLimb limb, Mesh mesh, Vector3 origin, bool now)
        {
            lastRemoval = new LimbRemoval(worldView.World, limb);
            if (now)
            {
                lastRemoval.Finish();
            }
            FellingYield.CapWood(limb, felling.WoodCap);
            var falling = new GameObject("Falling limb").AddComponent<FallingLimb>();
            falling.transform.SetParent(worldView.transform, false);
            System.Action<List<Vector3Int>> landed = felling.Landed;
            falling.Begin(
                worldView, limb, trees, mesh, origin, Materials(), felling.Fall, dustMaterial, dustColour, burstMaterial,
                (wood, leaves) =>
                {
                    OnLanded(wood, leaves);
                    landed?.Invoke(wood);
                },
                lastRemoval, frameMilliseconds);
            Felled?.Invoke(limb);
        }

        // Most of the limb's wood landed from an earlier fall.
        private bool HasFallen(FelledLimb limb)
        {
            int fallen = 0;
            int wood = 0;
            foreach (FelledSample sample in limb.Solid)
            {
                if (!sample.IsWood)
                {
                    continue;
                }
                wood++;
                fallen += landedWood.Contains(sample.Sample) ? 1 : 0;
            }
            return wood > 0 && fallen * 2 >= wood;
        }

        private void OnLanded(List<Vector3Int> wood, List<Vector3Int> leaves)
        {
            landedWood.UnionWith(wood);
            if (fallenLeaves != null)
            {
                fallenLeaves.Add(leaves);
            }
        }
        // The materials the world's chunks are drawn with, so the limb looks the same as the tree it came off.
        private Material[] Materials()
        {
            return worldView.ChunkMaterials;
        }

        // To the right of where the player looks, snapped to the nearest of +x, -x, +z, -z, in the world's own frame.
        private Vector3Int FallDirection()
        {
            Transform eye = view != null ? view : (Camera.main != null ? Camera.main.transform : transform);
            Vector3 right = worldView.transform.InverseTransformDirection(eye.right);
            if (Mathf.Abs(right.x) >= Mathf.Abs(right.z))
            {
                return new Vector3Int(right.x >= 0f ? 1 : -1, 0, 0);
            }
            return new Vector3Int(0, 0, right.z >= 0f ? 1 : -1);
        }
    }
}
