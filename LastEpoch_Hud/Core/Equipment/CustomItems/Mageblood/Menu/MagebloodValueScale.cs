using System;

namespace LastEpoch_Hud.Scripts.Core.CustomItems.Mageblood.Menu;

/// <summary>Converts a stat value between the file and the menu. Added percents are fractions in the file.</summary>
public static class MagebloodValueScale
{
    private const float PercentPerFraction = 100f;
    private const int DisplayDecimals = 4;
    private const float RelativeTolerance = 1e-6f;
    private const float AbsoluteTolerance = 1e-8f;

    public static MagebloodValueUnit Unit(MagebloodStatField field, bool addedAsPercent)
    {
        bool raw = field == MagebloodStatField.Added && !addedAsPercent;
        return raw ? MagebloodValueUnit.Raw : MagebloodValueUnit.Percent;
    }

    public static float ToDisplay(float fileValue, MagebloodStatField field, bool addedAsPercent)
    {
        if (!IsFraction(field, addedAsPercent))
        {
            return fileValue;
        }

        return MathF.Round(fileValue * PercentPerFraction, DisplayDecimals);
    }

    public static float ToFile(float displayValue, MagebloodStatField field, bool addedAsPercent)
    {
        return IsFraction(field, addedAsPercent) ? displayValue / PercentPerFraction : displayValue;
    }

    /// <summary>False when an Added value has no stat info, so its unit would be a guess.</summary>
    public static bool IsUnitKnown(MagebloodStatField field, bool statInfoRead)
    {
        return field != MagebloodStatField.Added || statInfoRead;
    }

    /// <summary>True with the file value when the shown value differs from the current one.</summary>
    public static bool TryEdit(
        float display,
        float current,
        MagebloodStatField field,
        bool addedAsPercent,
        out float file
    )
    {
        file = 0f;
        if (IsSame(display, current))
        {
            return false;
        }

        file = ToFile(display, field, addedAsPercent);
        return true;
    }

    private static bool IsSame(float a, float b)
    {
        float tolerance = MathF.Max(
            RelativeTolerance * MathF.Max(MathF.Abs(a), MathF.Abs(b)),
            AbsoluteTolerance
        );
        return MathF.Abs(a - b) < tolerance;
    }

    private static bool IsFraction(MagebloodStatField field, bool addedAsPercent)
    {
        return field == MagebloodStatField.Added && addedAsPercent;
    }
}
