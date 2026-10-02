using System;
using System.Collections.Generic;
using Clube.Core;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Clube.Debug
{
    /// <summary>
    /// Draws a point in a recorded mesh build with real scene objects, so it shows
    /// in the Game view as well as the Scene view (V19): spheres on the density
    /// samples, on the current voxel's corners and edge vertices, a line per cube
    /// edge, an outline for the triangle being added, and a mesh of every triangle
    /// finished so far.
    /// </summary>
    /// <remarks>
    /// Only the current voxel gets corner and edge markers, so that set is reused
    /// for every voxel and chunk size. The density field gets one sphere per sample,
    /// which suits the voxel lab; Chapter 2's chunk-scale field (K17) will need a
    /// cheaper way to draw thousands of points. Everything lives under one root,
    /// parented to the chunk so it shares its transform.
    /// </remarks>
    public sealed class StepThroughVisuals : IDisposable
    {
        /// <summary>Corner sphere radius, as a fraction of the voxel size.</summary>
        public const float CornerRadius = 0.08f;

        /// <summary>Radius of a corner whose bit is being added to the case index.</summary>
        public const float HighlightedCornerRadius = 0.14f;

        private const float VertexRadius = 0.06f;
        private const string ColorProperty = "_BaseColor";

        public static readonly Color SolidColor = new Color(1f, 0.55f, 0.15f);
        public static readonly Color EmptyColor = new Color(0.35f, 0.65f, 1f);
        public static readonly Color CrossedEdgeColor = new Color(1f, 0.95f, 0.3f);

        private static readonly Color CubeEdgeColor = new Color(0.55f, 0.55f, 0.55f);
        private static readonly Color VertexColor = new Color(1f, 0.95f, 0.3f);
        private static readonly Color TriangleOutlineColor = new Color(0.3f, 1f, 0.85f);

        private readonly GameObject root;
        private readonly Material markerMaterial;
        private readonly List<MeshRenderer> sampleSpheres = new List<MeshRenderer>();
        private readonly MeshRenderer[] cornerSpheres = new MeshRenderer[MarchingCubes.CornerCount];
        private readonly MeshRenderer[] vertexSpheres = new MeshRenderer[MarchingCubes.EdgeCount];
        private readonly LineRenderer[] edgeLines = new LineRenderer[MarchingCubes.EdgeCount];
        private readonly LineRenderer triangleOutline;
        private readonly Mesh surface;
        private readonly MaterialPropertyBlock properties = new MaterialPropertyBlock();

        private readonly List<Vector3> surfaceVertices = new List<Vector3>();
        private readonly List<int> surfaceTriangles = new List<int>();

        // Recording index of each triangle step, and how many are in the surface mesh.
        private readonly List<int> triangleSteps = new List<int>();
        private int surfaceTriangleCount = -1;

        private MeshingRecorder recording;

        /// <param name="chunk">Transform the recorded chunk-local positions are relative to.</param>
        /// <param name="markerMaterial">Unlit material for spheres and lines, tinted per object.</param>
        /// <param name="surfaceMaterial">Material for the partial surface, normally the chunk's own.</param>
        public StepThroughVisuals(Transform chunk, Material markerMaterial, Material surfaceMaterial)
        {
            this.markerMaterial = markerMaterial;
            root = new GameObject("Step Through Visuals");
            root.transform.SetParent(chunk, false);

            for (int corner = 0; corner < cornerSpheres.Length; corner++)
            {
                cornerSpheres[corner] = CreateSphere($"Corner {corner}");
            }
            for (int edge = 0; edge < MarchingCubes.EdgeCount; edge++)
            {
                vertexSpheres[edge] = CreateSphere($"Vertex {edge}");
                edgeLines[edge] = CreateLine($"Edge {edge}", 2);
            }
            triangleOutline = CreateLine("Triangle Outline", 0);

            surface = new Mesh { name = "Step Through Surface" };
            surface.MarkDynamic();
            var surfaceObject = new GameObject("Surface So Far");
            surfaceObject.transform.SetParent(root.transform, false);
            surfaceObject.AddComponent<MeshFilter>().sharedMesh = surface;
            var surfaceRenderer = surfaceObject.AddComponent<MeshRenderer>();
            surfaceRenderer.sharedMaterial = surfaceMaterial;
        }

        public bool Visible
        {
            get => root.activeSelf;
            set => root.SetActive(value);
        }

        /// <summary>Uses a new recording; call after every re-record.</summary>
        public void Load(MeshingRecorder newRecording)
        {
            recording = newRecording;
            triangleSteps.Clear();
            for (int i = 0; i < recording.Steps.Count; i++)
            {
                if (recording.Steps[i].Type == MeshingStepType.Triangle)
                {
                    triangleSteps.Add(i);
                }
            }
            surfaceTriangleCount = -1;

            Vector3Int count = recording.SampleCount;
            int samples = count.x * count.y * count.z;
            while (sampleSpheres.Count < samples)
            {
                sampleSpheres.Add(CreateSphere($"Sample {sampleSpheres.Count}"));
            }
        }

        /// <summary>Shows the build as it stands <paramref name="progress"/> (0-1) of the way through step <paramref name="stepIndex"/>.</summary>
        public void Show(int stepIndex, float progress)
        {
            if (recording == null || recording.Steps.Count == 0)
            {
                Visible = false;
                return;
            }

            MeshingStep step = recording.Steps[stepIndex];
            ShowSurface(stepIndex, progress);

            bool isField = step.Type == MeshingStepType.DensityField;
            ShowSamples(isField ? progress : -1f);
            if (isField)
            {
                HideVoxelMarkers();
                return;
            }

            RecordedVoxel voxel = recording.Voxels[step.VoxelIndex];
            float size = recording.Settings.VoxelSize;
            ShowCorners(voxel, step.Type, progress, size);
            ShowEdges(voxel, step.Type, progress, size);
            ShowVertices(voxel, stepIndex, progress, size);
            ShowTriangleOutline(voxel, step, progress);
        }

        public void Dispose()
        {
            Object.Destroy(surface);
            Object.Destroy(root);
        }

        // The density field, samples appearing in storage order; hidden when progress < 0.
        private void ShowSamples(float progress)
        {
            Vector3Int count = recording.SampleCount;
            int total = count.x * count.y * count.z;
            int shown = progress < 0f ? 0 : StepReveal.Revealed(progress, total);
            float size = recording.Settings.VoxelSize;

            for (int i = 0; i < sampleSpheres.Count; i++)
            {
                if (i >= shown)
                {
                    sampleSpheres[i].gameObject.SetActive(false);
                    continue;
                }

                var sample = new Vector3Int(i % count.x, i / count.x % count.y, i / (count.x * count.y));
                float density = recording.GetDensity(sample);
                Place(sampleSpheres[i], (Vector3)sample * size, CornerRadius * size, Grey(density));
            }
        }

        private void HideVoxelMarkers()
        {
            foreach (MeshRenderer sphere in cornerSpheres)
            {
                sphere.gameObject.SetActive(false);
            }
            foreach (MeshRenderer sphere in vertexSpheres)
            {
                sphere.gameObject.SetActive(false);
            }
            foreach (LineRenderer line in edgeLines)
            {
                line.gameObject.SetActive(false);
            }
            triangleOutline.positionCount = 0;
        }

        // Every triangle whose step is complete: steps before this one, plus this one at the end.
        private void ShowSurface(int stepIndex, float progress)
        {
            int count = 0;
            while (count < triangleSteps.Count &&
                   (triangleSteps[count] < stepIndex || (triangleSteps[count] == stepIndex && progress >= 1f)))
            {
                count++;
            }

            if (count == surfaceTriangleCount)
            {
                return;
            }

            surfaceTriangleCount = count;
            surfaceVertices.Clear();
            surfaceTriangles.Clear();
            for (int i = 0; i < count; i++)
            {
                MeshingStep step = recording.Steps[triangleSteps[i]];
                IReadOnlyList<Vector3> edgeVertices = recording.Voxels[step.VoxelIndex].EdgeVertices;
                foreach (int edge in new[] { step.Triangle.A, step.Triangle.B, step.Triangle.C })
                {
                    surfaceTriangles.Add(surfaceVertices.Count);
                    surfaceVertices.Add(edgeVertices[edge]);
                }
            }

            surface.Clear();
            surface.SetVertices(surfaceVertices);
            surface.SetTriangles(surfaceTriangles, 0);
            surface.RecalculateNormals();
            surface.RecalculateBounds();
        }

        // Grey by density until sampled; then orange (solid) or blue (empty). While the
        // case index is built, solid corners grow as their bit is added.
        private void ShowCorners(RecordedVoxel voxel, MeshingStepType type, float progress, float size)
        {
            int sampled = StepReveal.CornersSampled(type, progress);
            for (int corner = 0; corner < cornerSpheres.Length; corner++)
            {
                Color color = corner < sampled
                    ? (MarchingCubes.IsCornerSolid(voxel.CaseIndex, corner) ? SolidColor : EmptyColor)
                    : Grey(voxel.CornerValues[corner]);

                float radius = StepReveal.IsBitHighlighted(voxel, corner, type, progress) ? HighlightedCornerRadius : CornerRadius;
                Place(cornerSpheres[corner], CornerPosition(voxel, corner, size), radius * size, color);
            }
        }

        // Faint cube outline; crossed edges highlighted from the edge table step on.
        private void ShowEdges(RecordedVoxel voxel, MeshingStepType type, float progress, float size)
        {
            for (int edge = 0; edge < MarchingCubes.EdgeCount; edge++)
            {
                bool lit = StepReveal.IsEdgeLit(voxel, edge, type, progress);

                LineRenderer line = edgeLines[edge];
                line.gameObject.SetActive(true);
                line.SetPosition(0, CornerPosition(voxel, MarchingCubesTables.EdgeCorners[edge, 0], size));
                line.SetPosition(1, CornerPosition(voxel, MarchingCubesTables.EdgeCorners[edge, 1], size));
                line.widthMultiplier = (lit ? 0.035f : 0.012f) * size;
                SetColor(line, lit ? CrossedEdgeColor : CubeEdgeColor);
            }
        }

        // Vertices placed by earlier interpolate steps sit at their crossing; the
        // current one slides there from the edge's solid corner.
        private void ShowVertices(RecordedVoxel voxel, int stepIndex, float progress, float size)
        {
            foreach (MeshRenderer sphere in vertexSpheres)
            {
                sphere.gameObject.SetActive(false);
            }

            int voxelIndex = recording.Steps[stepIndex].VoxelIndex;
            for (int i = stepIndex; i >= 0 && recording.Steps[i].VoxelIndex == voxelIndex; i--)
            {
                MeshingStep step = recording.Steps[i];
                if (step.Type != MeshingStepType.Interpolate)
                {
                    continue;
                }

                Vector3 target = voxel.EdgeVertices[step.Edge];
                Vector3 position = target;
                if (i == stepIndex && progress < 1f)
                {
                    Vector3 start = CornerPosition(voxel, SolidCornerOf(voxel, step.Edge), size);
                    position = Vector3.Lerp(start, target, Mathf.SmoothStep(0f, 1f, progress));
                }

                Place(vertexSpheres[step.Edge], position, VertexRadius * size, VertexColor);
            }
        }

        // The triangle being added, drawn edge by edge in winding order.
        private void ShowTriangleOutline(RecordedVoxel voxel, MeshingStep step, float progress)
        {
            if (step.Type != MeshingStepType.Triangle || progress >= 1f)
            {
                triangleOutline.positionCount = 0;
                return;
            }

            Vector3 a = voxel.EdgeVertices[step.Triangle.A];
            Vector3 b = voxel.EdgeVertices[step.Triangle.B];
            Vector3 c = voxel.EdgeVertices[step.Triangle.C];
            Vector3[] corners = { a, b, c, a };

            float drawn = progress * 3f;
            int whole = Mathf.Min((int)drawn, 2);
            triangleOutline.positionCount = whole + 2;
            for (int i = 0; i <= whole; i++)
            {
                triangleOutline.SetPosition(i, corners[i]);
            }
            triangleOutline.SetPosition(whole + 1, Vector3.Lerp(corners[whole], corners[whole + 1], drawn - whole));
            triangleOutline.widthMultiplier = 0.03f * recording.Settings.VoxelSize;
            SetColor(triangleOutline, TriangleOutlineColor);
        }

        private MeshRenderer CreateSphere(string name)
        {
            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = name;
            Object.Destroy(sphere.GetComponent<Collider>());
            sphere.transform.SetParent(root.transform, false);
            sphere.SetActive(false);

            var renderer = sphere.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = markerMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            return renderer;
        }

        private LineRenderer CreateLine(string name, int positionCount)
        {
            var lineObject = new GameObject(name);
            lineObject.transform.SetParent(root.transform, false);

            var line = lineObject.AddComponent<LineRenderer>();
            line.sharedMaterial = markerMaterial;
            line.useWorldSpace = false;
            line.positionCount = positionCount;
            line.numCapVertices = 2;
            line.shadowCastingMode = ShadowCastingMode.Off;
            return line;
        }

        private void Place(MeshRenderer sphere, Vector3 localPosition, float radius, Color color)
        {
            sphere.gameObject.SetActive(true);
            sphere.transform.localPosition = localPosition;
            sphere.transform.localScale = Vector3.one * (radius * 2f);
            SetColor(sphere, color);
        }

        private void SetColor(Renderer renderer, Color color)
        {
            properties.SetColor(ColorProperty, color);
            renderer.SetPropertyBlock(properties);
        }

        private static Color Grey(float density)
        {
            return Color.Lerp(Color.black, Color.white, density);
        }

        private static Vector3 CornerPosition(RecordedVoxel voxel, int corner, float size)
        {
            return voxel.Origin + (Vector3)MarchingCubes.CornerOffset(corner) * size;
        }

        private static int SolidCornerOf(RecordedVoxel voxel, int edge)
        {
            int cornerA = MarchingCubesTables.EdgeCorners[edge, 0];
            return MarchingCubes.IsCornerSolid(voxel.CaseIndex, cornerA) ? cornerA : MarchingCubesTables.EdgeCorners[edge, 1];
        }
    }
}
