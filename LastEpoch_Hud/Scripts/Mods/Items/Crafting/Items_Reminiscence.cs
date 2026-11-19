using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarmonyLib;
using Il2Cpp;
using LastEpoch_Hud.Scripts.Core.Crafting;
using LastEpoch_Hud.Scripts.Core.ForceDrop;
using Newtonsoft.Json;
using LastEpoch_Hud.Scripts.ModUI.ForceDrop;
using LastEpoch_Hud.Scripts.Mods.Economy;

namespace LastEpoch_Hud.Scripts.Mods.Items.Crafting;

internal static class Items_Reminiscence
{
    private static readonly Random Rng = new();
    private static EncapsulateRegistry registry;
    private static string SavePath => Path.Combine(Directory.GetCurrentDirectory(), "Mods", Main.mod_name, "RemindedAffixes.json");

    private static Dictionary<uint, ObscuredMemory> pending;
    private static string PendingPath => Path.Combine(Path.GetDirectoryName(SavePath), "ObscuredMemories.json");

    private static Dictionary<uint, ObscuredMemory> Pending
    {
        get
        {
            if (pending == null)
            {
                pending = File.Exists(PendingPath)
                    ? JsonConvert.DeserializeObject<Dictionary<uint, ObscuredMemory>>(File.ReadAllText(PendingPath)) ?? new()
                    : new();
            }
            return pending;
        }
    }

    public static ObscuredMemory ObscuredOf(ItemData item) =>
        item.IsNullOrDestroyed() ? null : Pending.TryGetValue(item.individualID, out var memory)
            ? memory : BazaarCraftedItems.PreviewState(item)?.Memory;

    internal static bool RestoreMemory(ItemData item, ObscuredMemory memory)
    {
        Pending.TryGetValue(item.individualID, out var previous);
        Pending[item.individualID] = memory.Copy();
        try
        {
            SavePending();
            return true;
        }
        catch (Exception ex)
        {
            if (previous != null) Pending[item.individualID] = previous;
            else Pending.Remove(item.individualID);
            ErrorLog.Report(ex, "Bazaar memory delivery");
            return false;
        }
    }

