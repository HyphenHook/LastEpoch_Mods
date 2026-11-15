using LastEpoch_Hud.Scripts.Core.Crafting;

namespace LastEpoch_Hud.Tests.Core.Crafting;

public class EncapsulationMathTests
{
    [Fact]
    public void NeedsFourAffixes()
    {
        Assert.NotEqual("", EncapsulationMath.WhyNot(3, false));
        Assert.Equal("", EncapsulationMath.WhyNot(4, false));
        Assert.Equal("", EncapsulationMath.WhyNot(6, false));
    }

    [Fact]
    public void OnlyOnceOnAnItem()
    {
        Assert.NotEqual("", EncapsulationMath.WhyNot(5, true));
    }

    [Theory]
    [InlineData(4)]
    [InlineData(6)]
    public void EveryAffixIsEquallyLikely(int count)
    {
        var rng = new Random(7);
        var hits = new int[count];
        const int rolls = 60_000;
        for (int i = 0; i < rolls; i++)
        {
            hits[EncapsulationMath.Pick(count, rng)]++;
        }
        foreach (int h in hits)
        {
            Assert.InRange(h / (double)rolls, 1.0 / count - 0.01, 1.0 / count + 0.01);
        }
    }
}
