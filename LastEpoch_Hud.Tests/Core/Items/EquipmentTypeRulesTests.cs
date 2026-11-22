using LastEpoch_Hud.Scripts.Core.Items;

namespace LastEpoch_Hud.Tests.Core.Items;

public class EquipmentTypeRulesTests
{
    [Theory]
    [InlineData(0, true)]
    [InlineData(21, true)]
    [InlineData(24, true)]
    [InlineData(43, true)]
    [InlineData(-1, false)]
    [InlineData(25, false)]
    [InlineData(34, false)]
    [InlineData(41, false)]
    [InlineData(42, false)]
    [InlineData(44, true)]
    [InlineData(100, false)]
    public void EquipmentCategoryDoesNotAdmitIdolsMaterialsOrPoc(int type, bool expected) =>
        Assert.Equal(expected, EquipmentTypeRules.IsEquipment(type));
}