    private static void SavePending()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(PendingPath));
        string temporary = PendingPath + ".tmp";
        File.WriteAllText(temporary, JsonConvert.SerializeObject(Pending));
        File.Move(temporary, PendingPath, true);
    }

    private static EncapsulateRegistry Registry
    {
        get
        {
            if (registry == null)
            {
                try
                {
                    registry = EncapsulateRegistry.FromJson(File.Exists(SavePath) ? File.ReadAllText(SavePath) : "");
                }
                catch (IOException ex)
                {
                    Main.logger_instance?.Warning("Reminded affixes: " + ex.Message);
                    registry = new EncapsulateRegistry();
                }
            }
            return registry;
        }
    }

    private static bool Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SavePath));
            string temporary = SavePath + ".tmp";
            File.WriteAllText(temporary, Registry.ToJson());
            File.Move(temporary, SavePath, true);
            return true;
        }
        catch (Exception ex)
        {
            Main.logger_instance?.Warning("Could not save Reminded affixes: " + ex.Message);
            return false;
        }
    }

    public static ItemAffix RemindedOf(ItemData item)
    {
        if (item.IsNullOrDestroyed())
        {
            return null;
        }
        int affixId = Registry.AffixOf(item.individualID);
        if (affixId < 0) affixId = BazaarCraftedItems.PreviewState(item)?.RemindedAffixId ?? -1;
        if (affixId < 0)
        {
            return null;
        }

        ItemAffix affix = item.affixes?.ToArray().FirstOrDefault(a => a != null && a.affixId == affixId);
        if (affix == null && Registry.Has(item.individualID))
        {
            Registry.Remove(item.individualID);
            Save();
        }
        return affix;
    }

    public static bool IsReminded(ItemData item, ItemAffix affix) =>
        affix != null && RemindedOf(item)?.affixId == affix.affixId;

    internal static bool SetRemindedAffix(ItemData item, int? affixId)
    {
        uint id = item.individualID;
        if (id == 0) return false;
        int previous = Registry.AffixOf(id);
        if (previous == (affixId ?? -1)) return true;
        if (affixId.HasValue) Registry.Set(id, affixId.Value);
        else Registry.Remove(id);
        if (Save()) return true;
        if (previous >= 0) Registry.Set(id, previous);
        else Registry.Remove(id);
        return false;
    }

    private static List<ItemAffix> Eligible(ItemData item) =>
        item.affixes?.ToArray().Where(a => a != null && !CraftProtection.IsAttached(item, a)).ToList() ?? new();

    public static string WhyNot(ItemData item)
    {
        string equipment = CraftProtection.EquipmentRestriction(item);
        if (equipment.Length != 0) return equipment;
        if (item.isUniqueSetOrLegendary() || item.corrupted) return "Requires a non-Unique, non-Set, non-Legendary, uncorrupted item.";
        if (ObscuredOf(item) != null) return "Unveil the Obscured Memory before using another orb.";
        if (item.individualID == 0) return "The item has no persistent identity.";
        if (RemindedOf(item) != null) return "Remove the Reminded affix before using another Reminiscence Orb.";
        return Eligible(item).Count == 0 ? "The item needs at least one unsealed, unencapsulated affix." : "";
    }

    internal static ObscuredMemory PlanMemory(ItemData item, Random random, out ItemAffix original)
    {
        original = null;
        AffixList catalog = AffixList.get();
        var existing = item.affixes.ToArray().Where(a => a != null).ToList();
        var ids = existing.Select(a => (int)a.affixId).ToHashSet();
        var pool = BazaarCustomItems.Pool(item.itemType, item.subType, false, false);
        foreach (ItemAffix candidate in Eligible(item).OrderBy(_ => random.Next()))
        {
            AffixList.Affix definition = catalog?.GetAffix(candidate.affixId);
            if (definition == null || (definition.type != AffixList.AffixType.PREFIX && definition.type != AffixList.AffixType.SUFFIX)) continue;
            var legal = new ForceDropLegalAffixes(item.itemType, item.subType, item.rarity,
                existing.Where(a => a.affixId != candidate.affixId).Select(a => (int)a.affixId));
            var replacements = pool.Where(a => a != null && a.type == definition.type
                && a.specialAffixType == AffixList.SpecialAffixType.Standard && !ids.Contains(a.affixId)
                && ForceDropCatalog.MaximumTier(a) > candidate.affixTier
                && legal.OrdinaryReason(a, false, false).Length == 0).ToList();
            if (replacements.Count == 0) continue;

            var memory = new ObscuredMemory
            {
                Tier = candidate.affixTier,
                AffixType = (int)definition.type,
                OriginalAffixId = candidate.affixId,
                Choices = RollChoices(replacements, random: random),
            };
            if (memory.Choices.Count == 0) continue;
            original = candidate;
            return memory;
        }
        return null;
    }

    public static bool UseOn(ItemData item, out string message)
    {
        message = WhyNot(item);
        if (message.Length != 0) return false;
        ObscuredMemory memory = PlanMemory(item, Rng, out ItemAffix original);
        if (memory == null)
        {
            message = "No legal replacement affix was found.";
            return false;
        }
        int index = item.affixes.IndexOf(original);
        if (index < 0 || !AdvancedMaterials.TryTake(AdvancedMaterials.ReminiscenceOrb))
        {
            message = "Unable to apply the Reminiscence Orb.";
            return false;
        }

        uint id = item.individualID;
        bool removed = false;
        Pending.Add(id, memory);
        try
        {
            item.affixes.RemoveAt(index);
            removed = true;
            item.RefreshIDAndValues();
            SavePending();
        }
        catch (Exception ex)
        {
            Pending.Remove(id);
            if (removed)
            {
                item.affixes.Insert(index, original);
                item.RefreshIDAndValues();
            }
            AdvancedMaterials.Add(AdvancedMaterials.ReminiscenceOrb, 1);
            message = "The Obscured Memory could not be created.";
            Main.logger_instance?.Warning("Reminiscence Orb: " + ex.Message);
            return false;
        }
        message = "Obscured Memory created. Unveil it to choose a replacement.";
        return true;
    }
    private static List<MemoryChoice> RollChoices(List<AffixList.Affix> pool, List<MemoryChoice> previous = null, Random random = null)
    {
        random ??= Rng;
        var results = new List<MemoryChoice>();
        var fresh = pool.Where(a => previous == null || previous.All(old => old.AffixId != a.affixId)).ToList();
        while (results.Count < 4 && pool.Count > 0)
        {
            AffixList.Affix chosen = BazaarCustomItems.WeightedPick(fresh.Count > 0 ? fresh : pool, random);
            if (chosen == null) break;

            var old = previous?.Find(choice => choice.AffixId == chosen.affixId);
            int roll = random.Next(old == null ? 256 : 255);
            if (old != null && roll >= old.Roll) roll++;
            results.Add(new MemoryChoice { AffixId = (ushort)chosen.affixId, Roll = (byte)roll });
            pool.RemoveAll(a => a.affixId == chosen.affixId);
            fresh.RemoveAll(a => a.affixId == chosen.affixId);
        }
        return results;
    }

    public static string ReverieRestriction(ItemData item)
    {
        string equipment = CraftProtection.EquipmentRestriction(item);
        if (equipment.Length != 0) return equipment;
        ObscuredMemory memory = ObscuredOf(item);
        if (memory == null) return "Reverie requires an Obscured Memory.";
        if (item.corrupted || item.isUniqueSetOrLegendary()) return "This item can no longer hold a memory.";
        return "";
    }

    public static bool RevealMemory(ItemData item, out string message)
    {
        message = CraftProtection.EquipmentRestriction(item);
        if (message.Length != 0) return false;
        ObscuredMemory memory = ObscuredOf(item);
        message = "";
        if (memory == null)
        {
            message = "There is no memory to unveil.";
            return false;
        }
        if (memory.Revealed) return true;

        memory.Revealed = true;
        try
        {
            SavePending();
            return true;
        }
        catch (Exception ex)
        {
            memory.Revealed = false;
            Main.logger_instance?.Warning("Unveil Memory: " + ex.Message);
            message = "The unveiled memory could not be saved.";
            return false;
        }
    }

    public static bool RerollMemory(ItemData item, out string message)
    {
        message = ReverieRestriction(item);
        if (message.Length != 0) return false;
        ObscuredMemory memory = ObscuredOf(item);
        var existing = item.affixes.ToArray().Where(a => a != null).Select(a => (int)a.affixId).ToHashSet();
        var legal = new ForceDropLegalAffixes(item.itemType, item.subType, item.rarity, existing);
        var pool = BazaarCustomItems.Pool(item.itemType, item.subType, false, false)
            .Where(a => a != null && (int)a.type == memory.AffixType
                && a.specialAffixType == AffixList.SpecialAffixType.Standard
                && a.affixId != memory.OriginalAffixId && !existing.Contains(a.affixId)
                && ForceDropCatalog.MaximumTier(a) > memory.Tier && legal.OrdinaryReason(a, false, false).Length == 0)
            .ToList();
        List<MemoryChoice> choices = RollChoices(pool, memory.Choices);
        if (choices.Count == 0)
        {
            message = "No legal replacement memories were found.";
            return false;
        }
        if (!AdvancedMaterials.TryTake(AdvancedMaterials.ReverieOrb))
        {
            message = "You have no Reverie Orbs.";
            return false;
        }

        List<MemoryChoice> previous = memory.Choices;
        memory.Choices = choices;
        try
        {
            SavePending();
        }
        catch (Exception ex)
        {
            memory.Choices = previous;
            AdvancedMaterials.Add(AdvancedMaterials.ReverieOrb, 1);
            Main.logger_instance?.Warning("Reverie Orb: " + ex.Message);
            message = "The rerolled memories could not be saved.";
            return false;
        }
        message = memory.Revealed ? "All revealed memory options rerolled." : "All memory options rerolled. The memory is still obscured.";
        return true;
    }

    public static bool MatchesMemorySide(ObscuredMemory memory, MemoryChoice choice)
    {
        AffixList.Affix definition = AffixList.get()?.GetAffix(choice.AffixId);
        return definition != null && (int)definition.type == memory.AffixType;
    }

    public static ItemAffix Preview(ItemData item, ObscuredMemory memory, MemoryChoice choice) =>
        new(choice.AffixId, memory.Tier, choice.Roll, item.itemType, SealedAffixType.None);

    public static bool Choose(ItemData item, ObscuredMemory memory, MemoryChoice choice, out string message)
    {
        message = CraftProtection.EquipmentRestriction(item);
        if (message.Length != 0) return false;
        if (ObscuredOf(item) != memory || memory == null || !memory.Revealed || !memory.Choices.Contains(choice))
        {
            message = "This memory is no longer available.";
            return false;
        }

        var existing = item.affixes.ToArray().Where(a => a != null).ToList();
        AffixList.Affix definition = AffixList.get()?.GetAffix(choice.AffixId);
        var legal = new ForceDropLegalAffixes(item.itemType, item.subType, item.rarity, existing.Select(a => (int)a.affixId));
        if (item.corrupted || item.isUniqueSetOrLegendary() || definition == null
            || (int)definition.type != memory.AffixType || existing.Any(a => a.affixId == choice.AffixId)
            || existing.Count(a => !a.IsSealed) >= RecombinatorMath.MaxAffixes
            || existing.Count(a => !a.IsSealed && (int)a.affixType == memory.AffixType) >= RecombinatorMath.MaxPerSide
            || legal.OrdinaryReason(definition, false, false).Length > 0)
        {
            message = "That memory cannot fit the item's current affixes.";
            return false;
        }

        uint id = item.individualID;
        ItemAffix replacement = Preview(item, memory, choice);
        try
        {
            item.affixes.Add(replacement);
            item.RefreshIDAndValues();
            Pending.Remove(id);
            SavePending();
        }
        catch (Exception ex)
        {
            Pending[id] = memory;
            item.affixes.Remove(replacement);
            item.RefreshIDAndValues();
            message = "The memory could not be applied.";
            Main.logger_instance?.Warning("Unveil Memory: " + ex.Message);
            return false;
        }

        SetRemindedAffix(item, replacement.affixId);
        message = "Memory kept. The new affix is Reminded.";
        return true;
    }

    [HarmonyPatch(typeof(ItemData), nameof(ItemData.RefreshIDAndValues))]
    private static class RefreshPatch
    {
        [HarmonyPostfix]
        private static void Postfix(ItemData __instance)
        {
            if (!__instance.IsNullOrDestroyed() && Registry.Has(__instance.individualID))
            {
                RemindedOf(__instance);
            }
        }
    }

    internal static void CopyMarker(ItemData source, ItemData copy)
    {
        ObscuredMemory memory = ObscuredOf(source);
        if (memory != null && copy.individualID != 0)
        {
            Pending[copy.individualID] = memory.Copy();
            SavePending();
        }
        if (RemindedOf(source) != null && Registry.Copy(source.individualID, copy.individualID))
        {
            Save();
        }
    }
}
