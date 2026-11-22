using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Il2Cpp;
using Il2CppLE.Factions;
using Il2CppTMPro;
using LastEpoch_Hud.Scripts.Core.Crafting;
using LastEpoch_Hud.Scripts.ModUI.Shell.Host;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace LastEpoch_Hud.Scripts.Mods.Items.Crafting;

internal static partial class Items_MemoryReflectorWindow
{
    // ---------------------------------------------------------------- affix plates (item B's affixes)

    private static readonly List<AffixCardView> rows = new();
    private static readonly Regex ColourTags = new("</?color[^>]*>|</?link[^>]*>", RegexOptions.Compiled);
    private static readonly Regex LineBreaks = new(@"<br\s*/?>|\r\n|\r| | |\v|\\n", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>The affix worded the way the item's own tooltip words it, with its roll.</summary>
    private static string Wording(Items_MemoryReflector.Candidate c)
    {
        try
        {
            ItemDataUnpacked data = itemBForText.TryCast<ItemDataUnpacked>();
            if (data != null && c.Source != null)
            {
                string text = TooltipItemManager.FormatAffix(data, c.Source, TooltipMode.DefaultInfo, TooltipItemManager.SlotType.CRAFTING,
                    false, -1, -1, false, false, ValueRangeFormat.None, true, false);
                text = LineBreaks.Replace(ColourTags.Replace(text ?? "", ""), "\n");
                string[] lines = text.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).ToArray();
                if (lines.Length > 0)
                {
                    return string.Join("\n", lines);
                }
            }
        }
        catch (Exception)
        {
        }
        AffixList.Affix def = AffixList.get()?.GetAffix(c.AffixId);
        return def == null ? "Affix " + c.AffixId : def.affixDisplayName;
    }

    private static ItemData itemBForText => itemB;

    private static void AffixPlate(Items_MemoryReflector.Candidate c, float x, float y)
    {
        string text = Wording(c);
        if (!c.CanGo && c.Why.Length > 0)
        {
            text += "\n<size=75%>(" + c.Why + ")</size>";
        }
        ItemData owner = itemB ?? animB;
        var data = new AffixCardData
        {
            Text = text,
            Tier = c.Tier,
            Group = c.Placement == 1 ? AffixCard.Prefix : c.Placement == 2 ? AffixCard.Suffix : -1,
            Encapsulated = owner != null && c.Source != null && Items_Encapsulate.IsEncapsulated(owner, c.Source),
            Reminded = c.Reminded,
            Static = !c.CanGo,
            On = c.CanGo && c.AffixId == selectedId,
        };
        AffixCardView view = AffixCard.Build(Content, labelTemplate, bodyFont, data, x, y, dynamic);
        if (c.CanGo)
        {
            Button button = view.Bg.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            int id = c.AffixId;
            button.onClick.AddListener((Action)(() =>
            {
                if (animating)
                {
                    return;
                }
                selectedId = selectedId == id ? -1 : id;
                dirty = true;
            }));
        }
        rows.Add(view);
    }
}
