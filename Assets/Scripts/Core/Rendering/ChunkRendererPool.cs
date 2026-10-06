using System;
using System.Collections.Generic;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// Makes, reuses and keeps track of the GameObjects that draw a world's chunks (M1, P2):
    /// one <see cref="ChunkRenderer"/> per loaded chunk that has a surface. Chunks that are all
    /// air or all solid never get one, which is most of a world. Unity objects only; what to
    /// show comes from the <see cref="WorldStreamer"/>.
    /// </summary>
    public sealed class ChunkRendererPool
    {
        private readonly Transform parent;
        private readonly bool withColliders;
        private readonly Dictionary<Vector3Int, ChunkRenderer> active = new Dictionary<Vector3Int, ChunkRenderer>();
        private readonly Stack<ChunkRenderer> idle = new Stack<ChunkRenderer>();
        private Material[] materials;

        /// <param name="parent">Renderers sit under it, at their chunk's origin in its space.</param>
        /// <param name="withColliders">Give every renderer a <see cref="ChunkCollider"/> (M6).</param>
        public ChunkRendererPool(Transform parent, bool withColliders, Material[] materials)
        {
            this.parent = parent;
            this.withColliders = withColliders;
            this.materials = materials;
        }

        /// <summary>Every chunk shown, by coordinate.</summary>
        public IReadOnlyDictionary<Vector3Int, ChunkRenderer> Active => active;

        public bool TryGet(Vector3Int coord, out ChunkRenderer chunkRenderer)
        {
            return active.TryGetValue(coord, out chunkRenderer);
        }

        /// <summary>A renderer showing the chunk at its origin: an idle one if there is one, otherwise a new one.</summary>
        public ChunkRenderer Take(World world, Vector3Int coord, Chunk chunk, Func<ChunkMeshSettings> meshSettings)
        {
            ChunkRenderer chunkRenderer = idle.Count > 0 ? idle.Pop() : Create();
            chunkRenderer.transform.localPosition = world.Grid.ChunkOrigin(coord);
            chunkRenderer.Show(world, coord, chunk, meshSettings);
            active.Add(coord, chunkRenderer);
            return chunkRenderer;
        }

        /// <summary>Stops showing a chunk; its renderer waits for another.</summary>
        public void Release(Vector3Int coord)
        {
            if (!active.TryGetValue(coord, out ChunkRenderer chunkRenderer))
            {
                return;
            }
            active.Remove(coord);
            chunkRenderer.Hide();
            idle.Push(chunkRenderer);
        }

        /// <summary>The materials every renderer draws with (the terrain material first, M15); applies to idle ones too.</summary>
        public void SetMaterials(Material[] newMaterials)
        {
            materials = newMaterials;
            foreach (ChunkRenderer chunkRenderer in active.Values)
            {
                chunkRenderer.Renderer.sharedMaterials = materials;
            }
            foreach (ChunkRenderer chunkRenderer in idle)
            {
                chunkRenderer.Renderer.sharedMaterials = materials;
            }
        }

        private ChunkRenderer Create()
        {
            var chunkObject = new GameObject("Chunk", typeof(MeshFilter), typeof(MeshRenderer), typeof(ChunkRenderer));
            chunkObject.transform.SetParent(parent, false);
            chunkObject.GetComponent<MeshRenderer>().sharedMaterials = materials;
            if (withColliders)
            {
                chunkObject.AddComponent<ChunkCollider>();
            }
            return chunkObject.GetComponent<ChunkRenderer>();
        }
    }
}
