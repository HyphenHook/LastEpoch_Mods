using LastEpoch_Hud.Scripts.Core.Crafting;
using LastEpoch_Hud.Scripts.Core.Economy;
using Newtonsoft.Json;

namespace LastEpoch_Hud.Tests.Core.Economy;

public sealed class BazaarCraftingStateTests
{
    [Fact]
    public void AttunedListingsKeepTheirMarkerAndMultiplier()
    {
        var listing = new BazaarCraftingState
        {
            Attunement = new AttunementState { Multiplier = 0.85, Implicits = new byte[] { 201 } },
        };
        var restored = JsonConvert.DeserializeObject<BazaarCraftingState>(JsonConvert.SerializeObject(listing));
        var purchase = restored.Copy();
        Assert.NotNull(purchase.Attunement);
        Assert.Equal(0.85, purchase.Attunement.Multiplier);
        purchase.Attunement.Implicits[0] = 0;
        Assert.Equal(201, restored.Attunement.Implicits[0]);
    }

    [Fact]
    public void SavedListingKeepsBothMarkersOnTheSameAffix()
    {
        var state = new BazaarCraftingState { EncapsulatedAffixId = 719, RemindedAffixId = 719 };
        var restored = JsonConvert.DeserializeObject<BazaarCraftingState>(JsonConvert.SerializeObject(state));
        Assert.Equal(719, restored.EncapsulatedAffixId);
        Assert.Equal(719, restored.RemindedAffixId);
        Assert.Null(restored.Memory);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void SavedListingPreservesTheMemorySideTierAndExactChoices(int side)
    {
        var state = new BazaarCraftingState
        {
            Memory = new ObscuredMemory
            {
                Tier = 5,
                AffixType = side,
                OriginalAffixId = 123,
                Choices = new() { new MemoryChoice { AffixId = 719, Roll = 211 }, new MemoryChoice { AffixId = 720, Roll = 14 } },
            },
        };
        var restored = JsonConvert.DeserializeObject<BazaarCraftingState>(JsonConvert.SerializeObject(state));
        Assert.Equal(side, restored.Memory.AffixType);
        Assert.Equal(5, restored.Memory.Tier);
        Assert.Equal(123, restored.Memory.OriginalAffixId);
        Assert.False(restored.Memory.Revealed);
        Assert.Equal(new ushort[] { 719, 720 }, restored.Memory.Choices.Select(c => c.AffixId));
        Assert.Equal(new byte[] { 211, 14 }, restored.Memory.Choices.Select(c => c.Roll));
    }

    [Fact]
    public void UnveilingOrRerollingAPurchasedCopyCannotChangeTheListingOrItsTwin()
    {
        var listing = new BazaarCraftingState
        {
            EncapsulatedAffixId = 123,
            Memory = new ObscuredMemory { Choices = new() { new MemoryChoice { AffixId = 719, Roll = 211 } } },
        };
        var twin = listing.Copy();
        var purchase = listing.Copy();
        purchase.EncapsulatedAffixId = null;
        purchase.Memory.Revealed = true;
        purchase.Memory.Choices[0].AffixId = 999;
        purchase.Memory.Choices.Clear();
        Assert.Equal(123, listing.EncapsulatedAffixId);
        Assert.False(listing.Memory.Revealed);
        Assert.Equal(719, Assert.Single(listing.Memory.Choices).AffixId);
        Assert.False(twin.Memory.Revealed);
        Assert.Equal(719, Assert.Single(twin.Memory.Choices).AffixId);
    }

    [Fact]
    public void ExistingPendingMemorySaveRemainsCompatible()
    {
        const string json = "{\"87400817\":{\"Tier\":3,\"AffixType\":1,\"OriginalAffixId\":719,\"Revealed\":true,\"Choices\":[{\"AffixId\":720,\"Roll\":211}]}}";
        var saved = JsonConvert.DeserializeObject<Dictionary<uint, ObscuredMemory>>(json);
        var memory = saved[87400817];
        Assert.Equal(3, memory.Tier);
        Assert.Equal(1, memory.AffixType);
        Assert.Equal(719, memory.OriginalAffixId);
        Assert.True(memory.Revealed);
        Assert.Equal(211, Assert.Single(memory.Choices).Roll);
        var reopened = JsonConvert.DeserializeObject<Dictionary<uint, ObscuredMemory>>(JsonConvert.SerializeObject(saved));
        Assert.Equal(720, Assert.Single(reopened[87400817].Choices).AffixId);
    }

    [Fact]
    public void OlderMemoriesWithoutOriginalAffixIdentityKeepTheirChoices()
    {
        const string json = "{\"Tier\":4,\"AffixType\":0,\"Choices\":[{\"AffixId\":719,\"Roll\":200}]}";
        var restored = JsonConvert.DeserializeObject<ObscuredMemory>(json).Copy();
        Assert.Null(restored.OriginalAffixId);
        Assert.Equal(4, restored.Tier);
        Assert.Equal(200, Assert.Single(restored.Choices).Roll);
    }
}
