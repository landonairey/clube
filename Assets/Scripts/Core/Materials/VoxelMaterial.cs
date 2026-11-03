using UnityEngine;

namespace Clube.Core
{
    /// <summary>How ground of a material moves under gravity (GL308).</summary>
    public enum GroundFall
    {
        /// <summary>It stays where it is (plants, stumps, carved flat rock).</summary>
        Stays,

        /// <summary>It falls straight down into air below it (dirt, grass).</summary>
        Drops,

        /// <summary>It falls, and slides down sideways where the slope is steeper than 45° (sand).</summary>
        Slides,
    }

    /// <summary>
    /// How a material holds over air (GL327): how it falls once it lets go (<see cref="GroundFall"/>), how many samples out it
    /// can hang held sideways by ground that stands on something, and how much further for each solid sample over it.
    /// </summary>
    public readonly struct GroundHold
    {
        public GroundHold(GroundFall fall, int overhang = 0, int archReach = 0)
        {
            Fall = fall;
            Overhang = overhang;
            ArchReach = archReach;
        }

        public GroundFall Fall { get; }

        /// <summary>Samples out over air it can hang; 0: it falls whenever there's air under it.</summary>
        public int Overhang { get; }

        /// <summary>Extra overhang for each solid sample stacked over it, up to this many (cave ceilings).</summary>
        public int ArchReach { get; }

        /// <summary>Whether anything can hold it over air.</summary>
        public bool CanHang => Overhang > 0 || ArchReach > 0;
    }

    /// <summary>How dropped material of a kind piles up (GL17).</summary>
    public enum PileShape
    {
        /// <summary>Soft and medium ground (dirt, grass): a cone at its angle of repose.</summary>
        Cone,

        /// <summary>Hard material (stone, ore): a square block inside a build cell.</summary>
        Block,
    }

    /// <summary>What kind of material a <see cref="VoxelMaterial"/> is (M9).</summary>
    public enum VoxelMaterialCategory
    {
        /// <summary>Bulk ground: grass, dirt, stone.</summary>
        Aggregate,

        /// <summary>Holds a metal worth extracting: gold, silver, copper (3D, Chapter 5).</summary>
        Ore,

        /// <summary>Grows on the surface: wood and leaves (GL30).</summary>
        Plant,
    }

    /// <summary>
    /// The three kinds of ground a player keeps apart (GL114): what tools are made for (the pickaxe rock,
    /// the shovel soil, the hatchet plants) and which bin of the volume inventory mined material goes to.
    /// </summary>
    public enum MaterialKind
    {
        /// <summary>Stone, rock and ore.</summary>
        Rock,

        /// <summary>Soft ground: grass, dirt, clay.</summary>
        Soil,

        /// <summary>Wood and leaves.</summary>
        Plant,
    }

    /// <summary>
    /// One terrain material (M9): grass, dirt, stone, an ore. An asset, so new materials are
    /// added without code changes: make one, give it an unused <see cref="Id"/> and add it
    /// to the <see cref="MaterialRegistry"/>. Voxels store only the id (M10).
    /// </summary>
    /// <remarks>
    /// Where an ore generates is set per world (<see cref="OreSpec"/> in the terrain settings,
    /// O1), not here. Later chapters add to it rather than replace it, e.g. hidden mineral
    /// species (PR2).
    /// </remarks>
    [CreateAssetMenu(fileName = "VoxelMaterial", menuName = "Clube/Voxel Material")]
    public class VoxelMaterial : ScriptableObject
    {
        [Tooltip("Stored in every voxel of this material, and in saves, so never change it once used. Also its texture array layer.")]
        [SerializeField, Range(0, MaterialRegistry.MaxMaterials - 1)]
        private int id;

        [SerializeField]
        private string displayName = "Material";

        [SerializeField]
        private VoxelMaterialCategory category;

        [Tooltip("Surface texture, tiled over the terrain from every side (M11, M14). Square, the size of the other materials' textures.")]
        [SerializeField]
        private Texture2D albedo;

        [Tooltip("Flat colour in the Debug colours display mode (M15), to check where each material is at a glance.")]
        [SerializeField]
        private Color debugColor = Color.grey;

        [Tooltip("How hard it is to extract, relative to dirt = 1: how many hits of a tool it takes to break (GL3): each hit adds the tool's power, and it breaks once that reaches its hardness.")]
        [SerializeField, Min(0f)]
        private float hardness = 1f;

        [Tooltip("The item extracting it gives (O6).")]
        [SerializeField]
        private ItemDefinition drop;

        [Tooltip("How it piles up when dropped (GL17): a cone for soft ground, a block in the build cell for hard material.")]
        [SerializeField]
        private PileShape pileShape = PileShape.Cone;

        [Tooltip("Steepest slope a cone pile of it keeps, in degrees (its angle of repose).")]
        [SerializeField, Range(10f, 80f)]
        private float reposeAngle = 40f;

