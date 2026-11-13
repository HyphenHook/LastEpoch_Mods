using LastEpoch_Hud.Scripts.Core.Economy;

namespace LastEpoch_Hud.Tests.Core.Economy;

public class CraftingMathTests
{
    private static readonly double[] None = System.Array.Empty<double>();

    [Fact]
    public void NoForgingPotentialIsWorthNothing()
    {
        Assert.Equal(0, CraftingMath.Potential(0, 4, 100, None));
        Assert.Equal(0, CraftingMath.Potential(-3, 4, 100, None));
    }

    [Fact]
    public void MorePotentialIsNeverWorthLess()
    {
        double previous = -1;
        for (int fp = 0; fp <= 63; fp += 3)
        {
            double v = CraftingMath.Potential(fp, 4, 100, None);
            Assert.True(v >= previous, "more forging potential must not lower the value");
            previous = v;
        }
    }

    [Fact]
    public void PotentialIsWorthMoreWhenThereIsRoomToUseIt()
    {
        double fourOpen = CraftingMath.Potential(40, 4, 100, None);
        double oneOpen = CraftingMath.Potential(40, 1, 100, None);
        double noneOpen = CraftingMath.Potential(40, 0, 100, None);
        Assert.True(fourOpen > oneOpen);
        Assert.True(oneOpen > noneOpen);
        Assert.Equal(0, noneOpen);
    }

    [Fact]
    public void PotentialBeyondWhatCanBeUsedAddsNothing()
    {
        // One open slot, nothing to upgrade: the first few points are all that can be used.
        double modest = CraftingMath.Potential(18, 1, 100, None);
        double huge = CraftingMath.Potential(63, 1, 100, None);
        Assert.Equal(modest, huge, 6);
    }

    [Fact]
    public void BestCraftsAreSpentFirst()
    {
        // Only enough budget for one craft: it goes to the most valuable action.
        double v = CraftingMath.Potential(9, 1, 50, new[] { 200.0, 10.0 });
        Assert.Equal(200 * CraftingMath.SuccessRate, v, 6);
    }

    [Fact]
    public void UpgradesCountAsWellAsFillingSlots()
    {
        double fillOnly = CraftingMath.Potential(27, 1, 100, None);
        double fillAndUpgrade = CraftingMath.Potential(27, 1, 100, new[] { 80.0, 60.0 });
        Assert.True(fillAndUpgrade > fillOnly);
    }

    [Fact]
    public void IceAndBloodPotentialIsWorthMore()
    {
        double plain = CraftingMath.Potential(30, 3, 100, None, 1.0);
        double ice = CraftingMath.Potential(30, 3, 100, None, 1.35);
        Assert.True(ice > plain);
    }
}

public class SynergyMathTests
{
    private static (string, double)[] Items(params string[] categories) =>
        categories.Select(c => (c, 1.0)).ToArray();

    [Fact]
    public void UnrelatedAffixesHaveNoSynergy()
    {
        Assert.Equal(1.0, SynergyMath.Factor(Items("OFF_SPELL", "DEF_HEALTH", "MOVEMENT", "MANA")), 6);
    }

    [Fact]
    public void ASingleAffixHasNoSynergy()
    {
        Assert.Equal(1.0, SynergyMath.Factor(Items("OFF_SPELL")), 6);
    }

    [Fact]
    public void SharedThemeRaisesTheValueMoreWithEveryMatch()
    {
        double two = SynergyMath.Factor(Items("OFF_SPELL", "OFF_SPELL", "MOVEMENT"));
        double three = SynergyMath.Factor(Items("OFF_SPELL", "OFF_SPELL", "OFF_SPELL", "MOVEMENT"));
        double four = SynergyMath.Factor(Items("OFF_SPELL", "OFF_SPELL", "OFF_SPELL", "OFF_SPELL"));
        Assert.True(two > 1.0);
        Assert.True(three > two);
        Assert.True(four > three);
    }

    [Fact]
    public void ThemeMattersNotItsNameOrOrder()
    {
        double a = SynergyMath.Factor(Items("MINION", "OFF_MELEE", "MINION"));
        double b = SynergyMath.Factor(Items("OFF_MELEE", "MINION", "MINION"));
        Assert.Equal(a, b, 6);
    }

    [Fact]
    public void TopTierAffixesCountForMoreInTheTheme()
    {
        double ordinary = SynergyMath.Factor(new[] { ("OFF_SPELL", 1.0), ("OFF_SPELL", 1.0) });
        double exalted = SynergyMath.Factor(new[] { ("OFF_SPELL", 1.5), ("OFF_SPELL", 1.5) });
        Assert.True(exalted > ordinary);
    }

    [Fact]
    public void SynergyIsCapped()
    {
        var huge = Enumerable.Repeat(("OFF_SPELL", 1.5), 12).ToArray();
        Assert.Equal(SynergyMath.MaxFactor, SynergyMath.Factor(huge), 6);
    }

    [Fact]
    public void MissingCategoriesAreIgnored()
    {
        Assert.Equal(1.0, SynergyMath.Factor(new[] { ("", 1.0), (null, 1.0), ("OFF_SPELL", 1.0) }), 6);
        Assert.Equal(1.0, SynergyMath.Factor(null), 6);
    }
}
