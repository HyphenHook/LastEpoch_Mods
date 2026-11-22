using System;
using System.Collections.Generic;
using System.Linq;
using LastEpoch_Hud.Scripts.Core.Crafting;
using Il2Cpp;
using Il2CppTMPro;
using LastEpoch_Hud.Scripts.ModUI.Shell.Host;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace LastEpoch_Hud.Scripts.Mods.Items.Crafting;

internal static partial class Items_AdvancedForgeWindow
{
    // ---------------------------------------------------------------- the item's affixes

    private sealed class Line
    {
        public string Text;
        public int Tier;
        public bool Encapsulated, Reminded;
        public int Group; // AffixCard.Prefix, Suffix, Sealed, Primordial or Corrupted
    }

    private static List<Line> ReadAffixes()
    {
        var list = new List<Line>();
        ItemDataUnpacked data = item.TryCast<ItemDataUnpacked>();
        foreach (ItemAffix a in item.affixes.ToArray())
        {
            if (a == null)
            {
                continue;
            }
            string text = "Affix " + a.affixId;
            try
            {
                if (data != null)
                {
                    text = TooltipItemManager.FormatAffix(data, a, TooltipMode.DefaultInfo, TooltipItemManager.SlotType.CRAFTING,
                        false, -1, -1, false, false, ValueRangeFormat.None, true, false);
                    text = LineBreaks.Replace(ColourTags.Replace(text ?? "", ""), "\n");
                    text = string.Join("\n", text.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0));
                }
            }
            catch (Exception)
            {
            }
            int group = a.IsSealedCorrupted ? AffixCard.Corrupted : a.IsSealedPrimordial ? AffixCard.Primordial : a.IsSealed ? AffixCard.Sealed
                : a.affixType == AffixList.AffixType.PREFIX ? AffixCard.Prefix : AffixCard.Suffix;
            bool encapsulated = Items_Encapsulate.IsEncapsulated(item, a);
            list.Add(new Line { Text = text, Tier = a.DisplayTier, Group = group, Encapsulated = encapsulated, Reminded = !encapsulated && Items_Reminiscence.IsReminded(item, a) });
        }
        return list;
    }

    private static void Card(Line l, float x, float y)
    {
        var data = new AffixCardData { Text = l.Text, Tier = l.Tier, Group = l.Group, Encapsulated = l.Encapsulated, Reminded = l.Reminded, ReadOnly = true };
        AffixCard.Build(Content, labelTemplate, bodyFont, data, x, y, dynamic);
    }

    /// <summary>
    /// The item's affixes as cards, the same cards as in the other windows: prefixes in a column on the left, suffixes on
    /// the right, and below them the sealed, primordial and corrupted ones.
    /// </summary>
    private static void AffixPlates(float top, ObscuredMemory memory)
    {
        const float gap = 24;
        List<Line> all = ReadAffixes();
        if (memory != null)
        {
            all.Add(new Line { Text = "Obscured Memory", Tier = memory.Tier + 1,
                Group = memory.AffixType == (int)AffixList.AffixType.PREFIX ? AffixCard.Prefix : AffixCard.Suffix });
        }
        float leftX = CenterX - AffixCard.Width - gap / 2, rightX = CenterX + gap / 2;
        float prefixEnd = Column(all.Where(l => l.Group == AffixCard.Prefix).ToList(), leftX, top, AffixCard.Prefix);
        float suffixEnd = Column(all.Where(l => l.Group == AffixCard.Suffix).ToList(), rightX, top, AffixCard.Suffix);
        float y = Math.Max(prefixEnd, suffixEnd);

        List<Line> special = all.Where(l => l.Group >= AffixCard.Sealed).ToList();
        if (special.Count > 0)
        {
            y += 10;
            Label("Sealed, primordial and corrupted", CenterX - 200, y, 400, 20, 13, new Color(0.72f, 0.66f, 0.8f), TextAlignmentOptions.Center);
            y += 24;
            for (int i = 0; i < special.Count; i++)
            {
                Card(special[i], i % 2 == 0 ? leftX : rightX, y);
                if (i % 2 == 1 || i == special.Count - 1)
                {
                    y += AffixCard.Height + AffixCard.Gap;
                }
            }
        }
    }

    /// <summary>One column of cards under a coloured heading. Returns where it ends.</summary>
    private static float Column(List<Line> lines, float x, float y, int group)
    {
        Color colour = AffixCard.GroupColour(group);
        Label(AffixCard.GroupName(group) + "  " + lines.Count, x + 7f, y, AffixCard.Width - 14f, 20f, 13f, colour, TextAlignmentOptions.MidlineLeft);
        y += 26f;
        if (lines.Count == 0)
        {
            Label("Empty", x + 7f, y, AffixCard.Width - 14f, 20f, 12f, Dim, TextAlignmentOptions.MidlineLeft);
            return y + 22f;
        }
        foreach (Line l in lines)
        {
            Card(l, x, y);
            y += AffixCard.Height + AffixCard.Gap;
        }
        return y;
    }
}
