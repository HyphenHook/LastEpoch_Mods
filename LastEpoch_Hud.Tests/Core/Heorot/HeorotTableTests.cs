using LastEpoch_Hud.Scripts.Core.Heorot;

namespace LastEpoch_Hud.Tests.Core.Heorot;

[Collection("HeorotValuesState")]
public sealed class HeorotTableTests
{
    private const string Stats = "# note\nBuff,Kind,Property,Tags,Value\nPower,Added,Damage,Cold,120\nEnergy,AilmentChance,Chill,None,1.2\nEnergy,Increased,Damage,Cold,0.75\n";

    [Fact]
    public void StatRowsParse()
    {
        var errors = new List<string>();
        var rows = HeorotTable.ParseStats(Stats, errors);
        Assert.Empty(errors);
        Assert.Equal(3, rows.Count);
        Assert.Equal("Power", rows[0].Buff);
        Assert.Equal(120f, rows[0].Value);
        Assert.Equal("AilmentChance", rows[1].Kind);
        Assert.Equal(0.75f, rows[2].Value);
    }

    [Fact]
    public void BadStatRowsAreReportedAndTheRestLoad()
    {
        var errors = new List<string>();
        var rows = HeorotTable.ParseStats("Buff,Kind,Property,Tags,Value\nPower,Wrong,Damage,Cold,1\nPower,Added,Damage,Cold,abc\nDefence,Added,ColdResistance,None,0.65\n", errors);
        Assert.Single(rows);
        Assert.Equal(2, errors.Count);
    }

    [Fact]
    public void AMissingHeaderStopsTheStatLoad()
    {
        var errors = new List<string>();
        Assert.Empty(HeorotTable.ParseStats("Power,Added,Damage,Cold,120\n", errors));
        Assert.Single(errors);
    }

    [Fact]
    public void NamedValuesParseAndKeepUnknownNames()
    {
        var errors = new List<string>();
        var values = HeorotTable.ParseValues("Name,Value,Note\nIncreasedColdPerChill,0.3,per chill\nFutureBuffNumber,7,later\nIncreasedColdPerChill,9,duplicate\n", errors);
        Assert.Equal(0.3f, values["increasedcoldperchill"]);
        Assert.Equal(7f, values["FutureBuffNumber"]);
        Assert.Single(errors);
    }

    [Fact]
    public void RulesFollowTheTableAndReturnToTheirDefaults()
    {
        Assert.Equal(HeorotRules.DefaultIncreasedColdPerChill, HeorotRules.IncreasedColdPerChill);
        try
        {
            HeorotValues.Set(new Dictionary<string, float> { [HeorotValues.IncreasedColdPerChillName] = 0.5f, [HeorotValues.AdditionalChillStacksName] = 3 });
            Assert.Equal(1.5f, HeorotRules.PowerIncreasedCold(true, 3));
            Assert.Equal(6, HeorotRules.ChillLimit(3, true));
        }
        finally
        {
            HeorotValues.Set(null);
        }
        Assert.Equal(0.75f, HeorotRules.PowerIncreasedCold(true, 3));
        Assert.Equal(5, HeorotRules.ChillLimit(3, true));
    }
}
