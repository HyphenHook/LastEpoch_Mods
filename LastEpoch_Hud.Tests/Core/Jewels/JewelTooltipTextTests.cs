using LastEpoch_Hud.Scripts.Core.Jewels;

namespace LastEpoch_Hud.Tests.Core.Jewels;

public sealed class JewelTooltipTextTests
{
    [Fact]
    public void CorrectsCapturedNativeManaLeechHoverAndRange()
    {
        const string native = "<color=#C184FFFF><size=100%>+2% Melee Damage Mana Leech\n"
            + "<color=#99B0FF><cspace=-.4><font=\"Caladea SDF\">Tier: 7 (drop only)\n"
            + "Range: 2% to 2%</font></cspace></color>\n</color>";
        Assert.True(JewelTooltipText.Matches(native, "of Melee Damage Leeched as Mana"));
        Assert.False(JewelTooltipText.Matches(native, "of Damage Leeched as Mana"));
        string result = JewelTooltipText.ReplaceValues(native, "1.69%", "1.68%", "2.00%", true);
        Assert.Contains("<size=100%>1.69% of Melee Damage Leeched as Mana", result);
        Assert.Contains("Tier: 7 (drop only)", result);
        Assert.Contains("Range: 1.68% to 2.00%", result);
        Assert.DoesNotContain("+2%", result);
    }

    [Fact]
    public void ReplacesRollAndRangeWithoutChangingRichTextTags()
    {
        const string text = "<size=100%><color=#ffffff>2%</color> Melee Damage Leeched as Mana</size>\n<color=#99B0FF>Range: <b>2%</b> to <b>2%</b></color>";
        string result = JewelTooltipText.ReplaceValues(text, "1.69%", "1.68%", "2.00%", true);
        Assert.Equal("<size=100%><color=#ffffff>1.69%</color> Melee Damage Leeched as Mana</size>\n<color=#99B0FF>Range: <b>1.68%</b> to <b>2.00%</b></color>", result);
    }

    [Fact]
    public void MatchesNativeWordingAndSeparatesGlobalFromTaggedLeech()
    {
        const string text = "<size=100%>2% Melee Damage Leeched as Mana</size>";
        Assert.True(JewelTooltipText.Matches(text, "of Melee Damage Leeched as Mana"));
        Assert.False(JewelTooltipText.Matches(text, "of Damage Leeched as Mana"));
        Assert.True(JewelTooltipText.Matches("2% Damage Leeched as Mana", "of Damage Leeched as Mana"));
    }

    [Fact]
    public void AddsMissingRangeForCustomDamageRow()
    {
        const string text = "<size=100%>Gain 6% of Damage as Extra fire Damage</size>";
        Assert.True(JewelTooltipText.Matches(text, "of Damage as Extra fire Damage"));
        string result = JewelTooltipText.ReplaceValues(text, "6.25%", "5.00%", "7.00%", true);
        Assert.Contains("Gain 6.25%", result);
        Assert.Contains("Range: 5.00% to 7.00%", result);
        Assert.Contains("<size=100%>", result);
    }
}
