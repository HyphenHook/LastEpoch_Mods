using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using HarmonyLib;
using Il2Cpp;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using LastEpoch_Hud.Scripts.Core.CustomItems;
using LastEpoch_Hud.Scripts.Core.CustomItems.Affixes;
using LastEpoch_Hud.Scripts.Core.Heorot;
using LastEpoch_Hud.Scripts.Mods.Character.Heorot;
using LastEpoch_Hud.Scripts.Mods.Items.AutoLoot;
using LastEpoch_Hud.Scripts.Mods.Items.Charms;
using LastEpoch_Hud.Scripts.Mods.Items.CustomItems;
using UnityEngine;
using Random = System.Random;

namespace LastEpoch_Hud.Scripts.Mods.Items.Jewels;

internal static class HeorotCore
{
    private static readonly Random random = new();
    private static readonly CustomUniqueRegistrar registrar = CreateRegistrar();
    private static readonly HashSet<IntPtr> rolledDeaths = new();
    private static readonly HashSet<uint> cacheRecoveryAttempts = new();
    private static Actor buffOwner;
    private static int ownedBuffs;
    private static bool announced;
    private static bool droppedThisEncounter;

    internal static bool IsCore(ItemData item) => !item.IsNullOrDestroyed()
        && item.itemType == JewelBaseType.Id && item.subType == 0 && item.isUniqueSetOrLegendary()
        && HeorotCoreRules.IsCoreUnique(item.uniqueID);

    /// <summary>Heorot’s Core or Volcanic Shaman’s Core. Naming, tooltips, roll protection and the Eternity Cache treat them alike.</summary>
    internal static bool IsAnyCore(ItemData item) => IsCore(item) || VolcanicCore.IsCore(item);

    private static string NameOf(ItemData item) => VolcanicCore.IsCore(item) ? Core.Volcanic.VolcanicCoreRules.Name : HeorotCoreRules.Name;

    /// <summary>The unique tooltip text of either Core.</summary>
    private static string DescriptionOf(ItemData item, bool expanded) =>
        VolcanicCore.IsCore(item) ? VolcanicCore.Description(VolcanicCore.MaskOf(item), expanded) : Description(MaskOf(item), expanded);

    /// <summary>
    /// The buffs this Core gives (1 Power, 2 Energy, 4 Defence). They are the item's one unique roll, set when the Core drops.
    /// Nothing may change that roll afterwards: Echo, Attunement, corruption and the native roll randomiser all leave it alone.
    /// </summary>
    internal static int MaskOf(ItemData item)
    {
        if (!IsCore(item) || item.uniqueRolls == null || item.uniqueRolls.Length == 0) return 0;
        return HeorotCoreRules.MaskForRoll(item.uniqueRolls[0]);
    }

    private static CustomUniqueRegistrar CreateRegistrar()
    {
        ushort uniqueId = HeorotCoreRules.UniqueId;
        CustomItemLocalization.Table.Register(CustomItemKeys.UniqueName(uniqueId), _ => HeorotCoreRules.Name);
        CustomItemLocalization.Table.Register(CustomItemKeys.UniqueLore(uniqueId), _ => "Winter endures within the heart of the fallen god.");
        return new CustomUniqueRegistrar(new CustomUniqueDefinition
        {
            Spec = new CustomUniqueSpec
            {
                Name = HeorotCoreRules.Name, UniqueId = uniqueId, BaseType = JewelBaseType.Id,
                BaseId = 0, AddsBase = false, LevelRequirement = 80, OverrideLevelRequirement = true,
                EffectiveLevelForLegendaryPotential = 80,
            },
            Flags = () => new CustomUniqueFlags(false, false, true),
            Mods = Array.Empty<CustomUniqueMod>(),
            TooltipEntries = () => new byte[] { CustomUniqueAffixes.DescriptionEntry },
            Description = _ => "Grants Heorot’s buffs",
        });
    }

