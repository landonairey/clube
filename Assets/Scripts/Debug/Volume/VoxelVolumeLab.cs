using System.Collections.Generic;
using Clube.Core;
using UnityEngine;
using UnityEngine.Serialization;

namespace Clube.Debug
{
    /// <summary>
    /// Volume inspection for the voxel lab (1C): measures the parent voxel's fill
    /// three ways (V11, V12, V14) and shows the exact method's tetrahedra, pulled
    /// apart by an explode slider (V13), through a <see cref="TetrahedraView"/>.
    /// </summary>
    /// <remarks>
    /// Runs in edit mode too, since the voxel's corners are edited outside Play
    /// mode; the tetrahedra objects are never saved and are removed on disable.
    /// </remarks>
    [ExecuteAlways]
    public class VoxelVolumeLab : MonoBehaviour
    {
        private const string VertexColorShader = "Universal Render Pipeline/Particles/Unlit";

        [Header("Exact volume (V12, V13)")]
        [Tooltip("Decompose the solid into tetrahedra for the exact volume and draw them. " +
                 "Off skips that work entirely, and the readout has no exact volume.")]
        [SerializeField, FormerlySerializedAs("showTetrahedra")]
        private bool exactVolume = true;

        [Tooltip("How far each tetrahedron moves out from the cube centre, in voxel units.")]
        [SerializeField, Range(0f, 1f)]
        private float explodeDistance = 0.25f;

        [SerializeField, Range(0f, 1f)]
        private float faceOpacity = 0.35f;

        [Tooltip("Transparent material showing vertex colours and alpha, for the faces.")]
        [SerializeField]
        private Material faceMaterial;

        [Tooltip("Material showing vertex colours, for the edges. Falls back to URP Particles/Unlit if empty.")]
        [SerializeField]
        private Material edgeMaterial;

        [Header("Reference (V14)")]
        [Tooltip("Monte Carlo samples of the smooth trilinear field. 0 turns it off.")]
        [SerializeField, Min(0)]
        private int monteCarloSamples = 10000;

        private readonly List<Tetrahedron> tetrahedra = new List<Tetrahedron>();

        // Inputs of the last measurement, so it only reruns when something changed.
        private readonly float[] measuredCorners = new float[MarchingCubes.CornerCount];
        private float measuredIso = float.NaN;
        private EdgePlacement measuredPlacement;
        private int measuredSamples = -1;
        private bool measuredExact;

        private VolumeReport report;

        private TetrahedraView view;
        private Material ownedEdgeMaterial;

        // What the view last showed, so it is only rebuilt when something changed.
        private int measurementVersion;
        private int shownVersion = -1;
        private float shownExplode = -1f;
        private float shownOpacity = -1f;
        private float shownVoxelSize = -1f;

        public VoxelCornerEditor Corners => GetComponentInParent<VoxelCornerEditor>();

        /// <summary>Whether the exact volume and its tetrahedra are computed (and drawn) at all.</summary>
        public bool ExactVolume
        {
            get => exactVolume;
            set => exactVolume = value;
        }

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
            RefreshView();
            return report;
        }

        private void OnValidate()
        {
            // Force a remeasure, e.g. when the sample count changes.
            measuredSamples = -1;
        }

        private void LateUpdate()
        {
            Refresh();
            RefreshView();
        }

        private void OnDisable()
        {
            view?.Dispose();
            view = null;
            shownVersion = -1;
        }

        private void OnDestroy()
        {
            LabMeshObject.DestroyNow(ownedEdgeMaterial);
        }

        private void RefreshView()
        {
            VoxelCornerEditor corners = Corners;
            if (!isActiveAndEnabled || corners == null)
            {
                return;
            }

            if (!exactVolume || faceMaterial == null)
            {
                if (view != null)
                {
                    view.Visible = false;
                }
                return;
            }

            view ??= new TetrahedraView(transform, faceMaterial, EdgeMaterial());
            view.Visible = true;

            float voxelSize = corners.VoxelSize;
            if (shownVersion == measurementVersion && shownExplode == explodeDistance &&
                shownOpacity == faceOpacity && shownVoxelSize == voxelSize)
            {
                return;
            }

            view.Scale = voxelSize;
            view.Show(tetrahedra, explodeDistance, faceOpacity);
            shownVersion = measurementVersion;
            shownExplode = explodeDistance;
            shownOpacity = faceOpacity;
            shownVoxelSize = voxelSize;
        }

        private Material EdgeMaterial()
        {
            if (edgeMaterial != null)
            {
                return edgeMaterial;
            }
            ownedEdgeMaterial ??= new Material(Shader.Find(VertexColorShader)) { name = "Tetrahedra Edges" };
            return ownedEdgeMaterial;
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
            measuredExact = exactVolume;

            // With the exact volume off, the tetrahedra are never built at all.
            tetrahedra.Clear();
            if (exactVolume)
            {
                VoxelVolume.Tetrahedralise(values, iso, EdgeVertexPlacers.For(placement), tetrahedra);
            }

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
                VoxelVolume.Approximate(values), exactVolume, exact, trilinear, monteCarloSamples,
                positive, tetrahedra.Count - positive);
            measurementVersion++;
        }

        private bool IsUpToDate(IReadOnlyList<float> values, float iso, EdgePlacement placement)
        {
            if (iso != measuredIso || placement != measuredPlacement || monteCarloSamples != measuredSamples ||
                exactVolume != measuredExact)
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
    }
}
