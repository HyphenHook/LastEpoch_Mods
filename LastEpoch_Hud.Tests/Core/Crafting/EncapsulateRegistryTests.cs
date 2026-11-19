using LastEpoch_Hud.Scripts.Core.Crafting;

namespace LastEpoch_Hud.Tests.Core.Crafting;

public class EncapsulateRegistryTests
{
    [Fact]
    public void RemembersTheEncapsulatedAffix()
    {
        var r = new EncapsulateRegistry();
        r.Set(87400817, 719);
        Assert.True(r.Has(87400817));
        Assert.True(r.Is(87400817, 719));
        Assert.False(r.Is(87400817, 5));
        Assert.False(r.Has(1));
        Assert.Equal(-1, r.AffixOf(1));
    }

    [Fact]
    public void ACopyIsEncapsulatedToo()
    {
        var r = new EncapsulateRegistry();
        r.Set(87400817, 719);
        Assert.True(r.Copy(87400817, 826709183));
        Assert.True(r.Is(826709183, 719));
        Assert.False(r.Copy(1, 2));
        Assert.False(r.Copy(87400817, 87400817));
    }

    [Fact]
    public void SurvivesBeingSaved()
    {
        var r = new EncapsulateRegistry();
        r.Set(87400817, 719);
        EncapsulateRegistry back = EncapsulateRegistry.FromJson(r.ToJson());
        Assert.True(back.Is(87400817, 719));
        Assert.Equal(0, EncapsulateRegistry.FromJson("not json").Count);
    }

    [Fact]
    public void LegacySaveLoadsAndTheRenamedSaveTakesPrecedence()
    {
        string directory = Path.Combine(Path.GetTempPath(), "LastEpoch-Encapsulate-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string legacy = Path.Combine(directory, "Fractures.json");
        string current = Path.Combine(directory, "Encapsulate.json");
        try
        {
            const string original = "{\"87400817\":719}";
            File.WriteAllText(legacy, original);
            var registry = EncapsulateRegistry.Load(current, legacy);
            Assert.True(registry.Is(87400817, 719));
            File.WriteAllText(current, registry.ToJson());
            registry = EncapsulateRegistry.Load(current, legacy);
            Assert.True(registry.Is(87400817, 719));
            registry.Remove(87400817);
            File.WriteAllText(current, registry.ToJson());
            Assert.Equal(0, EncapsulateRegistry.Load(current, legacy).Count);
            Assert.Equal(original, File.ReadAllText(legacy));
        }
        finally
        {
            File.Delete(legacy);
            File.Delete(current);
            Directory.Delete(directory);
        }
    }
}
