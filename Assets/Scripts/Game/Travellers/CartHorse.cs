using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// The horse between a cart's shafts (GL616): the animals' low-poly body (<see cref="AnimalModels"/>, the "horse"
    /// row of <see cref="AnimalLooks"/>) with no brain of its own. <see cref="RoadCart"/> puts it on the road each
    /// frame; its legs step with the distance it has gone.
    /// </summary>
    public class CartHorse : MonoBehaviour
    {
        // Not one of the wild species (nothing spawns or hunts it): only the size its body is built to.
        private static readonly AnimalSpecies Species = new AnimalSpecies { Id = "horse", Name = "Horse", Length = 2.2f, ShoulderHeight = 1.5f };

        private AnimalRig rig;
        private float stride;
        private float lastDistance = float.NaN;

        /// <summary>Metres from the horse's middle to its nose, for whoever leads it.</summary>
        public static float HalfLength => Species.Length * 0.5f;

        public void Build(Material material)
        {
            rig = AnimalModels.Build(transform, Species, material);
            rig.Pose(0f, 0f, 0f, 0f, 0f, 0f);
        }

        /// <summary>Stands the horse at a place (scene space) facing along <paramref name="forward"/>; <paramref name="distance"/> is how far along its road it is.</summary>
        public void Place(Vector3 at, Vector3 forward, bool moving, float distance)
        {
            transform.position = at;
            if (forward.sqrMagnitude > 1e-6f)
            {
                transform.rotation = Quaternion.LookRotation(forward);
            }
            if (!float.IsNaN(lastDistance))
            {
                stride += Mathf.Abs(distance - lastDistance) * Mathf.PI * 2f / (Species.ShoulderHeight * 1.8f);
            }
            lastDistance = distance;
            rig.Pose(stride, moving ? 22f : 0f, 0f, 0f, 0f, 0f);
        }
    }
}
