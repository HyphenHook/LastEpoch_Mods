using System;
using System.Collections.Generic;
using System.Linq;
using Il2Cpp;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using LastEpoch_Hud.Scripts.Core.CustomItems;
using LastEpoch_Hud.Scripts.Core.CustomItems.Affixes;
using LastEpoch_Hud.Scripts.Core.Heorot;
using LastEpoch_Hud.Scripts.Core.Volcanic;
using LastEpoch_Hud.Scripts.Mods.Character.Volcanic;
using LastEpoch_Hud.Scripts.Mods.Items.CustomItems;
using Random = System.Random;

namespace LastEpoch_Hud.Scripts.Mods.Items.Jewels;

/// <summary>
/// Volcanic Shaman’s Core: a Jewel unique that grants Bhuldar’s Wrath, Logi’s Hunger and Herkir’s Vessel in any combination.
/// It follows Heorot’s Core: the combination is the item’s one unique roll, set when it drops and never changed afterwards.
/// The patches that name, describe and protect Core items live in <c>HeorotCore</c> and cover both Cores.
/// </summary>
internal static class VolcanicCore
{
    private static readonly Random random = new();
    private static readonly CustomUniqueRegistrar registrar = CreateRegistrar();
    private static readonly HashSet<IntPtr> shamansDown = new();
    private static readonly HashSet<IntPtr> rolledDeaths = new();
    private static Actor buffOwner;
    private static int ownedBuffs;
    private static bool announced;
    private static bool droppedThisEncounter;

    internal static bool IsRegistered => registrar.IsRegistered;

    internal static bool IsCore(ItemData item) => !item.IsNullOrDestroyed()
        && item.itemType == JewelBaseType.Id && item.subType == 0 && item.isUniqueSetOrLegendary()
        && VolcanicCoreRules.IsCoreUnique(item.uniqueID);

    /// <summary>The buffs this Core gives (1 Wrath, 2 Hunger, 4 Vessel), from its stored roll.</summary>
    internal static int MaskOf(ItemData item)
    {
        if (!IsCore(item) || item.uniqueRolls == null || item.uniqueRolls.Length == 0) return 0;
        return VolcanicCoreRules.MaskForRoll(item.uniqueRolls[0]);
    }

    private static CustomUniqueRegistrar CreateRegistrar()
    {
        ushort uniqueId = VolcanicCoreRules.UniqueId;
        CustomItemLocalization.Table.Register(CustomItemKeys.UniqueName(uniqueId), _ => VolcanicCoreRules.Name);
        CustomItemLocalization.Table.Register(CustomItemKeys.UniqueLore(uniqueId),
            _ => "Fire still burns within the three who split the world.");
        return new CustomUniqueRegistrar(new CustomUniqueDefinition
        {
            Spec = new CustomUniqueSpec
            {
                Name = VolcanicCoreRules.Name, UniqueId = uniqueId, BaseType = JewelBaseType.Id,
                BaseId = 0, AddsBase = false, LevelRequirement = 80, OverrideLevelRequirement = true,
                EffectiveLevelForLegendaryPotential = 80,
            },
            Flags = () => new CustomUniqueFlags(false, false, true),
            Mods = Array.Empty<CustomUniqueMod>(),
            TooltipEntries = () => new byte[] { CustomUniqueAffixes.DescriptionEntry },
            Description = _ => "Grants Bhuldar’s Anger, Logi’s Hunger and Herkir’s Vessel",
        });
    }

    internal static string Description(int mask, bool expanded)
    {
        var lines = new List<string>();
        var names = new[] { "Bhuldar’s Anger", "Logi’s Hunger", "Herkir’s Vessel" };
        for (int i = 0; i < 3; i++)
        {
            if ((mask & (1 << i)) == 0) continue;
            lines.Add("Grants " + names[i]);
            if (expanded) lines.Add("<color=#99B0FF>" + VolcanicBuffs.DescriptionFor((VolcanicBuffKind)i) + "</color>");
        }
        return string.Join("\n", lines);
    }

