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
    public class RoadCart : MonoBehaviour
    {
        // Metres ahead of the axle the horse's middle is, and the carter leading it.
        private const float HorseAhead = 2.5f;

        private TradeCartModel model;
        private CartHorse horse;
        private int loadedTrip = int.MinValue;

        public CartRun Run { get; private set; }

        /// <summary>The crates on the bed.</summary>
        public IReadOnlyList<StorageChest> Crates => model.Crates;

        /// <summary>The one-way trip its crates were last loaded for.</summary>
        public int LoadedTrip => loadedTrip;

        public void Build(CartRun run, Material material, GameObject cratePrefab)
        {
            Run = run;
            int seed = run.Index * 7919 + 5;
            model = gameObject.AddComponent<TradeCartModel>();
            model.Build(material, cratePrefab, seed);
            if (run.Kind.HorseDrawn)
            {
                var body = new GameObject("Horse");
                body.transform.SetParent(transform.parent, false);
                horse = body.AddComponent<CartHorse>();
                horse.Build(material);
            }
        }

        private void OnDestroy()
        {
            if (horse != null)
            {
                Destroy(horse.gameObject);
            }
        }

        /// <summary>Puts the cart where its run has it on the road, heading the way it goes.</summary>
        public void Place(WorldView view, RoadLine line, IReadOnlyList<RouteBridges.Span> bridges, CartPlace state)
        {
            float distance = state.Fraction * line.Length;
            float ahead = state.Outbound ? 1f : -1f;
            Transform world = view.transform;
            Vector3 axle = world.TransformPoint(Surface(view, line, bridges, distance));
            Vector3 front = Surface(view, line, bridges, distance + ahead * TradeCartModel.TiltReach);
            Vector3 back = Surface(view, line, bridges, distance - ahead * TradeCartModel.TiltReach);
            float lead = horse != null ? HorseAhead + CartHorse.HalfLength + 0.5f : TradeCartModel.CarterAhead;
            Vector3 carter = world.TransformPoint(Surface(view, line, bridges, distance + ahead * lead));
            model.PlaceAt(axle, carter, world.TransformDirection(front - back), 0f, state.Moving, distance);
            if (horse != null)
            {
                Vector3 at = world.TransformPoint(Surface(view, line, bridges, distance + ahead * HorseAhead));
                horse.Place(at, carter - axle, state.Moving, distance);
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
            var random = new System.Random(unchecked(seed * 31 + Run.Index * 977 + trip * 131));
            foreach (StorageChest crate in model.Crates)
            {
                if (crate.Storage == null)
                {
                    continue;
                }
                StorageText.Fill(crate.Storage, "");
                for (int k = 0; k < 3 && goods.Count > 0; k++)
                {
                    crate.Storage.Add(goods[random.Next(goods.Count)], 2 + random.Next(7));
                }
            }
        }

        // The road's surface at a distance along it, in the world's own metres: a bridge's deck, else the terrain, else the line.
        private static Vector3 Surface(WorldView view, RoadLine line, IReadOnlyList<RouteBridges.Span> bridges, float distance)
        {
            distance = Mathf.Clamp(distance, 0f, line.Length);
            Vector2 flat = line.Flat(distance);
            if (bridges != null)
            {
                foreach (RouteBridges.Span span in bridges)
                {
                    if (distance >= span.Start && distance <= span.End)
                    {
                        float t = Mathf.InverseLerp(span.Start, span.End, distance);
                        return new Vector3(flat.x, Mathf.Lerp(span.FromHeight, span.ToHeight, t), flat.y);
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
    }
}
