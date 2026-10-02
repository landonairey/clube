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
    /// in the Game view as well as the Scene view (V19): spheres on the current
    /// voxel's corners and edge vertices, a line per cube edge, an outline for the
    /// triangle being added, and a mesh of every triangle finished so far.
    /// </summary>
    /// <remarks>
    /// Only the current voxel gets markers, so the same fixed set of objects is
    /// reused for every voxel and chunk size. The objects live under one root,
    /// parented to the chunk so they share its transform.
    /// </remarks>
    public sealed class StepThroughVisuals : IDisposable
    {
        private const string ColorProperty = "_BaseColor";

        private static readonly Color SolidColor = new Color(1f, 0.55f, 0.15f);
        private static readonly Color EmptyColor = new Color(0.35f, 0.65f, 1f);
        private static readonly Color HighlightColor = new Color(1f, 0.95f, 0.3f);
        private static readonly Color CubeEdgeColor = new Color(0.55f, 0.55f, 0.55f);
        private static readonly Color VertexColor = new Color(1f, 0.95f, 0.3f);
        private static readonly Color TriangleOutlineColor = new Color(0.3f, 1f, 0.85f);

        private readonly GameObject root;
        private readonly MeshRenderer[] cornerSpheres = new MeshRenderer[MarchingCubes.CornerCount];
        private readonly MeshRenderer[] vertexSpheres = new MeshRenderer[MarchingCubes.EdgeCount];
        private readonly LineRenderer[] edgeLines = new LineRenderer[MarchingCubes.EdgeCount];
        private readonly LineRenderer triangleOutline;
        private readonly Mesh surface;
        private readonly MaterialPropertyBlock properties = new MaterialPropertyBlock();

        private readonly List<Vector3> surfaceVertices = new List<Vector3>();
        private readonly List<int> surfaceTriangles = new List<int>();

        // Recording index into the triangle steps, and how many are in the surface mesh.
        private readonly List<int> triangleSteps = new List<int>();
        private int surfaceTriangleCount = -1;

        private MeshingRecorder recording;

        /// <param name="chunk">Transform the recorded chunk-local positions are relative to.</param>
        /// <param name="markerMaterial">Unlit material for spheres and lines, tinted per object.</param>
        /// <param name="surfaceMaterial">Material for the partial surface, normally the chunk's own.</param>
        public StepThroughVisuals(Transform chunk, Material markerMaterial, Material surfaceMaterial)
        {
            root = new GameObject("Step Through Visuals");
            root.transform.SetParent(chunk, false);

            for (int corner = 0; corner < cornerSpheres.Length; corner++)
            {
                cornerSpheres[corner] = CreateSphere($"Corner {corner}", markerMaterial);
            }
            for (int edge = 0; edge < MarchingCubes.EdgeCount; edge++)
            {
                vertexSpheres[edge] = CreateSphere($"Vertex {edge}", markerMaterial);
                edgeLines[edge] = CreateLine($"Edge {edge}", markerMaterial, 2);
            }
            triangleOutline = CreateLine("Triangle Outline", markerMaterial, 0);

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
            RecordedVoxel voxel = recording.Voxels[step.VoxelIndex];
            float size = recording.Settings.VoxelSize;

            ShowSurface(stepIndex, progress);
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

        // Grey by density until classified; then orange (solid) or blue (empty). While
        // the case index is built, the corner adding its bit is enlarged.
        private void ShowCorners(RecordedVoxel voxel, MeshingStepType type, float progress, float size)
        {
            int classified = type == MeshingStepType.ReadCorners ? 0
                : type == MeshingStepType.Classify ? RevealedCount(progress, MarchingCubes.CornerCount)
                : MarchingCubes.CornerCount;
            int bitCorner = type == MeshingStepType.CaseIndex
                ? Mathf.Min(RevealedCount(progress, MarchingCubes.CornerCount), MarchingCubes.CornerCount - 1)
                : -1;

            for (int corner = 0; corner < cornerSpheres.Length; corner++)
            {
                float value = voxel.CornerValues[corner];
                Color color = corner < classified
                    ? (MarchingCubes.IsCornerSolid(voxel.CaseIndex, corner) ? SolidColor : EmptyColor)
                    : Color.Lerp(Color.black, Color.white, value);

                float radius = (corner == bitCorner ? 0.14f : 0.08f) * size;
                Place(cornerSpheres[corner], CornerPosition(voxel, corner, size), radius, color);
            }
        }

        // Faint cube outline; crossed edges highlighted from the edge table step on,
        // lighting up one at a time during it.
        private void ShowEdges(RecordedVoxel voxel, MeshingStepType type, float progress, float size)
        {
            int crossedCount = CountBits(voxel.CrossedEdgeMask);
            int highlighted = type < MeshingStepType.EdgeTable ? 0
                : type == MeshingStepType.EdgeTable ? RevealedCount(progress, crossedCount)
                : crossedCount;

            int crossedSoFar = 0;
            for (int edge = 0; edge < MarchingCubes.EdgeCount; edge++)
            {
                bool crossed = (voxel.CrossedEdgeMask & (1 << edge)) != 0;
                bool lit = crossed && crossedSoFar++ < highlighted;

                LineRenderer line = edgeLines[edge];
                line.SetPosition(0, CornerPosition(voxel, MarchingCubesTables.EdgeCorners[edge, 0], size));
                line.SetPosition(1, CornerPosition(voxel, MarchingCubesTables.EdgeCorners[edge, 1], size));
                line.widthMultiplier = (lit ? 0.035f : 0.012f) * size;
                SetColor(line, lit ? HighlightColor : CubeEdgeColor);
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

            for (int i = stepIndex; i >= 0 && recording.Steps[i].VoxelIndex == recording.Steps[stepIndex].VoxelIndex; i--)
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

                Place(vertexSpheres[step.Edge], position, 0.06f * size, VertexColor);
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

        private MeshRenderer CreateSphere(string name, Material material)
        {
            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = name;
            Object.Destroy(sphere.GetComponent<Collider>());
            sphere.transform.SetParent(root.transform, false);

            var renderer = sphere.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            return renderer;
        }

        private LineRenderer CreateLine(string name, Material material, int positionCount)
        {
            var lineObject = new GameObject(name);
            lineObject.transform.SetParent(root.transform, false);

            var line = lineObject.AddComponent<LineRenderer>();
            line.sharedMaterial = material;
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

        private static Vector3 CornerPosition(RecordedVoxel voxel, int corner, float size)
        {
            return voxel.Origin + (Vector3)MarchingCubes.CornerOffset(corner) * size;
        }

        private static int SolidCornerOf(RecordedVoxel voxel, int edge)
        {
            int cornerA = MarchingCubesTables.EdgeCorners[edge, 0];
            return MarchingCubes.IsCornerSolid(voxel.CaseIndex, cornerA) ? cornerA : MarchingCubesTables.EdgeCorners[edge, 1];
        }

        // How many of `count` items have appeared, revealing one per equal slice of the step.
        private static int RevealedCount(float progress, int count)
        {
            return progress >= 1f ? count : Mathf.FloorToInt(progress * count);
        }

        private static int CountBits(int mask)
        {
            int count = 0;
            for (; mask != 0; mask &= mask - 1)
            {
                count++;
            }
            return count;
        }
    }
}
