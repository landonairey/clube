using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// Strategy for turning edge vertices into mesh vertex indices (V4). Whether
    /// vertices are shared between triangles decides flat vs smooth normals.
    /// Chosen once per chunk build (A6).
    /// </summary>
    public interface IVertexWriter
    {
        /// <summary>Called before the edges of each voxel are written.</summary>
        void BeginVoxel(Vector3Int voxel);

        /// <summary>Returns the index of the vertex for <paramref name="edge"/> (0-11) of the current voxel.</summary>
        int Write(int edge, Vector3 position);
    }
}
