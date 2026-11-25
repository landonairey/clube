using System.Collections.Generic;
using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// One cart on one of the map's cart paths (GL616): the trade cart's model (<see cref="TradeCartModel"/>: bed,
    /// wheels, two crates that are real storage, a carter), hand-pulled, or horse-drawn with a <see cref="CartHorse"/>
    /// between the shafts and the carter leading it. <see cref="MapCarts"/> says where along the road it is; this puts
    /// it on the ground there (on the deck over a bridge) and loads its crates for each trip.
    /// </summary>
    public class RoadCart : RoadTraveller
    {
        // Metres ahead of the axle the horse's middle is, and the carter leading it.
        private const float HorseAhead = 2.5f;

        private TradeCartModel model;
        private CartHorse horse;
        private CrateCargo cargo;
        private Material material;
        private int loadedTrip = int.MinValue;
        private bool porting;

        /// <summary>The crates on the bed.</summary>
        public override IReadOnlyList<StorageChest> Crates => model.Crates;

        /// <summary>The one-way trip its crates were last loaded for.</summary>
        public int LoadedTrip => loadedTrip;

        public override void Build(CartRun run, TravellerKit kit)
        {
            Run = run;
            int seed = run.Index * 7919 + 5;
            material = kit.Material;
            model = gameObject.AddComponent<TradeCartModel>();
            model.Build(material, kit.CratePrefab, seed);
            cargo = new CrateCargo(model.Crates);
            foreach (StorageChest crate in model.Crates)
            {
                crate.gameObject.AddComponent<CargoCrate>().Taken += ReportRobbed;
            }
            if (run.Kind.HorseDrawn)
            {
                var body = new GameObject("Horse");
                body.transform.SetParent(transform.parent, false);
                horse = body.AddComponent<CartHorse>();
                horse.Build(material);
            }
            gameObject.AddComponent<Rideable>(); // GL727: a player who jumps on the bed rides along
            Presence = gameObject.AddComponent<OffMapPresence>();
            if (horse != null)
            {
                Presence.Include(horse.gameObject);
            }
        }

        private void OnDestroy()
        {
            if (horse != null)
            {
                Destroy(horse.gameObject);
            }
        }

        /// <summary>
        /// Puts the cart <paramref name="along"/> metres down its road (and on past its end, <paramref name="beyond"/>, for a
        /// road that leaves the map, GL725), heading the way it goes.
        /// </summary>
        public override void Place(WorldView view, RoadLine line, IReadOnlyList<RouteBridges.Span> bridges, RoadLine beyond, float along, RunPlace state)
        {
            // It stops short of each end by its own length, so whoever pulls it is still on the road; off the map's far end it is gone.
            float lead = horse != null ? HorseAhead + CartHorse.HalfLength + 0.5f : TradeCartModel.CarterAhead;
            float total = line.Length + (beyond != null ? beyond.Length : 0f);
            float distance = Mathf.Clamp(along, Mathf.Min(lead + 0.5f, total * 0.5f), beyond != null ? total : Mathf.Max(total - lead - 0.5f, total * 0.5f));
            float ahead = state.Outbound ? 1f : -1f;
            Transform world = view.transform;
            Vector3 axle = world.TransformPoint(Surface(view, line, bridges, beyond, distance));
            Vector3 front = Surface(view, line, bridges, beyond, distance + ahead * TradeCartModel.TiltReach);
            Vector3 back = Surface(view, line, bridges, beyond, distance - ahead * TradeCartModel.TiltReach);
            Vector3 carter = world.TransformPoint(Surface(view, line, bridges, beyond, distance + ahead * lead));
            model.PlaceAt(axle, carter, world.TransformDirection(front - back), 0f, state.Moving, distance);
            if (horse != null)
            {
                Vector3 at = world.TransformPoint(Surface(view, line, bridges, beyond, distance + ahead * HorseAhead));
                horse.Place(at, carter - axle, state.Moving, distance);
            }
        }

        /// <summary>Fills the crates for a trip, once: a few kinds of the goods given, the trip picking which.</summary>
        public override void Load(int trip, IReadOnlyList<ItemDefinition> goods, int seed)
        {
            loadedTrip = trip;
            cargo.Show(0, cargo.Count, trip, goods, CargoSeed(seed));
            if (porting)
            {
                porting = false;
                Porter.PutDown(model.Carter);
            }
        }

        /// <summary>
        /// GL726: the cart at a stop, after <see cref="Place"/>: crates emptied of what it came with
        /// (<paramref name="arrivedTrip"/>, <paramref name="arrived"/>) and filled with its next load one by one, the
        /// carter carrying each between the cart's tail and <paramref name="door"/> (world space) while it stands.
        /// </summary>
        public override void Stop(StopPlace place, int arrivedTrip, IReadOnlyList<ItemDefinition> arrived, int nextTrip, IReadOnlyList<ItemDefinition> next,
            int seed, Vector3 door)
        {
            TradeStop.CratesHeld(place, cargo.Count, out int first, out int last, out bool came);
            loadedTrip = came ? arrivedTrip : nextTrip;
            cargo.Show(first, last, loadedTrip, came ? arrived : next, CargoSeed(seed));
            bool working = place.Phase == StopPhase.Unloading || place.Phase == StopPhase.Loading;
            if (working && model.Carter != null)
            {
                porting = true;
                Vector3 tail = transform.position - transform.forward * 1.5f;
                Porter.Carry(model.Carter, tail, door, place.Progress, Mathf.Max(1, cargo.Count), place.Phase == StopPhase.Unloading, material);
            }
            else if (porting)
            {
                porting = false;
                Porter.PutDown(model.Carter);
            }
        }

        private int CargoSeed(int seed)
        {
            return unchecked(seed * 31 + Run.Index * 977);
        }
    }
}
