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
            version++;
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
            version++;
        }

        public void AddRange(List<Matrix4x4> matrices)
        {
            AddRange(matrices, matrices.Count);
        }

        /// <summary>Adds the first <paramref name="count"/> of the matrices.</summary>
        public void AddRange(List<Matrix4x4> matrices, int count)
        {
            count = Mathf.Min(count, matrices.Count);
            int i = 0;
            while (i < count)
            {
                int filled = counts.Count == 0 ? BatchSize : counts[counts.Count - 1];
                if (filled == BatchSize)
                {
                    Add(matrices[i++]);
                    continue;
                }
                // Straight into the last array, its bounds updated once for the run (GL416).
                int last = counts.Count - 1;
                Matrix4x4[] array = arrays[last];
                Bounds batch = bounds[last];
                int run = Mathf.Min(count - i, BatchSize - filled);
                for (int k = 0; k < run; k++)
                {
                    Matrix4x4 matrix = matrices[i + k];
                    array[filled + k] = matrix;
                    batch.Encapsulate(new Vector3(matrix.m03, matrix.m13, matrix.m23));
                }
                counts[last] = filled + run;
                bounds[last] = batch;
                Count += run;
                version++;
                i += run;
            }
        }

        // The GPU copy of every matrix, for DrawFromBuffer (GL416): uploaded once each time the instances change.
        private GraphicsBuffer buffer;
        private Matrix4x4[] staging;
        private int uploadedVersion = -1;
        private int version;

        /// <summary>
        /// Draws every instance in one call from a GPU buffer of their matrices (GL416), uploading it only after the instances
        /// changed; the shader reads the buffer named <paramref name="bufferId"/> by instance (procedural instancing, as
        /// <c>Clube/Grass</c> does). Far cheaper on the CPU than <see cref="Draw"/>, which copies every matrix to every draw
        /// each frame.
        /// </summary>
        public void DrawFromBuffer(in RenderParams parameters, Mesh mesh, int bufferId)
        {
            if (mesh == null || Count == 0 || parameters.matProps == null)
            {
                return;
            }
            if (uploadedVersion != version || buffer == null)
            {
                Upload();
            }
            RenderParams drawParameters = parameters;
            Bounds all = bounds[0];
            for (int i = 1; i < bounds.Count; i++)
            {
                all.Encapsulate(bounds[i]);
            }
            all.Expand(InstanceReach * 2f);
            drawParameters.worldBounds = all;
            // The block is copied when the draw is queued, so the next batch can set its own buffer on it.
            drawParameters.matProps.SetBuffer(bufferId, buffer);
            Graphics.RenderMeshPrimitives(drawParameters, mesh, 0, Count);
        }

        private void Upload()
        {
            if (buffer == null || buffer.count < Count)
            {
                int capacity = Mathf.Max(Count, buffer != null ? buffer.count * 2 : 1024);
                buffer?.Release();
                buffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, capacity, sizeof(float) * 16);
                staging = new Matrix4x4[capacity];
            }
            int at = 0;
            for (int i = 0; i < counts.Count; i++)
            {
                System.Array.Copy(arrays[i], 0, staging, at, counts[i]);
                at += counts[i];
            }
            buffer.SetData(staging, 0, 0, Count);
            uploadedVersion = version;
        }

        /// <summary>Frees the GPU buffer, if one was made.</summary>
        public void Dispose()
        {
            buffer?.Release();
            buffer = null;
            uploadedVersion = -1;
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
