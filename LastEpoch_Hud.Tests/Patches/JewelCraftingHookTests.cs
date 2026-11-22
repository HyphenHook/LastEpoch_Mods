using LastEpoch_Hud.Tests.Support;

namespace LastEpoch_Hud.Tests.Patches;

public class JewelCraftingHookTests
{
    private static readonly Lazy<Cpp2IlAddressIndex> Index = new(() => Cpp2IlAddressIndex.Load(GameEnvironment.Cpp2IlOutDir));

    [Theory]
    [InlineData("Il2Cpp.AffixList/Affix", "CanRollOn")]
    [InlineData("Il2Cpp.AffixList/Affix", "CanForgeOnItem")]
    [InlineData("Il2Cpp.ItemData", "AddAffixTier")]
    [InlineData("Il2Cpp.ItemList", "GetItemMaximumAffixes")]
    [InlineData("Il2Cpp.GenerateItems", "RollAffixes")]
    [InlineData("Il2Cpp.ItemData", "isForgeableItem")]
    [InlineData("Il2Cpp.ItemData", "CanBeCorrupted")]
    [InlineData("Il2Cpp.ItemData", "GetCorruptableItemCategory")]
    [InlineData("Il2Cpp.ItemData", "CorruptionOutcomeCanApplyToItem")]
    [InlineData("Il2Cpp.ItemData", "AddRandomSpecialAffix")]
    [InlineData("Il2Cpp.OneSlotItemContainer", "CanAddItemType")]
    [InlineData("Il2Cpp.ItemData", "RefreshIDAndValues")]
    [InlineData("Il2Cpp.ItemData", "setValuesFromSerialisation")]
    [InlineData("Il2Cpp.ItemDataUnpacked", "AfterIDChange")]
    [InlineData("Il2Cpp.ItemData", "getLegendaryPotentialTier")]
    [InlineData("Il2CppLE.UI.Tooltips.ItemTooltipContentBuilder", "GetPotentialInfo")]
    [InlineData("Il2Cpp.UITooltipItem", "SetAsItemTooltip")]
    public void JewelHooksExistAndTheirNativeEntriesCanBeDetoured(string type, string method)
    {
        GameEnvironment.SkipWithoutGame();
        GameEnvironment.SkipWithoutCpp2IlOut();
        using var module = GameEnvironment.ReadGameModule(Path.Combine(GameEnvironment.Il2CppDir, "Il2CppLE.dll"));
        Assert.Contains(module.GetType(type).Methods, m => m.Name == method);
        Assert.True(Index.Value.TryGetOffsets(type, method, out var offsets));
        Assert.NotEmpty(offsets);
        using var native = File.OpenHandle(GameEnvironment.GameAssemblyDll);
        foreach (long offset in offsets)
        {
            var code = new byte[HookSafety.CodeBytes];
            RandomAccess.Read(native, code, offset);
            Assert.False(HookSafety.TryFindShortBranch(code, out _), type + "." + method + " has an unsafe native entry.");
        }
    }
}
