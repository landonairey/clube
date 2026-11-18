using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Clube.Core
{
    /// <summary>
    /// GL718: settlements seen from far away. Each settlement's buildings are drawn as low-poly stand-ins
    /// (<see cref="BuildingStandIns"/>), not voxels, in two levels: every building instanced (a walls box and a roof
    /// prism each), then, past <see cref="MergeFrom"/>, one merged mesh per settlement; the two cross-fade by the screen's
    /// dither. A building whose middle stands on a chunk column the world draws in full is dropped in the shader
    /// (<see cref="ChunkCoverageMask"/>): the voxel building is there. The game hands it each settlement's parts.
    /// </summary>
    public sealed class DistantBuildingView : MonoBehaviour
    {
        // Instances per draw call: the most RenderMeshInstanced takes from an array.
        private const int BatchSize = 1023;
        private const int Shapes = 3;

        private static readonly int CoverageMaskId = Shader.PropertyToID("_CoverageMask");
        private static readonly int CoverageId = Shader.PropertyToID("_Coverage");
        private static readonly int FadeInId = Shader.PropertyToID("_FadeIn");
        private static readonly int FadeOutId = Shader.PropertyToID("_FadeOut");

        [Tooltip("The world whose settlements these are. Defaults to the WorldView on this object.")]
        [SerializeField]
        private WorldView worldView;

        [Tooltip("The Clube/Distant Buildings material (GPU instancing on).")]
        [SerializeField]
        private Material material;

        [SerializeField]
        private bool show = true;

        [Tooltip("How far from the camera stand-ins are drawn, in metres.")]
        [SerializeField, Range(500f, 8000f)]
        private float range = 6000f;

        [Tooltip("Metres from the camera where each building's own stand-in gives way to its settlement's merged mesh.")]
        [SerializeField, Range(100f, 4000f)]
        private float mergeFrom = 900f;

        [Tooltip("Metres over which the two levels cross-fade, and the stand-ins fade out at the range's end.")]
        [SerializeField, Range(0f, 200f)]
        private float fadeWidth = 60f;

        [Tooltip("Board walls, stone walls and roofs (sRGB): the voxel buildings' materials, on average, seen from afar.")]
        [SerializeField]
        private Color walls = new Color(0.55f, 0.42f, 0.3f);

        [SerializeField]
        private Color stoneWalls = new Color(0.58f, 0.57f, 0.54f);

        [SerializeField]
        private Color roofs = new Color(0.42f, 0.26f, 0.2f);

        private sealed class Settlement
        {
            public Vector3 Origin;
            public Bounds Bounds;
            public Matrix4x4[][] Parts;
            public UnityEngine.Mesh Merged;
        }

        private readonly Dictionary<int, Settlement> settlements = new Dictionary<int, Settlement>();
        private readonly Matrix4x4[][] arrays = new Matrix4x4[Shapes][];
        private readonly int[] counts = new int[Shapes];
        private readonly Plane[] frustum = new Plane[6];
        private readonly List<Settlement> merged = new List<Settlement>();
        private MaterialPropertyBlock nearProperties;
        private MaterialPropertyBlock farProperties;
        private UnityEngine.Mesh[] meshes;
        private (Color, Color, Color) builtColours;
        private Material runtimeMaterial;
        private ChunkCoverageMask coverage;
        private int drawnFrame = -1;

        public bool Show
        {
            get => show;
            set => show = value;
        }

        public float Range
        {
            get => range;
            set => range = Mathf.Clamp(value, 500f, 8000f);
        }

        /// <summary>Metres from the camera where the per-building level gives way to the merged one.</summary>
        public float MergeFrom
        {
            get => mergeFrom;
            set => mergeFrom = Mathf.Clamp(value, 100f, 4000f);
        }

        /// <summary>Settlements handed in.</summary>
        public int SettlementCount => settlements.Count;

        /// <summary>Building parts drawn last frame one by one, and settlements drawn merged.</summary>
        public int DrawnParts { get; private set; }

        public int DrawnMerged { get; private set; }

        /// <summary>Main-thread time of the last frame's drawing.</summary>
        public float LastUpdateMilliseconds { get; private set; }

        /// <summary>Sets a settlement's stand-ins, in the world's own frame, replacing what it had.</summary>
        public void SetSettlement(int index, Vector3 origin, IReadOnlyList<StandInPart> parts)
        {
            RemoveSettlement(index);
            if (parts == null || parts.Count == 0)
            {
                return;
            }
            Matrix4x4 toScene = transform.localToWorldMatrix;
            var counted = new int[Shapes];
            foreach (StandInPart part in parts)
            {
                counted[(int)part.Shape]++;
            }
            var made = new Settlement { Origin = toScene.MultiplyPoint3x4(origin), Parts = new Matrix4x4[Shapes][] };
            for (int shape = 0; shape < Shapes; shape++)
            {
                made.Parts[shape] = new Matrix4x4[counted[shape]];
                counted[shape] = 0;
            }
            bool first = true;
            foreach (StandInPart part in parts)
            {
                Matrix4x4 placed = toScene * part.Matrix;
                made.Parts[(int)part.Shape][counted[(int)part.Shape]++] = placed;
                Vector3 at = placed.MultiplyPoint3x4(Vector3.zero);
                Vector3 size = placed.lossyScale;
                var box = new Bounds(at + Vector3.up * size.y * 0.5f, new Vector3(Mathf.Max(size.x, size.z), size.y, Mathf.Max(size.x, size.z)) * 1.5f);
                if (first)
                {
                    made.Bounds = box;
                    first = false;
                }
                else
                {
                    made.Bounds.Encapsulate(box);
                }
            }
            made.Merged = StandInMeshes.Merge(parts, origin, LinearColours());
            settlements[index] = made;
        }

        public void RemoveSettlement(int index)
        {
            if (settlements.TryGetValue(index, out Settlement old))
            {
                DestroyMesh(old.Merged);
                settlements.Remove(index);
            }
        }

        public void Clear()
        {
            foreach (Settlement settlement in settlements.Values)
            {
                DestroyMesh(settlement.Merged);
            }
            settlements.Clear();
        }

        private void Awake()
        {
            if (worldView == null)
            {
                worldView = GetComponent<WorldView>();
            }
        }

        private void OnDisable()
        {
            coverage?.Dispose();
            coverage = null;
        }

        private void OnDestroy()
        {
            coverage?.Dispose();
            coverage = null;
            Clear();
            DestroyMeshes();
            if (runtimeMaterial != null)
            {
                Destroy(runtimeMaterial);
            }
        }

        private void LateUpdate()
        {
            using FrameCost.Scope cost = FrameCost.Of("Distant buildings");
            // Once a frame: a script pumping LateUpdate by hand would queue draws whose GPU buffers only a real frame frees.
            if (!show || material == null || settlements.Count == 0 || worldView == null || !worldView.IsReady || Time.frameCount == drawnFrame)
            {
                return;
            }
            drawnFrame = Time.frameCount;
            long started = System.Diagnostics.Stopwatch.GetTimestamp();
            Camera view = Camera.main;
            if (view == null)
            {
                return;
            }
            EnsureMeshes();
            runtimeMaterial ??= new Material(material) { name = material.name + " (runtime)", enableInstancing = true };
            UpdateCoverage();
            Collect(view);
            Draw(view);
            LastUpdateMilliseconds = (System.Diagnostics.Stopwatch.GetTimestamp() - started) * 1000f / System.Diagnostics.Stopwatch.Frequency;
        }

        // Each settlement in view: its buildings into this frame's arrays while near, its merged mesh while far (both in the fade).
        private void Collect(Camera view)
        {
            GeometryUtility.CalculateFrustumPlanes(view, frustum);
            Vector3 eye = view.transform.position;
            float half = fadeWidth * 0.5f;
            System.Array.Clear(counts, 0, Shapes);
            merged.Clear();
            foreach (Settlement settlement in settlements.Values)
            {
                if (!GeometryUtility.TestPlanesAABB(frustum, settlement.Bounds))
                {
                    continue;
                }
                float near = Mathf.Sqrt(settlement.Bounds.SqrDistance(eye));
                float far = Vector3.Distance(settlement.Bounds.center, eye) + settlement.Bounds.extents.magnitude;
                if (near > range)
                {
                    continue;
                }
                if (near < mergeFrom + half)
                {
                    for (int shape = 0; shape < Shapes; shape++)
                    {
                        Append(shape, settlement.Parts[shape]);
                    }
                }
                if (far > mergeFrom - half)
                {
                    merged.Add(settlement);
                }
            }
        }

        private void Append(int shape, Matrix4x4[] parts)
        {
            int needed = counts[shape] + parts.Length;
            if (arrays[shape] == null || arrays[shape].Length < needed)
            {
                var grown = new Matrix4x4[Mathf.Max(needed, arrays[shape] == null ? 256 : arrays[shape].Length * 2)];
                if (arrays[shape] != null)
                {
                    System.Array.Copy(arrays[shape], grown, counts[shape]);
                }
                arrays[shape] = grown;
            }
            System.Array.Copy(parts, 0, arrays[shape], counts[shape], parts.Length);
            counts[shape] = needed;
        }

        private void Draw(Camera view)
        {
            float half = fadeWidth * 0.5f;
            nearProperties ??= new MaterialPropertyBlock();
            farProperties ??= new MaterialPropertyBlock();
            nearProperties.SetVector(FadeInId, new Vector4(-2f, -1f));
            nearProperties.SetVector(FadeOutId, new Vector4(mergeFrom - half, mergeFrom + half));
            farProperties.SetVector(FadeInId, new Vector4(mergeFrom - half, mergeFrom + half));
            farProperties.SetVector(FadeOutId, new Vector4(range - fadeWidth, range));
            var bounds = new Bounds(view.transform.position, Vector3.one * (range * 2.2f));
            DrawnParts = 0;
            for (int shape = 0; shape < Shapes; shape++)
            {
                int count = counts[shape];
                if (count == 0)
                {
                    continue;
                }
                var parameters = new RenderParams(runtimeMaterial)
                {
                    shadowCastingMode = ShadowCastingMode.Off, receiveShadows = false, worldBounds = bounds, matProps = nearProperties,
                };
                for (int start = 0; start < count; start += BatchSize)
                {
                    Graphics.RenderMeshInstanced(parameters, meshes[shape], 0, arrays[shape], Mathf.Min(BatchSize, count - start), start);
                }
                DrawnParts += count;
            }
            var far = new RenderParams(runtimeMaterial)
            {
                shadowCastingMode = ShadowCastingMode.Off, receiveShadows = false, matProps = farProperties,
            };
            foreach (Settlement settlement in merged)
            {
                far.worldBounds = settlement.Bounds;
                Graphics.RenderMesh(far, settlement.Merged, 0, Matrix4x4.Translate(settlement.Origin));
            }
            DrawnMerged = merged.Count;
        }

        private void UpdateCoverage()
        {
            coverage ??= new ChunkCoverageMask(worldView, WorldView.MaxRenderDistance);
            Vector3Int focusChunk = worldView.FocusChunk;
            coverage.Update(new Vector2Int(focusChunk.x, focusChunk.z));
            WorldConfig config = worldView.Config;
            float chunkWidth = config.ChunkSize.x * config.VoxelSize;
            Vector3 origin = worldView.transform.position;
            runtimeMaterial.SetTexture(CoverageMaskId, coverage.Texture);
            runtimeMaterial.SetVector(CoverageId, new Vector4(
                origin.x + coverage.Origin.x * chunkWidth, origin.z + coverage.Origin.y * chunkWidth, chunkWidth, coverage.Size));
        }

        private Color[] LinearColours()
        {
            return new[] { walls.linear, stoneWalls.linear, roofs.linear };
        }

        private void EnsureMeshes()
        {
            if (meshes != null && builtColours == (walls, stoneWalls, roofs))
            {
                return;
            }
            DestroyMeshes();
            Color[] colours = LinearColours();
            meshes = new UnityEngine.Mesh[Shapes];
            for (int shape = 0; shape < Shapes; shape++)
            {
                meshes[shape] = StandInMeshes.Unit((StandInShape)shape, colours[shape]);
            }
            builtColours = (walls, stoneWalls, roofs);
        }

        private void DestroyMeshes()
        {
            if (meshes == null)
            {
                return;
            }
            foreach (UnityEngine.Mesh mesh in meshes)
            {
                DestroyMesh(mesh);
            }
            meshes = null;
        }

        private static void DestroyMesh(UnityEngine.Mesh mesh)
        {
            if (mesh == null)
            {
                return;
            }
            if (Application.isPlaying)
            {
                Destroy(mesh);
            }
            else
            {
                DestroyImmediate(mesh);
            }
        }
    }
}
