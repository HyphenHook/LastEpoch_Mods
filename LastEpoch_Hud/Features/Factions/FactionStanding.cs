using System;
using Il2CppLE.Factions;
using LastEpoch_Hud.Scripts.Core.Factions;

namespace LastEpoch_Hud.Scripts.Mods.Factions;

/// <summary>Applies faction edits to the current character's faction.</summary>
internal static class FactionStanding
{
    public static void GainFavor(FactionKind kind, int amount)
    {
        Apply(
            kind,
            "FactionStanding.GainFavor",
            faction =>
            {
                int gain = FactionStandingRules.ClampFavorGain(amount, faction.Favor);
                using (Mods.UI.SessionGainCounters.SuppressManualGrants())
                {
                    faction.GainFavor(gain, false);
                }
            }
        );
    }

    public static void SetRank(FactionKind kind, int rank)
    {
        Apply(
            kind,
            "FactionStanding.SetRank",
            faction => faction.Rank = FactionStandingRules.ClampRank(kind, rank)
        );
    }

    public static void SetReputation(FactionKind kind, int reputation)
    {
        Apply(
            kind,
            "FactionStanding.SetReputation",
            faction =>
                faction.Reputation = FactionStandingRules.ClampReputation(
                    reputation,
                    faction.TotalReputation
                )
        );
    }

    private static void Apply(FactionKind kind, string context, Action<Faction> edit)
    {
        try
        {
            if (!TryFindCurrent(kind, out Faction faction))
            {
                ErrorLog.Report("Faction not found: " + kind, context);
                return;
            }
            EnsureMember(faction);
            edit(faction);
            faction.SaveAndSync(false);
        }
        catch (Exception ex)
        {
            ErrorLog.Report(ex, context);
        }
    }

    private static bool TryFindCurrent(FactionKind kind, out Faction faction)
    {
        faction = null;
        if (Refs_Manager.faction_tracker.IsNullOrDestroyed())
        {
            return false;
        }
        if (Refs_Manager.faction_tracker.factions == null)
        {
            return false;
        }
        return Refs_Manager.faction_tracker.factions.TryGetValue(IdOf(kind), out faction)
            && !faction.IsNullOrDestroyed();
    }

    private static FactionID IdOf(FactionKind kind)
    {
        switch (kind)
        {
            case FactionKind.CircleOfFortune:
                return FactionID.CircleOfFortune;
            case FactionKind.MerchantsGuild:
                return FactionID.MerchantsGuild;
            case FactionKind.ForgottenKnights:
                return FactionID.ForgottenKnights;
            case FactionKind.TheWeaver:
                return FactionID.TheWeaver;
            default:
                throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
        }
    }

    private static void EnsureMember(Faction faction)
    {
        if (!faction.hasEverJoined)
        {
            faction.Join(true);
        }
        if (!faction.IsMember)
        {
            faction.IsMember = true;
        }
    }
}