    internal static void EnsureRegistered()
    {
        if (JewelBaseType.Registered && VolcanicBuffs.Registered) registrar.Update();
        if (!announced && registrar.IsRegistered)
        {
            announced = true;
            Main.logger_instance?.Msg("Registered Volcanic Shaman’s Core: one unique whose buff combination is a protected roll on the item; drops when the three Immortal Shamans fall.");
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
        int desired = 0;
        for (int slot = 0; slot < JewelEquipment.SlotCount; slot++) desired |= MaskOf(JewelEquipment.EquippedAt(slot));
        for (int i = 0; i < 3; i++)
        {
            int bit = 1 << i;
            var kind = (VolcanicBuffKind)i;
            if ((desired & bit) != 0)
            {
                if (VolcanicBuffs.IsActive(actor, kind)) continue;
                VolcanicBuffs.Apply(actor, kind, float.MaxValue);
                ownedBuffs |= bit;
            }
            else if ((ownedBuffs & bit) != 0)
            {
                VolcanicBuffs.Remove(actor, kind);
                ownedBuffs &= ~bit;
            }
        }
    }

    private static void RemoveOwnedBuffs()
    {
        try
        {
            if (!buffOwner.IsNullOrDestroyed())
                for (int i = 0; i < 3; i++)
                    if ((ownedBuffs & (1 << i)) != 0) VolcanicBuffs.Remove(buffOwner, (VolcanicBuffKind)i);
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
            shamansDown.Clear();
            droppedThisEncounter = false;
        }
    }

    internal static ItemDataUnpacked Create(int mask, int legendaryPotential)
    {
        if (mask < 1 || mask > HeorotCoreRules.CombinationCount || !registrar.IsRegistered)
            throw new InvalidOperationException("Volcanic Shaman’s Core is not registered.");
        if (legendaryPotential < 1 || legendaryPotential > 4) throw new ArgumentOutOfRangeException(nameof(legendaryPotential));
        var item = new ItemDataUnpacked
        {
            itemType = JewelBaseType.Id, subType = 0, uniqueID = VolcanicCoreRules.UniqueId,
            rarity = 7, legendaryPotential = (byte)legendaryPotential, LvlReq = 80,
            classReq = ItemList.ClassRequirement.Any,
            affixes = new Il2CppSystem.Collections.Generic.List<ItemAffix>(),
        };
        item.RollIndividualID();
        item.randomiseImplicitRolls();
        item.randomiseUniqueRolls();
        // The Core has no mods of its own, so its one unique roll is free to say which buffs it gives.
        byte roll = VolcanicCoreRules.RollForMask(mask, random.Next(HeorotCoreRules.RollsPerCombination));
        if (item.uniqueRolls != null && item.uniqueRolls.Length > 0) item.uniqueRolls[0] = roll;
        else item.uniqueRolls = new Il2CppStructArray<byte>(new[] { roll });
        item.RefreshIDAndValues();
        var restored = new ItemDataUnpacked(item.GetID());
        if (!IsCore(restored) || restored.uniqueID != item.uniqueID || restored.legendaryPotential != legendaryPotential
            || MaskOf(restored) != mask)
            throw new InvalidOperationException("Volcanic Shaman’s Core did not survive native item serialization.");
        return item;
    }

    internal static string Inspect()
    {
        if (!registrar.IsRegistered) return "Volcanic Shaman's Core registration is pending or failed. Check the mod log.";
        var lines = new List<string>();
        for (int mask = 1; mask <= HeorotCoreRules.CombinationCount; mask++)
        {
            for (int lp = 1; lp <= 4; lp++) Create(mask, lp);
            var item = Create(mask, 1);
            if (item.FullName != VolcanicCoreRules.Name || item.Implicits.Count != 0)
                throw new InvalidOperationException("Core name or implicit verification failed.");
            var tooltip = TooltipItemManager.GetUniqueDescription(item, -1, -1, item.uniqueID,
                TooltipMode.DefaultInfo, TooltipItemManager.SlotType.CRAFTING);
            lines.Add("mask=" + mask + " read back=" + MaskOf(new ItemDataUnpacked(item.GetID())) + " roll=" + item.uniqueRolls[0]
                + " unique=" + item.uniqueID + " name=" + item.FullName + "; tooltip=" + string.Join(" | ", tooltip.ToArray()));
        }
        return string.Join("\n", lines);
    }

    /// <summary>Volcanic Core's Testing toggle (override and testing both on): fights with no monolith run also count.</summary>
    internal static bool TestingMode
    {
        get { VariantChances(out bool direct); return direct; }
    }

    /// <summary>The weights (single, pair, triple) of this Core’s drops, and whether they are exact chances instead of scaled ones.</summary>
    private static double[] VariantChances(out bool direct)
    {
        direct = false;
        if (Save_Manager.instance.IsNullOrDestroyed() || !Save_Manager.instance.initialized) return DefaultVariantChances();
        var settings = Save_Manager.instance.data.Items.Drop;
        if (!settings.VolcanicCoreOverride || !settings.VolcanicCoreRatesInitialized) return DefaultVariantChances();
        direct = settings.VolcanicCoreIgnoreScaling;
        return HeorotCoreRules.ValidateVariantChances(settings.VolcanicCoreSingleChance,
            settings.VolcanicCoreDoubleChance, settings.VolcanicCoreTripleChance);
    }

    internal static double[] DefaultVariantChances()
    {
        var weights = VolcanicCoreRules.VariantWeights;
        double total = weights.Sum();
        return weights.Select(weight => total > 0 ? weight / total * VolcanicCoreRules.DropChance * 100 : 0).ToArray();
    }

    /// <summary>
    /// Called for every enemy death. When the last of the three Shamans falls in a level-100 Spirits of Fire fight,
    /// rolls the Core and returns what to drop (or null).
    /// </summary>
    internal static DropPlan EvaluateDeath(Actor enemy, bool offline, bool timelineBossEncounter, bool volcanoTimeline, int level,
        Action<string> log)
    {
        if (droppedThisEncounter || !registrar.IsRegistered || !volcanoTimeline) return null;
        if (!enemy.isBoss() || enemy.IsMinionOfPlayer() || !timelineBossEncounter) return null;
        if (!rolledDeaths.Add(enemy.Pointer)) return null;
        shamansDown.Add(enemy.Pointer);
        log("Volcanic Shaman’s Core: " + enemy.gameObject.name + " died (" + shamansDown.Count + " of " + VolcanicCoreRules.BossCount
            + "); offline=" + offline + ", level=" + level);
        if (!VolcanicCoreRules.CanDrop(offline, true, false, true, true, level, shamansDown.Count)) return null;
        var weights = VariantChances(out bool direct);
        double baseChance = HeorotCoreRules.CombinedChance(weights);
        if (baseChance <= 0)
        {
            log("Volcanic Shaman’s Core: no drop, the combined chance is 0 (rates " + string.Join("/", weights) + ").");
            return null;
        }
        double chance = direct ? baseChance
            : HeorotCore.ScaledChance(baseChance);
        bool dropped = random.NextDouble() < chance;
        log("Volcanic Shaman’s Core: drop chance " + chance + " (" + (direct ? "direct" : "scaled from " + baseChance)
            + ", rates " + string.Join("/", weights) + ") -> " + (dropped ? "drops" : "no drop"));
        return dropped ? new DropPlan { Position = enemy.position(), Corruption = HeorotCore.CurrentCorruption(), Level = level, VariantWeights = weights } : null;
    }

    internal sealed class DropPlan
    {
        internal UnityEngine.Vector3 Position;
        internal int Corruption;
        internal int Level;
        internal double[] VariantWeights;
    }

    /// <summary>Creates the Core planned by <see cref="EvaluateDeath" />. Call <see cref="Claim" /> just before dropping it.</summary>
    internal static ItemDataUnpacked Build(DropPlan plan, Action<string> log)
    {
        int count = HeorotCoreRules.WeightedTier(random.NextDouble(), plan.VariantWeights);
        int mask = HeorotCoreRules.MaskForVariant(count, random.Next(3));
        log("Volcanic Shaman’s Core: creating a " + count + "-buff Core (buffs " + mask + ").");
        var item = Create(mask, 1);
        int lp = item.RollLegendaryPotential(UniqueList.getUnique(VolcanicCoreRules.UniqueId),
            1, plan.Level, plan.Corruption, 1, out _, 1);
        item.legendaryPotential = (byte)Math.Clamp(lp, 1, 4);
        item.RefreshIDAndValues();
        return item;
    }

    internal static bool AlreadyDropped => droppedThisEncounter;

    /// <summary>Claims the encounter before entering the native drop path, including re-entrant callbacks.</summary>
    internal static void Claim() => droppedThisEncounter = true;
}
