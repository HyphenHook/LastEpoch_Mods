using System;
using LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter.Bar;
using LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter.Config.Resolve;
using UnityEngine;

namespace LastEpoch_Hud.Scripts.Mods.Items.CustomItems.Headhunter.Bar;

/// <summary>Per-frame hover check that drives the bar tooltip.</summary>
internal static class HeadhunterBarHover
{
    private static readonly HeadhunterHoverTracker _hover = new();
    private static int _index = -1;

    public static void Tick()
    {
        try
        {
            int index = HoveredIndex();
            int row = HeadhunterBuffBarView.RowAt(index);
            int stacks = HeadhunterBuffBarView.StacksAt(index);
            if (!_hover.Changed(row, stacks, HeadhunterBuffBarView.LayoutVersion))
            {
                return;
            }

            _index = index;
            Redraw(row, stacks);
        }
        catch (Exception ex)
        {
            ErrorLog.Report(ex, "Headhunter bar tooltip");
        }
    }

    private static int HoveredIndex()
    {
        if (!HeadhunterBuffBarView.IsVisible)
        {
            return -1;
        }

        Vector3 mouse = Input.mousePosition;
        return HeadhunterBuffBarView.IndexAt(mouse.x, mouse.y);
    }

    private static void Redraw(int row, int stacks)
    {
        HeadhunterResolvedConfig config = HeadhunterConfigLoader.Resolved;
        if (row < 0 || config == null || row >= config.Stats.Count)
        {
            HeadhunterBuffBarView.HideTooltip();
            return;
        }

        HeadhunterBuffStat stat = config.Stats[row];
        HeadhunterStatNames.TryRead(stat.StatId, out string gameName, out bool addedAsPercent);
        string label = HeadhunterBuffLabel.Format(
            gameName,
            HeadhunterStatNames.EnumName(stat.StatId),
            stat.AddedFor(stacks),
            stat.IncreasedFor(stacks),
            addedAsPercent,
            stacks,
            HeadhunterStatNames.GameTagName(stat.Tags),
            stat.Tags == 0 ? null : HeadhunterStatNames.TagEnumName(stat.Tags)
        );
        HeadhunterBuffBarView.ShowTooltip(_index, label);
    }
}
