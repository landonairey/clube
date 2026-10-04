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
    /// finished so far. The closing normals step (K18) grows a line from each
    /// triangle's centre along its face normal, then swaps in the finished mesh,
    /// shaded as the chunk is.
    /// </summary>
    /// <remarks>
    /// Only the current voxel gets corner and edge markers, so that set is reused
    /// for every voxel and chunk size. The density field is one combined mesh
    /// (<see cref="SampleSpheres"/>), so a whole chunk's samples cost one draw call
    /// (K17); above <see cref="SampleSpheres.MaxSamples"/> it isn't drawn.
    /// Everything lives under one root, parented to the chunk so it shares its transform.
    /// </remarks>
    public sealed class StepThroughVisuals : IDisposable
    {
        /// <summary>Corner sphere radius, as a fraction of the voxel size.</summary>
        public const float CornerRadius = 0.08f;

        /// <summary>Radius of a corner whose bit is being added to the case index.</summary>
        public const float HighlightedCornerRadius = 0.14f;

        private const float VertexRadius = 0.06f;

        // Normal line length, as a fraction of the voxel size.
        private const float NormalLength = 0.3f;

        private const string ColorProperty = "_BaseColor";

        // surfaceTriangleCount when the surface holds the finished mesh.
        private const int FinishedSurface = -2;

        public static readonly Color SolidColor = new Color(1f, 0.55f, 0.15f);
        public static readonly Color EmptyColor = new Color(0.35f, 0.65f, 1f);
        public static readonly Color CrossedEdgeColor = new Color(1f, 0.95f, 0.3f);

        private static readonly Color CubeEdgeColor = new Color(0.55f, 0.55f, 0.55f);
        private static readonly Color VertexColor = new Color(1f, 0.95f, 0.3f);
        private static readonly Color TriangleOutlineColor = new Color(0.3f, 1f, 0.85f);
        private static readonly Color NormalColor = new Color(0.3f, 0.8f, 1f);

        private readonly GameObject root;
        private readonly Material markerMaterial;
        private readonly SampleSpheres sampleSpheres;
        private readonly LabMeshObject normalLines;
        private readonly MeshRenderer[] cornerSpheres = new MeshRenderer[MarchingCubes.CornerCount];
        private readonly MeshRenderer[] vertexSpheres = new MeshRenderer[MarchingCubes.EdgeCount];
        private readonly LineRenderer[] edgeLines = new LineRenderer[MarchingCubes.EdgeCount];
        private readonly LineRenderer triangleOutline;
        private readonly Mesh surface;
        private readonly MaterialPropertyBlock properties = new MaterialPropertyBlock();

        private readonly List<Vector3> surfaceVertices = new List<Vector3>();
        private readonly List<int> surfaceTriangles = new List<int>();

        // The mesher's real output, shown once the normals step completes.
        private readonly List<Vector3> finishedVertices = new List<Vector3>();
        private readonly List<int> finishedTriangles = new List<int>();
        private readonly List<Vector3> finishedNormals = new List<Vector3>();
        private readonly List<Vector3> faceCentres = new List<Vector3>();
        private readonly List<Vector3> faceNormals = new List<Vector3>();
        private readonly List<Vector3> lineVertices = new List<Vector3>();
        private readonly List<int> lineIndices = new List<int>();

        // Recording index of each triangle step, and how many are in the surface mesh;
        // FinishedSurface when it holds the finished mesh instead.
        private readonly List<int> triangleSteps = new List<int>();
        private int surfaceTriangleCount = -1;
        private float shownNormalLength = -1f;

        private MeshingRecorder recording;

        /// <param name="chunk">Transform the recorded chunk-local positions are relative to.</param>
        /// <param name="markerMaterial">Unlit material for spheres and lines, tinted per object.</param>
        /// <param name="sampleMaterial">Vertex-colour material for the density field's spheres.</param>
        /// <param name="surfaceMaterial">Material for the partial surface, normally the chunk's own.</param>
        public StepThroughVisuals(Transform chunk, Material markerMaterial, Material sampleMaterial, Material surfaceMaterial)
        {
            this.markerMaterial = markerMaterial;
            root = new GameObject("Step Through Visuals");
            root.transform.SetParent(chunk, false);

            sampleSpheres = new SampleSpheres(root.transform, "Density Field", sampleMaterial);
            normalLines = new LabMeshObject(root.transform, "Normals", markerMaterial);
            normalLines.SetColor(NormalColor);

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
        /// <param name="vertices">The recorded build's output vertices, as the chunk's mesh gets them.</param>
        /// <param name="triangles">The recorded build's output triangles.</param>
        public void Load(MeshingRecorder newRecording, IReadOnlyList<Vector3> vertices, IReadOnlyList<int> triangles)
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
            shownNormalLength = -1f;

            Vector3Int count = recording.SampleCount;
            if (SampleSpheres.Total(count) <= SampleSpheres.MaxSamples)
            {
                sampleSpheres.SetGeometry(count, recording.Settings.VoxelSize, CornerRadius);
                sampleSpheres.SetDensities(recording.GetDensity, 1f);
            }

            LoadFinishedMesh(vertices, triangles);
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
            bool isNormals = step.Type == MeshingStepType.Normals;
            if (isNormals && progress >= 1f)
            {
                ShowFinishedSurface();
            }
            else
            {
                ShowSurface(stepIndex, progress);
            }
            ShowNormals(isNormals && progress < 1f ? progress : -1f);

            bool isField = step.Type == MeshingStepType.DensityField;
            ShowSamples(isField ? progress : -1f);
            if (step.VoxelIndex < 0)
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
            sampleSpheres.Dispose();
            normalLines.Dispose();
            Object.Destroy(surface);
            Object.Destroy(root);
        }

        // The density field, samples appearing in storage order; hidden when progress < 0
        // or when there are too many samples to draw.
        private void ShowSamples(float progress)
        {
            int total = SampleSpheres.Total(recording.SampleCount);
            if (progress < 0f || total > SampleSpheres.MaxSamples)
            {
                sampleSpheres.Visible = false;
                return;
            }

            sampleSpheres.Visible = true;
            sampleSpheres.Reveal(StepReveal.Revealed(progress, total));
        }

        // Keeps the mesher's output and the normals Unity gives it, the same call
        // ChunkView makes, so the closing step shows exactly what the chunk renders.
        private void LoadFinishedMesh(IReadOnlyList<Vector3> vertices, IReadOnlyList<int> triangles)
        {
            finishedVertices.Clear();
            finishedVertices.AddRange(vertices);
            finishedTriangles.Clear();
            finishedTriangles.AddRange(triangles);

            var scratch = new Mesh { indexFormat = IndexFormat.UInt32 };
            scratch.SetVertices(finishedVertices);
            scratch.SetTriangles(finishedTriangles, 0);
            scratch.RecalculateNormals();
            scratch.GetNormals(finishedNormals);
            Object.Destroy(scratch);

            // Each triangle's normal from its winding, the same convention Unity uses
            // (clockwise faces the viewer). Vertex normals are built from these.
            faceCentres.Clear();
            faceNormals.Clear();
            for (int i = 0; i + 2 < finishedTriangles.Count; i += 3)
            {
                Vector3 a = finishedVertices[finishedTriangles[i]];
                Vector3 b = finishedVertices[finishedTriangles[i + 1]];
                Vector3 c = finishedVertices[finishedTriangles[i + 2]];
                faceCentres.Add((a + b + c) / 3f);
                faceNormals.Add(Vector3.Cross(b - a, c - a).normalized);
            }
        }

        // Every triangle, with the shared vertices and normals the chunk's mesh has.
        private void ShowFinishedSurface()
        {
            if (surfaceTriangleCount == FinishedSurface)
            {
                return;
            }

            surfaceTriangleCount = FinishedSurface;
            surface.Clear();
            surface.indexFormat = finishedVertices.Count > ushort.MaxValue ? IndexFormat.UInt32 : IndexFormat.UInt16;
            surface.SetVertices(finishedVertices);
            surface.SetTriangles(finishedTriangles, 0);
            surface.SetNormals(finishedNormals);
            surface.RecalculateBounds();
        }

        // A line from each triangle's centre along its face normal, growing from 0 to
        // full length as the step plays; hidden when progress < 0.
        private void ShowNormals(float progress)
        {
            if (progress < 0f || faceCentres.Count == 0)
            {
                normalLines.Visible = false;
                return;
            }

            normalLines.Visible = true;
            float length = Mathf.SmoothStep(0f, 1f, progress) * NormalLength * recording.Settings.VoxelSize;
            if (Mathf.Approximately(length, shownNormalLength))
            {
                return;
            }

            shownNormalLength = length;
            lineVertices.Clear();
            lineIndices.Clear();
            for (int i = 0; i < faceCentres.Count; i++)
            {
                lineIndices.Add(lineVertices.Count);
                lineVertices.Add(faceCentres[i]);
                lineIndices.Add(lineVertices.Count);
                lineVertices.Add(faceCentres[i] + faceNormals[i] * length);
            }

            Mesh mesh = normalLines.Mesh;
            mesh.Clear();
            mesh.indexFormat = lineVertices.Count > ushort.MaxValue ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.SetVertices(lineVertices);
            mesh.SetIndices(lineIndices, MeshTopology.Lines, 0);
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
            surface.indexFormat = surfaceVertices.Count > ushort.MaxValue ? IndexFormat.UInt32 : IndexFormat.UInt16;
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
