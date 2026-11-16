using System.Collections.Generic;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// Shearing (GL615): the worker walks to each animal of the duty's species round the settlement in turn, works on
    /// it for a stroke and counts a fleece; the animal is left alive. Once every one in reach is shorn the round starts
    /// again.
    /// </summary>
    public sealed class ShearWork : IDutyWork
    {
        private const float Reach = 2.4f;

        private readonly HashSet<Animal> shorn = new HashSet<Animal>();
        private Animal sheep;

        public bool Seek(Villager worker, out Vector3 stand, out Vector3 face)
        {
            stand = face = default;
            if (sheep == null || !sheep.CanBeStruck || shorn.Contains(sheep))
            {
                sheep = AnimalSearch.Nearest(worker, animal => animal.CanBeStruck && !shorn.Contains(animal));
                if (sheep == null && shorn.Count > 0)
                {
                    shorn.Clear();
                    sheep = AnimalSearch.Nearest(worker, animal => animal.CanBeStruck);
                }
            }
            if (sheep == null)
            {
                return false;
            }
            face = worker.People.ToLocal(sheep.transform.position);
            Vector3 back = worker.At - face;
            back.y = 0f;
            stand = face + (back.sqrMagnitude > 0.01f ? back.normalized : Vector3.right) * 1.1f;
            return true;
        }

        public void Stroke(Villager worker)
        {
            if (sheep != null && (worker.People.ToLocal(sheep.transform.position) - worker.At).sqrMagnitude <= Reach * Reach)
            {
                shorn.Add(sheep);
                worker.Carry(1);
            }
        }
    }
}
