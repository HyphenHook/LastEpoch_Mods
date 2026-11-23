using LastEpoch_Hud.Scripts.Core.Heorot;
using LastEpoch_Hud.Tests.Support;
using Mono.Cecil;

namespace LastEpoch_Hud.Tests.Patches;

public sealed class HeorotCombatTests
{
    private const string CombatType = "LastEpoch_Hud.Scripts.Mods.Character.Heorot.HeorotCombat";

    [Fact]
    public void ChillUsesTheNativeByteIdAndDamageUsesTheColdIndex()
    {
        GameEnvironment.SkipWithoutGame();
        using var game = GameEnvironment.ReadGameModule(Path.Combine(GameEnvironment.Il2CppDir, "Il2CppLE.dll"));
        var chill = game.GetType("Il2Cpp.AilmentID");
        Assert.Equal("System.Byte", chill.Fields.Single(f => f.Name == "value__").FieldType.FullName);
        Assert.Equal(HeorotRules.ChillId, Convert.ToByte(chill.Fields.Single(f => f.Name == "Chill").Constant));
        Assert.Equal(2, Convert.ToInt32(game.GetType("Il2Cpp.DamageType").Fields.Single(f => f.Name == "COLD").Constant));
    }

    [Fact]
    public void SourceScopedChillTargetsTheCommonNativeApplicationWithAnExplicitCreator()
    {
        GameEnvironment.SkipWithoutGame();
        using var game = GameEnvironment.ReadGameModule(Path.Combine(GameEnvironment.Il2CppDir, "Il2CppLE.dll"));
        var method = game.GetType("Il2Cpp.AilmentReceiver").Methods.Single(m => m.Name == "ApplyAilmentWithDamageStats");
        Assert.Equal("Il2Cpp.Ailment", method.Parameters[0].ParameterType.FullName);
        Assert.Equal("Il2Cpp.Actor", method.Parameters[1].ParameterType.FullName);
        Assert.Equal("Il2Cpp.AilmentReceiver/ActiveAilment", method.ReturnType.FullName);
    }

    [Fact]
    public void DamageRecipeAllocationIsBehindTheOwnersPowerCheck()
    {
        GameEnvironment.SkipWithoutModBuild();
        using var mod = ModuleDefinition.ReadModule(GameEnvironment.ModDll);
        var method = mod.GetType(CombatType).NestedTypes.Single(t => t.Name == "CaptureDamageBuild")
            .Methods.Single(m => m.Name == "Postfix");
        var instructions = method.Body.Instructions.ToList();
        int active = instructions.FindIndex(i => i.Operand is MethodReference m && m.Name == "IsActive");
        int allocation = instructions.FindIndex(i => i.OpCode.Code == Mono.Cecil.Cil.Code.Newobj);
        Assert.True(active >= 0 && allocation > active);
        Assert.Contains(instructions.Take(active), i => i.Operand is MethodReference m && m.Name == "get_actor");
        Assert.Contains(instructions.Skip(active).Take(allocation - active),
            i => i.OpCode.FlowControl == Mono.Cecil.Cil.FlowControl.Cond_Branch);
    }

    [Fact]
    public void PowerRebuildsTheNativeIncreasedPoolBeforeProtection()
    {
        GameEnvironment.SkipWithoutModBuild();
        using var mod = ModuleDefinition.ReadModule(GameEnvironment.ModDll);
        var prefix = mod.GetType(CombatType).NestedTypes.Single(t => t.Name == "PowerDamage")
            .Methods.Single(m => m.Name == "Prefix");
        // The rebuild itself is shared by Heorot's Power (Cold) and Bhuldar's Wrath (Fire).
        Assert.Contains(prefix.Body.Instructions.Select(i => i.Operand).OfType<MethodReference>(), m => m.Name == "Rebuilt");
        var method = mod.GetType(CombatType).Methods.Single(m => m.Name == "Rebuilt");
        var calls = method.Body.Instructions.Select(i => i.Operand).OfType<MethodReference>().ToList();
        Assert.Contains(calls, m => m.DeclaringType.FullName == "Il2Cpp.Stats" && m.Name == "IncreasedStat");
        Assert.DoesNotContain(calls, m => m.Name == "MoreStat" || m.Name == "HealthDamage" || m.Name == "ApplyResistance");
        Assert.Contains(calls, m => m.Name == "buildDamageStats");
        Assert.Contains(calls, m => m.Name == "Copy");
        Assert.Contains(calls, m => m.Name == "RebuiltColdPreservingScale");
        Assert.Equal("Il2Cpp.DamageStats&", prefix.Parameters.Single(p => p.Name == "__0").ParameterType.FullName);
        var instructions = method.Body.Instructions.ToList();
        int increased = instructions.FindIndex(i => i.Operand is MethodReference m && m.Name == "IncreasedStat");
        // SP.Damage, the rule's damage tag (Cold or Fire), increased, specialTag 0, extraTag 0: no more/generic damage stat.
        Assert.Equal(Mono.Cecil.Cil.Code.Ldc_I4_0, instructions[increased - 5].OpCode.Code);
        Assert.DoesNotContain(instructions[increased - 4].OpCode.Code, new[] { Mono.Cecil.Cil.Code.Ldc_I4_0, Mono.Cecil.Cil.Code.Ldc_I4_4 });
        Assert.Equal(Mono.Cecil.Cil.Code.Ldc_I4_0, instructions[increased - 2].OpCode.Code);
        Assert.Equal(Mono.Cecil.Cil.Code.Ldc_I4_0, instructions[increased - 1].OpCode.Code);
    }

    [Fact]
    public void InactivePowerCleanupFreesNativeWeakHandlesAndSnapshotOwners()
    {
        GameEnvironment.SkipWithoutModBuild();
        using var mod = ModuleDefinition.ReadModule(GameEnvironment.ModDll);
        var method = mod.GetType(CombatType).Methods.Single(m => m.Name == "Tick");
        var calls = method.Body.Instructions.Select(i => i.Operand).OfType<MethodReference>().ToList();
        Assert.Contains(calls, m => m.Name == "il2cpp_gchandle_free");
        Assert.Contains(calls, m => m.Name == "Clear");
        Assert.Contains(calls, m => m.Name == "IsActive");
    }

    [Fact]
    public void OverflowUsesAPrivateDefinitionAndDoesNotPatchAGlobalGetter()
    {
        GameEnvironment.SkipWithoutModBuild();
        using var mod = ModuleDefinition.ReadModule(GameEnvironment.ModDll);
        var combat = mod.GetType(CombatType);
        var calls = combat.Methods.Single(m => m.Name == "ChillForSource").Body.Instructions
            .Select(i => i.Operand).OfType<MethodReference>().ToList();
        Assert.Contains(calls, m => m.Name == "Instantiate");
        Assert.Contains(calls, m => m.Name == "set_maxInstances");
        Assert.Contains(calls, m => m.Name == "set_maxStacksThatApplyBuffs");
        Assert.Contains(calls, m => m.Name == "getAilment");
        var patchNames = combat.NestedTypes.SelectMany(t => t.CustomAttributes)
            .Where(a => a.AttributeType.FullName == "HarmonyLib.HarmonyPatch")
            .SelectMany(a => a.ConstructorArguments).Where(a => a.Value is string)
            .Select(a => (string)a.Value).ToList();
        Assert.DoesNotContain("getMaxStacks", patchNames);
        Assert.DoesNotContain("get_maxInstances", patchNames);
    }
}