    /// <summary>Remembers a Core's roll before a native step that may redo the item's unique rolls.</summary>
    internal static byte[] RememberRoll(ItemData item) =>
        IsAnyCore(item) && item.uniqueRolls != null && item.uniqueRolls.Length > 0 ? item.uniqueRolls.ToArray() : null;

    internal static void RestoreRoll(ItemData item, byte[] remembered)
    {
        if (remembered == null || item.IsNullOrDestroyed()) return;
        item.uniqueRolls = new Il2CppStructArray<byte>(remembered);
    }

    private static string Description(int mask, bool expanded = false)
    {
        var lines = new List<string>();
        for (int i = 0; i < 3; i++)
        {
            if ((mask & (1 << i)) == 0) continue;
            var kind = (HeorotBuffKind)i;
            lines.Add("Grants Heorot’s " + kind);
            if (expanded) lines.Add("<color=#99B0FF>" + HeorotBuffs.DescriptionFor(kind) + "</color>");
        }
        return string.Join("\n", lines);
    }

    internal static void EnsureRegistered()
    {
        if (JewelBaseType.Registered && HeorotBuffs.Registered) registrar.Update();
        if (!announced && registrar.IsRegistered)
        {
            announced = true;
            Main.logger_instance?.Msg("Registered Heorot’s Core: one unique whose buff combination is a protected roll on the item; boss-only drops.");
        }
    }

    internal static void Tick()
    {
        var player = Refs_Manager.player_data;
        var actor = Refs_Manager.player_actor;
        if (player.IsNullOrDestroyed() || !player.IsOffline || !Scenes.IsGameScene()
            || actor.IsNullOrDestroyed() || actor.health.IsNullOrDestroyed() || actor.health.currentHealth <= 0)
        {
            RemoveOwnedBuffs();
            return;
        }
        if (buffOwner != actor)
        {
            RemoveOwnedBuffs();
            buffOwner = actor;
        }
        RepairCacheOutput(actor);
        int desired = 0;
        for (int slot = 0; slot < JewelEquipment.SlotCount; slot++)
        {
            var item = JewelEquipment.EquippedAt(slot);
            desired |= MaskOf(item);
        }
        for (int i = 0; i < 3; i++)
        {
            int bit = 1 << i;
            var kind = (HeorotBuffKind)i;
            if ((desired & bit) != 0)
            {
                if (HeorotBuffs.IsActive(actor, kind)) continue;
                HeorotBuffs.Apply(actor, kind, float.MaxValue);
                ownedBuffs |= bit;
            }
            else if ((ownedBuffs & bit) != 0)
            {
                HeorotBuffs.Remove(actor, kind);
                ownedBuffs &= ~bit;
            }
        }
    }

    // Older builds admitted Core inputs but omitted the native output category.
    // Their completed legendary remains in beforeMain after the donor is consumed.
    private static void RepairCacheOutput(Actor actor)
    {
        var manager = actor.itemContainersManager;
        if (manager.IsNullOrDestroyed() || !manager.fullyInitialisedAndLoaded) return;
        var cache = manager.eternityCache;
        if (cache == null || cache.beforeMain == null || cache.beforeOther == null || cache.after == null) return;
        EnsureCacheTypes(cache.beforeMain, cache.beforeOther, cache.after);
        var source = cache.beforeMain.content;
        if (source == null || !IsAnyCore(source.data) || source.data.rarity != 9
            || cache.beforeOther.content != null || cache.after.content != null) return;
        var item = source.data;
        if (!cacheRecoveryAttempts.Add(item.individualID)) return;
        try
        {
            string folder = Path.Combine(Directory.GetCurrentDirectory(), "Mods", Main.mod_name);
            Directory.CreateDirectory(folder);
            string backup = Path.Combine(folder, "HeorotCoreCacheRecovery-" + item.individualID + ".txt");
            if (!File.Exists(backup)) File.WriteAllText(backup, Convert.ToBase64String(item.GetID().ToArray()));
            if (!MoveCompletedCacheResult(cache.beforeMain, cache.after)) return;
            Main.logger_instance?.Msg("Recovered completed Heorot’s Core " + item.individualID
                + " into the Eternity Cache output; preserved the exact item and affixes.");
        }
        catch (Exception ex)
        {
            // A content-change callback can fail after a native insertion. Keep one copy.
            if (cache.beforeMain.content == source && cache.after.content?.data?.individualID == item.individualID)
                cache.after.TryRemoveItem(cache.after.content, 1, Context.LOADING);
            ErrorLog.Report(ex, "Heorot’s Core cache recovery");
        }
    }

