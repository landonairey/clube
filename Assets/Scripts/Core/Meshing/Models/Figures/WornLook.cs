using System;
using UnityEngine;

namespace Clube.Core
{
    /// <summary>
    /// Dresses a figure in what is worn (list inventory prototype, 20261124b item 5): each body slot's garment shows on its
    /// part in the garment's own colour (its icon tint), and an empty slot shows the body. Nothing on the legs leaves a
    /// plain linen wrap round the hips, so the figure is never naked. A garment worth no outfit points (the rags) is
    /// drawn torn and patched.
    /// </summary>
    public static class WornLook
    {
        /// <summary>The undyed linen of the loin wrap.</summary>
        public static readonly Color WrapCloth = new Color(0.8f, 0.74f, 0.6f, 1f);

        /// <summary>
        /// <paramref name="body"/> (the figure's proportions, skin and hair) in the items <paramref name="worn"/> gives for
        /// each <see cref="WearSlot"/> (null for an empty slot).
        /// </summary>
        public static FigureLook Dress(FigureLook body, Func<WearSlot, ItemDefinition> worn)
        {
            FigureLook look = body;
            look.Outfit = body.Outfit & FigureOutfit.Backpack;
            look.Prop = FigureProp.None;
            look.Wrap = Color.clear;
            look.Bracers = Color.clear;
            look.Gloves = Color.clear;

            ItemDefinition head = worn(WearSlot.Head);
            look.HatStyle = head != null ? FigureHat.Cap : FigureHat.None;
            look.Hat = head != null ? Opaque(head.IconTint) : body.Hat;

            ItemDefinition chest = worn(WearSlot.Chest);
            if (chest != null)
            {
                look.Shirt = Opaque(chest.IconTint);
                look.Accent = Shade(look.Shirt, 0.8f);
                look.Outfit |= FigureOutfit.Belt;
                if (chest.OutfitPoints <= 0)
                {
                    look.Outfit |= FigureOutfit.Patched;
                }
            }
            else
            {
                look.Shirt = body.Skin;
                look.Accent = Shade(body.Skin, 0.8f);
                look.Outfit |= FigureOutfit.BareArms;
            }

            ItemDefinition arms = worn(WearSlot.Arms);
            if (arms != null)
            {
                look.Bracers = Opaque(arms.IconTint);
            }
            ItemDefinition hands = worn(WearSlot.Hands);
            if (hands != null)
            {
                look.Gloves = Opaque(hands.IconTint);
            }

            ItemDefinition legs = worn(WearSlot.Legs);
            if (legs != null)
            {
                look.Trousers = Opaque(legs.IconTint);
            }
            else
            {
                look.Trousers = body.Skin;
                look.Wrap = WrapCloth;
            }

            ItemDefinition feet = worn(WearSlot.Feet);
            look.Boots = feet != null ? Opaque(feet.IconTint) : Shade(body.Skin, 0.95f);
            return look;
        }

        private static Color Opaque(Color color)
        {
            return new Color(color.r, color.g, color.b, 1f);
        }

        private static Color Shade(Color color, float by)
        {
            return new Color(Mathf.Clamp01(color.r * by), Mathf.Clamp01(color.g * by), Mathf.Clamp01(color.b * by), 1f);
        }
    }
}
