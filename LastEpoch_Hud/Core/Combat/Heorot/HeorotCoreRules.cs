using System;
using System.Collections.Generic;
using LastEpoch_Hud.Scripts.Core.Tuning;

namespace LastEpoch_Hud.Scripts.Core.Heorot;

public static class HeorotCoreRules
{
    public const string Name = "Heorot’s Core";
    public const ushort FirstUniqueId = 507;
    public const int CombinationCount = 7;
    public static double[] ValidateVariantChances(double single, double pair, double triple) =>
        new[] { ValidPercent(single), ValidPercent(pair), ValidPercent(triple) };

    private static double ValidPercent(double value) => double.IsFinite(value) ? Math.Clamp(value, 0, 100) : 0;

    public static double CombinedChance(IReadOnlyList<double> percentages)
    {
        double total = 0;
        foreach (double value in percentages) total += ValidPercent(value);
        return Math.Clamp(total / 100, 0, 1);
    }
    public static double DropChance => Math.Clamp(Tunables.Get("HeorotCore.DropChance", .10), 0, 1);
    public static double[] VariantWeights => Tunables.Series("HeorotCore.VariantWeight", new[] { 80d, 18d, 2d });

    public static int MaskFor(int uniqueId) =>
        uniqueId >= FirstUniqueId && uniqueId < FirstUniqueId + CombinationCount ? uniqueId - FirstUniqueId + 1 : 0;

    public static ushort UniqueIdFor(int mask)
    {
        if (mask < 1 || mask > CombinationCount) throw new ArgumentOutOfRangeException(nameof(mask));
        return (ushort)(FirstUniqueId + mask - 1);
    }

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
