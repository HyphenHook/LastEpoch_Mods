using System;
using System.IO;
using System.Linq;
using HarmonyLib;
using Il2Cpp;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using LastEpoch_Hud.Scripts.Core.Crafting;
using LastEpoch_Hud.Scripts.Mods.Economy;

using LastEpoch_Hud.Scripts.Core.Tuning;
namespace LastEpoch_Hud.Scripts.Mods.Items.Crafting;

internal static class Items_Attunement
{
    private static readonly Random Random = new();
    private static readonly AttunementStore Store = new(
        Path.Combine(Directory.GetCurrentDirectory(), "Mods", Main.mod_name, "Attunements.json"));

    internal static AttunementState StateOf(ItemData item)
    {
        if (item.IsNullOrDestroyed() || item.individualID == 0) return null;
        try
        {
            return Store.Find(item.individualID) ?? BazaarCraftedItems.PreviewState(item)?.Attunement;
        }
        catch (Exception ex)
        {
            ErrorLog.Report(ex, "Attunement metadata");
            return null;
        }
    }

    public static string WhyNot(ItemData item)
    {
        string equipment = CraftProtection.EquipmentRestriction(item);
        if (equipment.Length > 0) return equipment;
        if (item.rarity == 8) return "Attunement cannot be used on Set items.";
        if (Items_Reminiscence.ObscuredOf(item) != null) return CraftProtection.PendingMessage;
        if (item.individualID == 0) return "This item has no persistent identity.";
        if (item.affixes == null || item.implicitRolls == null || item.uniqueRolls == null)
            return "The item's numeric rolls are unavailable.";
        if (item.affixes.ToArray().Any(a => a == null)) return "The item's affix data is unavailable.";
        try
        {
            var state = Store.Find(item.individualID) ?? BazaarCraftedItems.PreviewState(item)?.Attunement;
            if (state != null) return "This base item has already been Attuned.";
        }
        catch (Exception ex)
        {
            ErrorLog.Report(ex, "Attunement metadata");
            return "The item's Attunement data could not be loaded.";
        }
        bool affixes = item.affixes.ToArray().Any(a => a != null && !CraftProtection.IsAttached(item, a));
        var unpacked = item.getAsUnpacked();
        bool implicits = !unpacked.IsNullOrDestroyed() && unpacked.Implicits != null && unpacked.Implicits.Count > 0;
        var unique = item.isUniqueOrLegendary() ? UniqueList.getUnique(item.uniqueID) : null;
        bool uniqueMods = unique?.mods != null && unique.mods.Count > 0;
        return affixes || implicits || uniqueMods ? "" : "This item has no eligible numeric stats to attune.";
    }

    private static byte[] Read(Il2CppStructArray<byte> rolls) =>
        Enumerable.Range(0, rolls.Count).Select(i => rolls[i]).ToArray();

    private static void Write(Il2CppStructArray<byte> target, byte[] values)
    {
        for (int i = 0; i < values.Length; i++) target[i] = values[i];
    }

