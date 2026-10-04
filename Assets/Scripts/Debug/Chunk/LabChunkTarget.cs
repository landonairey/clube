using System;
using Clube.Core;
using UnityEngine;

namespace Clube.Debug
{
    /// <summary>
    /// The chunk the lab tools on this object work on (M18). In VoxelLab and ChunkLab it's
    /// the <see cref="ChunkView"/> this object is on or under. In WorldLab it follows
    /// <see cref="ChunkFocus"/>: whichever chunk is focused, and the object moves onto that
    /// chunk so the tools' visuals sit on it. Tools hook <see cref="MeshRebuilt"/> and
    /// <see cref="Changed"/> here instead of a particular kind of chunk.
    /// </summary>
    /// <remarks>
    /// In WorldLab, put this object directly under the <see cref="WorldView"/>, so it
    /// shares the chunks' parent space.
    /// </remarks>
    [DefaultExecutionOrder(-100)]
    public class LabChunkTarget : MonoBehaviour
    {
        [Tooltip("Follow this chunk focus (WorldLab). Leave empty to use the ChunkView this object is on or under.")]
        [SerializeField]
        private ChunkFocus focus;

        private bool initialized;
        private IRenderedChunk current;

        /// <summary>Raised after every rebuild of the target chunk's mesh.</summary>
        public event Action<Mesh> MeshRebuilt;

        /// <summary>Raised when the target becomes another chunk, or none: selections and visuals of the old one no longer apply.</summary>
        public event Action Changed;

        /// <summary>The chunk on screen the tools work on, or null.</summary>
        public IRenderedChunk Current
        {
            get
            {
                Initialize();
                return current;
            }
        }

        /// <summary>The target's chunk, or null (nothing focused, or before Play mode).</summary>
        public Chunk Chunk => Current?.Chunk;

        /// <summary>The target's mesh settings. Only meaningful while <see cref="Chunk"/> is set.</summary>
        public ChunkMeshSettings MeshSettings => Current != null ? Current.MeshSettings : default;

        public float VoxelSize => MeshSettings.VoxelSize;

        public float IsoLevel => MeshSettings.IsoLevel;

        public Mesh Mesh => Current?.Mesh;

        /// <summary>True when this follows WorldLab's focused chunk rather than a fixed one.</summary>
        public bool FollowsFocus => focus != null;

        /// <summary>Writes one of the target chunk's samples (chunk-local) through its edit path (A7, M2).</summary>
        public void SetDensity(Vector3Int sample, float density)
        {
            Current?.SetDensity(sample, density);
        }

        /// <summary>Rebuilds the target chunk's mesh at the end of the frame.</summary>
        public void RequestRebuild()
        {
            Chunk?.MarkDirty();
        }

        private void Awake()
        {
            Initialize();
        }

        private void OnDestroy()
        {
            if (focus != null)
            {
                focus.FocusChanged -= OnFocusChanged;
            }
            SetCurrent(null);
        }

        // Runs on first use as well as in Awake: a tool on the same object may ask
        // for the target in its own Awake or OnEnable before this one's Awake.
        private void Initialize()
        {
            if (initialized)
            {
                return;
            }
            initialized = true;

            if (focus != null)
            {
                focus.FocusChanged += OnFocusChanged;
                SetCurrent(focus.FocusedRenderer);
            }
            else
            {
                SetCurrent(GetComponentInParent<ChunkView>());
            }
        }

        private void OnFocusChanged(Vector3Int? coord)
        {
            SetCurrent(focus.FocusedRenderer);
        }

        private void SetCurrent(IRenderedChunk chunk)
        {
            if (chunk == current)
            {
                return;
            }

            if (current != null)
            {
                current.MeshRebuilt -= OnMeshRebuilt;
                current.ChunkChanged -= OnChunkChanged;
            }
            current = chunk;
            if (current != null)
            {
                current.MeshRebuilt += OnMeshRebuilt;
                current.ChunkChanged += OnChunkChanged;

                // Sit on the followed chunk, so tools drawing in local space draw on it.
                if (focus != null)
                {
                    transform.SetPositionAndRotation(current.Transform.position, current.Transform.rotation);
                }
            }
            Changed?.Invoke();
        }

        private void OnMeshRebuilt(Mesh mesh)
        {
            MeshRebuilt?.Invoke(mesh);
        }

        // A pooled world chunk is being reused for another coordinate: it's no longer the focused chunk.
        private void OnChunkChanged(Chunk chunk)
        {
            if (focus != null)
            {
                SetCurrent(focus.FocusedRenderer);
                return;
            }
            Changed?.Invoke();
        }
    }
}
