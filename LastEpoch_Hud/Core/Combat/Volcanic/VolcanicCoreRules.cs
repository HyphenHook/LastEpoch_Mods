using System;
using LastEpoch_Hud.Scripts.Core.Heorot;
using LastEpoch_Hud.Scripts.Core.Tuning;

namespace LastEpoch_Hud.Scripts.Core.Volcanic;

/// <summary>
/// Volcanic Shaman’s Core: one Jewel unique that grants Bhuldar’s Wrath, Logi’s Hunger and Herkir’s Vessel in any combination.
/// It works like Heorot’s Core, so the combination is stored as a protected roll and read with the same bands.
/// </summary>
public static class VolcanicCoreRules
{
    public const string Name = "Volcanic Shaman’s Core";
    public const ushort UniqueId = 520;
    /// <summary>The Immortal Shamans: Bhuldar, Herkir and Logi. The Core drops when all three are down.</summary>
    public const int BossCount = 3;
    public const int TimelineLevel = 100;

    public static double DropChance => Math.Clamp(Tunables.Get("VolcanicCore.DropChance", .05), 0, 1);
    public static double[] VariantWeights => Tunables.Series("VolcanicCore.VariantWeight", new[] { 75d, 20d, 5d });

    public static bool IsCoreUnique(int uniqueId) => uniqueId == UniqueId;

    /// <summary>The buffs (1 Wrath, 2 Hunger, 4 Vessel) a stored roll stands for. The roll layout is the same as Heorot’s Core.</summary>
    public static int MaskForRoll(int roll) => HeorotCoreRules.MaskForRoll(roll);

    public static byte RollForMask(int mask, int jitter) => HeorotCoreRules.RollForMask(mask, jitter);

    /// <summary>The Core drops once, when the last of the three Shamans falls, at the end of an Empowered level-100 Spirits of Fire fight (the caller checks Empowered).</summary>
    public static bool CanDrop(bool offline, bool boss, bool minion, bool timelineBossEncounter,
        bool volcanoTimeline, int timelineLevel, int shamansDown) =>
        offline && boss && !minion && timelineBossEncounter && volcanoTimeline
        && timelineLevel == TimelineLevel && shamansDown >= BossCount;
}
