using System.Collections.Generic;
using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// One vessel on the water (GL618): the hull of its <see cref="VesselClass"/> (<see cref="BoatMeshes"/>), its hands
    /// on deck and its crates, which are real storage loaded for each trip as a cart's are. Whoever sails it
    /// (<see cref="MapBoats"/> by the timetable, a thieves' crew by its raid) says where it floats and which way it
    /// heads; this turns it there, sets it on the water and rocks it. Looking at it reads its name and business.
    /// </summary>
    public class Boat : MonoBehaviour, INoticeOnly
    {
        private const float TurnDegreesPerSecond = 35f;

        private readonly List<StorageChest> crates = new List<StorageChest>();
        private readonly List<FigureRig> crew = new List<FigureRig>();
        private float phase;
        private float heading;
        private bool placed;
        private int loadedTrip = int.MinValue;
        private string notice = string.Empty;

        public VesselClass Class { get; private set; }

        /// <summary>The vessel's own name.</summary>
        public string Name { get; private set; }

        /// <summary>The crates on deck.</summary>
        public IReadOnlyList<StorageChest> Crates => crates;

        /// <summary>The hands aboard, the helmsman first.</summary>
        public IReadOnlyList<FigureRig> Crew => crew;

        /// <summary>The one-way trip its crates were last loaded for.</summary>
        public int LoadedTrip => loadedTrip;

        /// <summary>The height of the water it last floated on.</summary>
        public float Surface { get; set; }

        /// <summary>Where it floats, in the world's own metres (x, z).</summary>
        public Vector2 Flat => new Vector2(transform.localPosition.x, transform.localPosition.z);

        public string Prompt => notice;

        public void Interact(PlayerInteractor by)
        {
        }

        /// <summary>What looking at the vessel reads.</summary>
        public void Describe(string text)
        {
            notice = text ?? string.Empty;
        }

        /// <summary>Builds the hull, the crates (from <paramref name="cratePrefab"/>) and the hands, dressed as <paramref name="crewLook"/>.</summary>
        public void Build(VesselClass vessel, string vesselName, Material material, GameObject cratePrefab, FigurePreset crewLook, int seed)
        {
            Class = vessel;
            Name = vesselName;
            phase = (seed & 0xff) * 0.37f;
            gameObject.AddComponent<MeshFilter>().sharedMesh = BoatMeshes.Build(vessel);
            gameObject.AddComponent<MeshRenderer>().sharedMaterial = material;
            // What the view lands on, and what feet find: the hull up to its deck.
            float deck = BoatMeshes.DeckHeight(vessel);
            var hull = gameObject.AddComponent<BoxCollider>();
            hull.center = new Vector3(0f, (deck - vessel.Draught) * 0.5f, -0.04f * vessel.Length);
            hull.size = new Vector3(vessel.Beam * 0.8f, deck + vessel.Draught, vessel.Length * 0.84f);
            float crateSide = Mathf.Min(TradeCartModel.CrateSize, vessel.Beam * 0.5f);
            if (cratePrefab != null)
            {
                foreach (Vector3 spot in BoatMeshes.CrateSpots(vessel, crateSide))
                {
                    GameObject crate = Instantiate(cratePrefab, transform);
                    crate.name = "Deck crate";
                    crate.transform.localPosition = spot + Vector3.up * 0.02f;
                    TradeCartModel.Fit(crate, crateSide);
                    if (crate.TryGetComponent(out PlacedObject placedObject))
                    {
                        placedObject.Fixed = true;
                        placedObject.Item = null;
                    }
                    if (crate.TryGetComponent(out StorageChest chest))
                    {
                        chest.MadeOf(1f);
                        crates.Add(chest);
                    }
                }
            }
            bool seated = BoatMeshes.IsOpen(vessel);
            List<Vector3> spots = BoatMeshes.CrewSpots(vessel);
            for (int i = 0; i < spots.Count; i++)
            {
                var hand = new GameObject(i == 0 ? "Helmsman" : "Deckhand");
                hand.transform.SetParent(transform, false);
                // Seated hands are on a thwart; the figure's own sitting pose lowers its hips.
                hand.transform.localPosition = spots[i] + (seated ? Vector3.up * (vessel.Freeboard * 0.72f - spots[i].y - 0.42f) : Vector3.zero);
                hand.transform.localRotation = Quaternion.Euler(0f, i == 0 ? 0f : (i % 2 == 0 ? -70f : 70f), 0f);
                FigureRig figure = hand.AddComponent<FigureRig>();
                figure.Build(crewLook, material, seed * 31 + i * 17);
                figure.Pose = seated ? FigureAction.Sit : FigureAction.Stand;
                crew.Add(figure);
            }
        }

        /// <summary>Fills the crates for a trip, once: a few kinds of the goods given, the trip picking which.</summary>
        public void Load(int trip, IReadOnlyList<ItemDefinition> goods, int seed)
        {
            if (trip == loadedTrip)
            {
                return;
            }
            loadedTrip = trip;
            CargoLoad.Fill(crates, goods, new System.Random(unchecked(seed * 31 + trip * 131 + (int)(phase * 100f))));
        }

        /// <summary>
        /// Floats the vessel at (x, z) on water whose surface is at <paramref name="surface"/>, heading along
        /// <paramref name="towards"/> (it comes round to a new heading at its own pace), all in the world's own metres.
        /// </summary>
        public void Float(Vector2 at, float surface, Vector2 towards, bool moving)
        {
            if (towards.sqrMagnitude > 1e-6f)
            {
                float wanted = Mathf.Atan2(towards.x, towards.y) * Mathf.Rad2Deg;
                heading = placed ? Mathf.MoveTowardsAngle(heading, wanted, TurnDegreesPerSecond * Time.deltaTime) : wanted;
            }
            placed = true;
            Surface = surface;
            float time = Time.time + phase;
            float sway = moving ? 1f : 0.5f;
            float bob = Mathf.Sin(time * 0.9f) * Class.Bob * sway;
            float roll = Mathf.Sin(time * 0.7f) * Class.Roll * sway, pitch = Mathf.Sin(time * 0.53f + 1.3f) * Class.Roll * 0.4f * sway;
            transform.localPosition = new Vector3(at.x, surface + bob, at.y);
            transform.localRotation = Quaternion.Euler(0f, heading, 0f) * Quaternion.Euler(pitch, 0f, roll);
        }
    }
}
