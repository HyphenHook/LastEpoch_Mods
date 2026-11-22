using LastEpoch_Hud.Core.Equipment;

namespace LastEpoch_Hud.Tests.Equipment;

public sealed class CatalogAppendRulesTests
{
    [Fact]
    public void PreservedCustomIdsCanAppendInOrder()
    {
        foreach (int id in new[] { 42, 43, 44 }) CatalogAppendRules.Validate(id, id, 41, false);
    }

    [Theory]
    [InlineData(41, 41, 41, false)]
    [InlineData(43, 42, 41, false)]
    [InlineData(42, 44, 41, false)]
    [InlineData(42, 42, 41, true)]
    public void ConflictsAndGapsCannotMutateCatalog(int id, int count, int maximum, bool occupied) =>
        Assert.Throws<InvalidOperationException>(() => CatalogAppendRules.Validate(id, count, maximum, occupied));

    [Theory]
    [InlineData(-1)]
    [InlineData(256)]
    public void UnserializableIdsAreRejected(int id) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => CatalogAppendRules.Validate(id, id, 41, false));
}
