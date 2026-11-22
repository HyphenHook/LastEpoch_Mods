using System;

namespace LastEpoch_Hud.Scripts.Core.Jewels;

/// <summary>Stable save IDs and tier rules. Tier arguments here are display tiers (1..7).</summary>
public static class JewelAffixRules
{
    public const byte BaseType = 44;
    public const int SubtypeCount = 4; // Cobalt, Ruby, Emerald, Prismatic
    public const int ExtremelyRareWeight = 10;
    public const int RareWeight = 100;
    public const int UncommonWeight = 1000;
    // ItemData.loadAffixFromSerialisation decodes ID high bits from a four-bit nibble
    // plus a second byte: save IDs are 12 bits even though ItemAffix.affixId is ushort.
    public const int MaximumPackedAffixId = 4095;
    public const int InheritedIdOffset = 2000;
    public const int ExclusiveIdStart = 4000;
    public const int ManaLeechIdStart = ExclusiveIdStart + 20;
    public const int ExtraDamageIdStart = ExclusiveIdStart + 30;
    public const int TakenAsIdStart = ExclusiveIdStart + 40;

    public static float IdolMultiplier(int tier) => tier switch
    {
        1 => 2.5f, 2 => 3.125f, 3 => 3.75f, 4 => 4.375f,
        5 => 5f, 6 => 6.5f, 7 => 8f,
        _ => throw new ArgumentOutOfRangeException(nameof(tier)),
    };

    public static int InheritedId(int sourceId)
    {
        // Keep the mapping stable across registration order and game reloads.
        if (sourceId < 0 || sourceId >= ExclusiveIdStart - InheritedIdOffset)
            throw new ArgumentOutOfRangeException(nameof(sourceId), "Native idol ID exceeds the reserved jewel range.");
        return InheritedIdOffset + sourceId;
    }

    public static bool IsCustomEffect(int id) => IsManaLeech(id) || IsExtraDamage(id);
    public static bool IsManaLeech(int id) => id >= ManaLeechIdStart && id < ManaLeechIdStart + 4;
    public static bool IsExtraDamage(int id) => id >= ExtraDamageIdStart && id < ExtraDamageIdStart + 7;
    public static bool IsTakenAs(int id) => id >= TakenAsIdStart && id < TakenAsIdStart + 7;
    public static int MinimumTier(int id) => IsExtraDamage(id) || IsTakenAs(id) ? 6 : 1;
    public static bool IsTierAllowed(int id, int tier) => tier >= MinimumTier(id) && tier <= 7;
    public static float Roll(float minimum, float maximum, byte roll) => minimum + (maximum - minimum) * (roll / 255f);

    /// <summary>Leech instances recover evenly over three seconds; rate changes shorten that duration.</summary>
    public static float LeechDuration(float increasedLeechRate) => 3f / Math.Max(0.01f, 1f + increasedLeechRate);

    public static (float Min, float Max) ExclusiveRange(int tier, float low, float high,
        float t6min, float t6max, float t7min, float t7max)
    {
        if (tier < 1 || tier > 7) throw new ArgumentOutOfRangeException(nameof(tier));
        if (tier == 6) return (t6min, t6max);
        if (tier == 7) return (t7min, t7max);
        float value = low + (high - low) * (tier - 1) / 4f;
        return (value, value);
    }

    /// <summary>All extra damage types use the original total, never the previously added extras.</summary>
    public static void ApplyExtraDamage(float[] damage, float[] fractions)
    {
        if (damage == null || damage.Length < 7 || fractions == null || fractions.Length < 7)
            throw new ArgumentException("Seven native damage types are required.");
        float original = 0;
        for (int i = 0; i < 7; i++) original += Math.Max(0, damage[i]);
        for (int i = 0; i < 7; i++) damage[i] += original * fractions[i];
    }

    /// <summary>Recover a portion of an instance while preserving its total under rate changes.</summary>
    public static float RecoverLeech(ref float remaining, ref float baseSeconds, float deltaTime, float increasedRate)
    {
        if (remaining <= 0 || baseSeconds <= 0 || deltaTime <= 0) return 0;
        float elapsed = deltaTime * (3f / LeechDuration(increasedRate));
        float recovered = remaining * Math.Min(1f, elapsed / baseSeconds);
        remaining -= recovered;
        baseSeconds = Math.Max(0, baseSeconds - elapsed);
        return recovered;
    }
}