    internal static void EnsureCacheTypes(params OneSlotItemContainer[] containers)
    {
        foreach (var container in containers)
        {
            if (container == null || container.allowedBaseType == null) continue;
            var allowed = container.allowedBaseType;
            if (allowed.Contains((int)JewelBaseType.Id)) continue;
            container.allowedBaseType = new Il2CppStructArray<int>(allowed.ToArray().Concat(new[] { (int)JewelBaseType.Id }).ToArray());
        }
    }

    internal static bool MoveCompletedCacheResult(EternityCacheBeforeMainContainer before, EternityCacheAfterContainer after)
    {
        var source = before.content;
        if (source == null || !IsAnyCore(source.data) || source.data.rarity != 9 || after.content != null) return false;
        if (!after.TryAddItem(source.data, 1, Context.LOADING)) return false;
        if (before.TryRemoveItem(source, 1, Context.LOADING)) return true;
        after.TryRemoveItem(after.content, 1, Context.LOADING);
        throw new InvalidOperationException("Could not remove the original cache entry; output move was rolled back.");
    }

    private static void RemoveOwnedBuffs()
    {
        try
        {
            if (!buffOwner.IsNullOrDestroyed())
                for (int i = 0; i < 3; i++)
                    if ((ownedBuffs & (1 << i)) != 0) HeorotBuffs.Remove(buffOwner, (HeorotBuffKind)i);
        }
        finally
        {
            ownedBuffs = 0;
            buffOwner = null;
        }
    }

    internal static void Reset()
    {
        try { RemoveOwnedBuffs(); }
        finally
        {
            rolledDeaths.Clear();
            cacheRecoveryAttempts.Clear();
            droppedThisEncounter = false;
        }
    }

    internal static ItemDataUnpacked Create(int mask, int legendaryPotential)
    {
        if (mask < 1 || mask > HeorotCoreRules.CombinationCount || !registrar.IsRegistered)
            throw new InvalidOperationException("Heorot’s Core is not registered.");
        if (legendaryPotential < 1 || legendaryPotential > 4) throw new ArgumentOutOfRangeException(nameof(legendaryPotential));
        var item = new ItemDataUnpacked
        {
            itemType = JewelBaseType.Id, subType = 0, uniqueID = HeorotCoreRules.UniqueId,
            rarity = 7, legendaryPotential = (byte)legendaryPotential, LvlReq = 80,
            classReq = ItemList.ClassRequirement.Any,
            affixes = new Il2CppSystem.Collections.Generic.List<ItemAffix>(),
        };
        item.RollIndividualID();
        item.randomiseImplicitRolls();
        item.randomiseUniqueRolls();
        // The Core has no mods of its own, so its one unique roll is free to say which buffs it gives.
        byte roll = HeorotCoreRules.RollForMask(mask, random.Next(HeorotCoreRules.RollsPerCombination));
        if (item.uniqueRolls != null && item.uniqueRolls.Length > 0) item.uniqueRolls[0] = roll;
        else item.uniqueRolls = new Il2CppStructArray<byte>(new[] { roll });
        item.RefreshIDAndValues();
        var restored = new ItemDataUnpacked(item.GetID());
        if (!IsCore(restored) || restored.uniqueID != item.uniqueID || restored.legendaryPotential != legendaryPotential
            || MaskOf(restored) != mask)
            throw new InvalidOperationException("Heorot’s Core did not survive native item serialization.");
        return item;
    }

