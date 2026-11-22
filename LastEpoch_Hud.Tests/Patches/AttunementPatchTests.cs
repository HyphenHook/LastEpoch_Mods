using LastEpoch_Hud.Tests.Support;
using Mono.Cecil;

namespace LastEpoch_Hud.Tests.Patches;

/// <summary>The native value overloads have independent implementations and each needs a safe hook.</summary>
public sealed class AttunementPatchTests
{
    private static readonly Lazy<Cpp2IlAddressIndex> Index = new(() => Cpp2IlAddressIndex.Load(GameEnvironment.Cpp2IlOutDir));

    [Fact]
    public void EquippedAffixesScaleTheFinalMultiplierInsteadOfTheZeroBasedBonus()
    {
        GameEnvironment.SkipWithoutGame();
        GameEnvironment.SkipWithoutModBuild();
        using var game = GameEnvironment.ReadGameModule(Path.Combine(GameEnvironment.Il2CppDir, "Il2CppLE.dll"));
        var target = game.GetType("Il2Cpp.AffixList").Methods.Single(m => m.Name == "ChangeAffixModifier");
        Assert.Equal("postRoundingEffectModifier", target.Parameters[7].Name);
        Assert.Equal(0f, target.Parameters[7].Constant);
        using var mod = ModuleDefinition.ReadModule(GameEnvironment.ModDll);
        var scaling = mod.GetType("LastEpoch_Hud.Scripts.Mods.Items.Crafting.Items_AttunementStats")
            .NestedTypes.Single(t => t.Name == "EquipmentAffixScaling");
        var prefix = scaling.Methods.Single(m => m.Name == "Prefix");
        Assert.Contains(prefix.Parameters, p => p.Name == "__7" && p.ParameterType.FullName == "System.Single&");
        Assert.DoesNotContain(prefix.Parameters, p => p.Name == "__1");
    }

    [Theory]
    [InlineData("SingleAffixScaling")]
    [InlineData("MultiAffixScaling")]
    public void DisplayedAffixesScaleTheNativeResultInsteadOfTheZeroBasedBonus(string name)
    {
        GameEnvironment.SkipWithoutModBuild();
        using var mod = ModuleDefinition.ReadModule(GameEnvironment.ModDll);
        var scaling = mod.GetType("LastEpoch_Hud.Scripts.Mods.Items.Crafting.Items_AttunementStats")
            .NestedTypes.Single(t => t.Name == name);
        Assert.DoesNotContain(scaling.Methods.Single(m => m.Name == "Prefix").Parameters,
            p => p.ParameterType.FullName == "System.Single&");
        var postfix = scaling.Methods.Single(m => m.Name == "Postfix");
        Assert.Contains(postfix.Parameters, p => p.Name == "__result" && p.ParameterType.FullName == "System.Single&");
        Assert.Contains(postfix.Parameters, p => p.Name == "__state");
        Assert.Contains(scaling.Methods, m => m.Name == "Finalizer");
    }

    [Theory]
    [InlineData("Il2Cpp.AffixList", "GetSingleAffixValue", "Il2Cpp.AffixList/SingleAffix")]
    [InlineData("Il2Cpp.AffixList", "GetMultiAffixPropertyValue", "Il2Cpp.AffixList/MultiAffix")]
    [InlineData("Il2Cpp.TooltipItemManager", "FormatAffix", "Il2Cpp.ItemDataUnpacked")]
    public void BothCalculationOverloadsSupportTheAttunementPrefix(string type, string method, string firstParameter)
    {
        GameEnvironment.SkipWithoutGame();
        GameEnvironment.SkipWithoutCpp2IlOut();
        using var module = GameEnvironment.ReadGameModule(Path.Combine(GameEnvironment.Il2CppDir, "Il2CppLE.dll"));
        var overloads = module.GetType(type).Methods.Where(m => m.Name == method).ToList();
        Assert.Equal(2, overloads.Count);
        Assert.All(overloads, m => Assert.Equal(firstParameter, m.Parameters[0].ParameterType.FullName));
        Assert.True(Index.Value.TryGetOffsets(type, method, out var offsets));
        Assert.Equal(2, offsets.Count);
        using var native = File.OpenHandle(GameEnvironment.GameAssemblyDll);
        foreach (long offset in offsets)
        {
            var code = new byte[HookSafety.CodeBytes];
            RandomAccess.Read(native, code, offset);
            Assert.False(HookSafety.TryFindShortBranch(code, out _),
                type + "." + method + " has an unsafe native entry point.");
        }
    }
}
