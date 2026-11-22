using LastEpoch_Hud.Scripts.Core.Jewels;
using LastEpoch_Hud.Tests.Support;

namespace LastEpoch_Hud.Tests.Core.Jewels;

public sealed class JewelImplicitTableTests
{
    private static readonly string[] Names = { "Cobalt Jewel", "Ruby Jewel", "Emerald Jewel", "Prismatic Jewel" };

    [Fact]
    public void ShippedTableParsesCleanlyAndCoversEverySubtype()
    {
        var errors = new List<string>();
        var rows = JewelImplicitTable.Parse(File.ReadAllText(Path.Combine(GameEnvironment.ModProjectDir, "Data", "JewelImplicits.csv")), errors);
        Assert.Empty(errors);
        Assert.True(JewelImplicitTable.CoversEverySubtype(rows));
        // Cobalt 2, Ruby 2, Emerald 1, Prismatic 2: Jewels already held keep one saved roll per implicit.
        Assert.Equal(new[] { 2, 2, 1, 2 }, Enumerable.Range(0, 4).Select(i => rows.Count(r => r.Subtype == i)).ToArray());
        var prismatic = rows.Where(r => r.Subtype == 3).ToList();
        Assert.Equal("AllAttributes", prismatic[0].Property);
        Assert.Equal(6f, prismatic[0].Min);
        Assert.Equal(20f, prismatic[0].Max);
        Assert.Equal("INCREASED", prismatic[1].ModType);
        Assert.Equal(0.06f, prismatic[1].Min);
    }

    [Fact]
    public void WrittenRowsReadBack()
    {
        var rows = new List<JewelImplicitRow> { new() { Subtype = 1, Property = "Health", ModType = "ADDED", Tags = "None", SpecialTag = 3, Min = 60, Max = 100.5f } };
        var errors = new List<string>();
        var back = JewelImplicitTable.Parse(JewelImplicitTable.Write(rows, Names), errors);
        Assert.Empty(errors);
        Assert.Single(back);
        Assert.Equal(3, back[0].SpecialTag);
        Assert.Equal(100.5f, back[0].Max);
    }

    [Fact]
    public void BadRowsAreReportedAndTheRestLoad()
    {
        string text = "Subtype,SubtypeName,Property,ModType,Tags,SpecialTag,Min,Max\n"
            + "0,Cobalt,Mana,ADDED,None,0,44,60\n"
            + "9,Nope,Mana,ADDED,None,0,1,2\n"
            + "1,Ruby,Health,MORE,None,0,1,2\n"
            + "2,Emerald,Damage,INCREASED,None,0,0.3,0.1\n";
        var errors = new List<string>();
        var rows = JewelImplicitTable.Parse(text, errors);
        Assert.Single(rows);
        Assert.Equal(3, errors.Count);
        Assert.False(JewelImplicitTable.CoversEverySubtype(rows));
    }
}
