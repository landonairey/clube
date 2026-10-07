using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// Drops a hand-placed object onto the terrain once the ground under it has loaded, so
    /// stations placed in a scene (GL10, GL11) stand on the surface whatever the generator
    /// made there. Keeps its x and z; only its height changes, once.
    /// </summary>
    public class SnapToGround : MonoBehaviour
    {
        [SerializeField]
        private WorldView worldView;

        [Tooltip("Lifted this far above the surface, in metres (e.g. half its height for a centred cube).")]
        [SerializeField]
        private float offset;

        [Tooltip("The ray down starts this far above the object, in metres.")]
        [SerializeField, Min(1f)]
        private float searchHeight = 60f;

        public bool IsPlaced { get; private set; }

        private void Update()
        {
            if (IsPlaced || worldView == null || !worldView.IsReady || !worldView.HasGround(transform.position))
            {
                return;
            }

            Vector3 from = transform.position + Vector3.up * searchHeight;
            if (worldView.Raycast(new Ray(from, Vector3.down), out Vector3 ground))
            {
                transform.position = ground + Vector3.up * offset;
                IsPlaced = true;
                enabled = false;
            }
        }
    }
}
