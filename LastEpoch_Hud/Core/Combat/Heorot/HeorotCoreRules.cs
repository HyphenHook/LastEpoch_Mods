using System;
using System.Collections.Generic;
using LastEpoch_Hud.Scripts.Core.Tuning;

namespace LastEpoch_Hud.Scripts.Core.Heorot;

public static class HeorotCoreRules
{
    public const string Name = "Heorot’s Core";
    /// <summary>The one unique every Heorot's Core is. Which buffs it gives is a roll stored on the item.</summary>
    public const ushort UniqueId = 507;
    public const int CombinationCount = 7;
    /// <summary>How many of the 256 roll values belong to each combination (the last combination also takes the 4 left over).</summary>
    public const int RollsPerCombination = 36;
    public static double[] ValidateVariantChances(double single, double pair, double triple) =>
        new[] { ValidPercent(single), ValidPercent(pair), ValidPercent(triple) };

    private static double ValidPercent(double value) => double.IsFinite(value) ? Math.Clamp(value, 0, 100) : 0;

    public static double CombinedChance(IReadOnlyList<double> percentages)
    {
        double total = 0;
        foreach (double value in percentages) total += ValidPercent(value);
        return Math.Clamp(total / 100, 0, 1);
    }
    public static double DropChance => Math.Clamp(Tunables.Get("HeorotCore.DropChance", .05), 0, 1);
    public static double[] VariantWeights => Tunables.Series("HeorotCore.VariantWeight", new[] { 75d, 20d, 5d });

    public static bool IsCoreUnique(int uniqueId) => uniqueId == UniqueId;

    /// <summary>
    /// The combinations in the order their roll bands run: the three single buffs, then the three pairs, then all three.
    /// A buff mask is 1 Power, 2 Energy, 4 Defence.
    /// </summary>
    private static readonly int[] BandOrder = { 1, 2, 4, 3, 5, 6, 7 };

    /// <summary>The buffs a stored roll stands for; 0 when the item has no roll.</summary>
    public static int MaskForRoll(int roll) =>
        roll < 0 ? 0 : BandOrder[Math.Min(CombinationCount - 1, roll / RollsPerCombination)];

    /// <summary>A roll value that stands for <paramref name="mask" />; <paramref name="jitter" /> picks where inside its band.</summary>
    public static byte RollForMask(int mask, int jitter)
    {
        int band = Array.IndexOf(BandOrder, mask);
        if (band < 0) throw new ArgumentOutOfRangeException(nameof(mask));
        return (byte)(band * RollsPerCombination + Math.Abs(jitter % RollsPerCombination));
    }

    /// <summary>Corruption outcomes that re-roll or randomise an item's rolls. A Core's buffs are a roll, so these can never apply to one.</summary>
    public static bool CorruptionOutcomeRerolls(int outcome) =>
        outcome is 8 /* SetRollValues */ or 14 /* SetRarityAndRandomize */ or 18 /* RandomizeAndSetRolls */ or 23 /* RandomizeItem */;

    public static int MaskForVariant(int buffCount, int choice) => buffCount switch
    {
        1 when choice >= 0 && choice < 3 => 1 << choice,
        2 when choice >= 0 && choice < 3 => 7 ^ (1 << choice),
        3 => 7,
        _ => throw new ArgumentOutOfRangeException(nameof(buffCount)),
    };

    public static int WeightedTier(double roll, IReadOnlyList<double> weights)
    {
        double total = 0;
        foreach (double weight in weights)
        {
            if (!double.IsFinite(weight) || weight < 0) throw new ArgumentOutOfRangeException(nameof(weights));
            total += weight;
        }
        if (!(total > 0) || !double.IsFinite(total)) throw new ArgumentOutOfRangeException(nameof(weights));
        double remaining = Math.Clamp(roll, 0, 1 - double.Epsilon) * total;
        for (int i = 0; i < weights.Count; i++)
        {
            remaining -= weights[i];
            if (remaining < 0) return i + 1;
        }
        for (int i = weights.Count - 1; i >= 0; i--)
            if (weights[i] > 0) return i + 1;
        throw new InvalidOperationException("No weighted outcome.");
    }

    public static bool CanDrop(bool offline, bool boss, bool minion, bool timelineBossEncounter,
        bool heorotTimeline, int timelineLevel, string actorName) =>
        offline && boss && !minion && timelineBossEncounter && heorotTimeline && timelineLevel == 100
        && actorName != null && actorName.IndexOf("Heorot", StringComparison.OrdinalIgnoreCase) >= 0;
}
