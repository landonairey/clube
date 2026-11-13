using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// Digging (GL615): an open pit beside the settlement, or a tunnel driven into the nearest slope, taken out of the
    /// ground a blow at a time with the duty's tool (<see cref="DigPlan"/> says which voxel is next). Every worker of a
    /// shop digs the same pit and the same tunnel: where they are follows from the settlement and the shop, not the
    /// worker. With no slope steep enough in reach, the tunnel's shift goes to the pit.
    /// </summary>
    public sealed class DigWork : IDutyWork
    {
        // The pit: voxels across at the top (half), and deep. The tunnel: across, high and long, in voxels.
        private const int PitRadius = 8, PitDepth = 8;
        private const int BoreWidth = 6, BoreHeight = 9, BoreLength = 56;
        private const float MouthRise = 2.6f, MouthRun = 4f;

        private readonly bool tunnel;
        private DigPlan plan;
        private bool laid;
        private bool bore;
        private Vector3 middle;
        private Vector2 along;
        private Vector3Int next;
        private bool hasNext;

        public DigWork(bool tunnel)
        {
            this.tunnel = tunnel;
        }

        public bool Seek(Villager worker, out Vector3 stand, out Vector3 face)
        {
            stand = face = default;
            World world = worker.People.View.World;
            float iso = worker.People.View.Config.IsoLevel;
            if (!laid)
            {
                laid = Lay(worker, world);
            }
            hasNext = plan != null && plan.Next(voxel => world.GetDensity(voxel) >= iso, out next);
            if (!hasNext)
            {
                return false;
            }
            face = world.Grid.SampleToWorld(next);
            Vector2 back = bore ? -along : new Vector2(middle.x - face.x, middle.z - face.z);
            back = back.sqrMagnitude > 0.01f ? back.normalized : Vector2.zero;
            float off = bore ? 0.9f : 0.6f;
            stand = new Vector3(face.x + back.x * off, bore ? middle.y : face.y, face.z + back.y * off);
            return true;
        }

        public void Stroke(Villager worker)
        {
            if (hasNext)
            {
                WorkerStrike.Hit(worker, next, out _);
            }
        }

        // Picks the place: the same for every worker of the shop. False while its ground is not loaded yet.
        private bool Lay(Villager worker, World world)
        {
            SettlementPeople people = worker.People;
            var random = new System.Random(worker.Site.Settlement.Index * 7919 + worker.Resident.Shop * 131 + 17);
            Rect town = worker.Site.Bounds;
            float edge = Mathf.Max(town.width, town.height) * 0.5f + 8f;
            Vector2 spot = town.center;
            bool found = false;
            for (int i = 0; i < 24 && !found; i++)
            {
                float angle = (float)random.NextDouble() * Mathf.PI * 2f;
                spot = town.center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (edge + (float)random.NextDouble() * worker.Duty.Range * 0.5f);
                float here = people.Ground(spot.x, spot.y);
                found = worker.MayWorkAt(spot, 6f) && people.IsDry(spot)
                    && Mathf.Abs(people.Ground(spot.x + 2f, spot.y) - here) < 1f && Mathf.Abs(people.Ground(spot.x, spot.y + 2f) - here) < 1f;
            }
            if (!found)
            {
                plan = null;
                return true;
            }
            if (tunnel && FindMouth(worker, spot, out Vector2 mouth, out along))
            {
                spot = mouth;
                bore = true;
            }
            float height = people.Ground(spot.x, spot.y);
            Vector3Int top = world.Grid.WorldToNearestSample(new Vector3(spot.x, height, spot.y));
            if (!world.GetDensity(top).HasValue)
            {
                return false;
            }
            middle = world.Grid.SampleToWorld(top);
            plan = bore ? DigPlan.Tunnel(top, along, BoreWidth, BoreHeight, BoreLength) : DigPlan.Pit(top, PitRadius, PitDepth);
            return true;
        }

        // The nearest place round a spot where the ground rises enough over a few metres to drive a bore into.
        private static bool FindMouth(Villager worker, Vector2 spot, out Vector2 mouth, out Vector2 into)
        {
            SettlementPeople people = worker.People;
            for (float distance = 0f; distance <= worker.Duty.Range; distance += 3f)
            {
                for (int a = 0; a < 16; a++)
                {
                    float angle = a * Mathf.PI / 8f;
                    var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                    Vector2 at = spot + direction * distance;
                    if (worker.MayWorkAt(at, 6f) && people.IsDry(at)
                        && people.Ground(at.x + direction.x * MouthRun, at.y + direction.y * MouthRun) - people.Ground(at.x, at.y) >= MouthRise)
                    {
                        mouth = at;
                        into = direction;
                        return true;
                    }
                }
            }
            mouth = spot;
            into = Vector2.up;
            return false;
        }
    }
}
