using LastEpoch_Hud.Scripts.Core.Crafting;

namespace LastEpoch_Hud.Tests.Core.Crafting;

public class FractureRegistryTests
{
    [Fact]
    public void RemembersTheFracturedAffix()
    {
        var r = new FractureRegistry();
        r.Set(87400817, 719);
        Assert.True(r.Has(87400817));
        Assert.True(r.Is(87400817, 719));
        Assert.False(r.Is(87400817, 5));
        Assert.False(r.Has(1));
        Assert.Equal(-1, r.AffixOf(1));
    }

    [Fact]
    public void ACopyIsFracturedToo()
    {
        var r = new FractureRegistry();
        r.Set(87400817, 719);
        Assert.True(r.Copy(87400817, 826709183));
        Assert.True(r.Is(826709183, 719));
        Assert.False(r.Copy(1, 2));
        Assert.False(r.Copy(87400817, 87400817));
    }

    [Fact]
    public void SurvivesBeingSaved()
    {
        var r = new FractureRegistry();
        r.Set(87400817, 719);
        FractureRegistry back = FractureRegistry.FromJson(r.ToJson());
        Assert.True(back.Is(87400817, 719));
        Assert.Equal(0, FractureRegistry.FromJson("not json").Count);
    }
}
