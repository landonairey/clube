using System.Collections.Generic;
using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// A thin white outline round whatever the player can use under the crosshair, within reach (GL368): the merchant's
    /// table, a chest, a sleeping roll (their meshes), and the things built of voxels: a stump or flat rock, a furnace or
    /// campfire, a boulder. A voxel thing's outline is a mesh of just its samples, built like the terrain's
    /// (<see cref="ChunkMeshBuilder"/>), kept while it stays aimed at and rebuilt now and then in case it was mined. Drawn
    /// with the Clube/Interact Outline shader. On the Player.
    /// </summary>
    public class InteractHighlight : MonoBehaviour
    {
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

        [Tooltip("The Clube/Interact Outline material.")]
        [SerializeField]
        private Material outline;

        private StumpWorktops stumps;
        private object shownFor;
        private float builtAt;
        private Mesh voxelMesh;
        private Vector3 voxelOrigin;
        private readonly List<Vector3Int> samples = new List<Vector3Int>();
        private readonly HashSet<Vector3Int> seen = new HashSet<Vector3Int>();
        private readonly Stack<Vector3Int> pending = new Stack<Vector3Int>();
        private readonly List<MeshFilter> filters = new List<MeshFilter>();

        private void LateUpdate()
        {
            if (outline == null || worldView == null || !worldView.IsReady || interactor == null)
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
                stumps ??= worldView.GetComponentInChildren<StumpWorktops>();
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
            component.GetComponentsInChildren(false, filters);
            foreach (MeshFilter filter in filters)
            {
                // Not the placement preview child that some prefabs carry.
                if (filter.sharedMesh == null || filter.name.Contains("preview") || !filter.TryGetComponent(out MeshRenderer meshRenderer) || !meshRenderer.enabled)
                {
                    continue;
                }
                for (int submesh = 0; submesh < filter.sharedMesh.subMeshCount; submesh++)
                {
                    Graphics.RenderMesh(new RenderParams(outline), filter.sharedMesh, submesh, filter.transform.localToWorldMatrix);
                }
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
            voxelMesh = builder.Mesh;
            voxelOrigin = world.Grid.SampleToWorld(first);
        }

        private void Render()
        {
            if (voxelMesh == null)
            {
                return;
            }
            Matrix4x4 matrix = worldView.transform.localToWorldMatrix * Matrix4x4.Translate(voxelOrigin);
            Graphics.RenderMesh(new RenderParams(outline), voxelMesh, 0, matrix);
        }

        private void OnDestroy()
        {
            if (voxelMesh != null)
            {
                Destroy(voxelMesh);
            }
        }
    }
}
