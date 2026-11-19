using System;
using System.IO;
using HarmonyLib;
using Il2Cpp;
using LastEpoch_Hud.Scripts.Core.Crafting;
using LastEpoch_Hud.Scripts.Mods.Economy;

namespace LastEpoch_Hud.Scripts.Mods.Items.Crafting;

/// <summary>
/// The encapsulated affixes (see <see cref="EncapsulateRegistry" />): loading, saving, asking, and copying them when the
/// game duplicates an item with a Rune of Creation.
/// </summary>
internal static class Items_Encapsulate
{
    private static EncapsulateRegistry _registry;
    private static string Folder => Path.Combine(Directory.GetCurrentDirectory(), "Mods", Main.mod_name);
    private static string SavePath => Path.Combine(Folder, "Encapsulate.json");
    private static string LegacySavePath => Path.Combine(Folder, "Fractures.json");

    public static EncapsulateRegistry Registry
    {
        get
        {
            if (_registry == null)
            {
                try
                {
                    bool migrate = !File.Exists(SavePath) && File.Exists(LegacySavePath);
                    _registry = EncapsulateRegistry.Load(SavePath, LegacySavePath);
                    if (migrate) Save();
                }
                catch (Exception ex)
                {
                    Main.logger_instance?.Warning("Encapsulation: could not read the save: " + ex.Message);
                    _registry = new EncapsulateRegistry();
                }
            }
            return _registry;
        }
    }

    public static bool Save()
    {
        try
        {
            Directory.CreateDirectory(Folder);
            string temporary = SavePath + ".tmp";
            File.WriteAllText(temporary, Registry.ToJson());
            File.Move(temporary, SavePath, true);
            return true;
        }
        catch (Exception ex)
        {
            Main.logger_instance?.Warning("Encapsulation: could not save: " + ex.Message);
            return false;
        }
    }

    internal static bool RestoreAffix(ItemData item, int affixId)
    {
        if (Registry.AffixOf(item.individualID) == affixId) return true;
        int previous = Registry.AffixOf(item.individualID);
        Registry.Set(item.individualID, affixId);
        if (Save()) return true;
        if (previous >= 0) Registry.Set(item.individualID, previous);
        else Registry.Remove(item.individualID);
        return false;
    }

    /// <summary>The encapsulated affix id of the item, or -1.</summary>
    public static int EncapsulatedAffix(ItemData item)
    {
        if (item.IsNullOrDestroyed())
        {
            return -1;
        }
        int id = Registry.AffixOf(item.individualID);
        if (id < 0) id = BazaarCraftedItems.PreviewState(item)?.EncapsulatedAffixId ?? -1;
        if (id < 0)
        {
            return -1;
        }
        foreach (ItemAffix a in item.affixes.ToArray())
        {
            if (a != null && a.affixId == id)
            {
                return id;
            }
        }
        return -1;
    }

    /// <summary>The item's encapsulated affix, or null.</summary>
    public static ItemAffix EncapsulatedOf(ItemData item)
    {
        int id = EncapsulatedAffix(item);
        if (id < 0)
        {
            return null;
        }
        foreach (ItemAffix a in item.affixes.ToArray())
        {
            if (a != null && a.affixId == id)
            {
                return a;
            }
        }
        return null;
    }

    public static bool IsEncapsulated(ItemData item, ItemAffix affix) =>
        affix != null && EncapsulatedAffix(item) == affix.affixId;

    // ------------------------------------------------------------------ copies

    internal static ItemData Duplicating;

    /// <summary>While a Rune of Creation duplicates an item, the copy gets its new id: the copy inherits the encapsulation.</summary>
    [HarmonyPatch(typeof(CraftingManager), nameof(CraftingManager.DuplicateItem))]
    private static class DuplicateItemPatch
    {
        [HarmonyPrefix]
        private static void Prefix(ItemData itemData) => Duplicating = itemData;

        [HarmonyFinalizer]
        private static void Finalizer() => Duplicating = null;
    }

    [HarmonyPatch(typeof(ItemData), nameof(ItemData.RollIndividualID))]
    private static class RollIdPatch
    {
        [HarmonyPostfix]
        private static void Postfix(ItemData __instance)
        {
            try
            {
                ItemData source = Duplicating;
                if (source.IsNullOrDestroyed() || __instance.Pointer == source.Pointer)
                {
                    return;
                }
                Items_Reminiscence.CopyMarker(source, __instance);
                if (Registry.Copy(source.individualID, __instance.individualID))
                {
                    Save();
                    Main.logger_instance?.Msg("Encapsulation: the copy of an item kept its encapsulated affix.");
                }
            }
            catch (Exception ex)
            {
                Main.logger_instance?.Warning("Encapsulation: copy failed: " + ex.Message);
            }
        }
    }
}
