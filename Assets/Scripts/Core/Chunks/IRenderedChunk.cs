using System;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// A chunk on screen: a lone <see cref="ChunkView"/> in the single-chunk labs, or one
    /// of a <see cref="WorldView"/>'s <see cref="ChunkRenderer"/>s. Lab tools work through
    /// this (A4, M18), so the same tools run on a lab's chunk and on a chunk of the world.
    /// </summary>
    public interface IRenderedChunk
    {
        /// <summary>The chunk shown, or null (before Play mode, or while a renderer is pooled).</summary>
        Chunk Chunk { get; }

        /// <summary>The chunk's sample (0,0,0) sits at this transform's origin.</summary>
        Transform Transform { get; }

        /// <summary>The settings the chunk's mesh is built with.</summary>
        ChunkMeshSettings MeshSettings { get; }

        /// <summary>The chunk's mesh, as last built.</summary>
        Mesh Mesh { get; }

        /// <summary>Draws <see cref="Mesh"/>; step-through hides it while it plays.</summary>
        Renderer Renderer { get; }

        /// <summary>Counts and timings from the last mesh build (K4).</summary>
        ChunkMeshStats LastBuildStats { get; }

        /// <summary>Raised after each rebuild, with normals and bounds already set.</summary>
        event Action<Mesh> MeshRebuilt;

        /// <summary>Raised when <see cref="Chunk"/> becomes another chunk, or null.</summary>
        event Action<Chunk> ChunkChanged;

        /// <summary>
        /// Writes one of the chunk's samples, chunk-local, through the single edit path (A7).
        /// A world chunk writes every copy of a border sample, so neighbours stay seamless (M2).
        /// </summary>
        void SetDensity(Vector3Int sample, float density);
    }
}
