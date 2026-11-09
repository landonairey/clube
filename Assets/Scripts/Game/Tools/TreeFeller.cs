using System.Collections.Generic;
using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// Felling (GL344): after every chop on a tree, asks <see cref="TreeFelling"/> whether a limb came off. If one did, it's
    /// meshed exactly as the terrain draws it (a scratch chunk of just its samples, built by <see cref="ChunkMeshBuilder"/>
    /// with the world's settings and materials), taken out of the voxels, and handed to a <see cref="FallingLimb"/> that
    /// tips it over to the player's right, snapped to the nearest of x and z so it lands on the grid, and puts it back as
    /// voxels when it has landed.
    /// </summary>
    /// <remarks>
    /// Meshing a limb takes a few milliseconds (a crown is about 32³ samples), so it's built when the cut goes through rather
    /// than ahead while chopping.
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

        [Tooltip("Where landed leaves go to decay (GL350); on World.")]
        [SerializeField]
        private FallenLeaves fallenLeaves;

        [Tooltip("The skills whose Woodcutting level caps the wood a felled piece keeps (GL468); found on this object when empty.")]
        [SerializeField]
        private PlayerSkills skills;

        private TreeMaterials trees;
        private World treesFor;

        // Where felled wood landed (GL349): wood cut off again from these just drops, with no second fall.
        private readonly HashSet<Vector3Int> landedWood = new HashSet<Vector3Int>();

        /// <summary>Raised when a limb comes off and starts to fall.</summary>
        public event System.Action<FelledLimb> Felled;

        /// <summary>Most wood, in volume, a felled piece keeps at the player's Woodcutting level (GL468).</summary>
        public float WoodCap => FellingYield.WoodCap(skills != null ? skills.Skills.Level(SkillType.Woodcutting) : 0);

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
        }

        private void OnChopped(VoxelBox box)
        {
            TryFell(box);
        }

        /// <summary>Fells whatever a chop on <paramref name="box"/> cut off, if anything; true when a limb started to fall.</summary>
        public bool TryFell(VoxelBox box)
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
            float iso = worldView.Config.IsoLevel;
            FelledLimb limb = TreeFelling.FindSevered(world, box.MinSample, box.MaxSample, trees, iso);
            if (limb == null)
            {
                return false;
            }
            TreeFelling.Remove(world, limb);
            if (HasFallen(limb))
            {
                // Already lying down (on an old leaf pile, say): it just drops onto the ground, as it is (GL349). It was
                // capped when it fell, so it isn't again (GL468).
                var wood = new List<Vector3Int>();
                var leaves = new List<Vector3Int>();
                TreeFelling.Land(world, limb, Vector3Int.zero, trees, iso, leafPacking: 1f, landedWood: wood, landedLeaves: leaves);
                OnLanded(wood, leaves);
                return true;
            }
            // Meshed whole, so the whole piece falls; wood past the player's cap is gone once it lands (GL468).
            Mesh mesh = BuildMesh(limb, out Vector3 origin);
            FellingYield.CapWood(limb, WoodCap);
            var falling = new GameObject("Falling limb").AddComponent<FallingLimb>();
            falling.transform.SetParent(worldView.transform, false);
            falling.Begin(worldView, limb, trees, mesh, origin, Materials(), FallDirection(), dustMaterial, dustColour, OnLanded);
            Felled?.Invoke(limb);
            return true;
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

        // The limb's samples in a scratch chunk one sample bigger than it all round (empty there, so the surface closes),
        // meshed like the world's chunks. Returns the mesh and the world position (in the world view's space) of its corner.
        private Mesh BuildMesh(FelledLimb limb, out Vector3 origin)
        {
            WorldGrid grid = worldView.World.Grid;
            Vector3Int first = limb.Min - Vector3Int.one;
            Vector3Int voxels = limb.Max - limb.Min + Vector3Int.one * 2;
            var chunk = new Chunk(voxels);
            foreach (FelledSample sample in limb.Solid)
            {
                chunk.SetDensity(sample.Sample - first, sample.Density);
                chunk.SetMaterial(sample.Sample - first, sample.Material);
            }
            foreach (FelledSample sample in limb.Fringe)
            {
                chunk.SetDensity(sample.Sample - first, sample.Density);
                chunk.SetMaterial(sample.Sample - first, sample.Material);
            }
            var builder = new ChunkMeshBuilder("Falling limb");
            builder.Build(chunk, worldView.Config.MeshSettings);
            origin = grid.SampleToWorld(first);
            return builder.Mesh;
        }

        // The materials the world's chunks are drawn with, so the limb looks the same as the tree it came off.
        private Material[] Materials()
        {
            foreach (ChunkRenderer chunkRenderer in worldView.Renderers.Values)
            {
                if (chunkRenderer != null && chunkRenderer.Renderer != null)
                {
                    return chunkRenderer.Renderer.sharedMaterials;
                }
            }
            return new Material[0];
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
