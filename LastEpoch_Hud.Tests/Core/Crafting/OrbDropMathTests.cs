using LastEpoch_Hud.Scripts.Core.Crafting;

namespace LastEpoch_Hud.Tests.Core.Crafting;

public class OrbDropMathTests
{
    [Fact]
    public void RarerEnemiesDropItMoreOften()
    {
        double normal = OrbDropMath.Chance(0, false);
        double magic = OrbDropMath.Chance(1, false);
        double rare = OrbDropMath.Chance(2, false);
        double boss = OrbDropMath.Chance(2, true);
        Assert.True(normal < magic && magic < rare && rare < boss);
    }

    [Fact]
    public void TheMultiplierScalesAndIsKeptAProbability()
    {
        Assert.Equal(OrbDropMath.Chance(2, false) * 2, OrbDropMath.Chance(2, false, 2), 9);
        Assert.Equal(0, OrbDropMath.Chance(2, false, 0));
        Assert.Equal(1, OrbDropMath.Chance(2, true, 1000));
    }
}