    internal static string Inspect()
    {
        if (!registrar.IsRegistered) return "Heorot's Core registration is pending or failed. Check the mod log.";
        var lines = new List<string>();
        var scaling = BossChanceGroup();
        foreach (int rarity in new[] { 0, 100, 300, 1000 })
            lines.Add("Native boss chance at increased rarity " + rarity + ": "
                + scaling.GetDropChance(rarity, 100, 0, 0, 0));
        var lpProbe = Create(1, 1);
        foreach (int corruption in new[] { 100, 300, 1000 })
        {
            int lp = lpProbe.RollLegendaryPotential(UniqueList.getUnique(lpProbe.uniqueID), 1, 100, corruption, 1, out _, 1);
            if (lp < 1 || lp > 4) throw new InvalidOperationException("Native Core LP roll exceeded 1–4.");
            lines.Add("Native LP roll succeeded at corruption " + corruption + ": " + lp);
        }
        for (int mask = 1; mask <= HeorotCoreRules.CombinationCount; mask++)
        {
            for (int lp = 1; lp <= 4; lp++) Create(mask, lp);
            var item = Create(mask, 1);
            if (item.FullName != HeorotCoreRules.Name || item.Implicits.Count != 0)
                throw new InvalidOperationException("Core name or implicit verification failed.");
            var tooltip = TooltipItemManager.GetUniqueDescription(item, -1, -1, item.uniqueID,
                TooltipMode.DefaultInfo, TooltipItemManager.SlotType.CRAFTING);
            var expanded = TooltipItemManager.GetUniqueDescription(item, -1, -1, item.uniqueID,
                TooltipMode.DefaultModRanges, TooltipItemManager.SlotType.CRAFTING);
            lines.Add("mask=" + mask + " read back=" + MaskOf(new ItemDataUnpacked(item.GetID())) + " roll=" + item.uniqueRolls[0] + " unique=" + item.uniqueID + " name=" + item.FullName
                + " implicits=" + item.Implicits.Count + " LP1..4 native serialization verified; tooltip="
                + string.Join(" | ", tooltip.ToArray()) + "; Alt=" + string.Join(" | ", expanded.ToArray()));
        }
        return string.Join("\n", lines);
    }

    private sealed class DropContext
    {
        internal Vector3 Position;
        internal int Corruption;
        internal int Level;
        internal double[] VariantWeights;
        internal VolcanicCore.DropPlan Volcanic;
    }

    internal static double[] DefaultVariantChances()
    {
        var weights = HeorotCoreRules.VariantWeights;
        double total = weights.Sum();
        return weights.Select(weight => total > 0 ? weight / total * HeorotCoreRules.DropChance * 100 : 0).ToArray();
    }

    private static double[] VariantChances(out bool direct)
    {
        direct = false;
        if (Save_Manager.instance.IsNullOrDestroyed() || !Save_Manager.instance.initialized)
            return DefaultVariantChances();
        var settings = Save_Manager.instance.data.Items.Drop;
        if (!settings.HeorotCoreOverride || !settings.HeorotCoreRatesInitialized) return DefaultVariantChances();
        direct = settings.HeorotCoreIgnoreScaling;
        return HeorotCoreRules.ValidateVariantChances(settings.HeorotCoreSingleChance,
            settings.HeorotCoreDoubleChance, settings.HeorotCoreTripleChance);
    }

    [HarmonyPatch(typeof(TooltipItemManager), nameof(TooltipItemManager.GetUniqueDescription))]
    private static class CoreExpandedDescription
    {
        private static void Postfix(ItemDataUnpacked __0, TooltipMode __4,
            ref Il2CppSystem.Collections.Generic.List<string> __result)
        {
            if (!IsAnyCore(__0)) return;
            bool expanded = __4 == TooltipMode.DefaultModRanges || __4 == TooltipMode.CompareModRanges
                || __4 == TooltipMode.Explanation;
            __result = new Il2CppSystem.Collections.Generic.List<string>();
            __result.Add(DescriptionOf(__0, expanded));
        }
    }

