using System;
using LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter.Bar;
using UnityEngine;

namespace LastEpoch_Hud.Scripts.Mods.Items.CustomItems.Mageblood.Bar;

/// <summary>Per-frame hover check that drives the flask bar tooltip.</summary>
internal static class MagebloodBarHover
{
    private static readonly HeadhunterHoverTracker _hover = new();

    public static void Tick()
    {
        try
        {
            int index = HoveredIndex();
            if (!_hover.Changed(index, MagebloodBar.ContentVersion, MagebloodBar.LayoutVersion))
            {
                return;
            }

            Redraw(index);
        }
        catch (Exception ex)
        {
            ErrorLog.Report(ex, "Mageblood bar tooltip");
        }
    }

    private static int HoveredIndex()
    {
        if (!MagebloodBar.IsVisible)
        {
            return -1;
        }

        Vector3 mouse = Input.mousePosition;
        return MagebloodBar.IndexAt(mouse.x, mouse.y);
    }

    private static void Redraw(int index)
    {
        if (index < 0)
        {
            MagebloodBar.HideTooltip();
            return;
        }

        MagebloodBar.ShowTooltip(index);
    }
}
