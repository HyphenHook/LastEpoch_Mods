using System;
using System.Collections.Generic;
using System.Linq;
using LastEpoch_Hud.Scripts.Core.CustomItems.Mageblood.Config;

namespace LastEpoch_Hud.Scripts.Core.CustomItems.Mageblood.Menu;

/// <summary>What the Flasks card shows: slots, their sliders and the dropdown options.</summary>
public sealed class MagebloodMenuLayout
{
    private static readonly MagebloodStatField[] _fields =
    {
        MagebloodStatField.Added,
        MagebloodStatField.Increased,
        MagebloodStatField.More,
    };

    private MagebloodMenuLayout(
        IReadOnlyList<string> names,
        IReadOnlyList<string> options,
        IReadOnlyList<MagebloodSlotLayout> slots
    )
    {
        Names = names;
        Options = options;
        Slots = slots;
    }

    /// <summary>Flask names in file order.</summary>
    public IReadOnlyList<string> Names { get; }

    /// <summary>Dropdown entries: <see cref="Names"/> in <see cref="MagebloodSlotOptions.Order"/>.</summary>
    public IReadOnlyList<string> Options { get; }

    public IReadOnlyList<MagebloodSlotLayout> Slots { get; }

    public static MagebloodMenuLayout Build(IReadOnlyList<MagebloodFlask> flasks)
    {
        if (flasks == null || flasks.Count == 0)
        {
            return new MagebloodMenuLayout(
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<MagebloodSlotLayout>()
            );
        }

        string[] names = flasks.Select(f => f.Name).ToArray();
        var slots = new List<MagebloodSlotLayout>();
        int count = Math.Min(MagebloodFlaskSlots.Max, flasks.Count);
        for (int i = 0; i < count; i++)
        {
            slots.Add(new MagebloodSlotLayout(flasks[i], SlidersOf(flasks[i])));
        }
        return new MagebloodMenuLayout(names, MagebloodSlotOptions.Order(names), slots);
    }

    /// <summary>The option index of the slot's flask, or -1 when the slot does not exist.</summary>
    public int OptionIndex(int slot)
    {
        if (slot < 0 || slot >= Slots.Count)
        {
            return -1;
        }

        return IndexOf(Options, Slots[slot].Flask.Name);
    }

    /// <summary>True when only values differ, so the controls can stay.</summary>
    public bool SameShape(MagebloodMenuLayout other)
    {
        if (other == null || !Names.SequenceEqual(other.Names))
        {
            return false;
        }

        for (int i = 0; i < Slots.Count; i++)
        {
            if (!Slots[i].Sliders.SequenceEqual(other.Slots[i].Sliders))
            {
                return false;
            }
        }
        return true;
    }

    private static List<MagebloodSliderSlot> SlidersOf(MagebloodFlask flask)
    {
        var sliders = new List<MagebloodSliderSlot>();
        for (int row = 0; row < flask.Rows.Count; row++)
        {
            AddRowSliders(sliders, flask, row);
        }
        return sliders;
    }

    private static void AddRowSliders(
        List<MagebloodSliderSlot> sliders,
        MagebloodFlask flask,
        int row
    )
    {
        MagebloodStatEntry entry = flask.Rows[row];
        foreach (MagebloodStatField field in _fields)
        {
            if (MagebloodSlotLayout.FieldValue(entry, field) == 0f)
            {
                continue;
            }

            var target = MagebloodValueTarget.ForStat(flask.Name, entry.RowText, field);
            sliders.Add(new MagebloodSliderSlot(row, target));
        }
    }

    private static int IndexOf(IReadOnlyList<string> list, string name)
    {
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] == name)
            {
                return i;
            }
        }
        return -1;
    }
}
