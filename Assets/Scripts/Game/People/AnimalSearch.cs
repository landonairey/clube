using System;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>Finds the animals a duty goes for (GL615): of its target species, within its range of the settlement.</summary>
    public static class AnimalSearch
    {
        /// <summary>The nearest such animal to the worker that <paramref name="wanted"/> accepts; null when there is none.</summary>
        public static Animal Nearest(Villager worker, Func<Animal, bool> wanted)
        {
            AnimalHerds herds = worker.People.Herds;
            if (herds == null)
            {
                return null;
            }
            Rect town = worker.Site.Bounds;
            float reach = Mathf.Max(town.width, town.height) * 0.5f + worker.Duty.Range;
            Animal best = null;
            float nearest = float.MaxValue;
            foreach (Animal animal in herds.Animals)
            {
                if (animal == null || Array.IndexOf(worker.Duty.Targets, animal.Species.Id) < 0 || !wanted(animal))
                {
                    continue;
                }
                Vector3 at = worker.People.ToLocal(animal.transform.position);
                if ((new Vector2(at.x, at.z) - town.center).sqrMagnitude > reach * reach)
                {
                    continue;
                }
                float distance = (at - worker.At).sqrMagnitude;
                if (distance < nearest)
                {
                    nearest = distance;
                    best = animal;
                }
            }
            return best;
        }
    }
}
