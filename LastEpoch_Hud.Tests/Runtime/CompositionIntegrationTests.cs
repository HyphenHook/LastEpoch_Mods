using LastEpoch_Hud.Tests.Support;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace LastEpoch_Hud.Tests.Runtime;

/// <summary>Inspect the actual compiled composition without loading Unity or the mod.</summary>
public sealed class CompositionIntegrationTests
{
    [Fact]
    public void CompositionKeepsLegacyOrderAndCatalogDependencies()
    {
        GameEnvironment.SkipWithoutModBuild();
        using var module = GameEnvironment.ReadGameModule(GameEnvironment.ModDll);
        var root = module.GetType("LastEpoch_Hud.Bootstrap.Composition.ModComposition");
        Assert.NotNull(root);
        var create = root.Methods.Single(m => m.Name == "Create");
        var names = create.Body.Instructions.Where(i => i.OpCode == OpCodes.Ldstr)
            .Select(i => (string)i.Operand).Where(s => s != "harmony").ToArray();
        Assert.Equal(new[] {
            "Data.Tunables", "Inspector.Start", "TestActions", "Base", "Inspector.Tick",
            "Catalog.Poc", "Catalog.Charms", "Catalog.Jewels", "Catalog.JewelAffixes",
            "Heorot.Buffs", "Heorot.Combat", "Bazaar.Market", "Bazaar.Refresh",
            "Crafting.Recombinator", "Crafting.MemoryReflector", "Crafting.AdvancedForge", "Crafting.IconSearch",
            "Drops.OrbVisuals", "Equipment.Accessories", "Equipment.BaseTypePoc",
            "Login.OfflineOnly", "Login.CharacterSelect", "Bazaar.WarmUp",
            "Scenes.Loaded", "Cosmetics.SceneLoaded", "Scenes.Unloaded",
            "Inspector.Stop", "Bazaar.Save", "Unity.Cache"
        }, names);
        Assert.Equal("LastEpoch_Hud.Application.Runtime.ModRuntime", root.Properties.Single(p => p.Name == "Runtime").PropertyType.FullName);
    }

    [Fact]
    public void WorkerFactoryRetainsAllTypedRegistrationsAndNamesInOrder()
    {
        GameEnvironment.SkipWithoutModBuild();
        using var module = GameEnvironment.ReadGameModule(GameEnvironment.ModDll);
        var manager = module.GetType("LastEpoch_Hud.Scripts.Mods_Manager");
        var register = manager.Methods.Single(m => m.Name == "RegisterWorkers");
        var names = register.Body.Instructions.Where(i => i.OpCode == OpCodes.Ldstr)
            .Select(i => (string)i.Operand).ToArray();
        Assert.Equal(new[] {
            "Mod_Character_AutoPotion", "Mod_Character_PotionReplenishment", "Mod_Character_Blessings",
            "Mod_Character_GodMode", "Mod_Character_LowLife", "Mod_Character_Buffs",
            "Mod_Character_SafeTp", "Mod_Character_TwoHandedShield", "Mod_Items_NbSocket",
            "Mod_Items_AutoStore_All10Sec", "Mod_Items_Headhunter", "Mod_Items_Mjolner",
            "Mod_Items_SandsOfSilk", "Mod_Items_EssentiaSanguis", "Mod_Items_Mageblood",
            "Mod_Items_CustomItemIcons", "Mod_Items_Temporalis", "Mod_Bank_Anywhere",
            "Mod_Monoliths_Complete_Objectives", "mod_bank_quad", "mod_damage_meter",
            "Mod_SessionGainCounters", "Mod_Teleport_To_Scene", "Mod_Summon_Options", "Mod_Skills_AutoCast"
        }, names);
        var components = register.Body.Instructions.Select(i => i.Operand).OfType<GenericInstanceMethod>()
            .Where(m => m.Name == "Worker").Select(m => m.GenericArguments.Single().Name).ToArray();
        Assert.Equal(new[] {
            "Character_AutoPotions", "Character_PotionReplenishment", "Character_Blessings",
            "Character_GodMode", "Character_LowLife", "Character_PermanentBuffs",
            "Character_TpSafe", "Character_TwoHandedShield", "Items_SocketsNb",
            "Items_AutoStore_WithTimer", "Items_HeadHunter", "Items_Mjolner",
            "Items_SandsOfSilk", "Items_EssentiaSanguis", "Items_Mageblood",
            "CustomItemIconUpdater", "Items_Temporalis", "Character_Bank_Anywhere",
            "Monoliths_CompleteObjective", "Bank_Quad", "DamageMeter", "SessionCounterRuntime",
            "Teleport_ToScene", "Summon_Options", "Skills_AutoCast"
        }, components);

        // The first argument after each name is the active flag, and the last
        // argument before Worker<T> is the clone flag. Check lifecycle-sensitive
        // initial states without constructing a Unity GameObject in the test host.
        var instructions = register.Body.Instructions;
        var states = new List<(string Name, bool Active, bool Clone)>();
        for (int i = 0; i < instructions.Count; i++)
        {
            if (instructions[i].Operand is not GenericInstanceMethod method || method.Name != "Worker") continue;
            int nameIndex = i - 1;
            while (instructions[nameIndex].OpCode != OpCodes.Ldstr) nameIndex--;
            states.Add(((string)instructions[nameIndex].Operand,
                instructions[nameIndex + 1].OpCode == OpCodes.Ldc_I4_1,
                instructions[i - 1].OpCode == OpCodes.Ldc_I4_1));
        }
        Assert.Equal(new[] { "Mod_Character_AutoPotion", "Mod_Character_GodMode", "Mod_Character_LowLife" },
            states.Where(s => !s.Active).Select(s => s.Name));
        Assert.Equal(new[] { "Mod_SessionGainCounters", "Mod_Summon_Options", "Mod_Skills_AutoCast" },
            states.Where(s => !s.Clone).Select(s => s.Name));

        var enable = manager.Methods.Single(m => m.Name == "Enable");
        Assert.Equal(new[] { "Mod_Character_GodMode", "Mod_Character_LowLife", "Mod_Character_AutoPotion", "Mod_Items_AutoStore_All10Sec" },
            enable.Body.Instructions.Where(i => i.OpCode == OpCodes.Ldstr).Select(i => (string)i.Operand));
    }
}
