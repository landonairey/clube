using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// What every thieving duty shares (GL617). A thief who carries anything takes it where his duty's
    /// <see cref="Duty.LootTo"/> says (the camp's stash) before all else; one who carries nothing and notices the player
    /// (<see cref="Duty.Notice"/>) goes for the player and takes from bag and purse; otherwise he is after the prey of
    /// his own duty, which the works built on this say: a cart (<see cref="WaylayWork"/>), a town's shop
    /// (<see cref="BurgleWork"/>), or nothing but the player (<see cref="RobWork"/>).
    /// </summary>
    public abstract class ThiefWork : IDutyWork
    {
        /// <summary>Metres within which a thief can lay hands on what he is after.</summary>
        protected const float Reach = 2.6f;

        private enum Aim
        {
            Prey,
            Player,
            Stash,
        }

        private Aim aim;

        public float Pace => aim == Aim.Player ? 2.3f : aim == Aim.Stash ? 1.3f : 1.15f;

        public bool Seek(Villager worker, out Vector3 stand, out Vector3 face)
        {
            stand = face = default;
            aim = Aim.Prey;
            ThievesCamps thieves = worker.People.Thieves;
            ThievesCamp camp = thieves != null ? thieves.CampOf(worker.Site.Settlement.Index) : null;
            if (camp == null)
            {
                return false;
            }
            if (worker.IsLoaded)
            {
                if (worker.Duty.LootTo != Duty.Stash)
                {
                    return false;
                }
                aim = Aim.Stash;
                Vector2 spot = thieves.StashSpot(worker.Site);
                stand = new Vector3(spot.x, worker.People.Ground(spot.x, spot.y), spot.y);
                Vector2 hut = worker.Site.Buildings.Count > 0 ? worker.Site.Buildings[0].Centre : spot + Vector2.up;
                face = new Vector3(hut.x, stand.y, hut.y);
                return true;
            }
            if (thieves.TryPlayerInReach(worker, camp, worker.Duty.Notice, out Vector3 player))
            {
                aim = Aim.Player;
                // An arm's length short of the player, not on top of them.
                Vector3 away = worker.At - player;
                away.y = 0f;
                stand = player + (away.sqrMagnitude > 0.01f ? away.normalized : Vector3.forward) * 1.3f;
                face = player;
                return true;
            }
            return SeekPrey(worker, thieves, camp, out stand, out face);
        }

        public void Stroke(Villager worker)
        {
            ThievesCamps thieves = worker.People.Thieves;
            ThievesCamp camp = thieves != null ? thieves.CampOf(worker.Site.Settlement.Index) : null;
            if (camp == null)
            {
                return;
            }
            switch (aim)
            {
                case Aim.Stash:
                    thieves.StashLoot(worker, camp);
                    break;
                case Aim.Player:
                    thieves.RobPlayer(worker, camp, Reach);
                    break;
                default:
                    StrokePrey(worker, thieves, camp);
                    break;
            }
        }

        public bool TryPost(Villager worker, out Vector2 post)
        {
            post = default;
            ThievesCamps thieves = worker.People.Thieves;
            ThievesCamp camp = thieves != null ? thieves.CampOf(worker.Site.Settlement.Index) : null;
            if (camp == null || !TryPostOf(camp, out post))
            {
                return false;
            }
            post = Spread(camp, post, worker.Resident.Index);
            return true;
        }

        /// <summary>The one far place the duty is done at, for a camp; false when it has none (or the camp no such prey).</summary>
        public virtual bool TryPostOf(ThievesCamp camp, out Vector2 post)
        {
            post = default;
            return false;
        }

        /// <summary>The prey of this duty: where to stand and what to face; false with nothing to go for.</summary>
        protected abstract bool SeekPrey(Villager worker, ThievesCamps thieves, ThievesCamp camp, out Vector3 stand, out Vector3 face);

        /// <summary>A stroke at the prey, standing where <see cref="SeekPrey"/> last said.</summary>
        protected abstract void StrokePrey(Villager worker, ThievesCamps thieves, ThievesCamp camp);

        /// <summary>A place of his own for each of a band about a post: a few metres off it on the camp's side, in a row.</summary>
        protected static Vector2 Spread(ThievesCamp camp, Vector2 post, int index)
        {
            Vector2 back = camp.Settlement.Position - post;
            back = back.sqrMagnitude > 1f ? back.normalized : Vector2.up;
            var along = new Vector2(back.y, -back.x);
            return post + back * 6f + along * ((index % 4 - 1.5f) * 2.2f);
        }

        protected static Vector3 OnGround(Villager worker, Vector2 spot)
        {
            return new Vector3(spot.x, worker.People.Ground(spot.x, spot.y), spot.y);
        }
    }

    /// <summary>
    /// "Hijack cart paths, trading paths" (GL617): the band lies in wait by the stretch of cart path nearest its camp
    /// (<see cref="ThiefHaunts.TryAmbush"/>). A cart that comes by with anything in its crates is stopped
    /// (<see cref="MapCarts.HoldFor"/>) and its crates emptied, stack by stack, into the thieves' sacks.
    /// </summary>
    public sealed class WaylayWork : ThiefWork
    {
        private const float Sight = 55f, StopWithin = 16f;

        private RoadCart cart;

        public override bool TryPostOf(ThievesCamp camp, out Vector2 post)
        {
            post = camp.Ambush;
            return camp.HasAmbush;
        }

        protected override bool SeekPrey(Villager worker, ThievesCamps thieves, ThievesCamp camp, out Vector3 stand, out Vector3 face)
        {
            stand = face = default;
            if (!camp.HasAmbush)
            {
                return false;
            }
            cart = thieves.LoadedCartNear(camp, Sight);
            if (cart != null)
            {
                Vector3 at = worker.People.ToLocal(cart.transform.position);
                stand = worker.People.ToLocal(cart.transform.position + cart.transform.right * 1.3f);
                face = at;
                if ((worker.At - at).magnitude < StopWithin)
                {
                    thieves.Carts.HoldFor(cart.Run.Index, 6f);
                }
                return true;
            }
            stand = OnGround(worker, Spread(camp, camp.Ambush, worker.Resident.Index));
            face = OnGround(worker, camp.Ambush);
            return true;
        }

        protected override void StrokePrey(Villager worker, ThievesCamps thieves, ThievesCamp camp)
        {
            if (cart == null || (worker.At - worker.People.ToLocal(cart.transform.position)).magnitude > Reach + 1.5f)
            {
                return;
            }
            thieves.Carts.HoldFor(cart.Run.Index, 6f);
            foreach (StorageChest crate in cart.Crates)
            {
                if (crate != null && !Robbery.IsEmpty(crate.Storage))
                {
                    int taken = Robbery.Take(crate.Storage, worker.Sack, worker.Duty.Stacks, worker.Random);
                    worker.Carry(taken);
                    camp.CartsRobbed += taken > 0 ? 1 : 0;
                    return;
                }
            }
        }
    }

    /// <summary>
    /// "They also steal from towns" (GL617): by night the band goes to the nearest honest settlement
    /// (<see cref="ThiefHaunts.Mark"/>), to the front of a shop, and takes a share of its keeper's coins and some of
    /// what it sells.
    /// </summary>
    public sealed class BurgleWork : ThiefWork
    {
        private SettlementKeeper keeper;

        public override bool TryPostOf(ThievesCamp camp, out Vector2 post)
        {
            post = default;
            if (camp.Mark == null)
            {
                return false;
            }
            Vector2 back = (camp.Settlement.Position - camp.Mark.Position).normalized;
            post = camp.Mark.Position + back * 40f;
            return true;
        }

        protected override bool SeekPrey(Villager worker, ThievesCamps thieves, ThievesCamp camp, out Vector3 stand, out Vector3 face)
        {
            stand = face = default;
            if (!TryPostOf(camp, out Vector2 post))
            {
                return false;
            }
            keeper = null;
            if (thieves.TryShopOf(camp.Mark, worker.Resident.Index, out Vector2 front, out Vector2 door, out SettlementKeeper its))
            {
                keeper = its;
                stand = OnGround(worker, front);
                face = OnGround(worker, door);
                return true;
            }
            // The town does not stand in the world (nobody near it): they wait at its edge.
            stand = OnGround(worker, Spread(camp, post, worker.Resident.Index));
            face = OnGround(worker, camp.Mark.Position);
            return true;
        }

        protected override void StrokePrey(Villager worker, ThievesCamps thieves, ThievesCamp camp)
        {
            if (keeper == null)
            {
                return;
            }
            float coins = Robbery.TakeCoins(keeper.Purse, worker.Purse, worker.Duty.Share, 80f);
            int goods = 0;
            for (int n = 0; n < worker.Duty.Stacks && keeper.Offers.Count > 0; n++)
            {
                ItemDefinition item = keeper.Offers[worker.Random.Next(keeper.Offers.Count)].Item;
                int count = 2 + worker.Random.Next(5);
                goods += item != null ? count - worker.Sack.Add(item, count) : 0;
            }
            worker.Carry(goods);
            if (coins > 0f || goods > 0)
            {
                camp.TownsRobbed++;
                thieves.Heard(worker, $"Thieves have been at {keeper.DisplayName} in {camp.Mark.Name}!");
            }
        }
    }

    /// <summary>"And they steal from the player" (GL617): about the camp with nothing else to do, the band has only the player to go for.</summary>
    public sealed class RobWork : ThiefWork
    {
        protected override bool SeekPrey(Villager worker, ThievesCamps thieves, ThievesCamp camp, out Vector3 stand, out Vector3 face)
        {
            stand = face = default;
            return false;
        }

        protected override void StrokePrey(Villager worker, ThievesCamps thieves, ThievesCamp camp)
        {
        }
    }
}
