using System.Collections.Generic;
using Clube.Core;
using UnityEngine;

namespace Clube.Game
{
    /// <summary>
    /// One animal in the world (GL604, GL606, GL607): the body of an <see cref="AnimalBrain"/>. Each frame it tells the
    /// brain the hour, where its herd and the player are, then walks where the brain heads, keeping its feet on the ground
    /// and refusing water and drops (<see cref="AnimalHerds.TryGround"/>), and poses its model: legs swinging with the
    /// distance walked, head down to graze, lying to rest. A tool's strike (<see cref="IToolTarget"/>) hurts it by the
    /// tool's power; it hops and bolts, or dies: then it lies on its side as a carcass, a container
    /// (<see cref="StorageChest"/>, opened with E) of its species' rolled drops, gone a while after it is emptied.
    /// </summary>
    public class Animal : MonoBehaviour, IToolTarget
    {
        /// <summary>Seconds between looks at the ground ahead.</summary>
        private const float ProbeSeconds = 0.15f;

        private const float HopSeconds = 0.3f;

        private AnimalHerds owner;
        private AnimalHerds.Herd herd;
        private AnimalRig rig;
        private AnimalLook look;
        private BoxCollider box;
        private int seed;
        private float yaw;
        private float stride, swing, dip, lie, onSide;
        private float probeTimer;
        private bool wayClear;
        private float hopTimer;
        private float phase;
        private float deadSeconds, emptySeconds;

        public AnimalSpecies Species { get; private set; }

        public AnimalBrain Brain { get; private set; }

        /// <summary>Its name among its herd's animals (<see cref="HerdSite.AnimalKey"/>): how the dead are remembered.</summary>
        public string Key { get; private set; }

        /// <summary>The carcass's contents, once it is dead.</summary>
        public StorageChest Carcass { get; private set; }

        public bool CanBeStruck => Brain != null && !Brain.IsDead;

        internal void Setup(AnimalHerds owner, AnimalHerds.Herd herd, AnimalSpecies species, int seed, string key, Material material)
        {
            this.owner = owner;
            this.herd = herd;
            this.seed = seed;
            Species = species;
            Key = key;
            Brain = new AnimalBrain(species, seed);
            look = AnimalLooks.For(species.Id);
            rig = AnimalModels.Build(transform, species, material);
            var random = new System.Random(seed);
            yaw = (float)random.NextDouble() * 360f;
            phase = (float)random.NextDouble() * 10f;
            transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            // What the view and a tool's swing land on.
            box = gameObject.AddComponent<BoxCollider>();
            float width = Mathf.Max(0.25f, rig.HalfWidth * 2.2f);
            box.center = new Vector3(0f, species.ShoulderHeight * 0.55f, species.Length * 0.08f);
            box.size = new Vector3(width, species.ShoulderHeight * 1.1f, species.Length);
            rig.Pose(0f, 0f, 0f, 0f, 0f, 0f);
        }

        /// <summary>A tool's hit (GL607): damage by its power; it bolts from the blow, or dies and becomes a carcass.</summary>
        public void Strike(ToolDefinition tool, Vector3 from)
        {
            if (!CanBeStruck)
            {
                return;
            }
            Vector3 local = transform.parent != null ? transform.parent.InverseTransformPoint(from) : from;
            hopTimer = HopSeconds;
            if (Brain.Hit(tool.Power * AnimalSpeciesTable.DamagePerPower, new Vector2(local.x, local.z)))
            {
                Die();
            }
        }

        private void Die()
        {
            var loot = new List<AnimalLoot>();
            Species.RollDrops(new System.Random(seed ^ 0x5bd1e995), loot);
            var contents = new Inventory(9);
            foreach (AnimalLoot each in loot)
            {
                ItemDefinition item = owner.Catalog != null ? owner.Catalog.Find(each.ItemId) : null;
                if (item != null)
                {
                    contents.Add(item, each.Count);
                }
            }
            Carcass = gameObject.AddComponent<StorageChest>();
            Carcass.Hold($"{Species.Name} carcass", contents);
            // Lying on its side: as long, as high as it was wide.
            float thick = Mathf.Max(0.2f, rig.HalfWidth * 2f);
            box.center = new Vector3(0f, thick * 0.5f, 0f);
            box.size = new Vector3(Species.ShoulderHeight, thick, Species.Length);
            owner.Died(this, herd);
        }

