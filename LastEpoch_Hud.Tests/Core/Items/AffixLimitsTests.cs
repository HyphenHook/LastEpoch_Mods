using LastEpoch_Hud.Scripts.Core.Items;

namespace LastEpoch_Hud.Tests.Core.Items;

public class AffixLimitsTests
{
    [Fact]
    public void TwelveAffixesInTotalIncludingSealsAndCorruption()
    {
        Assert.Equal(12, AffixLimits.MaxTotal);
        Assert.Equal(
            AffixLimits.MaxTotal,
            AffixLimits.OrdinarySlots + 1 + 1 + 1 // regular seal, Primordial seal, corruption
        );
        Assert.Equal(AffixLimits.OrdinarySlots, AffixLimits.SealedSlot);
        Assert.Equal(AffixLimits.OrdinarySlots + 1, AffixLimits.PrimordialSlot);
        Assert.Equal(AffixLimits.OrdinarySlots + 2, AffixLimits.ForceDropRows);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, 0)]
    [InlineData(4, 4)]
    [InlineData(6, 6)]
    [InlineData(7, 6)]
    [InlineData(9, 6)]
    public void RarityNeverReachesTheUniqueValues(int count, int rarity) =>
        Assert.Equal(rarity, AffixLimits.RarityForOrdinaryCount(count));
}
