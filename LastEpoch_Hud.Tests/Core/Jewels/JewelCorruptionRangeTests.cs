using LastEpoch_Hud.Scripts.Core.Jewels;

namespace LastEpoch_Hud.Tests.Core.Jewels;

public sealed class JewelCorruptionRangeTests
{
    [Theory]
    [InlineData(3029, 0, .1f, .2f, .2f, .3f)]
    [InlineData(3030, 0, .1f, .2f, .2f, .3f)]
    [InlineData(3032, 0, 20f, 40f, 120f, 240f)]
    [InlineData(3032, 1, .01f, .01f, .02f, .10f)]
    [InlineData(3034, 0, 25f, 35f, 75f, 200f)]
    [InlineData(3037, 0, 3f, 8f, 9f, 44f)]
    [InlineData(3038, 0, 4f, 8f, 12f, 44f)]
    [InlineData(3039, 0, 5f, 10f, 15f, 50f)]
    [InlineData(3041, 0, 3f, 8f, 9f, 44f)]
    [InlineData(3045, 0, .01f, .02f, .01f, .02f)]
    [InlineData(3047, 0, 15f, 30f, 45f, 200f)]
    [InlineData(3051, 0, 3f, 7f, 22.5f, 52.5f)]
    [InlineData(3070, 0, .008f, .008f, .01f, .03f)]
    [InlineData(3070, 1, .008f, .008f, .01f, .03f)]
    [InlineData(3112, 0, 1f, 4f, 1f, 4f)]
    public void AppliesAffixSpecificRangesToPrimaryAndSecondaryStats(int id, int propertyIndex,
        float minimum, float maximum, float expectedMin, float expectedMax)
    {
        var actual = JewelAffixRules.CorruptionRange(minimum, maximum, id, propertyIndex);
        Assert.Equal(expectedMin, actual.Min, 5);
        Assert.Equal(expectedMax, actual.Max, 5);
    }

    [Theory]
    [InlineData(3071)]
    [InlineData(3072)]
    public void BeeCorruptionsRollOneToThree(int affixId)
    {
        var actual = JewelAffixRules.CorruptionRange(.8f, .8f, affixId);
        Assert.Equal(1f, actual.Min);
        Assert.Equal(3f, actual.Max);
    }

    [Theory]
    [InlineData(.03f, .08f, .09f, .24f)]
    [InlineData(1f, 4f, 3f, 12f)]
    [InlineData(1f, 1f, 1f, 1f)]
    [InlineData(.01f, .01f, .01f, .05f)]
    [InlineData(.01f, .02f, .01f, .05f)]
    [InlineData(.008f, .008f, .008f, .008f)]
    [InlineData(.8f, .8f, .8f, .8f)]
    public void BalancesNativeRangesWithoutTierScaling(float minimum, float maximum, float expectedMin, float expectedMax)
    {
        var actual = JewelAffixRules.CorruptionRange(minimum, maximum);
        Assert.Equal(expectedMin, actual.Min, 5);
        Assert.Equal(expectedMax, actual.Max, 5);
    }
}
