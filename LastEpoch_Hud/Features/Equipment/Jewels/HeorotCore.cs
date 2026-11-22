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
    private static readonly CustomUniqueRegistrar[] registrars = CreateRegistrars();
    private static readonly HashSet<IntPtr> rolledDeaths = new();
    private static readonly HashSet<uint> cacheRecoveryAttempts = new();
    private static Actor buffOwner;
    private static int ownedBuffs;
    private static bool announced;
    private static bool droppedThisEncounter;

    internal static bool IsCore(ItemData item) => !item.IsNullOrDestroyed()
        && item.itemType == JewelBaseType.Id && item.subType == 0 && item.isUniqueSetOrLegendary()
        && HeorotCoreRules.MaskFor(item.uniqueID) != 0;

    private static CustomUniqueRegistrar[] CreateRegistrars()
    {
        var result = new CustomUniqueRegistrar[HeorotCoreRules.CombinationCount];
        for (int mask = 1; mask <= result.Length; mask++)
        {
            int buffs = mask;
            ushort uniqueId = HeorotCoreRules.UniqueIdFor(mask);
            CustomItemLocalization.Table.Register(CustomItemKeys.UniqueName(uniqueId), _ => HeorotCoreRules.Name);
            CustomItemLocalization.Table.Register(CustomItemKeys.UniqueLore(uniqueId), _ => "Winter endures within the heart of the fallen god.");
            result[mask - 1] = new CustomUniqueRegistrar(new CustomUniqueDefinition
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
                Description = _ => Description(buffs),
            });
        }
        return result;
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
        if (JewelBaseType.Registered && HeorotBuffs.Registered)
            foreach (var registrar in registrars) registrar.Update();
        if (!announced && registrars.All(registrar => registrar.IsRegistered))
        {
            announced = true;
            Main.logger_instance?.Msg("Registered Heorot’s Core: seven persistent combinations in three buff-count variants; boss-only drops.");
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
        for (int slot = 1; slot < CharmEquipment.SlotCount; slot++)
        {
            var item = CharmEquipment.EquippedAt(slot);
            if (IsCore(item)) desired |= HeorotCoreRules.MaskFor(item.uniqueID);
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
        if (source == null || !IsCore(source.data) || source.data.rarity != 9
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
        if (source == null || !IsCore(source.data) || source.data.rarity != 9 || after.content != null) return false;
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
        if (mask < 1 || mask > registrars.Length || !registrars[mask - 1].IsRegistered)
            throw new InvalidOperationException("Heorot’s Core is not registered.");
        if (legendaryPotential < 1 || legendaryPotential > 4) throw new ArgumentOutOfRangeException(nameof(legendaryPotential));
        var item = new ItemDataUnpacked
        {
            itemType = JewelBaseType.Id, subType = 0, uniqueID = HeorotCoreRules.UniqueIdFor(mask),
            rarity = 7, legendaryPotential = (byte)legendaryPotential, LvlReq = 80,
            classReq = ItemList.ClassRequirement.Any,
            affixes = new Il2CppSystem.Collections.Generic.List<ItemAffix>(),
        };
        item.RollIndividualID();
        item.randomiseImplicitRolls();
        item.randomiseUniqueRolls();
        item.RefreshIDAndValues();
        var restored = new ItemDataUnpacked(item.GetID());
        if (!IsCore(restored) || restored.uniqueID != item.uniqueID || restored.legendaryPotential != legendaryPotential)
            throw new InvalidOperationException("Heorot’s Core did not survive native item serialization.");
        return item;
    }

    internal static string Inspect()
    {
        if (!registrars.All(registrar => registrar.IsRegistered)) return "Heorot's Core registration is pending or failed. Check the mod log.";
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
            lines.Add("mask=" + mask + " unique=" + item.uniqueID + " name=" + item.FullName
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
            if (!IsCore(__0)) return;
            bool expanded = __4 == TooltipMode.DefaultModRanges || __4 == TooltipMode.CompareModRanges
                || __4 == TooltipMode.Explanation;
            __result = new Il2CppSystem.Collections.Generic.List<string>();
            __result.Add(Description(HeorotCoreRules.MaskFor(__0.uniqueID), expanded));
        }
    }

    [HarmonyPatch(typeof(GroundItemLabel), nameof(GroundItemLabel.SetLabelText))]
    private static class CoreGroundLabel
    {
        private static void Postfix(GroundItemLabel __instance)
        {
            if (!IsCore(__instance.getItemData()) || __instance.itemText.IsNullOrDestroyed()) return;
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
            if (IsCore(__instance)) __result = HeorotCoreRules.Name;
        }
    }

    [HarmonyPatch(typeof(ItemDataUnpacked), "get_Implicits")]
    private static class CoreImplicitData
    {
        private static void Postfix(ItemDataUnpacked __instance,
            ref Il2CppSystem.Collections.Generic.List<ItemList.EquipmentImplicit> __result)
        {
            if (IsCore(__instance)) __result = new Il2CppSystem.Collections.Generic.List<ItemList.EquipmentImplicit>();
        }
    }

    [HarmonyPatch(typeof(ItemList), nameof(ItemList.GetItemImplicits))]
    private static class CoreCatalogImplicits
    {
        private static bool Prefix(ItemData __0,
            ref Il2CppSystem.Collections.Generic.List<ItemList.EquipmentImplicit> __result)
        {
            if (!IsCore(__0)) return true;
            __result = new Il2CppSystem.Collections.Generic.List<ItemList.EquipmentImplicit>();
            return false;
        }
    }

    [HarmonyPatch(typeof(ItemData), nameof(ItemData.AddImplicitValuesToStatList))]
    private static class CoreImplicitStats
    {
        private static bool Prefix(ItemData __instance) => !IsCore(__instance);
    }

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
                if (droppedThisEncounter || !registrars.All(registrar => registrar.IsRegistered)) return;
                if (!__instance.TryGetActor(out Actor enemy) || enemy.IsNullOrDestroyed() || enemy.isPlayerActor) return;
                var player = Refs_Manager.player_data;
                var zone = Refs_Manager.monolith_zone_manager;
                var actor = Refs_Manager.player_actor;
                var runs = actor.IsNullOrDestroyed() ? null : actor.monolithRunsManager;
                var run = runs.IsNullOrDestroyed() ? null : runs.currentRun;
                if (run == null || run.timeline.IsNullOrDestroyed() || run.difficultyIndex < 0
                    || run.difficultyIndex >= run.timeline.difficulties.Count || zone.IsNullOrDestroyed()) return;
                int level = run.timeline.difficulties[run.difficultyIndex].level;
                if (!HeorotCoreRules.CanDrop(!player.IsNullOrDestroyed() && player.IsOffline, enemy.isBoss(), enemy.IsMinionOfPlayer(),
                    zone.isTimelineBossEncounter, run.timelineID == TimelineID.Heorot, level, enemy.gameObject.name)) return;
                if (!rolledDeaths.Add(enemy.Pointer)) return;
                var weights = VariantChances(out bool direct);
                double baseChance = HeorotCoreRules.CombinedChance(weights);
                if (baseChance <= 0) return;
                var endgame = BossLoot.GetActiveEndgameInfoForDrops();
                double chance = direct ? baseChance : BossChanceGroup(baseChance).GetDropChance(endgame.Item2, endgame.Item1, 0, endgame.Item3, endgame.Item4);
                if (random.NextDouble() < chance)
                    __state = new DropContext { Position = enemy.position(), Corruption = endgame.Item1, Level = level, VariantWeights = weights };
            }
            catch (Exception ex) { ErrorLog.Report(ex, "Heorot’s Core boss drop check"); }
        }

        private static void Postfix(DropContext __state)
        {
            if (__state == null || droppedThisEncounter) return;
            try
            {
                var ground = Refs_Manager.ground_item_manager;
                var player = Refs_Manager.player_actor;
                if (ground.IsNullOrDestroyed() || player.IsNullOrDestroyed()) return;
                int count = HeorotCoreRules.WeightedTier(random.NextDouble(), __state.VariantWeights);
                int mask = HeorotCoreRules.MaskForVariant(count, random.Next(3));
                var item = Create(mask, 1);
                int lp = item.RollLegendaryPotential(UniqueList.getUnique(HeorotCoreRules.UniqueIdFor(mask)),
                    1, __state.Level, __state.Corruption, 1, out _, 1);
                item.legendaryPotential = (byte)Math.Clamp(lp, 1, 4);
                item.RefreshIDAndValues();
                // Claim the encounter before entering the native drop path, including
                // re-entrant callbacks or a native failure after spawning the item.
                droppedThisEncounter = true;
                ModItemDrop.Drop(ground, player, item, __state.Position);
            }
            catch (Exception ex) { ErrorLog.Report(ex, "Heorot’s Core boss drop"); }
        }
    }
}
