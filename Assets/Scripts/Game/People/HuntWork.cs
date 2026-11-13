using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// Hunting (GL615): the worker goes after the nearest animal of the duty's species round the settlement and strikes
    /// it with the duty's tool, as the player would (<see cref="Animal.Strike"/>); it bolts, and is followed, until it
    /// dies. The hunter then works over the carcass a while and counts a hide; the carcass stays where it fell, with
    /// its drops, like one the player killed.
    /// </summary>
    public sealed class HuntWork : IDutyWork
    {
        private const float StrikeReach = 3f;
        private const int DressingStrokes = 6;

        private Animal prey;
        private int dressing;

        public bool Seek(Villager worker, out Vector3 stand, out Vector3 face)
        {
            stand = face = default;
            if (prey != null && !prey.CanBeStruck && dressing <= 0)
            {
                prey = null;
            }
            if (prey == null)
            {
                prey = AnimalSearch.Nearest(worker, animal => animal.CanBeStruck);
            }
            if (prey == null)
            {
                return false;
            }
            face = worker.People.ToLocal(prey.transform.position);
            Vector3 back = worker.At - face;
            back.y = 0f;
            stand = face + (back.sqrMagnitude > 0.01f ? back.normalized : Vector3.right) * 1.5f;
            return true;
        }

        public void Stroke(Villager worker)
        {
            if (prey == null)
            {
                return;
            }
            if (!prey.CanBeStruck)
            {
                if (--dressing <= 0)
                {
                    worker.Carry(1);
                }
                return;
            }
            if ((worker.People.ToLocal(prey.transform.position) - worker.At).sqrMagnitude <= StrikeReach * StrikeReach && worker.Tool != null)
            {
                prey.Strike(worker.Tool, worker.transform.position);
                if (!prey.CanBeStruck)
                {
                    dressing = DressingStrokes;
                }
            }
        }
    }
}
