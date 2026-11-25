using System;
using System.Collections.Generic;
using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// Something that plies one road of the map by its <see cref="CartRun"/> timetable (GL616, GL868): a cart
    /// (<see cref="RoadCart"/>) or a sherpa on foot (<see cref="Sherpa"/>). <see cref="MapCarts"/> says where along the
    /// road it is and what it carries; this is how it looks and moves there, and what it carries the goods in.
    /// </summary>
    public abstract class RoadTraveller : MonoBehaviour
    {
        public CartRun Run { get; protected set; }

        /// <summary>GL725: how much of it is still here on its way off the map.</summary>
        public OffMapPresence Presence { get; protected set; }

        /// <summary>What its goods ride in: a cart's crates, a sherpa's pack.</summary>
        public abstract IReadOnlyList<StorageChest> Crates { get; }

        /// <summary>GL729: the player took goods from it, and it counts as theft (for a sherpa, only when noticed).</summary>
        public event Action<IReadOnlyList<(ItemDefinition Item, float Amount)>> Robbed;

        /// <summary>Builds its looks and containers for its run.</summary>
        public abstract void Build(CartRun run, TravellerKit kit);

        /// <summary>
        /// Puts it <paramref name="along"/> metres down its road (and on past its end, <paramref name="beyond"/>, for a
        /// road that leaves the map, GL725), heading the way it goes.
        /// </summary>
        public abstract void Place(WorldView view, RoadLine line, IReadOnlyList<RouteBridges.Span> bridges, RoadLine beyond, float along, RunPlace state);

        /// <summary>Fills its containers for a trip, once: a few kinds of the goods given, the trip picking which.</summary>
        public abstract void Load(int trip, IReadOnlyList<ItemDefinition> goods, int seed);

        /// <summary>
        /// GL726: at a stop, after <see cref="Place"/>: emptied of what it came with (<paramref name="arrivedTrip"/>,
        /// <paramref name="arrived"/>) and filled with its next load, carried to and from <paramref name="door"/> (world space).
        /// </summary>
        public abstract void Stop(StopPlace place, int arrivedTrip, IReadOnlyList<ItemDefinition> arrived, int nextTrip, IReadOnlyList<ItemDefinition> next,
            int seed, Vector3 door);

        // The road's surface at a distance along it, in the world's own metres: a bridge's deck, else the terrain, else the line;
        // past the end of a road that leaves the map, the land on the way on (GL725).
        protected static Vector3 Surface(WorldView view, RoadLine line, IReadOnlyList<RouteBridges.Span> bridges, RoadLine beyond, float distance)
        {
            if (beyond != null && distance > line.Length)
            {
                distance -= line.Length;
                line = beyond;
                bridges = null;
            }
            distance = Mathf.Clamp(distance, 0f, line.Length);
            Vector2 flat = line.Flat(distance);
            if (bridges != null)
            {
                for (int s = 0; s < bridges.Count; s++)
                {
                    RouteBridges.Span span = bridges[s];
                    if (distance >= span.Start && distance <= span.End)
                    {
                        float t = Mathf.InverseLerp(span.Start, span.End, distance);
                        return new Vector3(flat.x, span.HeightAt(t), flat.y);
                    }
                }
            }
            float height = line.HeightAlong(distance);
            if (GroundProbe.TryGround(view, flat.x, flat.y, height + 0.8f, out float ground))
            {
                height = ground;
            }
            return new Vector3(flat.x, height, flat.y);
        }

        protected void ReportRobbed(IReadOnlyList<(ItemDefinition Item, float Amount)> taken)
        {
            Robbed?.Invoke(taken);
        }
    }

    /// <summary>What <see cref="MapCarts"/> hands a <see cref="RoadTraveller"/> to build it with.</summary>
    public sealed class TravellerKit
    {
        /// <summary>The low-poly vertex-colour material.</summary>
        public Material Material;

        /// <summary>The wooden crate (a storage chest) cart goods ride in.</summary>
        public GameObject CratePrefab;

        /// <summary>The map's price list (GL868: what a sherpa's goods are worth).</summary>
        public PriceList Prices;

        /// <summary>GL868: how the traveller feels about the player, kept by <see cref="MapCarts"/> for its run.</summary>
        public Disposition Disposition;

        /// <summary>GL868: holds the traveller where it is for some real seconds (it stops to trade).</summary>
        public Action<float> Hold;
    }
}
