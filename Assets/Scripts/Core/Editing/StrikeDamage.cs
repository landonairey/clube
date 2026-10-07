using System.Collections.Generic;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// How much damage tool hits have done to each sample towards breaking it (GL3), keyed by
    /// global sample. A sample is forgotten once it breaks. Kept only while playing: a
    /// half-broken rock heals when the game is reloaded, until saving says otherwise (S2).
    /// </summary>
    public sealed class StrikeDamage
    {
        private readonly Dictionary<Vector3Int, float> damage = new Dictionary<Vector3Int, float>();

        /// <summary>Samples with some damage.</summary>
        public int Count => damage.Count;

        /// <summary>The damage done to a sample so far, in hardness units; 0 when untouched.</summary>
        public float Get(Vector3Int sample)
        {
            return damage.TryGetValue(sample, out float value) ? value : 0f;
        }

        /// <summary>Adds damage to a sample and returns its total.</summary>
        public float Add(Vector3Int sample, float amount)
        {
            float total = Get(sample) + amount;
            damage[sample] = total;
            return total;
        }

        /// <summary>Forgets a sample's damage, e.g. once it broke.</summary>
        public void Forget(Vector3Int sample)
        {
            damage.Remove(sample);
        }

        public void Clear()
        {
            damage.Clear();
        }
    }
}
