using System.Collections.Generic;
using System.Linq;

namespace LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter.Config.Defaults;

/// <summary>The approved default affix map: monster mod key to the stat rows it gives.</summary>
public static class HeadhunterAffixDefaults
{
    public const int Since = 6;
    public const int RowsSince = 8;
    public const int AreaRowsSince = 12;

    public static readonly IReadOnlyList<HeadhunterVersionedAffix> VersionedAffixes =
        new List<HeadhunterVersionedAffix>
        {
            Row(1132170697, "Patient", "Damage"),
            Row(616463851, "Spiteful", "Damage", "HealthLeech"),
            Row(1687301026, "Rampaging", "Damage", "IncreasedLeechRate"),
            Row(1580632894, "of Blades", "Damage"),
            Row(776551363, "Vengeful", "AttackSpeed", "CastSpeed"),
            Row(-1425836300, "of the Lynx", "AttackSpeed", "CastSpeed"),
            Row(-1041637678, "of Rage", "AttackSpeed", "CastSpeed"),
            Row(-637814972, "of Rampancy", "Movespeed", "AttackSpeed"),
            Row(1557071591, "of Haste", "Movespeed"),
            Row(-727154184, "Healing", "HealthRegen", "ManaRegen"),
            Row(1337344846, "of the Lizard", "HealthRegen", "ManaRegen"),
            Row(-2045434097, "of the Ox", "Health", "Mana"),
            Row(-1272420556, "of Focus", "Health", "Mana"),
            Row(1986757351, "Familiar", "Health", "HealthLeech"),
            Row(1097354172, "Twinned", "Health"),
            Row(-1642806494, "Fracturing", "Health"),
            Row(324297367, "Shrouded", "DodgeRating", "IncreasedAreaForAreaSkills"),
            Row(880775705, "of Shadows", "DodgeRating"),
            Row(1150886571, "Protective", "Armour", "IncreasedAreaForAreaSkills"),
            Row(-1226075678, "Unrelenting", "IncreasedCooldownRecoverySpeed", "ManaEfficiency"),
            Row(1519424920, "Summoning", "Damage_Minion"),
            Row(-1782923815, "of Loathing", "CriticalChance"),
            Row(211926464, "of the Hawk", "CriticalChance"),
            Row(-475489041, "of Tenacity", "StunAvoidance"),
            Boost(-128668492, "of Cinders", "Fire"),
            Boost(-699621054, "of Gales", "Cold"),
            Boost(-941987364, "of Sparks", "Lightning"),
            Boost(253201527, "of Souls", "Necrotic"),
            Boost(1846890812, "of Rot", "Poison"),
            Boost(-1672367438, "of Dread", "Void"),
            Shred(1509816611, "of Wildfire", "Fire"),
            Shred(-269090453, "of Winter", "Cold"),
            Shred(490897223, "of Lodestone", "Lightning"),
            Shred(691915270, "of Damnation", "Necrotic"),
            Shred(-955180412, "of Rending", "Physical"),
            Shred(443654775, "of Affliction", "Poison"),
            Shred(580353563, "of Apathy", "Void"),
            Shred(-1284382844, "of Thunder", "Lightning"),
        };

    public static readonly IReadOnlyList<HeadhunterAffixEntry> AffixMap = VersionedAffixes
        .Select(row => row.Entry)
        .ToList();

    public static readonly IReadOnlyList<HeadhunterVersionedAffixRow> VersionedRows =
        new List<HeadhunterVersionedAffixRow>
        {
            Extra(-727154184, "ManaRegen", RowsSince),
            Extra(1337344846, "ManaRegen", RowsSince),
            Extra(-2045434097, "Mana", RowsSince),
            Extra(-1272420556, "Mana", RowsSince),
            Extra(-1226075678, "ManaEfficiency", RowsSince),
            Extra(616463851, "HealthLeech", RowsSince),
            Extra(1986757351, "HealthLeech", RowsSince),
            Extra(1687301026, "IncreasedLeechRate", RowsSince),
            Extra(324297367, "IncreasedAreaForAreaSkills", AreaRowsSince),
            Extra(1150886571, "IncreasedAreaForAreaSkills", AreaRowsSince),
        };

    private static HeadhunterVersionedAffixRow Extra(int modKey, string row, int since)
    {
        return new HeadhunterVersionedAffixRow(modKey, row, since);
    }

    private static HeadhunterVersionedAffix Row(int modKey, string note, params string[] rows)
    {
        return new HeadhunterVersionedAffix(new HeadhunterAffixEntry(modKey, note, rows), Since);
    }

    private static HeadhunterVersionedAffix Boost(int modKey, string note, string element)
    {
        return Row(modKey, note, "Damage_" + element, element + "Resistance");
    }

    private static HeadhunterVersionedAffix Shred(int modKey, string note, string element)
    {
        return Row(modKey, note, "Damage_" + element);
    }
}
