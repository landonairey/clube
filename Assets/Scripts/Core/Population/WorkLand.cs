using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// Where a settlement's people may fell and dig (GL615, GL731): outside their own settlement, off the roads, and never
    /// on the player's starting parcel or the eight round it, so the player always has trees and ground of their own to
    /// start with. Plain data: the parcels are the land's own squares (<see cref="LandParcels"/>), counted from the world
    /// origin, so nothing needs loading.
    /// </summary>
    public sealed class WorkLand
    {
        private readonly WorldPlan plan;
        private readonly float parcelSize;
        private readonly Vector2Int start;
        private readonly bool hasStart;

        /// <summary>Parcels kept round the starting one on each side (1: the 3 x 3 block).</summary>
        public const int StartRing = 1;

        /// <param name="plan">The world's plan: its roads are not worked. May be null.</param>
        /// <param name="start">Where the player starts (x, z, metres from the world origin); null for nowhere kept.</param>
        /// <param name="parcelSize">Width of a land parcel, in metres.</param>
        public WorkLand(WorldPlan plan, Vector2? start, float parcelSize)
        {
            this.plan = plan;
            this.parcelSize = Mathf.Max(1f, parcelSize);
            hasStart = start.HasValue;
            if (hasStart)
            {
                this.start = ParcelAt(start.Value);
            }
        }

        /// <summary>The parcel holding a point (x, z), as <see cref="LandParcels.ParcelAt"/> numbers them.</summary>
        public Vector2Int ParcelAt(Vector2 point)
        {
            return new Vector2Int(Mathf.FloorToInt(point.x / parcelSize), Mathf.FloorToInt(point.y / parcelSize));
        }

        /// <summary>The kept land round the start (x, z, metres): the starting parcel and the eight round it; empty with no start.</summary>
        public Rect StartLand => hasStart
            ? new Rect((start.x - StartRing) * parcelSize, (start.y - StartRing) * parcelSize, (2 * StartRing + 1) * parcelSize, (2 * StartRing + 1) * parcelSize)
            : Rect.zero;

        /// <summary>True when a point lies within <paramref name="margin"/> metres of the kept land round the start.</summary>
        public bool IsStartLand(Vector2 point, float margin = 0f)
        {
            if (!hasStart)
            {
                return false;
            }
            Rect kept = StartLand;
            return point.x > kept.xMin - margin && point.x < kept.xMax + margin && point.y > kept.yMin - margin && point.y < kept.yMax + margin;
        }

        /// <summary>
        /// True when a settlement's people may work at a point: more than <paramref name="margin"/> metres outside their
        /// settlement's bounds (<paramref name="town"/>), off every road and off the kept land round the start.
        /// </summary>
        public bool MayWork(Rect town, Vector2 point, float margin)
        {
            bool inTown = point.x > town.xMin - margin && point.x < town.xMax + margin && point.y > town.yMin - margin && point.y < town.yMax + margin;
            return !inTown && (plan == null || !plan.IsOnRoad(point, margin)) && !IsStartLand(point, margin);
        }
    }
}
