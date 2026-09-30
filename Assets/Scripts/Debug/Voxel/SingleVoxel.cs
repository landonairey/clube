using System.Collections.Generic;
using Clube.Core;
using UnityEngine;

namespace Clube.Debug
{
    /// <summary>
    /// A single Marching Cubes voxel whose 8 corner values are editable in the
    /// Inspector. The mesh is rebuilt whenever a value changes during Play mode.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class SingleVoxel : MonoBehaviour
    {
        [Tooltip("Corner values, 0 = empty, 1 = solid. Order: bottom face 0-3, top face 4-7.")]
        [SerializeField, Range(0f, 1f)]
        private float[] cornerValues = { 1f, 1f, 1f, 1f, 0f, 0f, 0f, 0f };

        [Tooltip("Corners at or above this value count as solid.")]
        [SerializeField, Range(0f, 1f)]
        private float isoLevel = 0.5f;

        [Tooltip("Edge length of the voxel in world units.")]
        [SerializeField, Min(0.01f)]
        private float size = 1f;

        private readonly List<Vector3> vertices = new List<Vector3>();
        private readonly List<int> triangles = new List<int>();

        private Mesh mesh;
        private bool isDirty = true;

        public IReadOnlyList<float> CornerValues => cornerValues;
        public float IsoLevel => isoLevel;
        public float Size => size;

        /// <summary>Local-space position of corner 0; keeps the voxel centred on the transform.</summary>
        public Vector3 Origin => Vector3.one * (-0.5f * size);

        private void Awake()
        {
            mesh = new Mesh { name = "SingleVoxel" };
            mesh.MarkDynamic();
            GetComponent<MeshFilter>().sharedMesh = mesh;
        }

        private void Update()
        {
            if (isDirty)
            {
                RebuildMesh();
                isDirty = false;
            }
        }

        private void OnDestroy()
        {
            if (mesh != null)
            {
                Destroy(mesh);
            }
        }

        // Called by Unity whenever an Inspector value changes, including in Play mode.
        private void OnValidate()
        {
            if (cornerValues == null || cornerValues.Length != MarchingCubes.CornerCount)
            {
                System.Array.Resize(ref cornerValues, MarchingCubes.CornerCount);
            }
            isDirty = true;
        }

        private void RebuildMesh()
        {
            vertices.Clear();
            triangles.Clear();
            MarchingCubes.Polygonise(cornerValues, isoLevel, Origin, size, vertices, triangles);

            mesh.Clear();
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
        }
    }
}
