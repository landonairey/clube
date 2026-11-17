using System.Collections.Generic;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>Where a settlement's horse-hire stand is (GL728): its hitching post, and the way the road leaves there.</summary>
    public sealed class HorseStand
    {
        /// <summary>The settlement (an index into the plan's settlements).</summary>
        public int Settlement;

        /// <summary>The signpost at its edge it stands by (an index into the plan's signposts).</summary>
        public int Signpost;

        /// <summary>The hitching post (x, z).</summary>
        public Vector2 Post;

        /// <summary>The way the road leaves the settlement there (x, z, a unit vector): the horse stands along it.</summary>
        public Vector2 Outward;

        /// <summary>The way from the road to the post (x, z, a unit vector).</summary>
        public Vector2 Roadside;

        /// <summary>Where a rider is set down on arriving here (x, z): on the road side of the post, clear of the horse and its groom.</summary>
        public Vector2 Arrival => Post - Roadside * 3f + Outward * 1.5f;
    }

    /// <summary>
    /// GL728: "There is an NPC with a Horse tied up to a post next to the Town Signs outside the Capital and each City
    /// offering a fast travel option between them for 250c." Every settlement whose type has
    /// <see cref="SettlementType.HorseHire"/> gets one stand, by the name board of its first edge signpost, a little
    /// further from the road than the board; a ride goes to any other stand for <see cref="Fare"/> coins.
    /// </summary>
    public static class HorseHire
    {
        /// <summary>Coins a ride costs, wherever it goes.</summary>
        public const int Fare = 250;

        /// <summary>Metres from the signpost to the hitching post, away from the road (the name board stands at 1.6).</summary>
        public const float FromPost = 4.2f;

        /// <summary>Metres the hitching post stands back along the road from the signpost, into the settlement.</summary>
        public const float Back = 2.5f;

        /// <summary>Metres from a horse town's centre within which a junction post stands in for a missing edge post.</summary>
        public const float FallbackReach = 400f;

        /// <summary>The stands of a plan whose signposts are laid (<see cref="WorldRoads.Finish"/>), in settlement order.</summary>
        public static List<HorseStand> Plan(WorldPlan plan)
        {
            var stands = new List<HorseStand>();
            if (plan == null)
            {
                return stands;
            }
            var done = new HashSet<int>();
            for (int i = 0; i < plan.Signposts.Count; i++)
            {
                PlannedSignpost post = plan.Signposts[i];
                if (!post.AtSettlement || post.Settlement < 0 || post.Settlement >= plan.Settlements.Count || done.Contains(post.Settlement))
                {
                    continue;
                }
                PlannedSettlement settlement = plan.Settlements[post.Settlement];
                if (settlement.Type == null || !settlement.Type.HorseHire || settlement.Type.Hostile)
                {
                    continue;
                }
                done.Add(post.Settlement);
                Vector2 outward = post.Outward.sqrMagnitude > 1e-6f ? post.Outward.normalized : Vector2.up;
                Vector2 roadside = post.Roadside.sqrMagnitude > 1e-6f ? post.Roadside.normalized : new Vector2(outward.y, -outward.x);
                stands.Add(new HorseStand
                {
                    Settlement = post.Settlement, Signpost = i, Outward = outward, Roadside = roadside,
                    Post = post.Position + roadside * FromPost - outward * Back,
                });
            }
            // A horse town whose roads meet a junction at its edge has no edge post (the junction replaced it, GL632): its
            // stand goes by the nearest post within FallbackReach, facing away from the town's centre.
            for (int s = 0; s < plan.Settlements.Count; s++)
            {
                PlannedSettlement settlement = plan.Settlements[s];
                if (done.Contains(s) || settlement.Type == null || !settlement.Type.HorseHire || settlement.Type.Hostile)
                {
                    continue;
                }
                int nearest = -1;
                float best = FallbackReach * FallbackReach;
                for (int i = 0; i < plan.Signposts.Count; i++)
                {
                    float distance = (plan.Signposts[i].Position - settlement.Position).sqrMagnitude;
                    if (distance < best)
                    {
                        best = distance;
                        nearest = i;
                    }
                }
                if (nearest < 0)
                {
                    continue;
                }
                PlannedSignpost post = plan.Signposts[nearest];
                Vector2 outward = post.Position - settlement.Position;
                outward = outward.sqrMagnitude > 1e-6f ? outward.normalized : Vector2.up;
                Vector2 roadside = post.Roadside.sqrMagnitude > 1e-6f ? post.Roadside.normalized : new Vector2(outward.y, -outward.x);
                stands.Add(new HorseStand
                {
                    Settlement = s, Signpost = nearest, Outward = outward, Roadside = roadside,
                    Post = post.Position + roadside * FromPost - outward * Back,
                });
            }
            stands.Sort((a, b) => a.Settlement.CompareTo(b.Settlement));
            return stands;
        }

        /// <summary>True when a purse of <paramref name="coins"/> pays for a ride.</summary>
        public static bool CanPay(float coins)
        {
            return coins + 1e-4f >= Fare;
        }
    }
}
