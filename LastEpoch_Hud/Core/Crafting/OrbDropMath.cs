namespace LastEpoch_Hud.Scripts.Core.Crafting;

/// <summary>How often an enemy drops an Orb of Encapsulation. Rarer enemies drop it more often; bosses most of all.</summary>
public static class OrbDropMath
{
    public const double Normal = 0.0003;
    public const double Magic = 0.002;
    public const double Rare = 0.015;
    public const double Boss = 0.10;

    /// <summary>Chance, 0 to 1, that one dying enemy drops an orb. Rarity: 0 normal, 1 magic, 2 rare.</summary>
    public static double Chance(int rarity, bool boss, double multiplier = 1.0)
    {
        double chance = boss ? Boss : rarity >= 2 ? Rare : rarity == 1 ? Magic : Normal;
        return System.Math.Clamp(chance * System.Math.Max(0, multiplier), 0, 1);
    }
}
