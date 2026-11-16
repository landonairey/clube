using UnityEngine;

namespace Clube.Core
{
    /// <summary>The figures the game dresses (GL608): one row of <see cref="FigureLooks"/> each.</summary>
    public enum FigurePreset
    {
        Blacksmith,
        MineKeeper,
        Merchant,
        Woodsman,
        Prospector,
        Worker,
        Foreman,
        Adventurer,
        Thief,
        Carter,
        Player,
        Sailor,
    }

    /// <summary>
    /// The outfit presets as data (GL608) and the variation a seed gives one person: a new look for an NPC is a new row in
    /// <see cref="Of(FigurePreset)"/>. Variation moves in a few steps (height, build, skin, hair, how faded the cloth is), so
    /// a town isn't clones yet people who come out alike still share meshes.
    /// </summary>
    public static class FigureLooks
    {
        private static readonly Color Leather = new Color(0.2f, 0.14f, 0.1f);
        private static readonly Color DarkCloth = new Color(0.27f, 0.24f, 0.22f);

        private static readonly Color[] SkinTones =
        {
            new Color(0.8f, 0.6f, 0.46f), new Color(0.86f, 0.68f, 0.54f), new Color(0.66f, 0.46f, 0.33f), new Color(0.48f, 0.33f, 0.24f),
        };

        private static readonly Color[] HairTones =
        {
            new Color(0.2f, 0.14f, 0.1f), new Color(0.12f, 0.1f, 0.08f), new Color(0.5f, 0.3f, 0.15f), new Color(0.62f, 0.5f, 0.3f),
            new Color(0.42f, 0.2f, 0.12f),
        };

        private static readonly float[] Heights = { 0.94f, 0.97f, 1f, 1.03f, 1.06f };
        private static readonly float[] Builds = { 0.9f, 1f, 1f, 1.12f };
        private static readonly float[] Fades = { 0.9f, 1f, 1.08f };

        private static readonly Color[] WorkerCloth =
        {
            new Color(0.42f, 0.38f, 0.33f), new Color(0.36f, 0.38f, 0.34f), new Color(0.45f, 0.4f, 0.32f), new Color(0.34f, 0.34f, 0.37f),
        };

        private static readonly Color[] WorkerPatch =
        {
            new Color(0.52f, 0.42f, 0.3f), new Color(0.3f, 0.33f, 0.27f), new Color(0.5f, 0.46f, 0.38f),
        };

        /// <summary>Every preset, for tools and tests.</summary>
        public static readonly FigurePreset[] All = (FigurePreset[])System.Enum.GetValues(typeof(FigurePreset));

        /// <summary>The keeper of a kind of shop.</summary>
        public static FigurePreset Of(ShopKind kind)
        {
            switch (kind)
            {
                case ShopKind.Blacksmith: return FigurePreset.Blacksmith;
                case ShopKind.Mining: return FigurePreset.MineKeeper;
                case ShopKind.Lumber: return FigurePreset.Woodsman;
                case ShopKind.Prospector: return FigurePreset.Prospector;
                default: return FigurePreset.Merchant;
            }
        }

