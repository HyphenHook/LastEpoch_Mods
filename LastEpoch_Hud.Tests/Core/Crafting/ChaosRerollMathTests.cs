using LastEpoch_Hud.Scripts.Core.Crafting;

namespace LastEpoch_Hud.Tests.Core.Crafting;

public class ChaosRerollMathTests
{
    [Fact]
    public void FourGlyphsAreNeeded()
    {
        Assert.False(ChaosRerollMath.CanAfford(3));
        Assert.True(ChaosRerollMath.CanAfford(4));
        Assert.True(ChaosRerollMath.CanAfford(40));
    }

    [Fact]
    public void SealedAndFoundOnlyTiersAreKept()
    {
        Assert.True(ChaosRerollMath.IsKept(3, isSealed: true));
        Assert.True(ChaosRerollMath.IsKept(6, isSealed: false));
        Assert.True(ChaosRerollMath.IsKept(7, isSealed: false));
        Assert.False(ChaosRerollMath.IsKept(5, isSealed: false));
        Assert.False(ChaosRerollMath.IsKept(1, isSealed: false));
    }

    [Fact]
    public void OnlyOrdinaryCraftableAffixesAreRerollable()
    {
        var affixes = new[] { (4, false), (5, false), (7, false), (2, true) };
        Assert.Equal(2, ChaosRerollMath.CountRerollable(affixes));
        Assert.Equal(0, ChaosRerollMath.CountRerollable(null));
    }

    [Fact]
    public void RerolledAffixesNeverReachFoundOnlyTiers()
    {
        Assert.Equal(7, ChaosRerollMath.TierWeights.Length);
        Assert.Equal(0, ChaosRerollMath.TierWeights[5]);
        Assert.Equal(0, ChaosRerollMath.TierWeights[6]);
        Assert.True(ChaosRerollMath.TierWeights[0] > 0);
    }
}