    [HarmonyPatch(typeof(GroundItemLabel), nameof(GroundItemLabel.SetLabelText))]
    private static class CoreGroundLabel
    {
        private static void Postfix(GroundItemLabel __instance)
        {
            if (!IsAnyCore(__instance.getItemData()) || __instance.itemText.IsNullOrDestroyed()) return;
            // Ground labels use their own cached/formatted text, independently of FullName.
            __instance.itemText.text = __instance.emphasized
                ? GroundItemLabel.EmphasisOpenTag + "Jewel" + GroundItemLabel.EmphasisCloseTag : "Jewel";
        }
    }

    [HarmonyPatch(typeof(ItemDataUnpacked), "get_FullName")]
    private static class CoreFullName
    {
        private static void Postfix(ItemDataUnpacked __instance, ref string __result)
        {
            if (IsAnyCore(__instance)) __result = NameOf(__instance);
        }
    }

    [HarmonyPatch(typeof(ItemDataUnpacked), "get_Implicits")]
    private static class CoreImplicitData
    {
        private static void Postfix(ItemDataUnpacked __instance,
            ref Il2CppSystem.Collections.Generic.List<ItemList.EquipmentImplicit> __result)
        {
            if (IsAnyCore(__instance)) __result = new Il2CppSystem.Collections.Generic.List<ItemList.EquipmentImplicit>();
        }
    }

    [HarmonyPatch(typeof(ItemList), nameof(ItemList.GetItemImplicits))]
    private static class CoreCatalogImplicits
    {
        private static bool Prefix(ItemData __0,
            ref Il2CppSystem.Collections.Generic.List<ItemList.EquipmentImplicit> __result)
        {
            if (!IsAnyCore(__0)) return true;
            __result = new Il2CppSystem.Collections.Generic.List<ItemList.EquipmentImplicit>();
            return false;
        }
    }

    [HarmonyPatch(typeof(ItemData), nameof(ItemData.AddImplicitValuesToStatList))]
    private static class CoreImplicitStats
    {
        private static bool Prefix(ItemData __instance) => !IsAnyCore(__instance);
    }

    /// <summary>A base chance scaled the way the game scales a rare boss drop (rarity, corruption and its chance cap).</summary>
    internal static double ScaledChance(double baseChance)
    {
        try
        {
            var endgame = BossLoot.GetActiveEndgameInfoForDrops();
            return BossChanceGroup(baseChance).GetDropChance(endgame.Item2, endgame.Item1, 0, endgame.Item3, endgame.Item4);
        }
        catch (Exception ex)
        {
            // Without a run there is no endgame state to scale by: use the base chance.
            Main.logger_instance?.Warning("Core drop scaling unavailable (" + ex.Message + "); using the base chance.");
            return baseChance;
        }
    }

    /// <summary>The current monolith corruption, for the Core’s legendary potential roll (0 when there is no run).</summary>
    internal static int CurrentCorruption()
    {
        try { return BossLoot.GetActiveEndgameInfoForDrops().Item1; }
        catch (Exception) { return 0; }
    }

    /// <summary>Heorot's Core Testing toggle (override and testing both on): fights with no monolith run also count.</summary>
    internal static bool TestingMode
    {
        get { VariantChances(out bool direct); return direct; }
    }

    private static bool IsShaman(string actorName) =>
        new[] { "Bhuldar", "Herkir", "Logi", "Shaman" }.Any(name => actorName.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0);