        /// <summary>A preset as designed, before any variation.</summary>
        public static FigureLook Of(FigurePreset preset)
        {
            FigureLook look = Base();
            switch (preset)
            {
                case FigurePreset.Blacksmith:
                    // Broad, bare forearms, a long leather apron.
                    look.Build = 1.12f;
                    look.Shirt = new Color(0.52f, 0.5f, 0.46f);
                    look.Accent = new Color(0.28f, 0.18f, 0.12f);
                    look.Outfit = FigureOutfit.Apron | FigureOutfit.Beard | FigureOutfit.Belt | FigureOutfit.BareArms;
                    look.Prop = FigureProp.Hammer;
                    break;
                case FigurePreset.MineKeeper:
                    look.Shirt = new Color(0.66f, 0.52f, 0.28f);
                    look.Hair = new Color(0.3f, 0.2f, 0.12f);
                    look.Hat = new Color(0.42f, 0.3f, 0.18f);
                    look.HatStyle = FigureHat.Cap;
                    look.Outfit = FigureOutfit.Beard | FigureOutfit.Belt;
                    look.Prop = FigureProp.Pick;
                    break;
                case FigurePreset.Merchant:
                    look.Shirt = new Color(0.25f, 0.48f, 0.3f);
                    look.Accent = new Color(0.82f, 0.78f, 0.66f);
                    look.Hair = new Color(0.12f, 0.1f, 0.08f);
                    look.Hat = new Color(0.55f, 0.2f, 0.18f);
                    look.HatStyle = FigureHat.Brimmed;
                    look.Outfit = FigureOutfit.Apron | FigureOutfit.Belt;
                    look.Prop = FigureProp.Broom;
                    break;
                case FigurePreset.Woodsman:
                    look.Shirt = new Color(0.66f, 0.18f, 0.16f);
                    look.Hair = new Color(0.5f, 0.3f, 0.15f);
                    look.Hat = new Color(0.2f, 0.28f, 0.22f);
                    look.HatStyle = FigureHat.Cap;
                    look.Accent = new Color(0.24f, 0.3f, 0.24f);
                    look.Outfit = FigureOutfit.Beard | FigureOutfit.Belt | FigureOutfit.Scarf;
                    look.Prop = FigureProp.Stick;
                    break;
                case FigurePreset.Prospector:
                    look.Build = 0.9f;
                    look.Shirt = new Color(0.24f, 0.32f, 0.55f);
                    look.Hair = new Color(0.85f, 0.85f, 0.82f);
                    look.Hat = new Color(0.2f, 0.24f, 0.4f);
                    look.HatStyle = FigureHat.Brimmed;
                    look.Outfit = FigureOutfit.Robe | FigureOutfit.Belt;
                    look.Prop = FigureProp.Book;
                    break;
                case FigurePreset.Worker:
                    // The company's diggers (GL566): dull, faded cloth with patches and ragged hems, a worn cap.
                    look.Shirt = WorkerCloth[0];
                    look.Accent = WorkerPatch[0];
                    look.Hair = new Color(0.25f, 0.18f, 0.12f);
                    look.Hat = new Color(0.3f, 0.28f, 0.25f);
                    look.HatStyle = FigureHat.Cap;
                    look.Outfit = FigureOutfit.Patched | FigureOutfit.Beard;
                    look.Prop = FigureProp.Pick;
                    break;
                case FigurePreset.Foreman:
                    // The crew lead (GL566): a fine long coat with gold buttons, a cravat and a tall hat, a grey beard.
                    look.Build = 1.12f;
                    look.Shirt = new Color(0.4f, 0.1f, 0.16f);
                    look.Hair = new Color(0.6f, 0.58f, 0.55f);
                    look.Hat = new Color(0.1f, 0.1f, 0.12f);
                    look.HatStyle = FigureHat.TopHat;
                    look.Accent = new Color(0.85f, 0.68f, 0.25f);
                    look.Outfit = FigureOutfit.Coat | FigureOutfit.Beard;
                    look.Prop = FigureProp.Pick;
                    break;
                case FigurePreset.Adventurer:
                    // The lone adventurer (GL476): a travelling cloak, a wide hat, a beard, a pack and a walking staff.
                    look.Shirt = new Color(0.36f, 0.42f, 0.26f);
                    look.Hair = new Color(0.55f, 0.5f, 0.45f);
                    look.Hat = new Color(0.32f, 0.24f, 0.16f);
                    look.HatStyle = FigureHat.Brimmed;
                    look.Accent = new Color(0.4f, 0.3f, 0.2f);
                    look.Outfit = FigureOutfit.Cloak | FigureOutfit.Beard | FigureOutfit.Belt | FigureOutfit.Backpack;
                    look.Prop = FigureProp.Staff;
                    break;
                case FigurePreset.Thief:
                    // The thief (GL475): dark clothes, a hood, a scarf, a dagger in hand.
                    look.Build = 0.9f;
                    look.Shirt = new Color(0.2f, 0.2f, 0.22f);
                    look.Trousers = new Color(0.16f, 0.16f, 0.18f);
                    look.Hair = new Color(0.15f, 0.1f, 0.07f);
                    look.Hat = new Color(0.14f, 0.13f, 0.16f);
                    look.HatStyle = FigureHat.Hood;
                    look.Accent = new Color(0.32f, 0.12f, 0.12f);
                    look.Outfit = FigureOutfit.Belt | FigureOutfit.Scarf;
                    look.Prop = FigureProp.Dagger;
                    break;
                case FigurePreset.Carter:
                    // The carter who pulls the trade cart (GL477): a work shirt, an apron and a cap.
                    look.Shirt = new Color(0.55f, 0.45f, 0.3f);
                    look.Accent = new Color(0.35f, 0.25f, 0.18f);
                    look.Hair = new Color(0.25f, 0.16f, 0.1f);
                    look.Hat = new Color(0.3f, 0.28f, 0.25f);
                    look.HatStyle = FigureHat.Cap;
                    look.Outfit = FigureOutfit.Apron | FigureOutfit.Belt;
                    break;
                case FigurePreset.Sailor:
                    // A hand on a boat (GL618): a blue shirt with bare arms, pale trousers, a cap.
                    look.Shirt = new Color(0.25f, 0.36f, 0.5f);
                    look.Trousers = new Color(0.62f, 0.58f, 0.48f);
                    look.Accent = new Color(0.7f, 0.66f, 0.56f);
                    look.Hair = new Color(0.25f, 0.16f, 0.1f);
                    look.Hat = new Color(0.22f, 0.26f, 0.34f);
                    look.HatStyle = FigureHat.Cap;
                    look.Outfit = FigureOutfit.Belt | FigureOutfit.BareArms;
                    break;
                default:
                    // The player: the blue-grey sleeve of the first-person arm, a pack on the back.
                    look.Shirt = new Color(0.24f, 0.28f, 0.36f);
                    look.Skin = new Color(0.78f, 0.56f, 0.42f);
                    look.Hair = new Color(0.3f, 0.2f, 0.12f);
                    look.Accent = new Color(0.45f, 0.33f, 0.2f);
                    look.Outfit = FigureOutfit.Belt | FigureOutfit.Backpack;
                    break;
            }
            return look;
        }