        private void Update()
        {
            if (Brain == null || owner == null)
            {
                return;
            }
            float dt = Time.deltaTime;
            if (Brain.IsDead)
            {
                Decay(dt);
                return;
            }
            Vector3 local = transform.localPosition;
            var at = new Vector2(local.x, local.z);
            Brain.Tick(dt, new AnimalSenses
            {
                Hour = owner.Hour, Position = at, Home = herd.Site.Centre, HerdCentre = herd.Centre,
                HasThreat = owner.HasPlayer, Threat = owner.PlayerAt,
            });

            float speed = 0f;
            if (Brain.Speed > 0f)
            {
                Vector2 heading = Brain.Heading;
                probeTimer -= dt;
                if (probeTimer <= 0f)
                {
                    probeTimer = ProbeSeconds;
                    float ahead = Mathf.Max(0.5f, Species.Length * 0.6f) + Brain.Speed * ProbeSeconds;
                    Vector2 probe = at + heading * ahead;
                    wayClear = owner.TryGround(probe.x, probe.y, local.y, out float aheadHeight, out bool wet)
                               && !wet && Mathf.Abs(aheadHeight - local.y) <= 0.3f + ahead * 0.6f;
                    if (!wayClear)
                    {
                        Brain.Blocked();
                    }
                }
                Vector2 next = at + heading * (Brain.Speed * dt);
                if (wayClear && owner.TryGround(next.x, next.y, local.y, out float ground, out _))
                {
                    speed = Brain.Speed;
                    local = new Vector3(next.x, Mathf.MoveTowards(local.y, ground, Mathf.Max(2f, speed * 1.5f) * dt), next.y);
                    transform.localPosition = local;
                }
                float wanted = Mathf.Atan2(heading.x, heading.y) * Mathf.Rad2Deg;
                yaw = Mathf.MoveTowardsAngle(yaw, wanted, (Brain.State == AnimalState.Flee ? 540f : 200f) * dt);
                transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            }
            else
            {
                probeTimer = 0f;
            }
            Animate(dt, speed);
        }

        // Legs by the distance walked, the head by what it is doing, the body down when it rests.
        private void Animate(float dt, float speed)
        {
            bool fleeing = Brain.State == AnimalState.Flee;
            stride += speed * dt * Mathf.PI * 2f / Mathf.Max(0.2f, Species.ShoulderHeight * (fleeing ? 3.2f : 1.8f));
            swing = Mathf.MoveTowards(swing, speed > 0f ? (fleeing ? 40f : 24f) : 0f, 160f * dt);
            float wantedDip = Brain.State switch
            {
                AnimalState.Graze => look.GrazeDip + Mathf.Sin(Time.time * 2.2f + phase) * 5f,
                AnimalState.Rest => 22f,
                AnimalState.Flee => -8f,
                _ => Mathf.Sin(Time.time * 0.7f + phase) * 4f,
            };
            dip = Mathf.MoveTowards(dip, wantedDip, 110f * dt);
            lie = Mathf.MoveTowards(lie, Brain.IsLying ? 1f : 0f, dt * 1.3f);
            hopTimer = Mathf.Max(0f, hopTimer - dt);
            float lift = Mathf.Sin(Mathf.PI * (1f - hopTimer / HopSeconds)) * (hopTimer > 0f ? Species.ShoulderHeight * 0.18f : 0f);
            lift += Mathf.Abs(Mathf.Sin(stride)) * swing / 40f * Species.ShoulderHeight * (fleeing ? 0.08f : 0.02f);
            rig.Pose(stride, swing, dip, lie, 0f, lift);
        }

        // The carcass: falls on its side, and goes a while after its last drop is taken (or long after, left alone).
        private void Decay(float dt)
        {
            if (onSide < 1f)
            {
                onSide = Mathf.MoveTowards(onSide, 1f, dt * 2.5f);
                rig.Pose(stride, 0f, 25f, lie, onSide, 0f);
            }
            deadSeconds += dt;
            emptySeconds = IsEmpty(Carcass.Storage) ? emptySeconds + dt : 0f;
            bool left = deadSeconds > owner.CarcassSeconds && !owner.IsNearPlayer(transform.localPosition, 30f);
            if ((emptySeconds > owner.EmptiedCarcassSeconds || left) && !owner.IsOpen(Carcass))
            {
                Destroy(gameObject);
            }
        }

        private static bool IsEmpty(Inventory inventory)
        {
            for (int slot = 0; slot < inventory.SlotCount; slot++)
            {
                if (!inventory[slot].IsEmpty)
                {
                    return false;
                }
            }
            return true;
        }
    }
}
