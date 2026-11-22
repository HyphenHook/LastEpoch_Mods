using System;

namespace LastEpoch_Hud.Scripts.Core.ModUI.Sliders;

/// <summary>A min/max setting pair that keeps Min &lt;= Max.</summary>
public readonly record struct MinMaxPair(float Min, float Max)
{
    /// <summary>Sets Min; raises Max to it when Max is lower.</summary>
    public MinMaxPair WithMin(float value) => new(value, Math.Max(Max, value));

    /// <summary>Sets Max; lowers Min to it when Min is higher.</summary>
    public MinMaxPair WithMax(float value) => new(Math.Min(Min, value), value);
}
