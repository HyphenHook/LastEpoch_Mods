using System.Collections.Generic;
using LastEpoch_Hud.Scripts.Core.CustomItems.Mageblood.Config;

namespace LastEpoch_Hud.Scripts.Core.CustomItems.Mageblood.Menu;

/// <summary>One slot row of the Flasks card: its flask and the sliders of its non-zero fields.</summary>
public sealed class MagebloodSlotLayout
{
    public MagebloodSlotLayout(MagebloodFlask flask, IReadOnlyList<MagebloodSliderSlot> sliders)
    {
        Flask = flask;
        Sliders = sliders;
    }

    public MagebloodFlask Flask { get; }
    public IReadOnlyList<MagebloodSliderSlot> Sliders { get; }

    public MagebloodStatEntry Row(MagebloodSliderSlot slider)
    {
        return Flask.Rows[slider.RowIndex];
    }

    /// <summary>The slider's field value as written in the file.</summary>
    public float FileValue(MagebloodSliderSlot slider)
    {
        return FieldValue(Row(slider), slider.Target.Field);
    }

    /// <summary>The slider's range, from the value it shows.</summary>
    public MagebloodSliderRange Range(MagebloodSliderSlot slider, bool addedAsPercent)
    {
        MagebloodStatField field = slider.Target.Field;
        float display = MagebloodValueScale.ToDisplay(FileValue(slider), field, addedAsPercent);
        return MagebloodSliderRanges.ForStat(Row(slider).Stat, field, addedAsPercent, display);
    }

    public static float FieldValue(MagebloodStatEntry row, MagebloodStatField field)
    {
        return field switch
        {
            MagebloodStatField.Added => row.Added,
            MagebloodStatField.Increased => row.Increased,
            _ => row.More,
        };
    }
}