        /// <summary>
        /// A preset as one person wears it: the same seed always gives the same person. The player is never varied.
        /// </summary>
        public static FigureLook Varied(FigurePreset preset, int seed)
        {
            FigureLook look = Of(preset);
            if (preset == FigurePreset.Player)
            {
                return look;
            }
            var random = new System.Random(seed);
            look.Height *= Heights[random.Next(Heights.Length)];
            look.Build *= Builds[random.Next(Builds.Length)];
            look.Skin = SkinTones[random.Next(SkinTones.Length)];
            float fade = Fades[random.Next(Fades.Length)];
            int hair = random.Next(HairTones.Length);
            int cloth = random.Next(WorkerCloth.Length);
            int patch = random.Next(WorkerPatch.Length);
            bool beard = random.Next(3) > 0;
            if (preset == FigurePreset.Worker)
            {
                look.Shirt = WorkerCloth[cloth];
                look.Accent = WorkerPatch[patch];
                look.Hair = HairTones[hair];
                look.Outfit = beard ? look.Outfit | FigureOutfit.Beard : look.Outfit & ~FigureOutfit.Beard;
            }
            else if (preset == FigurePreset.Carter || preset == FigurePreset.Thief || preset == FigurePreset.Sailor)
            {
                look.Hair = HairTones[hair];
            }
            look.Shirt = Faded(look.Shirt, fade);
            look.Trousers = Faded(look.Trousers, fade);
            return look;
        }

        private static FigureLook Base()
        {
            return new FigureLook
            {
                Height = 1f, Build = 1f, HeadSize = 1.1f,
                Skin = SkinTones[0], Hair = HairTones[0], Shirt = Color.gray, Trousers = DarkCloth, Boots = Leather, Accent = Leather, Hat = Leather,
            };
        }

        private static Color Faded(Color color, float by)
        {
            return new Color(Mathf.Clamp01(color.r * by), Mathf.Clamp01(color.g * by), Mathf.Clamp01(color.b * by), 1f);
        }
    }
}