        [Tooltip("Picked up whole (GL25): a hit on a connected piece of it no bigger than this many samples collects the whole piece at once, one drop per sample. 0: never; it's broken like any ground.")]
        [SerializeField, Range(0, 32)]
        private int pickUpPieceSize;

        [Tooltip("Bare hands take it from a pile of any size (GL133): dropped stone and ore, the loose stages.")]
        [SerializeField]
        private bool pickedFromPiles;

        [Tooltip("The tool tier it takes to work it (GL145): 0 bare hands (dirt, grass, clay, leaves), 1 a hand axe (wood, stone, ore), 2 stone tools, 3 copper (maple), 4 bronze.")]
        [SerializeField, Range(0, 5)]
        private int toolTier;

        [Tooltip("A skill level it also takes (GL152: maple needs Woodcutting 3). None: no level.")]
        [SerializeField]
        private SkillType requiredSkill;

        [SerializeField, Range(0, SkillSet.MaxLevel)]
        private int requiredLevel;

        [Tooltip("How far it sways in the wind, 0-1 (leaves 1, so the tops of the trees move; the Clube/Terrain Materials shader).")]
        [SerializeField, Range(0f, 1f)]
        private float windSway;

        [Tooltip("How much it shines, 0-1 (GL251: gold glints, so it's told from chalcopyrite): a metallic highlight and sparkles in the Clube/Terrain Materials shader.")]
        [SerializeField, Range(0f, 1f)]
        private float shine;

        [Tooltip("Whether it falls when nothing holds it up (GL308): dirt and grass drop straight down, sand also slides off slopes steeper than 45°.")]
        [SerializeField]
        private GroundFall fall;

        [Tooltip("How many samples out it can hang over air before it falls (GL327): held sideways through solid ground to " +
                 "ground standing on a solid column. 0: it falls whenever there's air under it (sand). Only for ground that falls.")]
        [SerializeField, Range(0, 16)]
        private int overhang;

        [Tooltip("Extra overhang for each solid sample stacked over it, up to this many (GL327): thick rock arches over a cave " +
                 "where a thin lip of it breaks off.")]
        [SerializeField, Range(0, 16)]
        private int archReach;

        [Tooltip("What a tool turns it into once broken (MF5, GL3), e.g. stone → cracked stone → loose stone. None: breaking it removes it and gives its drop.")]
        [SerializeField]
        private VoxelMaterial breaksInto;

        [Tooltip("A worktop (GL36): the player can use it where it stands, like a station, e.g. a tree stump.")]
        [SerializeField]
        private bool isWorktop;

        [Tooltip("Soft ground (GL39): grass, dirt, clay. Shovels reach wide on it; pickaxes only hit the one voxel aimed at. Everything else is hard.")]
        [SerializeField]
        private bool isSoft;

        [Tooltip("What it turns into left out on the ground (GL34), e.g. clay → brick. None: it never changes.")]
        [SerializeField]
        private VoxelMaterial driesInto;

        [Tooltip("In-game days it takes to dry once dropped on the ground.")]
        [SerializeField, Min(0f)]
        private float dryingDays = 2f;

        [Tooltip("What the mining journal says about it (GL73), for ores: how it looks and feels in the hand.")]
        [SerializeField]
        private MineralNotes notes = new MineralNotes();

        /// <summary>Makes a material in code (tests, tools); the game's materials are assets.</summary>
        public static VoxelMaterial Create(int id, string displayName, VoxelMaterialCategory category = VoxelMaterialCategory.Aggregate)
        {
            var material = CreateInstance<VoxelMaterial>();
            material.id = Mathf.Clamp(id, 0, MaterialRegistry.MaxMaterials - 1);
            material.displayName = displayName;
            material.category = category;
            material.name = displayName;
            return material;
        }

        public byte Id => (byte)id;

        public string DisplayName => displayName;

        public VoxelMaterialCategory Category => category;

        public Texture2D Albedo => albedo;

        public Color DebugColor => debugColor;

        public float Hardness => hardness;

        /// <summary>The item extracting it gives (O6), or null.</summary>
        public ItemDefinition Drop => drop;

        /// <summary>
        /// The material a tool turns it into once broken (MF5, GL3), or null when breaking it
        /// removes it (and gives its <see cref="Drop"/>).
        /// </summary>
        public VoxelMaterial BreaksInto => breaksInto;

        /// <summary>What it gives removed whole: the drop of the last stage of its break chain, or its own (loose stone gives stone).</summary>
        public ItemDefinition FinalDrop
        {
            get
            {
                ItemDefinition drop = Drop;
                for (VoxelMaterial stage = breaksInto; stage != null && stage != this; stage = stage.breaksInto)
                {
                    if (stage.Drop != null)
                    {
                        drop = stage.Drop;
                    }
                }
                return drop;
            }
        }

