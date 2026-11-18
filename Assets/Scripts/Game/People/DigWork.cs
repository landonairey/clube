using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// Digging (GL615): an open pit beside the settlement, or a tunnel driven into the nearest slope, taken out of the
    /// ground a blow at a time with the duty's tool (<see cref="DigPlan"/> says which voxel is next). Every worker of a
    /// shop digs the same run of pits and tunnels, its <see cref="DigSite"/> (GL730: the one the work done while
    /// nobody watched is applied to), going on to the next dig of the run once one is dug out. With no slope steep
    /// enough in reach, the tunnel's shift goes to the pits.
    /// </summary>
    public sealed class DigWork : IDutyWork
    {
        private readonly bool tunnel;
        private DigSite site;
        private int current;
        private Dig dig;
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
            if (site == null)
            {
                site = worker.People.WorksOf(worker.Site.Settlement.Index)?.SiteOf(worker.Resident.Shop, tunnel);
                if (site == null)
                {
                    return false;
                }
            }
            hasNext = false;
            while (!hasNext && site.TryGet(current, out dig))
            {
                if (!world.GetDensity(dig.Top).HasValue)
                {
                    // Its ground is not loaded yet.
                    return false;
                }
                hasNext = dig.Plan.Next(voxel => world.GetDensity(voxel) >= iso, out next);
                if (!hasNext)
                {
                    current++;
                }
            }
            if (!hasNext)
            {
                return false;
            }
            face = world.Grid.SampleToWorld(next);
            Vector2 back = dig.Bore ? -dig.Along : new Vector2(dig.Middle.x - face.x, dig.Middle.z - face.z);
            back = back.sqrMagnitude > 0.01f ? back.normalized : Vector2.zero;
            float off = dig.Bore ? 0.9f : 0.6f;
            stand = new Vector3(face.x + back.x * off, dig.Bore ? dig.Middle.y : face.y, face.z + back.y * off);
            return true;
        }

        public void Stroke(Villager worker)
        {
            if (hasNext)
            {
                WorkerStrike.Hit(worker, next, out _);
            }
        }
    }
}
