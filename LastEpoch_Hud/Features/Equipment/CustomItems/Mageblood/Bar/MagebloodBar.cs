using System.Collections.Generic;
using LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter.Bar;
using LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter.Config;
using LastEpoch_Hud.Scripts.Core.CustomItems.Mageblood;
using LastEpoch_Hud.Scripts.Core.CustomItems.Mageblood.Bar;
using LastEpoch_Hud.Scripts.Core.CustomItems.Mageblood.Config;
using LastEpoch_Hud.Scripts.Mods.Items.CustomItems.Headhunter.Bar;
using UnityEngine;

namespace LastEpoch_Hud.Scripts.Mods.Items.CustomItems.Mageblood.Bar;

/// <summary>The Mageblood flask icon row at the Headhunter bar spot.</summary>
internal static class MagebloodBar
{
    private static readonly BuffBarFrame _frame = new("MagebloodBuffBar");
    private static readonly MagebloodBarContent _content = new();
    private static readonly List<MagebloodBarSlot> _slots = new();
    private static readonly List<MagebloodFlask> _shown = new();

    public static bool IsVisible => _frame.IsVisible;

    public static int LayoutVersion => _frame.LayoutVersion;

    public static int ContentVersion => _content.Version;

    /// <summary>Shows the first count flasks; hides the bar when there are none.</summary>
    public static void Sync(
        IReadOnlyList<MagebloodFlask> flasks,
        int count,
        HeadhunterBarSettings settings
    )
    {
        int active = flasks == null ? 0 : MagebloodFlaskRule.ActiveCount(flasks.Count, count);
        if (active == 0 || !_frame.EnsureCreated(out bool created))
        {
            Hide();
            return;
        }

        if (created)
        {
            _slots.Clear();
            _content.Reset();
        }

        if (_content.NeedsRebuild(flasks, active))
        {
            Rebuild(flasks, active);
        }

        var grid = new HeadhunterBarGrid(active, settings.PerRow);
        if (_frame.Show(grid, settings))
        {
            PlaceSlots(grid);
        }
    }

    public static void Hide()
    {
        if (!_frame.IsVisible)
        {
            return;
        }

        _frame.Hide();
        _content.Reset();
    }

    public static int IndexAt(float x, float y)
    {
        return _frame.IndexAt(x, y);
    }

    public static void ShowTooltip(int index)
    {
        if (index < 0 || index >= _shown.Count || _slots.Count == 0)
        {
            return;
        }

        _frame.ShowTooltip(index, MagebloodFlaskTooltip.Text(_shown[index]), _slots[0].TextFont);
    }

    public static void HideTooltip()
    {
        _frame.HideTooltip();
    }

    private static void Rebuild(IReadOnlyList<MagebloodFlask> flasks, int count)
    {
        _shown.Clear();
        bool complete = true;
        for (int i = 0; i < count; i++)
        {
            complete &= ShowFlask(SlotAt(i), flasks[i]);
            _shown.Add(flasks[i]);
        }

        for (int i = count; i < _slots.Count; i++)
        {
            _slots[i].Hide();
        }

        _content.Built(flasks, count, complete);
    }

    private static bool ShowFlask(MagebloodBarSlot slot, MagebloodFlask flask)
    {
        Sprite sprite = MagebloodFlaskIcons.For(flask);
        if (sprite.IsNullOrDestroyed())
        {
            slot.Hide();
            return false;
        }

        slot.Show(sprite);
        return true;
    }

    private static void PlaceSlots(HeadhunterBarGrid grid)
    {
        for (int i = 0; i < grid.Count; i++)
        {
            _slots[i].PlaceAt(grid.CellX(i), grid.CellBottom(i));
        }
    }

    private static MagebloodBarSlot SlotAt(int index)
    {
        while (_slots.Count <= index)
        {
            GameObject entry = Object.Instantiate(
                HeadhunterBuffBarAssets.EntryPrefab,
                _frame.Panel
            );
            _slots.Add(new MagebloodBarSlot(entry));
        }

        return _slots[index];
    }
}
