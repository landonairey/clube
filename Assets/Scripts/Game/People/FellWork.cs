using System.Collections.Generic;
using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// Felling (GL615): the worker walks out to the nearest standing tree outside the settlement and chops through its
    /// trunk with the duty's tool; the cut tree falls the way the player's does (<see cref="TreeFeller"/>), away from
    /// the worker, and lies where it lands. GL672: it fells as at the highest Woodcutting level, so the whole tree comes
    /// down with no wood lost, then chops up the wood that landed, taking it out of the ground, before the next tree,
    /// further out as the nearer ones go. GL730: the trees are the settlement's claimed ones (<see cref="SettlementWorks.Trees"/>).
    /// </summary>
    public sealed class FellWork : IDutyWork
    {
        private const float StandOff = 0.9f;
        private readonly List<Vector2> spots = new List<Vector2>();
        private readonly HashSet<int> bare = new HashSet<int>();
        private bool listed;
        private readonly List<Vector3Int> lying = new List<Vector3Int>();
        private Vector3Int cut;
        private bool hasCut;
        private bool cuttingUp;

        public bool Seek(Villager worker, out Vector3 stand, out Vector3 face)
        {
            stand = face = default;
            SettlementPeople people = worker.People;
            World world = people.View.World;
            float iso = people.View.Config.IsoLevel;
            if (people.Trees == null || people.View.Trees == null)
            {
                return false;
            }
            if (!listed)
            {
                // GL730: the settlement's claimed trees, in the order the work done while nobody watched takes them.
                SettlementWorks works = people.WorksOf(worker.Site.Settlement.Index);
                if (works == null)
                {
                    return false;
                }
                listed = true;
                spots.AddRange(works.Trees);
            }
            if (NearestLying(worker, world, people.Trees, iso, out cut))
            {
                // The felled tree first: stand beside the piece of it nearest, at the ground.
                hasCut = cuttingUp = true;
                face = world.Grid.SampleToWorld(cut);
                Vector2 off = new Vector2(worker.At.x, worker.At.z) - new Vector2(face.x, face.z);
                off = off.sqrMagnitude > 0.01f ? off.normalized : Vector2.right;
                float x = face.x + off.x * StandOff, z = face.z + off.y * StandOff;
                stand = new Vector3(x, people.Ground(x, z), z);
                return true;
            }
            cuttingUp = false;
            hasCut = TreeFinder.FindTrunk(world, people.Trees, spots, bare, people.Ground, spot => worker.MayWorkAt(spot, 3f), iso, out cut);
            if (!hasCut)
            {
                return false;
            }
            face = world.Grid.SampleToWorld(cut);
            Vector2 away = new Vector2(worker.At.x, worker.At.z) - new Vector2(face.x, face.z);
            away = away.sqrMagnitude > 0.01f ? away.normalized : Vector2.right;
            stand = new Vector3(face.x + away.x * StandOff, face.y - TreeFinder.ChopHeight, face.z + away.y * StandOff);
            return true;
        }

        public void Stroke(Villager worker)
        {
            if (!hasCut)
            {
                return;
            }
            WorkerStrike.Hit(worker, cut, out VoxelReach reach);
            TreeFeller feller = worker.People.Feller;
            if (feller != null && !cuttingUp)
            {
                Vector3 at = worker.People.View.World.Grid.SampleToWorld(cut);
                float dx = at.x - worker.At.x, dz = at.z - worker.At.z;
                Vector3Int fall = Mathf.Abs(dx) >= Mathf.Abs(dz) ? new Vector3Int(dx >= 0f ? 1 : -1, 0, 0) : new Vector3Int(0, 0, dz >= 0f ? 1 : -1);
                feller.TryFell(reach.Box, fall, float.MaxValue, wood => lying.AddRange(wood));
            }
        }

        // The landed wood nearest the worker that is still there; what has gone (cut, or burnt) is dropped from the list.
        private bool NearestLying(Villager worker, World world, TreeMaterials trees, float iso, out Vector3Int sample)
        {
            sample = default;
            float best = float.MaxValue;
            for (int i = lying.Count - 1; i >= 0; i--)
            {
                if (!TreeFinder.IsWood(world, trees, lying[i], iso))
                {
                    lying.RemoveAt(i);
                    continue;
                }
                Vector3 at = world.Grid.SampleToWorld(lying[i]);
                float distance = (new Vector2(at.x, at.z) - new Vector2(worker.At.x, worker.At.z)).sqrMagnitude;
                if (distance < best)
                {
                    best = distance;
                    sample = lying[i];
                }
            }
            return lying.Count > 0;
        }
    }
}