    public static bool UseOn(ItemData item, out double multiplier, out string message)
    {
        multiplier = 1;
        message = WhyNot(item);
        if (message.Length > 0) return false;

        bool wasCorrupted = item.corrupted;
        var oldAffixes = item.affixes.ToArray().Select(a => a.affixRoll).ToArray();
        var oldImplicits = Read(item.implicitRolls);
        var oldUnique = Read(item.uniqueRolls);
        var affixes = (byte[])oldAffixes.Clone();
        var implicits = (byte[])oldImplicits.Clone();
        var unique = (byte[])oldUnique.Clone();
        Random.NextBytes(implicits);
        // A Core (Heorot's or Volcanic Shaman's) keeps its buff roll: Attunement never changes it.
        if (!LastEpoch_Hud.Scripts.Mods.Items.Jewels.HeorotCore.IsAnyCore(item)) Random.NextBytes(unique);
        double factor = AttunementState.RollMultiplier(Random.Next(AttunementState.MaxRoll + 1));
        var next = new AttunementState
        {
            ItemType = item.itemType,
            SubType = item.subType,
            UniqueId = item.uniqueID,
            Multiplier = factor,
            Implicits = implicits,
            Unique = unique,
        };
        for (int i = 0; i < item.affixes.Count; i++)
        {
            ItemAffix affix = item.affixes[i];
            if (CraftProtection.IsAttached(item, affix))
            {
                continue;
            }
            affixes[i] = (byte)Random.Next(256);
            next.Affixes.Add(new AttunedAffix { Id = affix.affixId, Tier = affix.affixTier, Roll = affixes[i], Multiplier = factor });
        }
        if (!AdvancedMaterials.TryTake(AdvancedMaterials.AttunementOrb))
        {
            message = "You have no Orbs of Attunement.";
            return false;
        }
        bool committed = false;
        try
        {
            for (int i = 0; i < affixes.Length; i++) item.affixes[i].affixRoll = affixes[i];
            Write(item.implicitRolls, implicits);
            Write(item.uniqueRolls, unique);
            item.corrupted = true;
            item.RefreshIDAndValues();
            // Store the canonical serialized roll arrays, including the game's unused-slot normalization.
            var canonical = new ItemDataUnpacked(item.GetID());
            if (!canonical.corrupted || canonical.affixes.Count != item.affixes.Count
                || canonical.itemType != item.itemType || canonical.subType != item.subType
                || canonical.uniqueID != item.uniqueID || canonical.individualID != item.individualID)
                throw new InvalidDataException("The Attuned item did not preserve its corruption or affix layout.");
            for (int i = 0; i < item.affixes.Count; i++)
            {
                var expected = item.affixes[i];
                var restored = canonical.affixes.ToArray().FirstOrDefault(a => a.affixId == expected.affixId
                    && a.affixTier == expected.affixTier && a.affixRoll == expected.affixRoll
                    && a.IsSealed == expected.IsSealed && a.IsSealedPrimordial == expected.IsSealedPrimordial
                    && a.IsSealedCorrupted == expected.IsSealedCorrupted);
                if (restored == null)
                    throw new InvalidDataException("An affix changed during Attunement serialization.");
            }
            next.Implicits = Read(canonical.implicitRolls);
            next.Unique = Read(canonical.uniqueRolls);
            Write(item.implicitRolls, next.Implicits);
            Write(item.uniqueRolls, next.Unique);
            Store.Set(item.individualID, next);
            committed = true;
            item.RefreshIDAndValues();
            multiplier = factor;
            message = "";
            return true;
        }
        catch (Exception ex)
        {
            try
            {
                for (int i = 0; i < oldAffixes.Length; i++) item.affixes[i].affixRoll = oldAffixes[i];
                Write(item.implicitRolls, oldImplicits);
                Write(item.uniqueRolls, oldUnique);
                item.corrupted = wasCorrupted;
                if (committed) Store.Set(item.individualID, null);
                item.RefreshIDAndValues();
            }
            catch (Exception rollback)
            {
                ErrorLog.Report(rollback, "Restore item after failed Attunement");
            }
            finally
            {
                AdvancedMaterials.Add(AdvancedMaterials.AttunementOrb, 1);
            }
            ErrorLog.Report(ex, "Apply Orb of Attunement");
            message = "The Attunement could not be saved.";
            return false;
        }
    }

    internal static string TooltipCaption(ItemData item)
    {
        var state = StateOf(item);
        return state == null ? "" : "<color=#CCA4FF>Attuned · "
            + state.Multiplier.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + "×</color>";
    }

    [HarmonyPatch]
    private static class LegacyTooltipMetadata
    {
        private static System.Collections.Generic.IEnumerable<System.Reflection.MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(TooltipItemManager), nameof(TooltipItemManager.CreateTooltipDefaultContent));
            yield return AccessTools.Method(typeof(TooltipItemManager), nameof(TooltipItemManager.CreateTooltipFullContent));
        }

        [HarmonyPostfix]
        private static void Postfix(ItemDataUnpacked __0, UITooltipItem.ItemTooltipInfo __result)
        {
            if (__result == null) return;
            string caption = TooltipCaption(__0);
            if (caption.Length == 0 || (__result.loreText ?? "").Contains(caption)) return;
            __result.loreText = string.IsNullOrEmpty(__result.loreText) ? caption : __result.loreText + "\n" + caption;
        }
    }

    [HarmonyPatch(typeof(Il2CppLE.UI.Tooltips.ItemTooltipContentBuilder), nameof(Il2CppLE.UI.Tooltips.ItemTooltipContentBuilder.GetItemTooltipInfo))]
    private static class ModernTooltipMetadata
    {
        [HarmonyPostfix]
        private static void Postfix(ItemDataUnpacked __0, Il2CppLE.UI.Tooltips.ItemTooltipInfo __result)
        {
            if (__result == null) return;
            string caption = TooltipCaption(__0);
            if (caption.Length == 0) return;
            __result.Description = string.IsNullOrEmpty(__result.Description) ? caption : __result.Description + "\n\n" + caption;
        }
    }

    internal static bool Restore(ItemData item, AttunementState state)
    {
        if (state == null) return true;
        try
        {
            if (Store.Find(item.individualID) == null) Store.Set(item.individualID, state);
            return true;
        }
        catch (Exception ex)
        {
            ErrorLog.Report(ex, "Restore Attunement");
            return false;
        }
    }

    [HarmonyPatch(typeof(ItemData), nameof(ItemData.RollIndividualID))]
    private static class PreserveBaseMetadata
    {
        [HarmonyPrefix]
        private static void Prefix(ItemData __instance, out AttunementState __state) =>
            __state = StateOf(__instance) ?? StateOf(Items_Encapsulate.Duplicating);

        [HarmonyPostfix]
        private static void Postfix(ItemData __instance, AttunementState __state)
        {
            if (__state != null) Restore(__instance, __state);
        }
    }
}
