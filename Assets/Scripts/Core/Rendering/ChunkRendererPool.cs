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
        private Material[] farMaterials;

        // Materials after the first are overlays (the build grid, M22): a second pass over every
        // triangle, so only chunks within the overlay radius of the focus draw them.
        private readonly HashSet<Vector3Int> withOverlays = new HashSet<Vector3Int>();
        private Vector3Int overlayCentre;
        private int overlayRadius = -1;

        // Which columns (x, z) draw their chunks (P18); null draws every one. Chunks outside stay
        // loaded and keep their renderer, switched off.
        private Func<Vector2Int, bool> isColumnDrawn;

        /// <param name="parent">Renderers sit under it, at their chunk's origin in its space.</param>
        /// <param name="withColliders">Give every renderer a <see cref="ChunkCollider"/> (M6).</param>
        public ChunkRendererPool(Transform parent, bool withColliders, Material[] materials)
        {
            this.parent = parent;
            this.withColliders = withColliders;
            SetMaterialArrays(materials);
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
            ShowOverlays(coord, chunkRenderer, IsWithinOverlayRadius(coord), force: true);
            ApplyDrawn(coord, chunkRenderer);
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
            withOverlays.Remove(coord);
            chunkRenderer.Renderer.forceRenderingOff = false;
            chunkRenderer.Hide();
            idle.Push(chunkRenderer);
        }

        /// <summary>The materials every renderer draws with (the terrain material first, M15); applies to idle ones too.</summary>
        public void SetMaterials(Material[] newMaterials)
        {
            SetMaterialArrays(newMaterials);
            foreach (KeyValuePair<Vector3Int, ChunkRenderer> entry in active)
            {
                entry.Value.Renderer.sharedMaterials = withOverlays.Contains(entry.Key) ? materials : farMaterials;
            }
            foreach (ChunkRenderer chunkRenderer in idle)
            {
                chunkRenderer.Renderer.sharedMaterials = farMaterials;
            }
        }

        /// <summary>
        /// Overlays (every material after the first) only on chunks within <paramref name="radius"/>
        /// chunks of <paramref name="centre"/>, horizontally; a negative radius puts them on every chunk.
        /// Cheap when nothing changed, so call it every frame.
        /// </summary>
        public void SetOverlayFocus(Vector3Int centre, int radius)
        {
            if (centre == overlayCentre && radius == overlayRadius)
            {
                return;
            }
            overlayCentre = centre;
            overlayRadius = radius;
            foreach (KeyValuePair<Vector3Int, ChunkRenderer> entry in active)
            {
                ShowOverlays(entry.Key, entry.Value, IsWithinOverlayRadius(entry.Key));
            }
        }

        /// <summary>
        /// Which columns (x, z) draw their chunks (P18): the others' renderers are switched off, not
        /// released. Null draws every one. Applies to every renderer shown now and to those shown later.
        /// </summary>
        public void SetDrawRule(Func<Vector2Int, bool> rule)
        {
            isColumnDrawn = rule;
            RefreshDrawn();
        }

        /// <summary>Applies the draw rule to every renderer, e.g. after it changed everywhere.</summary>
        public void RefreshDrawn()
        {
            foreach (KeyValuePair<Vector3Int, ChunkRenderer> entry in active)
            {
                ApplyDrawn(entry.Key, entry.Value);
            }
        }

        /// <summary>Applies the draw rule to the column's renderers, layers 0 to <paramref name="height"/> - 1.</summary>
        public void RefreshColumn(Vector2Int column, int height)
        {
            for (int y = 0; y < height; y++)
            {
                var coord = new Vector3Int(column.x, y, column.y);
                if (active.TryGetValue(coord, out ChunkRenderer chunkRenderer))
                {
                    ApplyDrawn(coord, chunkRenderer);
                }
            }
        }

        private void ApplyDrawn(Vector3Int coord, ChunkRenderer chunkRenderer)
        {
            bool hidden = isColumnDrawn != null && !isColumnDrawn(new Vector2Int(coord.x, coord.z));
            if (chunkRenderer.Renderer.forceRenderingOff != hidden)
            {
                chunkRenderer.Renderer.forceRenderingOff = hidden;
            }
        }

        private bool IsWithinOverlayRadius(Vector3Int coord)
        {
            if (overlayRadius < 0)
            {
                return true;
            }
            int dx = coord.x - overlayCentre.x;
            int dz = coord.z - overlayCentre.z;
            return dx * dx + dz * dz <= overlayRadius * overlayRadius;
        }

        // A renderer taken from the pool may still carry its last chunk's materials, hence force.
        private void ShowOverlays(Vector3Int coord, ChunkRenderer chunkRenderer, bool show, bool force = false)
        {
            if (!force && show == withOverlays.Contains(coord))
            {
                return;
            }
            if (show)
            {
                withOverlays.Add(coord);
            }
            else
            {
                withOverlays.Remove(coord);
            }
            chunkRenderer.Renderer.sharedMaterials = show ? materials : farMaterials;
        }

        private void SetMaterialArrays(Material[] newMaterials)
        {
            materials = newMaterials;
            farMaterials = newMaterials != null && newMaterials.Length > 1 ? new[] { newMaterials[0] } : newMaterials;
        }

        private ChunkRenderer Create()
        {
            var chunkObject = new GameObject("Chunk", typeof(MeshFilter), typeof(MeshRenderer), typeof(ChunkRenderer));
            chunkObject.transform.SetParent(parent, false);
            chunkObject.GetComponent<MeshRenderer>().sharedMaterials = farMaterials;
            if (withColliders)
            {
                chunkObject.AddComponent<ChunkCollider>();
            }
            return chunkObject.GetComponent<ChunkRenderer>();
        }
    }
}
