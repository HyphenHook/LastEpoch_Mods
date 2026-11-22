using System;

namespace LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter.Config;

/// <summary>Aura glow strength per distinct buff, with a cap; 1 = a rare monster's glow.</summary>
public readonly record struct HeadhunterAuraCurve(bool Enabled, float PerBuff, float Cap)
{
    public const float Off = 0f;

    /// <summary>Glow strength for a distinct buff count.</summary>
    public float Strength(int buffs)
    {
        if (!Enabled || buffs <= 0)
        {
            return Off;
        }

        return Math.Min(buffs * PerBuff, Cap);
    }
}
