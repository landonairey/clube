using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// Tells the player how deep in water it is (GL316): each frame, before it moves, how much of its capsule is under the
    /// water in the world's voxel data (<see cref="WaterQuery"/>), which turns swimming on and off
    /// (<see cref="PlayerController.Submersion"/>), and whether the camera is under, for the underwater view.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    [RequireComponent(typeof(PlayerController), typeof(CharacterController))]
    public sealed class PlayerSwimming : MonoBehaviour
    {
        [Tooltip("The world whose water this swims in. Optional: found in the scene.")]
        [SerializeField]
        private WorldView worldView;

        private PlayerController player;
        private CharacterController body;

        /// <summary>True while the camera (the player's head) is under water.</summary>
        public bool HeadUnderwater { get; private set; }

        private void Awake()
        {
            player = GetComponent<PlayerController>();
            body = GetComponent<CharacterController>();
            if (worldView == null)
            {
                worldView = FindAnyObjectByType<WorldView>();
            }
        }

        private void Update()
        {
            World world = worldView != null ? worldView.World : null;
            Vector3 feet = transform.TransformPoint(body.center) - Vector3.up * (body.height * 0.5f);
            player.Submersion = WaterQuery.Submersion(world, feet, body.height);
            Transform head = player.Head != null ? player.Head : transform;
            HeadUnderwater = WaterQuery.IsUnderwater(world, head.position);
        }

        private void OnDisable()
        {
            HeadUnderwater = false;
            if (player != null)
            {
                player.Submersion = 0f;
            }
        }
    }
}
