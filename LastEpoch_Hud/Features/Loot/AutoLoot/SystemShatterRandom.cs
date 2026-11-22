using System;
using LastEpoch_Hud.Scripts.Core.Items.AutoLoot;

namespace LastEpoch_Hud.Scripts.Mods.Items.AutoLoot;

/// <summary>Shatter dice on System.Random: no interop call and the game's random state stays untouched.</summary>
public sealed class SystemShatterRandom : IShatterRandom
{
    private readonly Random _random = new();

    public int RollPercent() => _random.Next(100);
}
