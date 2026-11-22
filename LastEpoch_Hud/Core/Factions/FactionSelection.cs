using System.Collections.Generic;
using System.Linq;

namespace LastEpoch_Hud.Scripts.Core.Factions;

/// <summary>Maps the Factions dropdown option to a faction.</summary>
public static class FactionSelection
{
    /// <summary>Faction order of options 1..N (single source).</summary>
    private static readonly FactionKind[] _order =
    {
        FactionKind.CircleOfFortune,
        FactionKind.MerchantsGuild,
        FactionKind.ForgottenKnights,
        FactionKind.TheWeaver,
    };

    /// <summary>Dropdown option texts (English locale keys), built once.</summary>
    public static IReadOnlyList<string> OptionLabels { get; } =
        new[] { "Select" }.Concat(_order.Select(LabelOf)).ToArray();

    /// <summary>English locale key of a faction name.</summary>
    public static string LabelOf(FactionKind kind)
    {
        switch (kind)
        {
            case FactionKind.CircleOfFortune:
                return "Circle of Fortune";
            case FactionKind.MerchantsGuild:
                return "Merchant's Guild";
            case FactionKind.ForgottenKnights:
                return "Forgotten Knights";
            case FactionKind.TheWeaver:
                return "The Woven";
            default:
                return string.Empty;
        }
    }

    /// <summary>Option 0 is the "Select" placeholder; options 1..N are the factions.</summary>
    public static bool TryKindAt(int optionIndex, out FactionKind kind)
    {
        kind = FactionKind.CircleOfFortune;
        if (optionIndex < 1 || optionIndex > _order.Length)
        {
            return false;
        }

        kind = _order[optionIndex - 1];
        return true;
    }
}
