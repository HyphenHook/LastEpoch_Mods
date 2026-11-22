namespace LastEpoch_Hud.Scripts.Core.Factions;

/// <summary>Limits for favor, rank and reputation edits.</summary>
public static class FactionStandingRules
{
    public const int MaxFavor = 999999;

    public static int MaxRank(FactionKind kind)
    {
        switch (kind)
        {
            case FactionKind.CircleOfFortune:
            case FactionKind.MerchantsGuild:
                return 12;
            default:
                return 10;
        }
    }

    public static int ClampRank(FactionKind kind, int requested)
    {
        if (requested < 0)
        {
            return 0;
        }
        int max = MaxRank(kind);
        return requested > max ? max : requested;
    }

    public static int ClampFavorGain(int requested, int currentFavor)
    {
        if (requested <= 0)
        {
            return 0;
        }
        int current = currentFavor < 0 ? 0 : currentFavor;
        if (current >= MaxFavor)
        {
            return 0;
        }
        int room = MaxFavor - current;
        return requested > room ? room : requested;
    }

    public static int ClampReputation(int requested, int totalReputation)
    {
        if (requested < 0 || totalReputation < 0)
        {
            return 0;
        }
        return requested > totalReputation ? totalReputation : requested;
    }
}
