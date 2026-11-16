using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// A bucket (GL319): carries water taken out of the voxel data. One to a stack; the stack's
    /// <see cref="ItemStack.Wear"/> is the water in it, in volume units (one full water sample is one unit, like a mined
    /// ground sample). Attack scoops, Place pours (Game's <c>PlayerBucket</c>).
    /// </summary>
    [CreateAssetMenu(fileName = "Bucket", menuName = "Clube/Bucket")]
    public class BucketDefinition : ItemDefinition
    {
        [Tooltip("Most water it holds, in volume units (full water samples).")]
        [SerializeField, Min(1)]
        private int capacity = 100;

        [Tooltip("How far round the point aimed at a scoop takes water, in metres.")]
        [SerializeField, Min(0.1f)]
        private float scoopRadius = 0.6f;

        [Tooltip("Units poured by a single press of Place.")]
        [SerializeField, Min(1)]
        private int pourAmount = 5;

        [Tooltip("Units a second poured while Place is held.")]
        [SerializeField, Min(1f)]
        private float pourRate = 30f;

        // A dropped bucket lies on the ground as its model (GL610).
        protected override DropForm KindDropForm => DropForm.Model;

        public int Capacity => capacity;

        /// <summary>
        /// The most water this bucket holds (GL395): its <see cref="Capacity"/> at the stack's quality, so a 60% bucket
        /// carries 60 of 100 units. At least 1.
        /// </summary>
        public int CapacityOf(ItemStack stack)
        {
            return Mathf.Max(1, Mathf.RoundToInt(capacity * stack.Quality));
        }

        public float ScoopRadius => scoopRadius;

        public int PourAmount => pourAmount;

        public float PourRate => pourRate;

        /// <summary>The water in a bucket's stack, in units.</summary>
        public int WaterIn(ItemStack stack)
        {
            return Mathf.Clamp(stack.Wear, 0, CapacityOf(stack));
        }

        /// <summary>The same bucket holding <paramref name="water"/> units.</summary>
        public ItemStack WithWater(ItemStack stack, int water)
        {
            return new ItemStack(this, 1, stack.Contents, Mathf.Clamp(water, 0, CapacityOf(stack)), stack.Quality);
        }

        /// <summary>Makes a bucket in code (tests).</summary>
        public static BucketDefinition CreateBucket(int capacity = 100)
        {
            BucketDefinition bucket = Create<BucketDefinition>("wooden-bucket", "Wooden bucket", 1);
            bucket.capacity = capacity;
            return bucket;
        }
    }
}
