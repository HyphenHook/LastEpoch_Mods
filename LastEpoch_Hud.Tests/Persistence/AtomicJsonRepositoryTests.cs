using LastEpoch_Hud.Scripts.Infrastructure.Persistence;
using Newtonsoft.Json;

namespace LastEpoch_Hud.Tests.Persistence;

public sealed class AtomicJsonRepositoryTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "LastEpoch-Persistence-" + Guid.NewGuid().ToString("N"));
    private string SavePath => Path.Combine(directory, "state.json");
    private AtomicJsonRepository<Dictionary<uint, int>> Open(bool backup = false) =>
        new(SavePath, new JsonStorePolicy { KeepBackup = backup });

    [Fact]
    public void MissingFileDoesNotCreateDefaultsOrDirectories()
    {
        Assert.False(Open().TryLoad(out var value));
        Assert.Null(value);
        Assert.False(Directory.Exists(directory));
    }

    [Theory]
    [InlineData("{")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("null")]
    [InlineData("[]")]
    public void InvalidFileThrowsAndPreservesPrimaryAndBackup(string json)
    {
        Directory.CreateDirectory(directory);
        File.WriteAllText(SavePath, json);
        File.WriteAllText(SavePath + ".bak", "{\"123\":7}");
        Assert.ThrowsAny<JsonException>(() => Open(true).TryLoad(out _));
        Assert.Equal(json, File.ReadAllText(SavePath));
        Assert.Equal("{\"123\":7}", File.ReadAllText(SavePath + ".bak"));
    }

    [Fact]
    public void BackupContainsPreviousCommittedDocumentAndNeverReplacesMissingPrimary()
    {
        var store = Open(true);
        store.Save(new() { [87400817] = 3 });
        Assert.False(File.Exists(SavePath + ".bak"));
        string previous = File.ReadAllText(SavePath);
        store.Save(new() { [87400817] = 4 });
        Assert.Equal(previous, File.ReadAllText(SavePath + ".bak"));
        Assert.True(Open().TryLoad(out var loaded));
        Assert.Equal(4, loaded[87400817]);
        File.Delete(SavePath);
        Assert.False(Open(true).TryLoad(out _));
        Assert.Equal(previous, File.ReadAllText(SavePath + ".bak"));
    }

    [Fact]
    public void FailedReplacementPreservesPrimaryBackupAndAllowsRetry()
    {
        var store = Open(true);
        store.Save(new() { [1] = 2 });
        store.Save(new() { [1] = 3 });
        string primary = File.ReadAllText(SavePath), backup = File.ReadAllText(SavePath + ".bak");
        using (var held = new FileStream(SavePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            Assert.ThrowsAny<IOException>(() => store.Save(new() { [1] = 4 }));
        Assert.Equal(primary, File.ReadAllText(SavePath));
        Assert.Equal(backup, File.ReadAllText(SavePath + ".bak"));
        Assert.False(File.Exists(SavePath + ".tmp"));
        store.Save(new() { [1] = 4 });
        Assert.True(Open().TryLoad(out var loaded));
        Assert.Equal(4, loaded[1]);
    }

    [Fact]
    public void AbandonedStagingFileIsPreservedForRecovery()
    {
        var store = Open();
        store.Save(new() { [1] = 2 });
        File.WriteAllText(SavePath + ".tmp", "{\"1\":3}");
        Assert.ThrowsAny<IOException>(() => store.Save(new() { [1] = 4 }));
        Assert.Equal("{\"1\":3}", File.ReadAllText(SavePath + ".tmp"));
        Assert.True(store.TryLoad(out var loaded));
        Assert.Equal(2, loaded[1]);
    }

    [Fact]
    public void ValidationRunsBeforePublishingOrAcceptingARead()
    {
        var store = new AtomicJsonRepository<Dictionary<uint, int>>(SavePath, validate: value =>
        {
            if (value.Values.Any(v => v < 0)) throw new JsonSerializationException("Negative balance");
        });
        store.Save(new() { [1] = 2 });
        Assert.Throws<JsonSerializationException>(() => store.Save(new() { [1] = -1 }));
        Assert.Equal("{\"1\":2}", File.ReadAllText(SavePath));
        File.WriteAllText(SavePath, "{\"1\":-1}");
        Assert.Throws<JsonSerializationException>(() => store.TryLoad(out _));
        Assert.Equal("{\"1\":-1}", File.ReadAllText(SavePath));
    }

    [Fact]
    public void CharmBytesKeepTheirExistingBase64WireFormat()
    {
        var store = new AtomicJsonRepository<Dictionary<string, byte[]>>(SavePath);
        store.Save(new() { ["character|CharmSlot1"] = new byte[] { 0, 43, 255 } });
        Assert.Equal("{\"character|CharmSlot1\":\"ACv/\"}", File.ReadAllText(SavePath));
        Assert.True(store.TryLoad(out var loaded));
        Assert.Equal(new byte[] { 0, 43, 255 }, loaded["character|CharmSlot1"]);
    }

    [Fact]
    public void ExistingBackupIsUntouchedWhenBackupPolicyIsDisabled()
    {
        var store = Open();
        store.Save(new() { [1] = 2 });
        File.WriteAllText(SavePath + ".bak", "retain me");
        store.Save(new() { [1] = 3 });
        Assert.Equal("retain me", File.ReadAllText(SavePath + ".bak"));
        Assert.Equal("{\"1\":3}", File.ReadAllText(SavePath));
    }

    public void Dispose()
    {
        if (Directory.Exists(directory)) Directory.Delete(directory, true);
    }
}
