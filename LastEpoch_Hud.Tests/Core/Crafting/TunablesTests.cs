using LastEpoch_Hud.Scripts.Core.Crafting;
using LastEpoch_Hud.Scripts.Core.Tuning;
using LastEpoch_Hud.Tests.Support;

namespace LastEpoch_Hud.Tests.Core.Crafting;

public sealed class TunablesTests
{
    private static string CsvPath => Path.Combine(GameEnvironment.ModProjectDir, "Data", "CraftingNumbers.csv");

    private static Dictionary<string, double> Shipped()
    {
        var errors = new List<string>();
        var table = TunablesTable.Parse(File.ReadAllText(CsvPath), errors);
        Assert.Empty(errors);
        return table;
    }

    private static List<RecombAffix> Picks(params int[] tiers) =>
        tiers.Select((tier, i) => new RecombAffix { AffixId = i + 1, DisplayedTier = tier, Weight = 1, Placement = 1 + i % 2 }).ToList();

    // Every number the crafting maths can give, so two runs can be compared.
    private static object[] Snapshot() => new object[]
    {
        RecombinatorMath.SurvivalChance(Picks(1, 3, 5), false), RecombinatorMath.SurvivalChance(Picks(2, 6), true),
        RecombinatorMath.SuccessChance(Picks(1, 2, 3, 4), 6, 6, false, 3, 4), RecombinatorMath.SuccessChance(Picks(2, 2, 2, 2, 2), 6, 6, true, 3, 3),
        RecombinatorMath.Gold(Picks(1, 5, 7), true), RecombinatorMath.Bones(Picks(2, 6), false), RecombinatorMath.Embers(Picks(3, 4, 7), true),
        RecombinatorMath.FlatGold, RecombinatorMath.FlatEmbers, RecombinatorMath.PrimordialBoost, RecombinatorMath.PrimordialBaseBones,
        RecombinatorMath.ResultCount(3, 4, 10, () => 0.05, 6, true), RecombinatorMath.ResultCount(3, 4, 10, () => 0.95, 6, true),
        ReflectorMath.Chance(4, 3), ReflectorMath.Chance(5, 7), ReflectorMath.LegendaryChance(3, 4),
        ReflectorMath.MemoryAmber(5, 6), ReflectorMath.Gold(4, 2), ReflectorMath.Bones(5, 7),
        ReflectorMath.LegendaryMemoryAmber(4, 3), ReflectorMath.LegendaryGold(2, 5), ReflectorMath.LegendaryBones(5, 2),
        OrbDropMath.Chance(0, false), OrbDropMath.Chance(1, false), OrbDropMath.Chance(2, false), OrbDropMath.Chance(0, true),
        ChaosRerollMath.MaxRerolledTier, string.Join("/", ChaosRerollMath.TierWeights),
        EncapsulationMath.MinAffixes, AttunementState.MinMultiplier, AttunementState.MaxMultiplier,
        AttunementState.RollMultiplier(0), AttunementState.RollMultiplier(600), AttunementState.MaxRoll,
        LastEpoch_Hud.Scripts.Core.Heorot.HeorotCoreRules.DropChance,
        string.Join("/", LastEpoch_Hud.Scripts.Core.Heorot.HeorotCoreRules.VariantWeights),
        LastEpoch_Hud.Scripts.Core.Volcanic.VolcanicCoreRules.DropChance,
        string.Join("/", LastEpoch_Hud.Scripts.Core.Volcanic.VolcanicCoreRules.VariantWeights),
    };

    [Fact]
    public void TheShippedTableMatchesTheBuiltInDefaults()
    {
        var defaults = Snapshot();
        // The Cores are explicitly balanced through CSV overrides.
        var shipped = Shipped().Where(pair => !pair.Key.StartsWith("HeorotCore.") && !pair.Key.StartsWith("VolcanicCore."))
            .ToDictionary(pair => pair.Key, pair => pair.Value);
        using (Tunables.Scope(shipped))
        {
            Assert.Equal(defaults, Snapshot());
        }
    }

    [Fact]
    public void EveryNameInTheShippedTableIsUsedByTheMaths()
    {
        // Changing each number must change something in the snapshot, so a misspelt name in the CSV cannot hide.
        object[] baseline;
        object[] extraBaseline;
        using (Tunables.Scope(Shipped()))
        {
            baseline = Snapshot();
            extraBaseline = Extra();
        }
        foreach (var (name, value) in Shipped())
        {
            if (name.StartsWith("Bazaar.") || name == "OrbDrop.ReminiscenceChance") continue; // used outside the maths
            var changed = new Dictionary<string, double>(Shipped()) { [name] = value + 7 };
            using (Tunables.Scope(changed))
            {
                // The tier ceilings and legendary rows only show in some inputs, so try a few extra ones.
                bool differs = !Snapshot().SequenceEqual(baseline)
                    || !Extra().SequenceEqual(extraBaseline);
                Assert.True(differs, name + " changes nothing, so the maths does not read it.");
            }
        }
    }

