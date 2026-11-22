using System.Collections.Generic;
using LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter.Bar;
using LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter.Config;
using UnityEngine;

namespace LastEpoch_Hud.Scripts.Mods.Items.CustomItems.Headhunter.Bar;

/// <summary>Owns the Headhunter bar slots and shows them in the shared bar frame.</summary>
internal static class HeadhunterBuffBarView
{
    private const float PausedAlpha = 0.45f;
    private static readonly List<HeadhunterBarSlot> _slots = new();
    private static readonly BuffBarFrame _frame = new("HeadhunterBuffBar");

    public static bool IsVisible => _frame.IsVisible;

    public static int LayoutVersion => _frame.LayoutVersion;

    public static int IndexAt(float x, float y)
    {
        return _frame.IndexAt(x, y);
    }

    public static int RowAt(int index)
    {
        if (index < 0 || index >= _frame.Grid.Count)
        {
            return -1;
        }

        return _slots[index].Row;
    }

    public static int StacksAt(int index)
    {
        if (index < 0 || index >= _frame.Grid.Count)
        {
            return 0;
        }

        return _slots[index].Stacks;
    }

    public static void ShowTooltip(int index, string text)
    {
        if (index < 0 || index >= _slots.Count)
        {
            return;
        }

        _frame.ShowTooltip(index, text, _slots[0].TextFont);
    }

    public static void HideTooltip()
    {
        _frame.HideTooltip();
    }

    public static void Show(
        IReadOnlyList<HeadhunterBarEntry> entries,
        HeadhunterBarSettings settings,
        bool paused
    )
    {
        if (entries.Count == 0 || !_frame.EnsureCreated(out bool created))
        {
            Hide();
            return;
        }

        if (created)
        {
            _slots.Clear();
        }

        _frame.SetAlpha(paused ? PausedAlpha : 1f);
        for (int i = 0; i < entries.Count; i++)
        {
            SlotAt(i).Show(entries[i]);
        }

        for (int i = entries.Count; i < _slots.Count; i++)
        {
            _slots[i].Hide();
        }

        var grid = new HeadhunterBarGrid(entries.Count, settings.PerRow);
        if (_frame.Show(grid, settings))
        {
            PlaceSlots(grid);
        }
    }

    public static void Hide()
    {
        _frame.Hide();
    }

    private static void PlaceSlots(HeadhunterBarGrid grid)
    {
        for (int i = 0; i < grid.Count; i++)
        {
            _slots[i].PlaceAt(grid.CellX(i), grid.CellBottom(i));
        }
    }

    private static HeadhunterBarSlot SlotAt(int index)
    {
        while (_slots.Count <= index)
        {
            GameObject entry = Object.Instantiate(
                HeadhunterBuffBarAssets.EntryPrefab,
                _frame.Panel
            );
            _slots.Add(new HeadhunterBarSlot(entry));
        }

        return _slots[index];
    }
}
