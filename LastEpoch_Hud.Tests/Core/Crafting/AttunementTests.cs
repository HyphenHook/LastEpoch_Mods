using LastEpoch_Hud.Scripts.Core.Crafting;
using Newtonsoft.Json;

namespace LastEpoch_Hud.Tests.Core.Crafting;

public sealed class AttunementTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "LastEpoch-Attunement-" + Guid.NewGuid().ToString("N"));
    private string SavePath => Path.Combine(directory, "Attunements.json");

    public AttunementTests() => Directory.CreateDirectory(directory);

    private static AttunementState Applied(double multiplier = 1.3) => new()
    {
        ItemType = 0,
        SubType = 3,
        UniqueId = 72,
        Multiplier = multiplier,
        Implicits = new byte[] { 40, 200 },
        Unique = new byte[] { 100, 210 },
        Affixes = new() { new AttunedAffix { Id = 719, Tier = 5, Roll = 240, Multiplier = multiplier } },
    };

    [Theory]
    [InlineData(0, 0.70)]
    [InlineData(300, 1.00)]
    [InlineData(600, 1.30)]
    [InlineData(-1, 0.70)]
    [InlineData(999, 1.30)]
    public void MultiplierStaysWithinTheSpecifiedBounds(int roll, double expected) =>
        Assert.Equal(expected, AttunementState.RollMultiplier(roll), 10);

    [Theory]
    [InlineData(0f, 1.28, 0.28f)]
    [InlineData(0f, 0.7, -0.3f)]
    [InlineData(0f, 1.0, 0f)]
    [InlineData(0.17f, 1.3, 0.521f)]
    [InlineData(0.5f, 0.8, 0.2f)]
    [InlineData(-0.25f, 1.2, -0.1f)]
    public void NativePostRoundingBonusScalesTheWholeRolledStat(float originalBonus, double multiplier, float expectedBonus)
    {
        float bonus = AttunementState.ScaleEffectBonus(originalBonus, multiplier);
        Assert.Equal(expectedBonus, bonus, 5);
        const double rolled = 40;
        Assert.Equal(rolled * (1 + originalBonus) * multiplier, rolled * (1 + bonus), 4);
    }

    [Fact]
    public void TheFirstUsePermanentlyExhaustsTheBaseItem()
    {
        Assert.Null(new AttunementStore(SavePath).Find(87400817));
        var store = new AttunementStore(SavePath);
        store.Set(87400817, Applied());
        Assert.NotNull(new AttunementStore(SavePath).Find(87400817));
    }

    [Fact]
    public void ScalingBelongsToTheAttunedAffixRoll()
    {
        var state = Applied();
        Assert.Equal(1.3, state.AffixMultiplier(719, 5, 240));
        Assert.Equal(1, state.AffixMultiplier(720, 5, 240));
        Assert.Equal(1, state.AffixMultiplier(719, 4, 240));
        Assert.Equal(1, state.AffixMultiplier(719, 5, 239));
    }

    [Fact]
    public void DifferentImplicitOrUniqueRollsDoNotReceiveStaleScaling()
    {
        var state = Applied();
        Assert.Equal(1.3, state.ImplicitMultiplier(0, 3, new byte[] { 40, 200 }));
        Assert.Equal(1, state.ImplicitMultiplier(0, 4, new byte[] { 40, 200 }));
        Assert.Equal(1, state.ImplicitMultiplier(0, 3, new byte[] { 40, 201 }));
        Assert.Equal(1.3, state.UniqueMultiplier(72, new byte[] { 100, 210 }));
        Assert.Equal(1, state.UniqueMultiplier(73, new byte[] { 100, 210 }));
        Assert.Equal(1, state.UniqueMultiplier(72, new byte[] { 101, 210 }));
    }

    [Fact]
    public void FixedImplicitAndUniqueStatsCanReceiveScaling()
    {
        var state = Applied();
        state.Implicits = Array.Empty<byte>();
        state.Unique = Array.Empty<byte>();
        Assert.Equal(1.3, state.ImplicitMultiplier(0, 3, Array.Empty<byte>()));
        Assert.Equal(1.3, state.UniqueMultiplier(72, Array.Empty<byte>()));
    }

    [Fact]
    public void CopiesKeepTheMarkerAndIndependentStatRecords()
    {
        var original = Applied();
        var copy = original.Copy();
        Assert.Equal(original.Multiplier, copy.Multiplier);
        copy.Affixes[0].Multiplier = 0.7;
        copy.Implicits[0] = 0;
        copy.Unique[0] = 0;
        Assert.Equal(1.3, original.Affixes[0].Multiplier);
        Assert.Equal(40, original.Implicits[0]);
        Assert.Equal(100, original.Unique[0]);
    }

    [Fact]
    public void PreviouslyProtectedAffixesCanRetainTheirOwnMultiplier()
    {
        var state = Applied(0.7);
        state.Affixes.Add(new AttunedAffix { Id = 800, Tier = 4, Roll = 100, Multiplier = 1.2 });
        var store = new AttunementStore(SavePath);
        store.Set(87400817, state);
        var restored = new AttunementStore(SavePath).Find(87400817);
        Assert.Equal(0.7, restored.AffixMultiplier(719, 5, 240));
        Assert.Equal(1.2, restored.AffixMultiplier(800, 4, 100));
        Assert.NotNull(restored);
    }

    [Fact]
    public void FailedWriteDoesNotSpendTheUseInTheSavedOrLiveMetadata()
    {
        var store = new AttunementStore(SavePath);
        Directory.CreateDirectory(SavePath + ".tmp");
        Assert.NotNull(Record.Exception(() => store.Set(87400817, Applied())));
        Assert.Null(store.Find(87400817));
        Assert.False(File.Exists(SavePath));
        Directory.Delete(SavePath + ".tmp");
        store.Set(87400817, Applied());
        Assert.NotNull(new AttunementStore(SavePath).Find(87400817));
    }

    [Fact]
    public void StoreKeepsAnIndependentSnapshotOfTheCraftingPlan()
    {
        var plan = Applied();
        var store = new AttunementStore(SavePath);
        store.Set(87400817, plan);
        plan.Multiplier = 0.7;
        plan.Affixes.Clear();
        Assert.Equal(1.3, store.Find(87400817).Multiplier);
        Assert.Single(store.Find(87400817).Affixes);
    }

    [Theory]
    [InlineData("{")]
    [InlineData("null")]
    [InlineData("{\"1\":{}}")]
    [InlineData("{\"1\":{\"Multiplier\":9}}")]
    public void DamagedMetadataCannotClearTheAttunedMarker(string json)
    {
        File.WriteAllText(SavePath, json);
        var store = new AttunementStore(SavePath);
        Assert.ThrowsAny<JsonException>(() => store.Find(87400817));
        Assert.ThrowsAny<JsonException>(() => store.Set(87400817, Applied()));
        Assert.Equal(json, File.ReadAllText(SavePath));
    }

    [Theory]
    [InlineData(0.7)]
    [InlineData(1.0)]
    [InlineData(1.3)]
    public void EveryMultiplierIncludingNeutralKeepsTheAttunedMarker(double multiplier)
    {
        var store = new AttunementStore(SavePath);
        store.Set(87400817, Applied(multiplier));
        Assert.Equal(multiplier, new AttunementStore(SavePath).Find(87400817).Multiplier);
        Assert.DoesNotContain("Uses", File.ReadAllText(SavePath));
        Assert.DoesNotContain("CanUse", File.ReadAllText(SavePath));
    }

    [Fact]
    public void LegacyUseCountIsIgnoredAndRemovedWhenSaving()
    {
        File.WriteAllText(SavePath, "{\"87400817\":{\"Uses\":3,\"Multiplier\":1.0}}");
        var store = new AttunementStore(SavePath);
        var attuned = store.Find(87400817);
        Assert.NotNull(attuned);
        Assert.Equal(1.0, attuned.Multiplier);
        store.Set(87400817, attuned);
        Assert.DoesNotContain("Uses", File.ReadAllText(SavePath));
        Assert.NotNull(new AttunementStore(SavePath).Find(87400817));
    }
    public void Dispose() => Directory.Delete(directory, true);
}
