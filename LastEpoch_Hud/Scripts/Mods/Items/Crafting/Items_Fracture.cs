using System;
using System.IO;
using HarmonyLib;
using Il2Cpp;
using LastEpoch_Hud.Scripts.Core.Crafting;

namespace LastEpoch_Hud.Scripts.Mods.Items.Crafting;

/// <summary>
/// The fractured affixes (see <see cref="FractureRegistry" />): loading, saving, asking, and copying them when the
/// game duplicates an item with a Rune of Creation.
/// </summary>
internal static class Items_Fracture
{
    private static FractureRegistry _registry;
    private static string Folder => Path.Combine(Directory.GetCurrentDirectory(), "Mods", Main.mod_name);
    private static string SavePath => Path.Combine(Folder, "Fractures.json");

    public static FractureRegistry Registry
    {
        get
        {
            if (_registry == null)
            {
                try
                {
                    _registry = FractureRegistry.FromJson(File.Exists(SavePath) ? File.ReadAllText(SavePath) : "");
                }
                catch (Exception ex)
                {
                    Main.logger_instance?.Warning("Fractures: could not read the save: " + ex.Message);
                    _registry = new FractureRegistry();
                }
            }
            return _registry;
        }
    }

    public static void Save()
    {
        try
        {
            Directory.CreateDirectory(Folder);
            File.WriteAllText(SavePath, Registry.ToJson());
        }
        catch (Exception ex)
        {
            Main.logger_instance?.Warning("Fractures: could not save: " + ex.Message);
        }
    }

    /// <summary>The fractured affix id of the item, or -1. Forgets a fracture whose affix is no longer on the item.</summary>
    public static int FracturedAffix(ItemData item)
    {
        if (item.IsNullOrDestroyed())
        {
            return -1;
        }
        int id = Registry.AffixOf(item.individualID);
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

    public static bool IsFractured(ItemData item, ItemAffix affix) =>
        affix != null && FracturedAffix(item) == affix.affixId;

    // ------------------------------------------------------------------ copies

    internal static ItemData Duplicating;

    /// <summary>While a Rune of Creation duplicates an item, the copy gets its new id: the copy inherits the fracture.</summary>
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
                if (Registry.Copy(source.individualID, __instance.individualID))
                {
                    Save();
                    Main.logger_instance?.Msg("Fractures: the copy of an item kept its fractured affix.");
                }
            }
            catch (Exception ex)
            {
                Main.logger_instance?.Warning("Fractures: copy failed: " + ex.Message);
            }
        }
    }
}
