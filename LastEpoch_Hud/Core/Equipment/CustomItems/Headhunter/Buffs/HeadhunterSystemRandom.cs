using System;

namespace LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter.Buffs;

/// <summary>Random source backed by one System.Random.</summary>
public sealed class HeadhunterSystemRandom : IHeadhunterRandom
{
    private readonly Random _random = new();

    public int Next(int maxExclusive)
    {
        return _random.Next(maxExclusive);
    }
}
