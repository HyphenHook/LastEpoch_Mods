using LastEpoch_Hud.Tests.Support;
using Mono.Cecil;

namespace LastEpoch_Hud.Tests.Patches;

/// <summary>Native nullable boxing must never override the category in a serialized search.</summary>
public sealed class BazaarStallInteropTests
{
    [Fact]
    public void QueryUsesSerializedStallInsteadOfBoxedNullableFields()
    {
        GameEnvironment.SkipWithoutModBuild();
        using var module = ModuleDefinition.ReadModule(GameEnvironment.ModDll);
        var prefix = module.GetType("LastEpoch_Hud.Scripts.Mods.Economy.BazaarQueryItemsPatch")
            .Methods.Single(m => m.Name == "Prefix");
        var calls = prefix.Body.Instructions.Select(i => i.Operand).OfType<MethodReference>().ToList();
        Assert.Contains(calls, m => m.DeclaringType.Name == "BazaarQuery" && m.Name == "Read");
        Assert.DoesNotContain(calls, m => m.Name == "get_currentStallType"
            || m.DeclaringType.FullName.StartsWith("Il2CppSystem.Nullable`1"));
    }

    [Fact]
    public void OpenConstructsNativeNullableInsteadOfWritingItsBackingFields()
    {
        GameEnvironment.SkipWithoutModBuild();
        using var module = ModuleDefinition.ReadModule(GameEnvironment.ModDll);
        var prefix = module.GetType("LastEpoch_Hud.Scripts.Mods.Economy.BazaarPanelOpenOfflinePatch")
            .Methods.Single(m => m.Name == "Prefix");
        var calls = prefix.Body.Instructions.Select(i => i.Operand).OfType<MethodReference>()
            .Where(m => m.DeclaringType.FullName.StartsWith("Il2CppSystem.Nullable`1")).ToList();
        Assert.Contains(calls, m => m.Name == ".ctor" && m.Parameters.Count == 1);
        Assert.DoesNotContain(calls, m => m.Name is "get_value" or "set_value" or "get_hasValue" or "set_hasValue");
    }
}
