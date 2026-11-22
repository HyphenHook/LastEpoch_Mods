using System.Collections.Generic;

namespace LastEpoch_Hud.Scripts.Core.Factions;

/// <summary>English locale keys of the Factions card rows.</summary>
public static class FactionCardLabels
{
    public const string Faction = "Faction";
    public const string Favor = "Favor";
    public const string Rank = "Rank";
    public const string Reputation = "Reputation";
    public const string GainFavor = "Gain Favor";
    public const string SetRank = "Set Rank";
    public const string SetReputation = "Set Reputation";

    /// <summary>Every card row label.</summary>
    public static IReadOnlyList<string> All { get; } =
        new[] { Faction, Favor, Rank, Reputation, GainFavor, SetRank, SetReputation };
}
