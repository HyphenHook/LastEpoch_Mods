using LastEpoch_Hud.Scripts.Core.Jewels;
using LastEpoch_Hud.Tests.Support;

namespace LastEpoch_Hud.Tests.Core.Jewels;

[Collection("JewelAffixRulesState")]
public sealed class JewelAffixTableTests
{
    private static JewelAffixRow Row(int id, string name = "Jewel Test", int weight = 100)
    {
        var row = new JewelAffixRow { Id = id, Name = name, Weight = weight, MinTier = 6, Step = 0.01f, Side = "Suffix", Property = "Strength", ModType = "ADDED", Title = "Mighty", TitleType = "Custom" };
        for (int tier = 0; tier < JewelAffixRow.TierCount; tier++)
        {
            row.Min[tier] = tier + 0.5f;
            row.Max[tier] = tier + 1.25f;
        }
        return row;
    }

    [Fact]
    public void WrittenRowsReadBackExactly()
    {
        var errors = new List<string>();
        var rows = JewelAffixTable.Parse(JewelAffixTable.Write(new[] { Row(4000), Row(2123, "Name, with \"quotes\"") }), errors);
        Assert.Empty(errors);
        Assert.Equal(2, rows.Count);
        Assert.Equal(4000, rows[0].Id);
        Assert.Equal("Name, with \"quotes\"", rows[1].Name);
        Assert.Equal(6, rows[0].MinTier);
        Assert.Equal(0.01f, rows[0].Step);
        Assert.Equal("Mighty", rows[0].Title);
        Assert.Equal("Custom", rows[0].TitleType);
        Assert.Equal(2.5f, rows[0].Min[2]);
        Assert.Equal(7.25f, rows[0].Max[6]);
    }

    [Fact]
    public void CommentsAndBlankLinesAreIgnored()
    {
        string text = "# note\n\n" + JewelAffixTable.Write(new[] { Row(4001) });
        var errors = new List<string>();
        Assert.Single(JewelAffixTable.Parse(text, errors));
        Assert.Empty(errors);
    }

    [Fact]
    public void ABadRowIsReportedAndTheRestStillLoad()
    {
        string good = JewelAffixTable.Write(new[] { Row(4000), Row(4001) });
        string broken = good.Replace("4001,Exclusive,0,Jewel Test,Mighty,Custom,Suffix,Strength,ADDED,,100,6,0", "4001,Exclusive,0,Jewel Test,Mighty,Custom,Suffix,Strength,ADDED,,100,9,0");
        var errors = new List<string>();
        var rows = JewelAffixTable.Parse(broken, errors);
        Assert.Single(rows);
        Assert.Equal(4000, rows[0].Id);
        Assert.Single(errors);
        Assert.Contains("MinTier", errors[0]);
    }

    [Fact]
    public void DuplicateIdsAndInvertedRangesAreRejected()
    {
        string text = JewelAffixTable.Write(new[] { Row(4000), Row(4000) });
        var errors = new List<string>();
        Assert.Single(JewelAffixTable.Parse(text, errors));
        Assert.Contains("twice", errors[0]);

        var inverted = Row(4002);
        inverted.Min[0] = 5;
        inverted.Max[0] = 1;
        errors.Clear();
        Assert.Empty(JewelAffixTable.Parse(JewelAffixTable.Write(new[] { inverted }), errors));
        Assert.Single(errors);
    }

    [Fact]
    public void AMissingHeaderStopsTheLoad()
    {
        var errors = new List<string>();
        Assert.Empty(JewelAffixTable.Parse("4000,Exclusive,0,x,,,Prefix,,,,1,1,0\n", errors));
        Assert.Single(errors);
    }

    [Fact]
    public void MinimumTierOverridesReplaceTheDefaultsAndCanBeCleared()
    {
        int id = JewelAffixRules.ExtraDamageIdStart;
        Assert.Equal(6, JewelAffixRules.MinimumTier(id));
        try
        {
            JewelAffixRules.SetMinimumTierOverrides(new Dictionary<int, int> { [id] = 3 });
            Assert.Equal(3, JewelAffixRules.MinimumTier(id));
            Assert.Equal(1, JewelAffixRules.MinimumTier(JewelAffixRules.ManaLeechIdStart));
        }
        finally
        {
            JewelAffixRules.SetMinimumTierOverrides(null);
        }
        Assert.Equal(6, JewelAffixRules.MinimumTier(id));
    }

    [Fact]
    public void EveryShippedAffixHasATitleThatMatchesItsSide()
    {
        var errors = new List<string>();
        var rows = JewelAffixTable.Parse(File.ReadAllText(Path.Combine(GameEnvironment.ModProjectDir, "Data", "JewelAffixes.csv")), errors);
        Assert.Empty(errors);
        Assert.Equal(163, rows.Count);
        foreach (var row in rows)
        {
            Assert.False(string.IsNullOrWhiteSpace(row.Title), "affix " + row.Id + " has no Title, so the item name would fall back to its Name");
            bool suffixWording = row.Title.StartsWith("of ", StringComparison.OrdinalIgnoreCase);
            Assert.True(suffixWording == string.Equals(row.Side, "Suffix", StringComparison.OrdinalIgnoreCase),
                "affix " + row.Id + " is a " + row.Side + " but its Title is \"" + row.Title + "\"");
        }
    }
}
