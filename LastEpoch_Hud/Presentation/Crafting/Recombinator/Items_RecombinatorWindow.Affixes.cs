using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Il2Cpp;
using Il2CppTMPro;
using LastEpoch_Hud.Scripts.Core.Crafting;
using LastEpoch_Hud.Scripts.Mods.Character;
using LastEpoch_Hud.Scripts.ModUI.ForceDrop;
using LastEpoch_Hud.Scripts.ModUI.Shell.Host;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace LastEpoch_Hud.Scripts.Mods.Items.Crafting;

internal static partial class Items_RecombinatorWindow
{
    private static readonly List<AffixCardView> rows = new();
    private static readonly Regex ColourTags = new("</?color[^>]*>|</?link[^>]*>", RegexOptions.Compiled);

    /// <summary>The affix worded the way the item's own tooltip words it, with its roll. May run over several lines.</summary>
    private static int loggedAffixes;
    private static readonly Regex LineBreaks = new(@"<br\s*/?>|\r\n|\r| | |\v|\\n", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static string ItemWording(RecombAffix a)
    {
        try
        {
            ItemData owner = a.Source == 0 ? offer.A : offer.B;
            ItemAffix affix = owner.affixes.ToArray().FirstOrDefault(x => x != null && !x.IsSealed && x.affixId == a.AffixId);
            ItemDataUnpacked data = owner.TryCast<ItemDataUnpacked>();
            if (affix != null && data != null)
            {
                string text = TooltipItemManager.FormatAffix(data, affix, TooltipMode.DefaultInfo, TooltipItemManager.SlotType.CRAFTING,
                    false, -1, -1, false, false, ValueRangeFormat.None, true, false);
                if (loggedAffixes < 12)
                {
                    loggedAffixes++;
                    Main.logger_instance?.Msg("Recombinator affix text: " + (text ?? "null").Replace("\n", "<LF>").Replace("\r", "<CR>"));
                }
                text = ColourTags.Replace(text ?? "", "");
                // whatever the game uses to break a line becomes a real line break
                text = LineBreaks.Replace(text, "\n");
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
        AffixList.Affix def = AffixList.get()?.GetAffix(a.AffixId);
        return def == null ? "Affix " + a.AffixId : NativeItemNames.AffixName(def);
    }

    private static Color TierColour(int tier) => tier switch
    {
        <= 1 => new Color(0.66f, 0.65f, 0.62f),
        2 => new Color(0.62f, 0.74f, 0.64f),
        3 => new Color(0.50f, 0.80f, 0.55f),
        4 => new Color(0.48f, 0.68f, 1f),
        5 => new Color(0.70f, 0.56f, 1f),
        6 => new Color(0.90f, 0.52f, 0.92f),
        _ => new Color(1f, 0.66f, 0.28f),
    };

    private const float ColumnWidth = 176f, AffixRowHeight = 34f;

    private static void ListAffixes(List<RecombAffix> list, float x, float y)
    {
        float cursor = y;
        foreach (RecombAffix a in list)
        {
            string text = ItemWording(a);
            if (!(a.LegalOnA && a.LegalOnB))
            {
                text += "\n<size=75%>(fits one base only)</size>";
            }
            AffixRow(a, text, x, cursor, deterministic && selected.Contains(Key(a)), deterministic);
            cursor += AffixRowHeight + AffixCard.Gap;
        }
    }

    /// <summary>One affix as a card, which is the same in every window. In the deterministic mode a click chooses it.</summary>
    private static void AffixRow(RecombAffix a, string text, float x, float y, bool on, bool interactive)
    {
        var data = new AffixCardData
        {
            Text = text,
            Tier = a.DisplayedTier,
            Group = a.Placement == 1 ? AffixCard.Prefix : a.Placement == 2 ? AffixCard.Suffix : -1,
            Reminded = a.Reminded,
            Static = !interactive,
            On = on,
        };
        AffixCardView view = AffixCard.Build(Content, labelTemplate, bodyFont, data, x, y, dynamic);
        view.Rect.sizeDelta = new Vector2(ColumnWidth, AffixRowHeight);
        view.Tier.fontSize = 7f;
        view.Name.fontSizeMax = 11f;
        view.Name.overflowMode = TextOverflowModes.Ellipsis;
        if (interactive)
        {
            Button button = view.Bg.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener((Action)(() => Toggle(a)));
        }
        rows.Add(view);
    }

    private static void TickRows()
    {
        UnityEngine.Camera cam = null;
        Canvas canvas = root.GetComponentInParent<Canvas>();
        if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            cam = canvas.worldCamera;
        }
        AffixCard.Tick(rows, false, cam);
    }
}