    // Copy the native Heorot rare-drop curve without changing its loot asset.
    private static BossLoot.LootGroup BossChanceGroup(double? baseChance = null)
    {
        var loot = Resources.FindObjectsOfTypeAll<BossLoot>().FirstOrDefault(asset => asset.name == "Heorot BossLoot");
        var template = loot?.lootGroups?.ToArray().FirstOrDefault(group => group.UseRarityScaling() && group.dropChance < 1);
        if (template == null) throw new InvalidOperationException("Native Heorot rarity-scaled boss loot is unavailable.");
        return new BossLoot.LootGroup
        {
            chanceModifier = template.chanceModifier,
            dropChance = (float)(baseChance ?? HeorotCoreRules.DropChance),
            maxDropChance = template.maxDropChance,
            chanceDisparityHalflifeFromIncreasedRarity = template.chanceDisparityHalflifeFromIncreasedRarity,
            chanceMultiplierPerCorruption = template.chanceMultiplierPerCorruption,
            corruptionOffsetForChanceMultiplier = template.corruptionOffsetForChanceMultiplier,
            multiplyChanceBySquareTierPercent = template.multiplyChanceBySquareTierPercent,
            chanceMultiplierFromCorruptionCappedAt1 = template.chanceMultiplierFromCorruptionCappedAt1,
        };
    }

    [HarmonyPatch(typeof(DeathItemDrop), nameof(DeathItemDrop.OnDeath))]
    private static class BossDrop
    {
        private static void Prefix(DeathItemDrop __instance, out DropContext __state)
        {
            __state = null;
            try
            {
                if (!registrar.IsRegistered && !VolcanicCore.IsRegistered) return;
                if (!__instance.TryGetActor(out Actor enemy) || enemy.IsNullOrDestroyed() || enemy.isPlayerActor) return;
                var player = Refs_Manager.player_data;
                var zone = Refs_Manager.monolith_zone_manager;
                var actor = Refs_Manager.player_actor;
                var runs = actor.IsNullOrDestroyed() ? null : actor.monolithRunsManager;
                var run = runs.IsNullOrDestroyed() ? null : runs.currentRun;
                string actorName = enemy.gameObject.name;
                bool heorotNamed = actorName.IndexOf("Heorot", StringComparison.OrdinalIgnoreCase) >= 0;
                if (zone.IsNullOrDestroyed()) return;
                bool runKnown = run != null && !run.timeline.IsNullOrDestroyed() && run.difficultyIndex >= 0
                    && run.difficultyIndex < run.timeline.difficulties.Count;
                int level;
                bool heorotTimeline, volcanoTimeline;
                if (runKnown)
                {
                    // A Core only drops from an EMPOWERED monolith boss at level 100 (the level is checked in the rules).
                    level = run.timeline.difficulties[run.difficultyIndex].level;
                    heorotTimeline = run.timelineID == TimelineID.Heorot && run.IsEmpowered;
                    volcanoTimeline = run.timelineID == TimelineID.Volcano && run.IsEmpowered;
                }
                else
                {
                    // No monolith run to read, so nothing proves this is an Empowered level-100 fight. Normal play drops nothing.
                    // Each Core's Testing toggle lets a boss fight with no run count, so repeat fights can be tested.
                    level = Core.Volcanic.VolcanicCoreRules.TimelineLevel;
                    heorotTimeline = TestingMode && actorName.IndexOf("Heorot Boss", StringComparison.OrdinalIgnoreCase) >= 0;
                    volcanoTimeline = VolcanicCore.TestingMode && IsShaman(actorName);
                    if (heorotNamed || IsShaman(actorName))
                        Main.logger_instance?.Msg("Heorot’s Core: " + actorName + " died with no monolith run to read, so it cannot be shown to be an Empowered level-100 fight"
                            + ((heorotTimeline || volcanoTimeline) ? "; Testing is on, so it counts." : ". Turn on the Core's Testing toggle to allow this for tests."));
                }
                bool offline = !player.IsNullOrDestroyed() && player.IsOffline;
                // Spirits of Fire: Volcanic Shaman’s Core, once the last of the three Shamans has fallen.
                var volcanic = VolcanicCore.EvaluateDeath(enemy, offline, zone.isTimelineBossEncounter,
                    volcanoTimeline, level, text => Main.logger_instance?.Msg(text));
                if (volcanic != null)
                {
                    __state = new DropContext { Volcanic = volcanic };
                    return;
                }
                if (droppedThisEncounter || !registrar.IsRegistered) return;
                bool named = heorotNamed;
                if (named)
                    Main.logger_instance?.Msg("Heorot’s Core: " + enemy.gameObject.name + " died. isBoss=" + enemy.isBoss() + ", offline=" + offline
                        + ", minion=" + enemy.IsMinionOfPlayer() + ", timelineBossEncounter=" + zone.isTimelineBossEncounter
                        + ", heorotTimeline=" + heorotTimeline + ", level=" + level
                        + ", alreadyDropped=" + droppedThisEncounter);
                if (!HeorotCoreRules.CanDrop(offline, enemy.isBoss(), enemy.IsMinionOfPlayer(),
                    zone.isTimelineBossEncounter, heorotTimeline, level, enemy.gameObject.name)) return;
                if (!rolledDeaths.Add(enemy.Pointer)) return;
                var weights = VariantChances(out bool direct);
                double baseChance = HeorotCoreRules.CombinedChance(weights);
                if (baseChance <= 0)
                {
                    Main.logger_instance?.Msg("Heorot’s Core: no drop, the combined chance is 0 (rates " + string.Join("/", weights) + ").");
                    return;
                }
                double chance = direct ? baseChance : ScaledChance(baseChance);
                bool dropped = random.NextDouble() < chance;
                Main.logger_instance?.Msg("Heorot’s Core: drop chance " + chance + " (" + (direct ? "direct" : "scaled from " + baseChance)
                    + ", rates " + string.Join("/", weights) + ") -> " + (dropped ? "drops" : "no drop"));
                if (dropped)
                    __state = new DropContext { Position = enemy.position(), Corruption = CurrentCorruption(), Level = level, VariantWeights = weights };
            }
            catch (Exception ex) { ErrorLog.Report(ex, "Core boss drop check"); }
        }

