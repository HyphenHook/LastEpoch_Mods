using System;

namespace LastEpoch_Hud.Scripts.Core.CustomItems.Mageblood.Menu;

/// <summary>Slider bounds per editable value; a bound stretches when the file holds more.</summary>
public static class MagebloodSliderRanges
{
    public const float MaxResistancesMax = 20f;

    public static MagebloodSliderRange ForMaxResistances(float value)
    {
        return new MagebloodSliderRange(0f, StretchMax(MaxResistancesMax, value), IsWhole(value));
    }

    /// <summary>Range of one stat field, in display units. Never reaches 0 for stat values.</summary>
    public static MagebloodSliderRange ForStat(
        string stat,
        MagebloodStatField field,
        bool addedAsPercent,
        float displayValue
    )
    {
        MagebloodSliderRange basis = Base(stat, field, addedAsPercent, displayValue);
        return new MagebloodSliderRange(
            StretchMin(basis.Min, displayValue),
            StretchMax(basis.Max, displayValue),
            IsWhole(displayValue)
        );
    }

    private static MagebloodSliderRange Base(
        string stat,
        MagebloodStatField field,
        bool addedAsPercent,
        float value
    )
    {
        if (float.IsFinite(value) && value < 0f)
        {
            return Whole(-50f, -1f);
        }

        return field switch
        {
            MagebloodStatField.Increased => IncreasedBase(stat),
            MagebloodStatField.Added => AddedBase(stat, addedAsPercent),
            _ => Whole(1f, 200f),
        };
    }

    private static MagebloodSliderRange IncreasedBase(string stat)
    {
        return stat switch
        {
            "Movespeed" or "AttackSpeed" or "CastSpeed" => Whole(1f, 100f),
            "CriticalChance" => Whole(1f, 300f),
            _ => Whole(1f, 200f),
        };
    }

    private static MagebloodSliderRange AddedBase(string stat, bool addedAsPercent)
    {
        if (stat is "Armour" or "DodgeRating")
        {
            return Whole(10f, 2000f);
        }
        if (IsResistance(stat))
        {
            return Whole(1f, 75f);
        }
        return addedAsPercent ? Whole(1f, 100f) : Whole(1f, 1000f);
    }

    private static bool IsResistance(string stat)
    {
        return stat != null
            && (
                stat.EndsWith("Resistance", StringComparison.Ordinal)
                || stat.EndsWith("Resistances", StringComparison.Ordinal)
            );
    }

    private static MagebloodSliderRange Whole(float min, float max)
    {
        return new MagebloodSliderRange(min, max, true);
    }

    private static float StretchMax(float max, float value)
    {
        if (!float.IsFinite(value) || value == 0f || value <= max)
        {
            return max;
        }

        return max > 0f ? MathF.Ceiling(value) : value;
    }

    private static float StretchMin(float min, float value)
    {
        if (!float.IsFinite(value) || value == 0f || value >= min)
        {
            return min;
        }

        return min < 0f ? MathF.Floor(value) : value;
    }

    private static bool IsWhole(float value)
    {
        return !float.IsFinite(value) || MathF.Abs(value - MathF.Round(value)) < 0.001f;
    }
}
