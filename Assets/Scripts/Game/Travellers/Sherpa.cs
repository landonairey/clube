using System.Collections.Generic;
using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// A travelling sherpa (GL868): "traveling sherpas that come into towns on footpaths to trade goods. You can steal
    /// from their backpacks that they are wearing." Walks a footpath by its <see cref="CartRun"/> timetable like a cart,
    /// into the town at each end to the shop that takes their goods, with a big pack (<see cref="SherpaPack"/>) on their
    /// back that holds the load of the trip. They trade (<see cref="SherpaTrader"/>) from that pack, and the pack can be
    /// opened from behind: a theft they notice (<see cref="SherpaTrade.Notices"/>) costs their trust and is reported like
    /// one from a cart.
    /// </summary>
    public class Sherpa : RoadTraveller
    {
        // How far ahead along the road the facing is taken from.
        private const float LookAhead = 0.8f;

        // The pack's build quality, which sets its slots.
        private const float PackQuality = 0.4f;

        private readonly List<StorageChest> packs = new List<StorageChest>(1);
        private FigureRig figure;
        private CrateCargo cargo;
        private SherpaTrader trader;
        private TravellerKit kit;

        public override IReadOnlyList<StorageChest> Crates => packs;

        /// <summary>The pack on their back.</summary>
        public StorageChest Pack => packs.Count > 0 ? packs[0] : null;

        /// <summary>True while they are walking (not standing at a stop or held).</summary>
        public bool Walking { get; private set; }

        public override void Build(CartRun run, TravellerKit kit)
        {
            Run = run;
            this.kit = kit;
            int seed = run.Index * 7919 + 5;
            figure = gameObject.AddComponent<FigureRig>();
            figure.Build(FigureLooks.Varied(FigurePreset.Sherpa, seed), kit.Material, seed);
            figure.Speed = run.Kind.Speed;
            StorageChest pack = SherpaPack.Build(figure.Torso, kit.Material, PackQuality);
            packs.Add(pack);
            cargo = new CrateCargo(packs);
            pack.gameObject.AddComponent<CargoCrate>().Taken += Taken;
            trader = gameObject.AddComponent<SherpaTrader>();
            trader.Setup(kit.Prices, pack.Storage, kit.Disposition, kit.Hold);
            Presence = gameObject.AddComponent<OffMapPresence>();
        }

        public override void Place(WorldView view, RoadLine line, IReadOnlyList<RouteBridges.Span> bridges, RoadLine beyond, float along, RunPlace state)
        {
            float total = line.Length + (beyond != null ? beyond.Length : 0f);
            float distance = Mathf.Clamp(along, Mathf.Min(0.5f, total * 0.5f), beyond != null ? total : Mathf.Max(total - 0.5f, total * 0.5f));
            float ahead = state.Outbound ? 1f : -1f;
            Transform world = view.transform;
            Vector3 at = world.TransformPoint(Surface(view, line, bridges, beyond, distance));
            Vector3 next = world.TransformPoint(Surface(view, line, bridges, beyond, distance + ahead * LookAhead));
            Vector3 heading = next - at;
            heading.y = 0f;
            transform.position = at;
            if (heading.sqrMagnitude > 1e-4f)
            {
                transform.rotation = Quaternion.LookRotation(heading);
            }
            Walking = state.Moving;
            figure.Pose = state.Moving ? FigureAction.Walk : FigureAction.Stand;
        }

        public override void Load(int trip, IReadOnlyList<ItemDefinition> goods, int seed)
        {
            cargo.Show(0, cargo.Count, trip, goods, CargoSeed(seed));
        }

        public override void Stop(StopPlace place, int arrivedTrip, IReadOnlyList<ItemDefinition> arrived, int nextTrip, IReadOnlyList<ItemDefinition> next,
            int seed, Vector3 door)
        {
            // At the shop the pack is emptied of what they brought and filled with what they take on.
            TradeStop.CratesHeld(place, cargo.Count, out int first, out int last, out bool came);
            cargo.Show(first, last, came ? arrivedTrip : nextTrip, came ? arrived : next, CargoSeed(seed));
        }

        private int CargoSeed(int seed)
        {
            return unchecked(seed * 31 + Run.Index * 977);
        }

        // The player closed the pack having taken goods: did the sherpa see?
        private void Taken(IReadOnlyList<(ItemDefinition Item, float Amount)> taken)
        {
            Camera view = Camera.main;
            Vector3 toThief = view != null ? view.transform.position - transform.position : Vector3.zero;
            bool noticed = SherpaTrade.Notices(Walking && !trader.IsHeldToTrade, new Vector2(transform.forward.x, transform.forward.z),
                new Vector2(toThief.x, toThief.z));
            ToolHint hint = FindAnyObjectByType<ToolHint>();
            if (!noticed)
            {
                hint?.Show("The sherpa walks on, none the wiser");
                return;
            }
            float coins = 0f;
            foreach ((ItemDefinition item, float amount) in taken)
            {
                coins += Theft.Worth(kit.Prices, item, amount);
            }
            kit.Disposition?.Lower(SherpaTrade.OwnPointsFor(coins));
            // The shop the goods were for hears of it, as for a cart (GL729).
            ReportRobbed(taken);
            hint?.Show(SherpaTrade.Trades(kit.Disposition != null ? kit.Disposition.Value : 0)
                ? "The sherpa saw you! Their prices go up, and the town will hear of it"
                : "The sherpa saw you! They won't trade with a thief, and the town will hear of it");
        }
    }
}
