using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// Footsteps: a step sound every <see cref="stepLength"/> metres walked on the ground, picked
    /// by the material under the player's feet (<see cref="PlayerSounds.StepsOn"/>), and one on
    /// landing from a fall.
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public class PlayerFootsteps : MonoBehaviour
    {
        [SerializeField]
        private WorldView worldView;

        [SerializeField]
        private PlayerSounds sounds;

        [Tooltip("Plays the steps; a 2D source on the player.")]
        [SerializeField]
        private AudioSource source;

        [Tooltip("Metres walked between steps.")]
        [SerializeField, Min(0.1f)]
        private float stepLength = 1.7f;

        [Tooltip("Slower than this (metres per second) makes no steps, so standing still or nudging is silent.")]
        [SerializeField, Min(0f)]
        private float minSpeed = 0.5f;

        private PlayerController player;
        private Vector3 lastPosition;
        private float walked;
        private bool wasGrounded = true;

        private void Awake()
        {
            player = GetComponent<PlayerController>();
            lastPosition = transform.position;
        }

        private void Update()
        {
            Vector3 position = transform.position;
            Vector3 moved = position - lastPosition;
            lastPosition = position;
            bool grounded = player.IsGrounded;
            if (!player.enabled || sounds == null)
            {
                wasGrounded = grounded;
                return;
            }

            if (grounded && !wasGrounded)
            {
                Step();
                walked = 0f;
            }
            wasGrounded = grounded;
            if (!grounded)
            {
                return;
            }

            float distance = new Vector2(moved.x, moved.z).magnitude;
            // A teleport or respawn isn't a walk.
            if (Time.deltaTime <= 0f || distance / Time.deltaTime < minSpeed || distance > stepLength)
            {
                return;
            }
            walked += distance;
            if (walked >= stepLength)
            {
                walked -= stepLength;
                Step();
            }
        }

        private void Step()
        {
            sounds.StepsOn(GroundMaterial()).Play(source);
        }

        // The material of the solid ground just under the feet, or null when it can't be found.
        private VoxelMaterial GroundMaterial()
        {
            if (worldView == null || !worldView.IsReady || worldView.Config.Materials == null)
            {
                return null;
            }
            World world = worldView.World;
            CharacterController body = player.Body;
            Vector3 feet = transform.TransformPoint(body.center) - transform.up * (body.height * 0.5f);
            Vector3Int sample = world.Grid.WorldToNearestSample(worldView.transform.InverseTransformPoint(feet));
            float iso = worldView.Config.IsoLevel;
            // The nearest sample may be air above the surface: look a few below.
            for (int i = 0; i < 3; i++, sample.y--)
            {
                if (world.GetDensity(sample) >= iso && world.GetMaterial(sample) is byte id)
                {
                    return worldView.Config.Materials.Get(id);
                }
            }
            return null;
        }
    }
}
