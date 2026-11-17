using System;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>How a kind of tree is built (GL721).</summary>
    public enum TreeForm
    {
        /// <summary>A trunk with large branches climbing out of its upper half and leaf balls at their ends (birch, maple).</summary>
        Broadleaf,

        /// <summary>A straight trunk to the top, whorls of short drooping branches narrowing upwards into a cone (pine).</summary>
        Conifer,
    }

    /// <summary>
    /// A kind of tree past the first two (GL721): a row of <see cref="TreeGeneration"/>, so a new species is data. Birch and
    /// maple stay as <see cref="TreeGeneration"/>'s own fields (the worlds' saved settings name them); these rows are kinds 2 on.
    /// A species takes its <see cref="Share"/> of the trees everywhere, rising to <see cref="ColdShare"/> where the land is
    /// cold (its temperature from the map's biomes, <see cref="ITreeClimate.TemperatureAt"/>).
    /// </summary>
    [Serializable]
    public sealed class TreeSpecies
    {
        [SerializeField]
        private string name = "Pine";

        [SerializeField]
        private TreeForm form = TreeForm.Conifer;

        [SerializeField]
        private VoxelMaterial wood;

        [SerializeField]
        private VoxelMaterial leaves;

        [Tooltip("Its base, left when the trunk is cut. None: the first kind's stump.")]
        [SerializeField]
        private VoxelMaterial stump;

        [Tooltip("Lowest and highest full-grown tree, in metres from the ground to the top of the trunk.")]
        [SerializeField]
        private Vector2 height = new Vector2(16f, 60f);

        [Tooltip("A full-grown tree's trunk radius at the ground, in metres (thicker past 20 m tall).")]
        [SerializeField, Range(0.15f, 1.5f)]
        private float trunkRadius = 0.3f;

        [Tooltip("Share of the trees that are this kind where the land isn't cold (or a map has no climate).")]
        [SerializeField, Range(0f, 1f)]
        private float share = 0.1f;

        [Tooltip("Share of the trees that are this kind where the land is coldest.")]
        [SerializeField, Range(0f, 1f)]
        private float coldShare = 0.8f;

        [Tooltip("Degrees where its share starts rising (x) and where it reaches the cold share (y).")]
        [SerializeField]
        private Vector2 coldFrom = new Vector2(12f, 4f);

        [Tooltip("The bark colour the distant trees draw it in.")]
        [SerializeField]
        private Color distantBark = new Color(0.33f, 0.24f, 0.17f);

        [Tooltip("The leaf colour the distant trees draw it in.")]
        [SerializeField]
        private Color distantLeaves = new Color(0.12f, 0.27f, 0.15f);

        public TreeSpecies()
        {
        }

        public TreeSpecies(string name, TreeForm form, VoxelMaterial wood, VoxelMaterial leaves, VoxelMaterial stump)
        {
            this.name = name;
            this.form = form;
            this.wood = wood;
            this.leaves = leaves;
            this.stump = stump;
        }

        public string Name => name;

        public TreeForm Form
        {
            get => form;
            set => form = value;
        }

        public VoxelMaterial Wood
        {
            get => wood;
            set => wood = value;
        }

        public VoxelMaterial Leaves
        {
            get => leaves;
            set => leaves = value;
        }

        /// <summary>Its own stump, or null for the first kind's.</summary>
        public VoxelMaterial Stump
        {
            get => stump;
            set => stump = value;
        }

        public Vector2 Height
        {
            get => new Vector2(Mathf.Max(1f, Mathf.Min(height.x, height.y)), Mathf.Max(1f, height.x, height.y));
            set => height = value;
        }

        public float TrunkRadius
        {
            get => trunkRadius;
            set => trunkRadius = Mathf.Clamp(value, 0.15f, 1.5f);
        }

        public float Share
        {
            get => share;
            set => share = Mathf.Clamp01(value);
        }

        public float ColdShare
        {
            get => coldShare;
            set => coldShare = Mathf.Clamp01(value);
        }

        /// <summary>Degrees where its share starts rising (x) and where it reaches <see cref="ColdShare"/> (y).</summary>
        public Vector2 ColdFrom
        {
            get => coldFrom;
            set => coldFrom = value;
        }

        public Color DistantBark => distantBark;
        public Color DistantLeaves => distantLeaves;

        /// <summary>True when it can grow: wood and leaves set.</summary>
        public bool CanGrow => wood != null && leaves != null;

        /// <summary>Its share of the trees at a temperature in degrees; NaN (no climate) gives <see cref="Share"/>.</summary>
        public float ShareAt(float temperature)
        {
            if (!CanGrow)
            {
                return 0f;
            }
            if (float.IsNaN(temperature) || Mathf.Approximately(coldFrom.x, coldFrom.y))
            {
                return share;
            }
            return Mathf.Lerp(share, coldShare, Mathf.InverseLerp(coldFrom.x, coldFrom.y, temperature));
        }
    }

    /// <summary>One kind of tree as the shape needs it (GL721): birch (0), maple (1) or a <see cref="TreeSpecies"/> row (2 on).</summary>
    public readonly struct TreeKind
    {
        public TreeKind(string name, TreeForm form, VoxelMaterial wood, VoxelMaterial leaves, VoxelMaterial stump, Vector2 height, float trunkRadius)
        {
            Name = name;
            Form = form;
            Wood = wood;
            Leaves = leaves;
            Stump = stump;
            Height = height;
            TrunkRadius = trunkRadius;
        }

        public string Name { get; }
        public TreeForm Form { get; }
        public VoxelMaterial Wood { get; }
        public VoxelMaterial Leaves { get; }
        public VoxelMaterial Stump { get; }
        public Vector2 Height { get; }
        public float TrunkRadius { get; }
    }
}
