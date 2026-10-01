using System.Collections.Generic;
using Clube.Core;
using UnityEngine;

namespace Clube.Debug
{
    /// <summary>
    /// Volume inspection for the voxel lab (1C): measures the parent voxel's fill
    /// three ways (V11, V12, V14) and draws the exact method's tetrahedra, pulled
    /// apart by an explode slider (V13). Positive tetrahedra get their own colour;
    /// negative ones (which subtract volume) are red.
    /// </summary>
    /// <remarks>
    /// Sits on a child object with no renderer of its own. Unity skips an object's
    /// gizmos once its renderer bounds leave the view, and the voxel's renderer has
    /// no bounds worth speaking of outside Play mode.
    /// </remarks>
    public class VoxelVolumeLab : MonoBehaviour
    {
        private static readonly Color NegativeColor = new Color(1f, 0.25f, 0.2f);
        private static readonly Vector3 CubeCentre = new Vector3(0.5f, 0.5f, 0.5f);

        [Header("Tetrahedra (V13)")]
        [SerializeField]
        private bool showTetrahedra = true;

        [Tooltip("How far each tetrahedron moves out from the cube centre, in voxel units.")]
        [SerializeField, Range(0f, 1f)]
        private float explodeDistance = 0.25f;

        [SerializeField, Range(0f, 1f)]
        private float faceOpacity = 0.35f;

        [Header("Reference (V14)")]
        [Tooltip("Monte Carlo samples of the smooth trilinear field. 0 turns it off.")]
        [SerializeField, Min(0)]
        private int monteCarloSamples = 10000;

        private readonly List<Tetrahedron> tetrahedra = new List<Tetrahedron>();
        private readonly List<Mesh> tetrahedronMeshes = new List<Mesh>();

        // Inputs of the last measurement, so it only reruns when something changed.
        private readonly float[] measuredCorners = new float[MarchingCubes.CornerCount];
        private float measuredIso = float.NaN;
        private EdgePlacement measuredPlacement;
        private int measuredSamples = -1;

        private VolumeReport report;

        public VoxelCornerEditor Corners => GetComponentInParent<VoxelCornerEditor>();

        /// <summary>How far tetrahedra are pushed out from the cube centre, 0 (assembled) to 1.</summary>
        public float ExplodeDistance
        {
            get => explodeDistance;
            set => explodeDistance = Mathf.Clamp01(value);
        }

        /// <summary>Current measurements, recomputed only when the corners or settings changed.</summary>
        public VolumeReport Measure()
        {
            Refresh();
            return report;
        }

        private void OnDrawGizmos()
        {
            VoxelCornerEditor corners = Corners;
            if (!showTetrahedra || corners == null)
            {
                return;
            }

            Refresh();
            Gizmos.matrix = corners.transform.localToWorldMatrix * Matrix4x4.Scale(Vector3.one * corners.VoxelSize);

            int positiveIndex = 0;
            for (int i = 0; i < tetrahedra.Count; i++)
            {
                Tetrahedron tetrahedron = tetrahedra[i];
                Color color = tetrahedron.SignedVolume >= 0f ? DistinctColor(positiveIndex++) : NegativeColor;

                Vector3 outwards = tetrahedron.Centroid - CubeCentre;
                Vector3 offset = outwards.sqrMagnitude > 1e-8f ? outwards.normalized * explodeDistance : Vector3.zero;

                Gizmos.color = new Color(color.r, color.g, color.b, faceOpacity);
                Gizmos.DrawMesh(tetrahedronMeshes[i], offset);

                Gizmos.color = color;
                DrawEdges(tetrahedron, offset);
            }
        }

        private void OnValidate()
        {
            // Force a remeasure, e.g. when the sample count changes.
            measuredSamples = -1;
        }

        private void OnDestroy()
        {
            DestroyMeshes();
        }