    private static object[] Extra() => Single().Concat(Rest()).ToArray();

    // One affix of each tier, so every per-tier number is reached.
    private static object[] Single() => Enumerable.Range(1, 7).Select(t => (object)RecombinatorMath.SurvivalChance(Picks(t), false)).ToArray();

    private static object[] Rest() => new object[]
    {
        RecombinatorMath.SuccessChance(Picks(1, 2, 3, 4, 5), 6, 6, false, 2, 2), RecombinatorMath.SuccessChance(Picks(1, 2, 3, 4), 6, 6, false, 2, 2),
        RecombinatorMath.SurvivalChance(Picks(1, 1, 1, 1, 1, 1), false), RecombinatorMath.SurvivalChance(Picks(1, 1, 1, 1, 1), false),
        RecombinatorMath.SurvivalChance(Picks(1, 1, 1, 1), false), RecombinatorMath.SurvivalChance(Picks(1, 1), false), RecombinatorMath.SurvivalChance(Picks(4), false),
        ReflectorMath.LegendaryChance(1, 1), ReflectorMath.LegendaryChance(2, 1), ReflectorMath.LegendaryChance(4, 1), ReflectorMath.LegendaryChance(5, 1), ReflectorMath.LegendaryChance(3, 6),
        ReflectorMath.Chance(4, 1), ReflectorMath.Chance(4, 2), ReflectorMath.Chance(4, 4), ReflectorMath.Chance(4, 5), ReflectorMath.Chance(4, 6),
        ReflectorMath.MemoryAmber(4, 1), ReflectorMath.MemoryAmber(4, 2), ReflectorMath.MemoryAmber(4, 3), ReflectorMath.MemoryAmber(4, 4), ReflectorMath.MemoryAmber(4, 5), ReflectorMath.MemoryAmber(4, 7),
        RecombinatorMath.Gold(Picks(1), false), RecombinatorMath.Gold(Picks(2), false), RecombinatorMath.Gold(Picks(3), false), RecombinatorMath.Gold(Picks(4), false),
        RecombinatorMath.Gold(Picks(5), false), RecombinatorMath.Gold(Picks(6), false), RecombinatorMath.Gold(Picks(7), false),
        RecombinatorMath.ResultCount(3, 3, 10, () => 0.5, 6, true), RecombinatorMath.ResultCount(3, 3, 10, () => 0.01, 6, true), RecombinatorMath.ResultCount(3, 3, 10, () => 0.99, 6, true),
        ReflectorMath.LegendaryMemoryAmber(4, 3), ReflectorMath.LegendaryMemoryAmber(2, 3), ReflectorMath.LegendaryGold(4, 3), ReflectorMath.LegendaryBones(4, 3),
        string.Join("/", ChaosRerollMath.TierWeights),
    };

    [Fact]
    public void ChangingANumberChangesTheResult()
    {
        using (Tunables.Scope(new Dictionary<string, double> { ["Recombinator.PrimordialBoost"] = 2.0, ["Attunement.MinMultiplier"] = 0.5, ["Attunement.MaxMultiplier"] = 1.5 }))
        {
            Assert.Equal(2.0, RecombinatorMath.PrimordialBoost);
            Assert.Equal(0.5, AttunementState.RollMultiplier(0));
            Assert.Equal(1000, AttunementState.MaxRoll);
            Assert.Equal(1.5, AttunementState.RollMultiplier(1000), 10);
        }
        Assert.Equal(1.5, RecombinatorMath.PrimordialBoost);
        Assert.Equal(600, AttunementState.MaxRoll);
    }

    [Fact]
    public void TierListsTakeTheirNumberedRowsAndKeepDefaultsForTheRest()
    {
        using (Tunables.Scope(new Dictionary<string, double> { ["Reflector.TierFactor.3"] = 2 }))
        {
            Assert.Equal(0.2 * 2, ReflectorMath.Chance(4, 3), 10);
            Assert.Equal(0.2 * 1.25, ReflectorMath.Chance(4, 1), 10);
        }
    }

    [Fact]
    public void NarrowingTheAttunementRangeNeverMakesASavedMultiplierInvalid()
    {
        using (Tunables.Scope(new Dictionary<string, double> { ["Attunement.MinMultiplier"] = 0.9, ["Attunement.MaxMultiplier"] = 1.1 }))
        {
            Assert.True(AttunementState.LowestValidMultiplier <= 0.70);
            Assert.True(AttunementState.HighestValidMultiplier >= 1.30);
        }
    }

    [Fact]
    public void BadRowsAreReportedAndTheRestLoad()
    {
        var errors = new List<string>();
        var table = TunablesTable.Parse("Name,Value,Note\nA,1,ok\nB,abc,bad\nA,2,dup\nC,3\n", errors);
        Assert.Equal(1.0, table["A"]);
        Assert.Single(table);
        Assert.Equal(3, errors.Count);
        Assert.Empty(TunablesTable.Parse("A,1,x\n", new List<string>()));
    }
}
