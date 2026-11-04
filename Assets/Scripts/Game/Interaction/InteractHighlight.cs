using System.Collections.Generic;
using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// A pixel-thin white outline round whatever the player can use under the crosshair, within reach (GL368, GL372): the
    /// merchant's table, a chest, a sleeping roll (their meshes), and the things built of voxels: a stump or flat rock, a
    /// furnace or campfire, a boulder. A voxel thing's outline is a mesh of just its samples, built like the terrain's
    /// (<see cref="ChunkMeshBuilder"/>), kept while it stays aimed at and rebuilt now and then in case it was mined. A model
    /// with a child named <see cref="OutlineShapeName"/> is outlined by that mesh instead of its own (the merchant table's
    /// shape fills the gaps between its posts, so only its outer boundary is drawn). Drawn with the Clube/Interact Outline
    /// shader: a stencil mask of every mesh first, then the rim outside it. On the Player.
    /// </summary>
    public class InteractHighlight : MonoBehaviour
    {
        /// <summary>The child whose mesh, when a model has one, is outlined in place of the model's own meshes.</summary>
        public const string OutlineShapeName = "Outline shape";

        // Largest connected piece of boulder material that counts as a boulder: a mountain's stone isn't outlined.
        private const int MaxBoulderSamples = 6000;

        // Seconds between rebuilds of a voxel outline that stays aimed at.
        private const float RebuildSeconds = 1f;

        private static readonly Vector3Int[] Around =
        {
            Vector3Int.up, Vector3Int.down, Vector3Int.right, Vector3Int.left, new Vector3Int(0, 0, 1), new Vector3Int(0, 0, -1),
        };

        [SerializeField]
        private PlayerInteractor interactor;

        [SerializeField]
        private WorldView worldView;

        [SerializeField]
        private BoulderBashing bashing;

        [Tooltip("The Clube/Interact Outline material (the rim); the stencil mask is made from it.")]
        [SerializeField]
        private Material outline;

        private Material rim;
        private Material mask;
        private StumpWorktops stumps;
        private object shownFor;
        private float builtAt;
        private Mesh voxelMesh;
        private Vector3 voxelOrigin;
        private readonly List<Vector3Int> samples = new List<Vector3Int>();
        private readonly HashSet<Vector3Int> seen = new HashSet<Vector3Int>();
        private readonly Stack<Vector3Int> pending = new Stack<Vector3Int>();
        private readonly List<MeshFilter> filters = new List<MeshFilter>();
        private readonly List<(Mesh mesh, Matrix4x4 matrix)> draws = new List<(Mesh, Matrix4x4)>();
        private readonly Dictionary<Mesh, Mesh> smoothed = new Dictionary<Mesh, Mesh>();

        private void Awake()
        {
            stumps = GetComponent<StumpWorktops>();
            if (outline == null)
            {
                return;
            }
            // Both drawn in the transparent queue, the mask first, so every mesh of the target is masked before any rim.
            rim = new Material(outline) { name = "Interact outline rim", renderQueue = 3051 };
            rim.SetFloat("_StencilComp", (float)UnityEngine.Rendering.CompareFunction.NotEqual);
            rim.SetFloat("_StencilPass", (float)UnityEngine.Rendering.StencilOp.Keep);
            rim.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.LessEqual);
            rim.SetFloat("_ColorMask", 15f);
            mask = new Material(outline) { name = "Interact outline mask", renderQueue = 3050 };
            mask.SetFloat("_WidthPixels", 0f);
            mask.SetFloat("_StencilComp", (float)UnityEngine.Rendering.CompareFunction.Always);
            mask.SetFloat("_StencilPass", (float)UnityEngine.Rendering.StencilOp.Replace);
            mask.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.Always);
            mask.SetFloat("_ColorMask", 0f);
        }

        private void LateUpdate()
        {
            if (rim == null || worldView == null || !worldView.IsReady || interactor == null)
            {
                return;
            }
            IInteractable target = interactor.Target;
            if (target is INoticeOnly)
            {
                target = null;
            }
            if (target is Component component)
            {
                if (component.TryGetComponent(out TerrainStructure structure) && structure.IsStanding && structure.Stamped.Count > 0)
                {
                    DrawVoxels(component, structure.Stamped);
                    return;
                }
                if (stumps != null && component.transform.parent == stumps.transform)
                {
                    DrawVoxels(component, stumps.LastPiece);
                    return;
                }
                DrawMeshes(component);
                return;
            }
            if (target == null && bashing != null && bashing.TryGetAimedBoulder(out Vector3Int boulder))
            {
                DrawBoulder(boulder);
                return;
            }
            shownFor = null;
        }

        private void DrawMeshes(Component component)
        {
            shownFor = component;
            draws.Clear();
            Transform shape = component.transform.Find(OutlineShapeName);
            if (shape != null && shape.TryGetComponent(out MeshFilter shapeFilter) && shapeFilter.sharedMesh != null)
            {
                draws.Add((Smoothed(shapeFilter.sharedMesh), shape.localToWorldMatrix));
            }
            else
            {
                component.GetComponentsInChildren(false, filters);
                foreach (MeshFilter filter in filters)
                {
                    // Not the placement preview child that some prefabs carry.
                    if (filter.sharedMesh == null || filter.name.Contains("preview") || !filter.TryGetComponent(out MeshRenderer meshRenderer) || !meshRenderer.enabled)
                    {
                        continue;
                    }
                    draws.Add((Smoothed(filter.sharedMesh), filter.transform.localToWorldMatrix));
                }
            }
            foreach ((Mesh mesh, Matrix4x4 matrix) in draws)
            {
                Graphics.RenderMesh(new RenderParams(mask), mesh, 0, matrix);
            }
            foreach ((Mesh mesh, Matrix4x4 matrix) in draws)
            {
                Graphics.RenderMesh(new RenderParams(rim), mesh, 0, matrix);
            }
        }

        // A boulder: the connected boulder material from the aimed sample, if it's small enough to be one.
        private void DrawBoulder(Vector3Int start)
        {
            if (shownFor is Vector3Int key && seen.Contains(start) && Time.time - builtAt < RebuildSeconds)
            {
                Render();
                return;
            }
            World world = worldView.World;
            float iso = worldView.Config.IsoLevel;
            byte material = world.GetMaterial(start) ?? 0;
            seen.Clear();
            pending.Clear();
            seen.Add(start);
            pending.Push(start);
            while (pending.Count > 0)
            {
                Vector3Int sample = pending.Pop();
                foreach (Vector3Int step in Around)
                {
                    Vector3Int next = sample + step;
                    if (seen.Contains(next) || (world.GetDensity(next) ?? 0f) < iso || (world.GetMaterial(next) ?? 0) != material)
                    {
                        continue;
                    }
                    seen.Add(next);
                    if (seen.Count > MaxBoulderSamples)
                    {
                        shownFor = null;
                        return;
                    }
                    pending.Push(next);
                }
            }
            Build(seen);
            shownFor = start;
            Render();
        }

        private void DrawVoxels(object key, IEnumerable<Vector3Int> pieceSamples)
        {
            if (!Equals(shownFor, key) || Time.time - builtAt >= RebuildSeconds || voxelMesh == null)
            {
                Build(pieceSamples);
                shownFor = key;
            }
            Render();
        }

        // A mesh of the samples and the thin ramp round them, in a scratch chunk a sample bigger all round.
        private void Build(IEnumerable<Vector3Int> pieceSamples)
        {
            builtAt = Time.time;
            World world = worldView.World;
            float iso = worldView.Config.IsoLevel;
            samples.Clear();
            Vector3Int min = Vector3Int.one * int.MaxValue;
            Vector3Int max = Vector3Int.one * int.MinValue;
            foreach (Vector3Int sample in pieceSamples)
            {
                if ((world.GetDensity(sample) ?? 0f) < iso)
                {
                    continue;
                }
                samples.Add(sample);
                min = Vector3Int.Min(min, sample);
                max = Vector3Int.Max(max, sample);
            }
            if (samples.Count == 0)
            {
                voxelMesh = null;
                return;
            }
            Vector3Int first = min - Vector3Int.one * 2;
            var chunk = new Chunk(max - min + Vector3Int.one * 4);
            foreach (Vector3Int sample in samples)
            {
                chunk.SetDensity(sample - first, world.GetDensity(sample) ?? 1f);
                chunk.SetMaterial(sample - first, world.GetMaterial(sample) ?? 0);
                foreach (Vector3Int step in Around)
                {
                    Vector3Int next = sample + step;
                    float density = world.GetDensity(next) ?? 0f;
                    if (density > 0f && density < iso)
                    {
                        chunk.SetDensity(next - first, density);
                    }
                }
            }
            var builder = new ChunkMeshBuilder("Interact outline");
            builder.Build(chunk, worldView.Config.MeshSettings);
            if (voxelMesh != null)
            {
                Destroy(voxelMesh);
            }
            voxelMesh = SmoothCopy(builder.Mesh);
            Destroy(builder.Mesh);
            voxelOrigin = world.Grid.SampleToWorld(first);
        }

        private void Render()
        {
            if (voxelMesh == null)
            {
                return;
            }
            Matrix4x4 matrix = worldView.transform.localToWorldMatrix * Matrix4x4.Translate(voxelOrigin);
            Graphics.RenderMesh(new RenderParams(mask), voxelMesh, 0, matrix);
            Graphics.RenderMesh(new RenderParams(rim), voxelMesh, 0, matrix);
        }

        // A copy of a mesh in one submesh, with each vertex's normal the average over every vertex at its place: a flat-shaded
        // box's corners then push out together, so the rim has no gaps at the corners.
        private Mesh Smoothed(Mesh source)
        {
            if (smoothed.TryGetValue(source, out Mesh copy) && copy != null)
            {
                return copy;
            }
            copy = SmoothCopy(source);
            smoothed[source] = copy;
            return copy;
        }

        private static Mesh SmoothCopy(Mesh source)
        {
            Vector3[] vertices = source.vertices;
            Vector3[] normals = source.normals;
            if (normals == null || normals.Length != vertices.Length)
            {
                source.RecalculateNormals();
                normals = source.normals;
            }
            var sums = new Dictionary<Vector3Int, Vector3>();
            var keys = new Vector3Int[vertices.Length];
            for (int i = 0; i < vertices.Length; i++)
            {
                keys[i] = Vector3Int.RoundToInt(vertices[i] * 10000f);
                sums.TryGetValue(keys[i], out Vector3 sum);
                sums[keys[i]] = sum + normals[i];
            }
            var smooth = new Vector3[vertices.Length];
            for (int i = 0; i < vertices.Length; i++)
            {
                smooth[i] = sums[keys[i]].normalized;
            }
            var indices = new List<int>();
            for (int submesh = 0; submesh < source.subMeshCount; submesh++)
            {
                indices.AddRange(source.GetTriangles(submesh));
            }
            var copy = new Mesh { name = source.name + " (outline)" };
            copy.indexFormat = vertices.Length > 65535 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            copy.vertices = vertices;
            copy.normals = smooth;
            copy.SetTriangles(indices, 0);
            copy.bounds = source.bounds;
            return copy;
        }

        private void OnDestroy()
        {
            if (voxelMesh != null)
            {
                Destroy(voxelMesh);
            }
            foreach (Mesh copy in smoothed.Values)
            {
                if (copy != null)
                {
                    Destroy(copy);
                }
            }
            if (rim != null)
            {
                Destroy(rim);
                Destroy(mask);
            }
        }
    }
}
