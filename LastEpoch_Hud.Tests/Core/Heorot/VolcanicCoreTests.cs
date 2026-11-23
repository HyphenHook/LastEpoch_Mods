using LastEpoch_Hud.Scripts.Core.Heorot;
using LastEpoch_Hud.Scripts.Core.Volcanic;

namespace LastEpoch_Hud.Tests.Core.Heorot;

[Collection("HeorotValuesState")]
public sealed class VolcanicCoreTests
{
    private static string ShippedFile(string name)
    {
        string folder = AppContext.BaseDirectory;
        while (folder != null && !File.Exists(Path.Combine(folder, "LastEpoch_Hud", "Data", name)))
            folder = Path.GetDirectoryName(folder);
        Assert.NotNull(folder);
        return File.ReadAllText(Path.Combine(folder, "LastEpoch_Hud", "Data", name));
    }

    [Fact]
    public void TheShippedStatsFileHasTheAgreedBuffs()
    {
        var errors = new List<string>();
        var rows = HeorotTable.ParseStats(ShippedFile("VolcanicStats.csv"), errors);
        Assert.Empty(errors);
        float Value(string buff, string property) => rows.Single(r => r.Buff == buff && r.Property == property).Value;
        Assert.Equal(30f, Value("Wrath", "Damage"));
        Assert.Equal(.85f, Value("Wrath", "Ignite"));
        Assert.Equal(.35f, Value("Hunger", "FireResShred"));
        Assert.Equal(.6f, rows.Single(r => r.Buff == "Hunger" && r.Kind == "Increased").Value);
        Assert.Equal(.3f, Value("Vessel", "FireResistance"));
        Assert.Equal(.3f, Value("Vessel", "NecroticResistance"));
        Assert.Equal(6, rows.Count);
    }

    [Fact]
    public void TheShippedValuesFileMatchesTheBuiltInDefaults()
    {
        var errors = new List<string>();
        var values = HeorotTable.ParseValues(ShippedFile("VolcanicValues.csv"), errors);
        Assert.Empty(errors);
        VolcanicValues.Set(values);
        try
        {
            Assert.Equal(.25f, VolcanicValues.IncreasedFirePerIgnite);
            Assert.Equal(.02f, VolcanicValues.FireLeech);
            Assert.Equal(.02f, VolcanicValues.NecroticLeech);
            Assert.Equal(1f, VolcanicValues.IgniteAttackersChance);
            Assert.Equal(10f, VolcanicValues.IgniteDamagePerResistance);
        }
        finally { VolcanicValues.Set(new Dictionary<string, float>()); }
    }

    [Fact]
    public void WrathScalesWithIgniteStacksAndTheWearersEffect()
    {
        VolcanicValues.Set(new Dictionary<string, float>());
        Assert.Equal(0f, VolcanicValues.WrathIncreasedFire(0));
        Assert.Equal(.75f, VolcanicValues.WrathIncreasedFire(3), 4);
        Assert.Equal(.9f, VolcanicValues.WrathIncreasedFire(3, .2f), 4);
        Assert.Equal(0f, VolcanicValues.WrathIncreasedFire(float.NaN));
    }

    [Fact]
    public void HungerLeechesTwoPercentOfEachDamageType()
    {
        VolcanicValues.Set(new Dictionary<string, float>());
        Assert.Equal(7f, VolcanicValues.LeechedHealth(200, 150), 4);
        Assert.Equal(4f, VolcanicValues.LeechedHealth(200, 0), 4);
        Assert.Equal(0f, VolcanicValues.LeechedHealth(-5, float.NaN));
    }

    [Fact]
    public void IgniteDamageScalesWithFireResistance()
    {
        VolcanicValues.Set(new Dictionary<string, float>());
        Assert.Equal(750f, VolcanicValues.IgniteDamage(.75f), 3);
        Assert.Equal(300f, VolcanicValues.IgniteDamage(.3f), 3);
        Assert.Equal(1200f, VolcanicValues.IgniteDamage(1.2f), 3); // uncapped: above the game's 75% cap still counts
        Assert.Equal(0f, VolcanicValues.IgniteDamage(-.2f));
        Assert.Equal(0f, VolcanicValues.IgniteDamage(float.NaN));
        VolcanicValues.Set(new Dictionary<string, float> { ["IgniteDamagePerResistance"] = 2.5f });
        try { Assert.Equal(187.5f, VolcanicValues.IgniteDamage(.75f), 3); }
        finally { VolcanicValues.Set(new Dictionary<string, float>()); }
    }

    [Theory]
    [InlineData(1.2f, .1, 2)]
    [InlineData(1.2f, .5, 1)]
    [InlineData(1f, .99, 1)]
    [InlineData(.4f, .3, 1)]
    [InlineData(.4f, .6, 0)]
    [InlineData(0f, 0, 0)]
    public void VesselIgnitesAttackersWithTheWholePartAndARemainderChance(float chance, double roll, int stacks) =>
        Assert.Equal(stacks, VolcanicValues.IgniteStacks(chance, roll));

    [Fact]
    public void TheCoreUsesItsOwnUniqueAndTheSameBuffBandsAsHeorotsCore()
    {
        Assert.NotEqual(HeorotCoreRules.UniqueId, VolcanicCoreRules.UniqueId);
        Assert.True(VolcanicCoreRules.IsCoreUnique(VolcanicCoreRules.UniqueId));
        Assert.False(VolcanicCoreRules.IsCoreUnique(HeorotCoreRules.UniqueId));
        foreach (int mask in Enumerable.Range(1, 7))
            Assert.Equal(mask, VolcanicCoreRules.MaskForRoll(VolcanicCoreRules.RollForMask(mask, 17)));
    }

    [Theory]
    [InlineData(true, true, false, true, true, 100, 3, true)]
    [InlineData(true, true, false, true, true, 100, 2, false)]
    [InlineData(true, true, false, true, true, 90, 3, false)]
    [InlineData(true, true, false, false, true, 100, 3, false)]
    [InlineData(true, true, false, true, false, 100, 3, false)]
    [InlineData(true, true, true, true, true, 100, 3, false)]
    [InlineData(false, true, false, true, true, 100, 3, false)]
    public void TheCoreDropsOnlyWhenTheThirdShamanFallsAtLevel100(bool offline, bool boss, bool minion, bool timelineBoss,
        bool volcano, int level, int down, bool expected) =>
        Assert.Equal(expected, VolcanicCoreRules.CanDrop(offline, boss, minion, timelineBoss, volcano, level, down));
}