        private static void Postfix(DropContext __state)
        {
            if (__state == null) return;
            try
            {
                var ground = Refs_Manager.ground_item_manager;
                var player = Refs_Manager.player_actor;
                if (ground.IsNullOrDestroyed() || player.IsNullOrDestroyed()) return;
                if (__state.Volcanic != null)
                {
                    if (VolcanicCore.AlreadyDropped) return;
                    var core = VolcanicCore.Build(__state.Volcanic, text => Main.logger_instance?.Msg(text));
                    VolcanicCore.Claim();
                    ModItemDrop.Drop(ground, player, core, __state.Volcanic.Position);
                    return;
                }
                if (droppedThisEncounter) return;
                int count = HeorotCoreRules.WeightedTier(random.NextDouble(), __state.VariantWeights);
                int mask = HeorotCoreRules.MaskForVariant(count, random.Next(3));
                Main.logger_instance?.Msg("Heorot’s Core: creating a " + count + "-buff Core (buffs " + mask + ").");
                var item = Create(mask, 1);
                int lp = item.RollLegendaryPotential(UniqueList.getUnique(HeorotCoreRules.UniqueId),
                    1, __state.Level, __state.Corruption, 1, out _, 1);
                item.legendaryPotential = (byte)Math.Clamp(lp, 1, 4);
                item.RefreshIDAndValues();
                // Claim the encounter before entering the native drop path, including
                // re-entrant callbacks or a native failure after spawning the item.
                droppedThisEncounter = true;
                ModItemDrop.Drop(ground, player, item, __state.Position);
            }
            catch (Exception ex) { ErrorLog.Report(ex, "Core boss drop"); }
        }
    }
}