        /// <summary>True when the player can work at it where it stands (GL36), like a stump.</summary>
        public bool IsWorktop => isWorktop;

        /// <summary>True for soft ground (GL39): tools made for hard material only hit one voxel of it.</summary>
        public bool IsSoft => isSoft;

        /// <summary>Which kind of ground it is (GL114): plants by category, soil when soft, otherwise rock.</summary>
        public MaterialKind Kind => category == VoxelMaterialCategory.Plant ? MaterialKind.Plant : isSoft ? MaterialKind.Soil : MaterialKind.Rock;

        /// <summary>What it turns into left out on the ground (GL34), or null when it never changes.</summary>
        public VoxelMaterial DriesInto => driesInto;

        /// <summary>In-game days it takes to dry once dropped (GL34).</summary>
        public float DryingDays => dryingDays;

        /// <summary>The mining journal's description of it (GL73).</summary>
        public MineralNotes Notes => notes;

        /// <summary>How it piles up when dropped (GL17).</summary>
        public PileShape PileShape => pileShape;

        /// <summary>Steepest slope of a cone pile, in degrees.</summary>
        public float ReposeAngle => reposeAngle;

        /// <summary>
        /// The largest connected piece of it (in samples) a hit picks up whole, like a stone
        /// lying on the grass (GL25); 0 when it's never picked up whole.
        /// </summary>
        public int PickUpPieceSize => pickUpPieceSize;

        /// <summary>True when bare hands take it from a pile of any size (GL133: dropped cobbles).</summary>
        public bool PickedFromPiles => pickedFromPiles;

        /// <summary>The tool tier it takes to work it (GL145): 0 for bare hands.</summary>
        public int ToolTier => toolTier;

        /// <summary>A skill it also takes (GL152), or <see cref="SkillType.None"/>.</summary>
        public SkillType RequiredSkill => requiredSkill;

        /// <summary>The level of <see cref="RequiredSkill"/> it takes.</summary>
        public int RequiredLevel => requiredLevel;

        /// <summary>How far it sways in the wind, 0-1: leaves sway, the ground does not.</summary>
        public float WindSway => windSway;

        /// <summary>How much it shines, 0-1 (GL251): gold does, the rest of the ground is matt.</summary>
        public float Shine => shine;

        /// <summary>Whether it falls when nothing holds it up (GL308).</summary>
        public GroundFall Fall => fall;

        /// <summary>How it falls and how far it holds over air (GL327).</summary>
        public GroundHold Hold => new GroundHold(fall, overhang, archReach);

        /// <summary>
        /// How rough its surface is drawn, 0-1 (GL268): rock fully (bumps and cracks on stone faces), soft ground a little,
        /// plants not at all (the shader moves and shades the surface by it; the voxels are unchanged).
        /// </summary>
        public float Relief => Kind == MaterialKind.Rock ? 1f : Kind == MaterialKind.Soil ? 0.35f : 0f;

        /// <summary>Sets what it takes to work it (GL145, GL152), for materials made in code (tests).</summary>
        public VoxelMaterial WithToolTier(int tier, SkillType skill = SkillType.None, int level = 0)
        {
            toolTier = Mathf.Clamp(tier, 0, 5);
            requiredSkill = skill;
            requiredLevel = Mathf.Clamp(level, 0, SkillSet.MaxLevel);
            return this;
        }

        /// <summary>Sets how it piles, for materials made in code (tests, tools).</summary>
        public VoxelMaterial WithPile(PileShape shape, float angle = 40f)
        {
            pileShape = shape;
            reposeAngle = Mathf.Clamp(angle, 10f, 80f);
            return this;
        }

        /// <summary>Sets the largest piece picked up whole, for materials made in code (tests, tools).</summary>
        public VoxelMaterial WithPickUp(int pieceSize)
        {
            pickUpPieceSize = Mathf.Clamp(pieceSize, 0, 32);
            return this;
        }

        /// <summary>Lets hands take it from any pile (GL133), for materials made in code (tests).</summary>
        public VoxelMaterial WithPickedFromPiles()
        {
            pickedFromPiles = true;
            return this;
        }

        /// <summary>Marks it soft ground, for materials made in code (tests, tools).</summary>
        public VoxelMaterial WithSoft(bool soft = true)
        {
            isSoft = soft;
            return this;
        }

        /// <summary>Sets how it dries, for materials made in code (tests, tools).</summary>
        public VoxelMaterial WithDrying(VoxelMaterial into, float days)
        {
            driesInto = into;
            dryingDays = Mathf.Max(0f, days);
            return this;
        }

        /// <summary>Sets how it breaks, for materials made in code (tests, tools).</summary>
        public VoxelMaterial WithBreaking(float hardness, ItemDefinition drop = null, VoxelMaterial breaksInto = null)
        {
            this.hardness = Mathf.Max(0f, hardness);
            this.drop = drop;
            this.breaksInto = breaksInto;
            return this;
        }
    }
}
