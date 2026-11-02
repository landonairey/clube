using System.Collections.Generic;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// Instance matrices packed into arrays of up to 1023, the most <see cref="Graphics.RenderMeshInstanced"/>
    /// takes from an array, kept and reused until refilled. The flora draws (<see cref="GrassScatter"/>,
    /// <see cref="BushScatter"/>) fill one per mesh when what's drawn changes and draw it every frame.
    /// </summary>
    public sealed class InstanceBatches
    {
        private const int BatchSize = 1023;

        private readonly List<Matrix4x4[]> arrays = new List<Matrix4x4[]>();
        private readonly List<int> counts = new List<int>();

        /// <summary>Instances held.</summary>
        public int Count { get; private set; }

        public void Clear()
        {
            counts.Clear();
            Count = 0;
        }

        public void Add(Matrix4x4 matrix)
        {
            int filled = counts.Count == 0 ? BatchSize : counts[counts.Count - 1];
            if (filled == BatchSize)
            {
                if (arrays.Count == counts.Count)
                {
                    arrays.Add(new Matrix4x4[BatchSize]);
                }
                counts.Add(0);
                filled = 0;
            }
            arrays[counts.Count - 1][filled] = matrix;
            counts[counts.Count - 1] = filled + 1;
            Count++;
        }

        public void AddRange(List<Matrix4x4> matrices)
        {
            for (int i = 0; i < matrices.Count; i++)
            {
                Add(matrices[i]);
            }
        }

        public void Draw(in RenderParams parameters, Mesh mesh)
        {
            if (mesh == null)
            {
                return;
            }
            for (int i = 0; i < counts.Count; i++)
            {
                Graphics.RenderMeshInstanced(parameters, mesh, 0, arrays[i], counts[i]);
            }
        }
    }
}
