using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// A torch standing on the ground (GL287): lit as it's placed, burning by game time on from what it had already burned in
    /// the hand (<see cref="PlacedObject.Wear"/>), its light flickering over the flame, and gone once it has burned out.
    /// Picked back up, it's a lit torch with the burning it has left.
    /// </summary>
    [RequireComponent(typeof(PlacedObject))]
    public class PlacedTorch : MonoBehaviour
    {
        [Tooltip("Where the flame is, above the torch's foot.")]
        [SerializeField]
        private Vector3 flameAt = new Vector3(0f, 1.15f, 0f);

        [Tooltip("The smoke rising from the flame (the Drying steam material, tinted grey).")]
        [SerializeField]
        private Material smokeMaterial;

        [Tooltip("The flame's material (Embers: glowing); it flickers (GL304).")]
        [SerializeField]
        private Material flameMaterial;

        private PlacedObject placed;
        private TorchFlame flame;
        private GameClockTicks ticks;

        private void Awake()
        {
            placed = GetComponent<PlacedObject>();
            ticks = new GameClockTicks(FindAnyObjectByType<GameClock>());
            flame = new TorchFlame(transform, flameAt, 18f, 6f, smokeMaterial, flameMaterial);
        }

        private void Update()
        {
            if (!(placed.Item is TorchDefinition torch))
            {
                return;
            }
            placed.Wear += ticks.WholeSeconds();
            if (placed.Wear >= torch.LifeSeconds)
            {
                Destroy(gameObject);
                return;
            }
            flame.Flicker(Mathf.Lerp(0.55f, 1f, (1f - placed.Wear / torch.LifeSeconds) * 4f));
        }
    }
}
