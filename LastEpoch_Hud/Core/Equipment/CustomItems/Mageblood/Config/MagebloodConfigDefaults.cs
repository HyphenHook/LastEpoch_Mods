using System.Collections.Generic;

namespace LastEpoch_Hud.Scripts.Core.CustomItems.Mageblood.Config;

/// <summary>The built-in flask list (PoE utility flasks mapped to Last Epoch stats). Order is slot order.</summary>
public static class MagebloodConfigDefaults
{
    public const int CurrentVersion = 1;
    public const int DefaultsVersion = 2;
    public const int UnstampedDefaultsVersion = 1;

    public static readonly MagebloodVersionedValue MaxResistances = new(5f, 2);

    public static readonly IReadOnlyList<MagebloodVersionedFlask> VersionedFlasks = BuildTable();

    public static readonly IReadOnlyList<MagebloodFlaskEntry> Flasks = BuildEntries();

    public static readonly MagebloodConfig Config = new()
    {
        Version = CurrentVersion,
        Flasks = Flasks,
        MaxResistances = MaxResistances.Value,
    };

    private static MagebloodVersionedFlask[] BuildTable()
    {
        return new[]
        {
            Since1("Quicksilver", new MagebloodStatEntry("Movespeed", Increased: 30f)),
            Since1(
                "Silver",
                new MagebloodStatEntry("AttackSpeed", Increased: 20f),
                new MagebloodStatEntry("CastSpeed", Increased: 20f),
                new MagebloodStatEntry("Movespeed", Increased: 20f)
            ),
            Since1("Granite", new MagebloodStatEntry("Armour", Added: 500f)),
            Since1("Diamond", new MagebloodStatEntry("CriticalChance", Increased: 100f)),
            Since1("Jade", new MagebloodStatEntry("DodgeRating", Added: 500f)),
            Since1("Bismuth", new MagebloodStatEntry("AllResistances", Added: 0.35f)),
            Since1("Basalt", new MagebloodStatEntry("DamageTaken", More: -15f, Tag: "Physical")),
            Since1(
                "Amethyst",
                new MagebloodStatEntry("VoidResistance", Added: 0.7f),
                new MagebloodStatEntry("NecroticResistance", Added: 0.7f),
                new MagebloodStatEntry("PoisonResistance", Added: 0.7f)
            ),
            Since1(
                "Quartz",
                new MagebloodStatEntry("DodgeRating", Added: 250f),
                new MagebloodStatEntry("Movespeed", Increased: 10f)
            ),
            Since1("Sulphur", new MagebloodStatEntry("Damage", Increased: 40f)),
            Since1("Gold", new MagebloodStatEntry("IncreasedDropRate", Added: 0.2f)),
        };
    }

    private static MagebloodFlaskEntry[] BuildEntries()
    {
        var entries = new MagebloodFlaskEntry[VersionedFlasks.Count];
        for (int i = 0; i < entries.Length; i++)
        {
            entries[i] = VersionedFlasks[i].Entry;
        }
        return entries;
    }

    private static MagebloodVersionedFlask Since1(string name, params MagebloodStatEntry[] rows)
    {
        var entry = new MagebloodFlaskEntry
        {
            Name = name,
            Icon = null,
            Stats = rows,
        };
        return new MagebloodVersionedFlask(entry, 1);
    }
}
