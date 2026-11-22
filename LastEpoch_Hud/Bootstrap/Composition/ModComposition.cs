using System;
using LastEpoch_Hud.Application.Runtime;
using LastEpoch_Hud.Core.Runtime;
using LastEpoch_Hud.Scripts;
using LastEpoch_Hud.Scripts.Dev;
using LastEpoch_Hud.Scripts.Mods.Character.Heorot;
using LastEpoch_Hud.Scripts.Mods.Economy;
using LastEpoch_Hud.Scripts.Mods.Items.BaseTypePoc;
using LastEpoch_Hud.Scripts.Mods.Items.Charms;
using LastEpoch_Hud.Scripts.Mods.Items.Crafting;
using LastEpoch_Hud.Scripts.Mods.Items.Drop;
using LastEpoch_Hud.Scripts.Mods.Items.Jewels;
using LastEpoch_Hud.Scripts.Mods.Login;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LastEpoch_Hud.Bootstrap.Composition;

public static class ModComposition
{
    public static ModRuntime Runtime { get; private set; }

    /// <summary>Call once after Main sets its logger; subsequent calls return the same runtime.
    /// Initialize starts the inspector. Main retains ownership of its startup messages.</summary>
    public static ModRuntime Create(HarmonyLib.Harmony harmony)
    {
        if (harmony == null) throw new ArgumentNullException(nameof(harmony));
        return Runtime ??= new ModRuntime(new IRuntimeModule[]
        {
            ModuleActions.Initialize("Inspector.Start", () => InspectorServer.Start(harmony)),
            ModuleActions.Initialize("TestActions", () => Scripts.ModUI.Pages.ItemsTestDropsPage.Configure(TestActionFactory.Create())),
            ModuleActions.Tick("Base", () =>
            {
                if (!Base.Initializing && !Base.Initialized) Base.Init();
            }),
            ModuleActions.Tick("Inspector.Tick", InspectorServer.Tick),
            // Append-only catalog allocation must precede accessory consumers. Each step
            // has its own fault boundary, and remains retryable while game data loads.
            ModuleActions.Tick("Catalog.Poc", EquipmentBaseTypePoc.EnsureCatalog),
            ModuleActions.Tick("Catalog.Charms", CharmBaseType.Ensure),
            ModuleActions.Tick("Catalog.Jewels", JewelBaseType.Ensure),
            ModuleActions.Tick("Catalog.JewelAffixes", () =>
            {
                if (JewelBaseType.Registered) JewelAffixes.EnsureRegistered();
            }),
            ModuleActions.Tick("Heorot.Buffs", HeorotBuffs.Tick),
            ModuleActions.Tick("Heorot.Combat", HeorotCombat.Tick),
            ModuleActions.Tick("Bazaar.Market", OfflineBazaarMarket.Tick),
            ModuleActions.Tick("Bazaar.Refresh", BazaarRefresh.Tick),
            ModuleActions.Tick("Crafting.Recombinator", Items_RecombinatorWindow.Tick),
            ModuleActions.Tick("Crafting.MemoryReflector", Items_MemoryReflectorWindow.Tick),
            ModuleActions.Tick("Crafting.AdvancedForge", Items_AdvancedForgeWindow.Tick),
            ModuleActions.Tick("Drops.OrbVisuals", OrbItemVisuals.Tick),
            ModuleActions.Tick("Equipment.Accessories", CharmEquipment.Tick),
            ModuleActions.Tick("Equipment.BaseTypePoc", EquipmentBaseTypePoc.Tick),
            ModuleActions.Tick("Login.OfflineOnly", Login_OfflineOnly.Tick),
            ModuleActions.Tick("Login.CharacterSelect", Login_OfflineCharacterSelect.Tick),
            ModuleActions.Draw("Bazaar.WarmUp", OfflineBazaarMarket.DrawWarmUp),
            ModuleActions.SceneLoaded("Scenes.Loaded", (_, _) => UpdateScene()),
            ModuleActions.SceneLoaded("Cosmetics.SceneLoaded", (_, _) => Scripts.Mods.Cosmetics.OfflineCosmeticCatalog.OnSceneLoaded()),
            ModuleActions.SceneUnloaded("Scenes.Unloaded", (_, _) => UpdateScene()),
            // Preserve the legacy quit order, even if one of these actions fails.
            ModuleActions.Shutdown("Inspector.Stop", InspectorServer.Stop),
            ModuleActions.Shutdown("Bazaar.Save", OfflineBazaarMarket.SaveOnQuit),
            ModuleActions.Shutdown("Unity.Cache", () => { Caching.ClearCache(); }),
        }, new LoggerFaultSink());
    }

    private static void UpdateScene() => Scenes.SceneName = SceneManager.GetActiveScene().name;

    private sealed class LoggerFaultSink : IRuntimeFaultSink
    {
        public void Report(string module, LifecyclePhase phase, Exception exception, int repeats) =>
            Main.logger_instance?.Error($"Runtime {module}/{phase} ({repeats} suppressed): {exception}");
    }
}
