using System.Collections.Generic;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// Instance matrices packed into arrays of up to 1023, the most <see cref="Graphics.RenderMeshInstanced"/>
    /// takes from an array, kept and reused until refilled. The flora draws (<see cref="GrassScatter"/>,
    /// <see cref="BushScatter"/>) fill one per mesh when what's drawn changes and draw it every frame. Each array keeps the
    /// bounds of its instances' positions, handed to the draw (GL379): without them Unity works out the bounds of every
    /// instance again each frame.
    /// </summary>
    public sealed class InstanceBatches
    {
        private const int BatchSize = 1023;

        // How far an instance reaches round its position, in metres, for the batch bounds.
        private const float InstanceReach = 3f;

        private readonly List<Matrix4x4[]> arrays = new List<Matrix4x4[]>();
        private readonly List<int> counts = new List<int>();
        private readonly List<Bounds> bounds = new List<Bounds>();

        /// <summary>Instances held.</summary>
        public int Count { get; private set; }

        public void Clear()
        {
            counts.Clear();
            bounds.Clear();
            Count = 0;
        }

        public void Add(Matrix4x4 matrix)
        {
            int filled = counts.Count == 0 ? BatchSize : counts[counts.Count - 1];
            Vector3 position = matrix.GetColumn(3);
            if (filled == BatchSize)
            {
                if (arrays.Count == counts.Count)
                {
                    arrays.Add(new Matrix4x4[BatchSize]);
                }
                counts.Add(0);
                bounds.Add(new Bounds(position, Vector3.zero));
                filled = 0;
            }
            int last = counts.Count - 1;
            arrays[last][filled] = matrix;
            counts[last] = filled + 1;
            Bounds batch = bounds[last];
            batch.Encapsulate(position);
            bounds[last] = batch;
            Count++;
        }

        public void AddRange(List<Matrix4x4> matrices)
        {
            AddRange(matrices, matrices.Count);
        }

        /// <summary>Adds the first <paramref name="count"/> of the matrices.</summary>
        public void AddRange(List<Matrix4x4> matrices, int count)
        {
            count = Mathf.Min(count, matrices.Count);
            for (int i = 0; i < count; i++)
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
            RenderParams batchParameters = parameters;
            for (int i = 0; i < counts.Count; i++)
            {
                Bounds batch = bounds[i];
                batch.Expand(InstanceReach * 2f);
                batchParameters.worldBounds = batch;
                Graphics.RenderMeshInstanced(batchParameters, mesh, 0, arrays[i], counts[i]);
            }
        }
    }
}
