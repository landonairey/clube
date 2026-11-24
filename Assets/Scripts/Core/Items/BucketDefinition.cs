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

        [Tooltip("Item 14: true for the bucket holding salt water from the sea.")]
        [SerializeField]
        private bool holdsSaltWater;

        [Tooltip("Item 14: the same bucket with the other water: on the plain bucket, the one holding salt water; on that one, the plain bucket it empties back into.")]
        [SerializeField]
        private BucketDefinition otherForm;

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

        /// <summary>A bucket holding water, fresh or salt, is a Liquid (batch 2, item 14); an empty one is a Tool.</summary>
        public override ItemType TypeOf(ItemStack stack)
        {
            return WaterIn(stack) > 0 ? ItemType.Liquid : base.TypeOf(stack);
        }

        public float ScoopRadius => scoopRadius;

        public int PourAmount => pourAmount;

        public float PourRate => pourRate;

        /// <summary>The water in a bucket's stack, in units.</summary>
        public int WaterIn(ItemStack stack)
        {
            return Mathf.Clamp(stack.Wear, 0, CapacityOf(stack));
        }

        /// <summary>True for the bucket holding salt water (item 14).</summary>
        public bool HoldsSaltWater => holdsSaltWater;

        /// <summary>The plain bucket (item 14): this one, or the one a bucket of salt water empties back into.</summary>
        public BucketDefinition FreshForm => holdsSaltWater && otherForm != null ? otherForm : this;

        /// <summary>The bucket holding salt water (item 14): this one, or the plain bucket's salt form (itself when it has none).</summary>
        public BucketDefinition SaltForm => holdsSaltWater ? this : otherForm != null ? otherForm : this;

        /// <summary>
        /// The same bucket holding <paramref name="water"/> units of the water it holds now. A bucket of salt water emptied
        /// is the plain bucket again (item 14).
        /// </summary>
        public ItemStack WithWater(ItemStack stack, int water)
        {
            return WithWater(stack, water, false);
        }

        /// <summary>
        /// The same bucket holding <paramref name="water"/> units, with <paramref name="addsSalt"/> for salt water scooped in
        /// (item 14): any salt water makes it a bucket of salt water, and an empty bucket is the plain one.
        /// </summary>
        public ItemStack WithWater(ItemStack stack, int water, bool addsSalt)
        {
            int units = Mathf.Clamp(water, 0, CapacityOf(stack));
            BucketDefinition form = units > 0 && (addsSalt || holdsSaltWater) ? SaltForm : FreshForm;
            return new ItemStack(form, 1, stack.Contents, units, stack.Quality);
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
