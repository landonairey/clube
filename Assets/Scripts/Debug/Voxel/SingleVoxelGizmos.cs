using Clube.Core;
using UnityEngine;

namespace Clube.Debug
{
    /// <summary>
    /// Draws the voxel's cube outline and its corner values as spheres
    /// (black = 0, white = 1). Editor visualisation only.
    /// </summary>
    [RequireComponent(typeof(SingleVoxel))]
    public class SingleVoxelGizmos : MonoBehaviour
    {
        [SerializeField, Min(0f)]
        private float cornerRadius = 0.05f;

        [SerializeField]
        private Color outlineColor = new Color(1f, 1f, 1f, 0.3f);

        private void OnDrawGizmos()
        {
            var voxel = GetComponent<SingleVoxel>();
            if (voxel.CornerValues == null || voxel.CornerValues.Count != MarchingCubes.CornerCount)
            {
                return;
            }

            Gizmos.matrix = transform.localToWorldMatrix;

            Gizmos.color = outlineColor;
            Gizmos.DrawWireCube(Vector3.zero, Vector3.one * voxel.Size);

            for (int corner = 0; corner < MarchingCubes.CornerCount; corner++)
            {
                float value = voxel.CornerValues[corner];
                Gizmos.color = Color.Lerp(Color.black, Color.white, value);
                Vector3 position = voxel.Origin + voxel.Size * MarchingCubes.CornerPosition(corner);
                Gizmos.DrawSphere(position, cornerRadius * voxel.Size);
            }
        }
    }
}
