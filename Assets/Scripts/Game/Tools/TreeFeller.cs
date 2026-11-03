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

        private TreeMaterials trees;
        private World treesFor;

        /// <summary>Raised when a limb comes off and starts to fall.</summary>
        public event System.Action<FelledLimb> Felled;

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
            }
            FelledLimb limb = TreeFelling.FindSevered(world, box.MinSample, box.MaxSample, trees, worldView.Config.IsoLevel);
            if (limb == null)
            {
                return false;
            }
            Mesh mesh = BuildMesh(limb, out Vector3 origin);
            TreeFelling.Remove(world, limb);
            var falling = new GameObject("Falling limb").AddComponent<FallingLimb>();
            falling.transform.SetParent(worldView.transform, false);
            falling.Begin(worldView, limb, trees, mesh, origin, Materials(), FallDirection(), dustMaterial, dustColour);
            Felled?.Invoke(limb);
            return true;
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
