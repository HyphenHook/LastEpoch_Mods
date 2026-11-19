using LastEpoch_Hud.Scripts.Core.Crafting;
using Newtonsoft.Json;

namespace LastEpoch_Hud.Tests.Core.Crafting;

public sealed class StashMaterialStoreTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "LastEpoch-StashMaterials-" + Guid.NewGuid().ToString("N"));
    private string SavePath => Path.Combine(directory, "AdvancedMaterials.Stashes.json");
    private string LegacyPath => Path.Combine(directory, "AdvancedMaterials.json");
    private const string Orb = "EchoOrb";
    private readonly string season = StashMaterialStore.ScopeKey(true, null, "STASH_CYCLE_7_0");
    private readonly string legacy = StashMaterialStore.ScopeKey(true, null, "STASH_0");

    public StashMaterialStoreTests() => Directory.CreateDirectory(directory);

    private StashMaterialStore Open() => new(SavePath, LegacyPath);

    [Fact]
    public void SameNativeStashSharesAcrossCharactersAndOfflineSessionIdentities()
    {
        string same = StashMaterialStore.ScopeKey(true, "another-offline-session", "STASH_CYCLE_7_0");
        var store = Open();
        Assert.True(store.TryChange(season, Orb, 9));
        Assert.Equal(9, store.Count(same, Orb));
        Assert.Equal(9, Open().Count(same, Orb));
    }

    [Theory]
    [InlineData("STASH_0")]
    [InlineData("STASH_CYCLE_8_0")]
    [InlineData("STASH_CYCLE_7_1")]
    [InlineData("STASH_CYCLE_7_3")]
    [InlineData("STASH_CYCLE_7_4")]
    [InlineData("STASH_CYCLE_7_2_character-one")]
    [InlineData("STASH_CYCLE_7_2_character-two")]
    public void DifferentNativeStashGroupsHaveIndependentBalances(string stashId)
    {
        var store = Open();
        string other = StashMaterialStore.ScopeKey(true, null, stashId);
        Assert.True(store.TryChange(season, Orb, 7));
        Assert.Equal(0, store.Count(other, Orb));
        Assert.True(store.TryChange(other, Orb, 2));
        Assert.Equal(7, store.Count(season, Orb));
        Assert.Equal(2, Open().Count(other, Orb));
    }

    [Fact]
    public void OnlineAccountsAndOfflineStorageAreSeparated()
    {
        string first = StashMaterialStore.ScopeKey(false, "account-one", "STASH_0");
        string second = StashMaterialStore.ScopeKey(false, "account-two", "STASH_0");
        var store = Open();
        Assert.True(store.TryChange(first, Orb, 3));
        Assert.Equal(0, store.Count(second, Orb));
        Assert.Equal(0, store.Count(legacy, Orb));
        Assert.Null(StashMaterialStore.ScopeKey(false, null, "STASH_0"));
    }

    [Fact]
    public void ImportsOldGlobalBalanceOnceAndRetainsOriginalFile()
    {
        const string original = "{\"EchoOrb\":12,\"ChaosOrb\":5}";
        File.WriteAllText(LegacyPath, original);
        var store = Open();
        Assert.Equal(12, store.Count(season, Orb));
        Assert.Equal(5, store.Count(season, "ChaosOrb"));
        Assert.True(store.TryChange(season, Orb, -1));
        Assert.Equal(0, store.Count(legacy, Orb));
        Assert.Equal(11, Open().Count(season, Orb));
        Assert.Equal(0, Open().Count(legacy, Orb));
        Assert.Equal(original, File.ReadAllText(LegacyPath));
    }

    [Fact]
    public void UnavailableScopeCannotClaimLegacyBalanceOrWrite()
    {
        File.WriteAllText(LegacyPath, "{\"EchoOrb\":8}");
        var store = Open();
        Assert.Null(StashMaterialStore.ScopeKey(true, null, null));
        store.Initialize(null);
        Assert.Equal(0, store.Count(null, Orb));
        Assert.False(store.TryChange(null, Orb, 1));
        Assert.False(File.Exists(SavePath));
        Assert.Equal(8, store.Count(season, Orb));
    }

    [Fact]
    public void InitializationAssignsLegacyBalanceToFirstLoadedStash()
    {
        File.WriteAllText(LegacyPath, "{\"EchoOrb\":8}");
        var store = Open();
        store.Initialize(season);
        Assert.Equal(0, store.Count(legacy, Orb));
        Assert.Equal(8, Open().Count(season, Orb));
    }

    [Fact]
    public void FailedMigrationCanRetryWithoutAssigningOrDuplicatingTheBalance()
    {
        File.WriteAllText(LegacyPath, "{\"EchoOrb\":8}");
        Directory.CreateDirectory(SavePath + ".tmp");
        var store = Open();
        Assert.NotNull(Record.Exception(() => store.Count(season, Orb)));
        Assert.False(File.Exists(SavePath));
        Directory.Delete(SavePath + ".tmp");
        Assert.Equal(8, store.Count(legacy, Orb));
        Assert.Equal(0, store.Count(season, Orb));
        Assert.Equal(8, Open().Count(legacy, Orb));
    }

    [Fact]
    public void FailedWriteKeepsThePreviousBalanceInMemoryAndOnDisk()
    {
        var store = Open();
        Assert.True(store.TryChange(season, Orb, 6));
        Directory.CreateDirectory(SavePath + ".tmp");
        Assert.NotNull(Record.Exception(() => store.TryChange(season, Orb, -1)));
        Assert.Equal(6, store.Count(season, Orb));
        Assert.Equal(6, Open().Count(season, Orb));
        Directory.Delete(SavePath + ".tmp");
        Assert.True(store.TryChange(season, Orb, -1));
        Assert.Equal(5, Open().Count(season, Orb));
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("{}")]
    [InlineData("{\"Version\":2,\"LegacyImportedTo\":\"stash\",\"Groups\":{}}")]
    [InlineData("{\"Version\":1,\"LegacyImportedTo\":\"stash\",\"Groups\":{\"stash\":{\"EchoOrb\":-1}}}")]
    public void InvalidSavesArePreservedAndCannotBeOverwritten(string contents)
    {
        File.WriteAllText(SavePath, contents);
        var store = Open();
        Assert.ThrowsAny<JsonException>(() => store.Count(season, Orb));
        Assert.ThrowsAny<JsonException>(() => store.TryChange(season, Orb, 1));
        Assert.Equal(contents, File.ReadAllText(SavePath));
    }

    [Fact]
    public void InvalidLegacySaveCannotBeMigratedAsAnEmptyBalance()
    {
        File.WriteAllText(LegacyPath, "not json");
        Assert.ThrowsAny<JsonException>(() => Open().Count(season, Orb));
        Assert.False(File.Exists(SavePath));
        Assert.Equal("not json", File.ReadAllText(LegacyPath));
    }

    [Fact]
    public void PreventsOverspendingAndOverflow()
    {
        var store = Open();
        Assert.False(store.TryChange(season, Orb, -1));
        Assert.True(store.TryChange(season, Orb, int.MaxValue));
        Assert.False(store.TryChange(season, Orb, 1));
        Assert.Equal(int.MaxValue, Open().Count(season, Orb));
        Assert.True(store.TryChange(season, Orb, -int.MaxValue));
        Assert.Equal(0, store.Count(season, Orb));
    }

    [Fact]
    public void ConcurrentCreditsOnTheSameStoreAreSaved()
    {
        var store = Open();
        Parallel.For(0, 30, _ => Assert.True(store.TryChange(season, Orb, 1)));
        Assert.Equal(30, Open().Count(season, Orb));
    }

    [Fact]
    public void NativeStashMergeMovesBalancesOnceAcrossReloads()
    {
        var store = Open();
        Assert.True(store.TryChange(season, Orb, 7));
        Assert.True(store.TryChange(legacy, Orb, 2));
        store.Merge(season, legacy);
        Assert.Equal(0, store.Count(season, Orb));
        Assert.Equal(9, store.Count(legacy, Orb));
        var reopened = Open();
        reopened.Merge(season, legacy);
        Assert.Equal(0, reopened.Count(season, Orb));
        Assert.Equal(9, reopened.Count(legacy, Orb));
    }

    [Fact]
    public void FailedMergePreservesBothBalancesAndCanBeRetried()
    {
        var store = Open();
        Assert.True(store.TryChange(season, Orb, 7));
        Assert.True(store.TryChange(legacy, Orb, 2));
        Directory.CreateDirectory(SavePath + ".tmp");
        Assert.NotNull(Record.Exception(() => store.Merge(season, legacy)));
        Assert.Equal(7, store.Count(season, Orb));
        Assert.Equal(2, Open().Count(legacy, Orb));
        Directory.Delete(SavePath + ".tmp");
        store.Merge(season, legacy);
        Assert.Equal(9, Open().Count(legacy, Orb));
    }

    [Fact]
    public void MergeLeavesOverflowInTheSourceStash()
    {
        var store = Open();
        Assert.True(store.TryChange(season, Orb, 7));
        Assert.True(store.TryChange(legacy, Orb, int.MaxValue - 2));
        store.Merge(season, legacy);
        Assert.Equal(int.MaxValue, store.Count(legacy, Orb));
        Assert.Equal(5, Open().Count(season, Orb));
    }

    public void Dispose() => Directory.Delete(directory, true);
}