        private void Refresh()
        {
            VoxelCornerEditor corners = Corners;
            if (corners == null)
            {
                return;
            }

            IReadOnlyList<float> values = corners.CornerValues;
            float iso = corners.IsoLevel;
            EdgePlacement placement = corners.EdgePlacement;
            if (IsUpToDate(values, iso, placement))
            {
                return;
            }

            for (int corner = 0; corner < MarchingCubes.CornerCount; corner++)
            {
                measuredCorners[corner] = values[corner];
            }
            measuredIso = iso;
            measuredPlacement = placement;
            measuredSamples = monteCarloSamples;

            VoxelVolume.Tetrahedralise(values, iso, EdgeVertexPlacers.For(placement), tetrahedra);

            float exact = 0f;
            int positive = 0;
            foreach (Tetrahedron tetrahedron in tetrahedra)
            {
                exact += tetrahedron.SignedVolume;
                if (tetrahedron.SignedVolume >= 0f)
                {
                    positive++;
                }
            }

            float trilinear = monteCarloSamples > 0 ? VoxelVolume.SampleTrilinear(values, iso, monteCarloSamples) : 0f;
            report = new VolumeReport(
                VoxelVolume.Approximate(values), exact, trilinear, monteCarloSamples, positive, tetrahedra.Count - positive);

            RebuildMeshes();
        }

        private bool IsUpToDate(IReadOnlyList<float> values, float iso, EdgePlacement placement)
        {
            if (iso != measuredIso || placement != measuredPlacement || monteCarloSamples != measuredSamples)
            {
                return false;
            }

            for (int corner = 0; corner < MarchingCubes.CornerCount; corner++)
            {
                if (values[corner] != measuredCorners[corner])
                {
                    return false;
                }
            }
            return true;
        }

        private void RebuildMeshes()
        {
            DestroyMeshes();
            foreach (Tetrahedron tetrahedron in tetrahedra)
            {
                tetrahedronMeshes.Add(BuildMesh(tetrahedron));
            }
        }

        private void DestroyMeshes()
        {
            foreach (Mesh mesh in tetrahedronMeshes)
            {
                if (Application.isPlaying)
                {
                    Destroy(mesh);
                }
                else
                {
                    DestroyImmediate(mesh);
                }
            }
            tetrahedronMeshes.Clear();
        }

        // Four outward-facing triangles. A negative tetrahedron is inside out, so two
        // corners are swapped to keep its faces pointing outwards for shading.
        private static Mesh BuildMesh(Tetrahedron tetrahedron)
        {
            Vector3 p0 = tetrahedron.Apex;
            Vector3 p1 = tetrahedron.A;
            Vector3 p2 = tetrahedron.SignedVolume >= 0f ? tetrahedron.B : tetrahedron.C;
            Vector3 p3 = tetrahedron.SignedVolume >= 0f ? tetrahedron.C : tetrahedron.B;

            var mesh = new Mesh { name = "Tetrahedron", hideFlags = HideFlags.HideAndDontSave };
            mesh.SetVertices(new List<Vector3> { p1, p2, p3, p0, p3, p2, p0, p1, p3, p0, p2, p1 });
            mesh.SetTriangles(new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 }, 0);
            mesh.RecalculateNormals();
            return mesh;
        }

        private static void DrawEdges(Tetrahedron tetrahedron, Vector3 offset)
        {
            Vector3 apex = tetrahedron.Apex + offset;
            Vector3 a = tetrahedron.A + offset;
            Vector3 b = tetrahedron.B + offset;
            Vector3 c = tetrahedron.C + offset;

            Gizmos.DrawLine(apex, a);
            Gizmos.DrawLine(apex, b);
            Gizmos.DrawLine(apex, c);
            Gizmos.DrawLine(a, b);
            Gizmos.DrawLine(b, c);
            Gizmos.DrawLine(c, a);
        }

        // Golden-ratio hue steps keep neighbouring tetrahedra visibly different. Hues
        // stay between orange and violet, leaving red for negative tetrahedra.
        private static Color DistinctColor(int index)
        {
            float hue = 0.08f + (index * 0.618034f % 1f) * 0.8f;
            return Color.HSVToRGB(hue, 0.6f, 1f);
        }
    }
}
