using System.IO;
using System.Runtime.InteropServices;
using HarmonyLib;
using Il2Cpp;
using Newtonsoft.Json;
using UnityEngine;

namespace LastEpoch_Hud.Scripts.Mods.Items;

public class Items_Drop_ForginPotencial
{
    // The item id stores forging potential in 6 bits (0-63). Anything higher is saved here
    // and written back after the game reloads the id, including across a forge craft.
    const int StoredMax = 63;
    static readonly System.Collections.Generic.Dictionary<System.IntPtr, byte> byPointer =
        new System.Collections.Generic.Dictionary<System.IntPtr, byte>();
    static readonly System.Collections.Generic.Dictionary<uint, byte> byId =
        new System.Collections.Generic.Dictionary<uint, byte>();
    static bool loaded;

    public static bool CanRun()
    {
        if ((Hud_Manager.IsPauseOpen()) && (ModUI.ForceDrop.ForceDropModel.Enabled))
        {
            return false;
        }
        else if ((Scenes.IsGameScene()) && (!Save_Manager.instance.IsNullOrDestroyed()))
        {
            if (!Save_Manager.instance.data.IsNullOrDestroyed())
            {
                return Save_Manager.instance.data.Items.Drop.Enable_ForginPotencial;
            }
            else
            {
                return false;
            }
        }
        else
        {
            return false;
        }
    }

    public static void Keep(ItemData item, byte value)
    {
        if (item.IsNullOrDestroyed())
        {
            return;
        }
        if (value <= StoredMax)
        {
            Forget(item);
        }
        else
        {
            Remember(item, value);
        }
        item.forgingPotential = value;
    }

    public static void Stamp(ItemData item, byte value)
    {
        Keep(item, value);
        if (item.IsNullOrDestroyed())
        {
            return;
        }
        item.RebuildID();
        if (value > StoredMax)
        {
            item.forgingPotential = value;
        }
    }

    static byte Roll()
    {
        int min = Mathf.RoundToInt(Save_Manager.instance.data.Items.Drop.ForginPotencial_Min);
        int max = Mathf.RoundToInt(Save_Manager.instance.data.Items.Drop.ForginPotencial_Max);
        if (max < min)
        {
            int swap = min;
            min = max;
            max = swap;
        }
        if (min < 0)
        {
            min = 0;
        }
        if (max > 255)
        {
            max = 255;
        }
        if (min == max)
        {
            return (byte)max;
        }
        return (byte)Random.RandomRange(min, max + 1f);
    }

    static bool CanStamp(ItemDataUnpacked item)
    {
        if (item.IsNullOrDestroyed() || item.itemType >= 100)
        {
            return false;
        }
        return !item.isUniqueSetOrLegendary();
    }

    static string FilePath()
    {
        string folder =
            (
                !Save_Manager.instance.IsNullOrDestroyed()
                && !string.IsNullOrEmpty(Save_Manager.instance.path)
            )
                ? Save_Manager.instance.path
                : Directory.GetCurrentDirectory() + @"\Mods\" + Main.mod_name + @"\";
        return folder + "ForgingPotential.json";
    }

    static void Load()
    {
        if (loaded)
        {
            return;
        }
        loaded = true;
        try
        {
            string path = FilePath();
            if (!File.Exists(path))
            {
                return;
            }
            var saved = JsonConvert.DeserializeObject<System.Collections.Generic.Dictionary<
                uint,
                byte
            >>(File.ReadAllText(path));
            if (saved == null)
            {
                return;
            }
            foreach (var pair in saved)
            {
                if (pair.Key != 0 && pair.Value > StoredMax)
                {
                    byId[pair.Key] = pair.Value;
                }
            }
        }
        catch (System.Exception ex)
        {
            Main.logger_instance?.Warning("Forging potential load failed: " + ex.Message);
        }
    }

    static void Save()
    {
        try
        {
            string path = FilePath();
            string folder = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(folder) && !Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }
            File.WriteAllText(path, JsonConvert.SerializeObject(byId));
        }
        catch (System.Exception ex)
        {
            Main.logger_instance?.Warning("Forging potential save failed: " + ex.Message);
        }
    }

    static uint IdOf(ItemData item)
    {
        try
        {
            return item.individualID;
        }
        catch
        {
            return 0;
        }
    }

    static void Remember(ItemData item, byte value)
    {
        Load();
        if (item.Pointer != System.IntPtr.Zero)
        {
            byPointer[item.Pointer] = value;
        }
        uint id = IdOf(item);
        if (id == 0)
        {
            return;
        }
        if (byId.TryGetValue(id, out byte have) && have == value)
        {
            return;
        }
        byId[id] = value;
        Save();
    }

    static void Forget(ItemData item)
    {
        Load();
        if (!item.IsNullOrDestroyed() && item.Pointer != System.IntPtr.Zero)
        {
            byPointer.Remove(item.Pointer);
        }
        uint id = item.IsNullOrDestroyed() ? 0 : IdOf(item);
        if (id != 0 && byId.Remove(id))
        {
            Save();
        }
    }

    static bool TryGet(ItemData item, out byte value)
    {
        Load();
        value = 0;
        if (item.IsNullOrDestroyed())
        {
            return false;
        }
        uint id = IdOf(item);
        if (id != 0 && byId.TryGetValue(id, out value))
        {
            if (item.Pointer != System.IntPtr.Zero)
            {
                byPointer[item.Pointer] = value;
            }
            return true;
        }
        if (item.Pointer != System.IntPtr.Zero && byPointer.TryGetValue(item.Pointer, out value))
        {
            if (id != 0)
            {
                byId[id] = value;
                Save();
            }
            return true;
        }
        return false;
    }

    static void Restore(ItemData item)
    {
        if (!TryGet(item, out byte value))
        {
            return;
        }
        if (item.forgingPotential != value)
        {
            item.forgingPotential = value;
        }
    }

    // The forge rolls a cost and subtracts it, then SetForgingPotential clamps the result to 63.
    // Seeding the field with 63 makes that subtraction return the real cost. The original total is restored minus that cost.
    static void CostPrefix(ItemData item, out int state)
    {
        state = -1;
        if (item.IsNullOrDestroyed())
        {
            return;
        }
        state = item.forgingPotential;
        if (state > StoredMax && item.Pointer != System.IntPtr.Zero)
        {
            Marshal.WriteByte(item.Pointer, 0x18, (byte)StoredMax);
        }
    }

    static void CostPostfix(ItemData item, int state)
    {
        if (state <= StoredMax || item.IsNullOrDestroyed() || item.Pointer == System.IntPtr.Zero)
        {
            return;
        }
        int after = Marshal.ReadByte(item.Pointer, 0x18);
        if (after > StoredMax)
        {
            return;
        }
        int spent = StoredMax - after;
        int next = state - spent;
        if (next < 0)
        {
            next = 0;
        }
        if (next > 255)
        {
            next = 255;
        }
        Marshal.WriteByte(item.Pointer, 0x18, (byte)next);
        if (next > StoredMax)
        {
            Remember(item, (byte)next);
        }
        else
        {
            Forget(item);
        }
    }

    [HarmonyPatch(typeof(ItemData), nameof(ItemData.applyForgingPotentialCost))]
    public class ItemData_applyForgingPotentialCost
    {
        [HarmonyPrefix]
        static void Prefix(ItemData __instance, out int __state)
        {
            CostPrefix(__instance, out __state);
        }

        [HarmonyPostfix]
        static void Postfix(ItemData __instance, int __state)
        {
            CostPostfix(__instance, __state);
        }
    }

    [HarmonyPatch(typeof(ItemData), nameof(ItemData.applyForgingPotentialCostFromShard))]
    public class ItemData_applyForgingPotentialCostFromShard
    {
        [HarmonyPrefix]
        static void Prefix(ItemData __instance, out int __state)
        {
            CostPrefix(__instance, out __state);
        }

        [HarmonyPostfix]
        static void Postfix(ItemData __instance, int __state)
        {
            CostPostfix(__instance, __state);
        }
    }

    [HarmonyPatch(typeof(ItemData), nameof(ItemData.applyForgingPotentialCostForNonAffix))]
    public class ItemData_applyForgingPotentialCostForNonAffix
    {
        [HarmonyPrefix]
        static void Prefix(ItemData __instance, out int __state)
        {
            CostPrefix(__instance, out __state);
        }

        [HarmonyPostfix]
        static void Postfix(ItemData __instance, int __state)
        {
            CostPostfix(__instance, __state);
        }
    }

    [HarmonyPatch(typeof(ItemData), nameof(ItemData.SetForgingPotential))]
    public class ItemData_SetForgingPotential
    {
        // Force drop calls this with the slider value. The game then stores at most 63, which is what the tooltip shows.
        // A craft passes the already reduced value, and writing the old total back here freezes the tooltip.
        [HarmonyPostfix]
        static void Postfix(ItemData __instance, int __0)
        {
            if (__0 <= StoredMax || __0 > 255)
            {
                return;
            }
            if (__instance.IsNullOrDestroyed() || __instance.Pointer == System.IntPtr.Zero)
            {
                return;
            }
            byte tracked = 0;
            bool have = TryGet(__instance, out tracked);
            int keep = have ? tracked : __0;
            if (keep <= StoredMax || keep > 255)
            {
                return;
            }
            if (!have)
            {
                Remember(__instance, (byte)keep);
            }
            if (Marshal.ReadByte(__instance.Pointer, 0x18) == keep)
            {
                return;
            }
            Marshal.WriteByte(__instance.Pointer, 0x18, (byte)keep);
        }
    }

    [HarmonyPatch(typeof(GenerateItems), "rollForgingPotential")]
    public class GenerateItems_rollForgingPotential
    {
        [HarmonyPrefix]
        static bool Prefix(ref int __result, ref ItemDataUnpacked __0)
        {
            if (!CanRun() || __0.IsNullOrDestroyed())
            {
                return true;
            }
            byte roll = Roll();
            __result = roll;
            if (CanStamp(__0))
            {
                Stamp(__0, roll);
            }
            return false;
        }
    }

    [HarmonyPatch(typeof(ItemData), nameof(ItemData.RefreshIDAndValues))]
    public class ItemData_RefreshIDAndValues
    {
        [HarmonyPostfix]
        static void Postfix(ItemData __instance)
        {
            Restore(__instance);
        }
    }

    [HarmonyPatch(typeof(ItemData), nameof(ItemData.setValuesFromSerialisation))]
    public class ItemData_setValuesFromSerialisation
    {
        [HarmonyPostfix]
        static void Postfix(ItemData __instance)
        {
            Restore(__instance);
        }
    }

    [HarmonyPatch(typeof(GenerateItems), nameof(GenerateItems.DropItemAtPoint))]
    public class GenerateItems_DropItemAtPoint
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.Last)]
        static void Prefix(ItemDataUnpacked __0)
        {
            if (!CanRun() || !CanStamp(__0))
            {
                return;
            }
            if (!byPointer.TryGetValue(__0.Pointer, out byte value))
            {
                value = Roll();
            }
            Stamp(__0, value);
        }

        [HarmonyPostfix]
        [HarmonyPriority(Priority.Last)]
        static void Postfix(ItemDataUnpacked __0)
        {
            if (!CanRun() || !CanStamp(__0))
            {
                return;
            }
            if (
                !byPointer.TryGetValue(__0.Pointer, out byte value)
                && !byId.TryGetValue(IdOf(__0), out value)
            )
            {
                value = Roll();
            }
            Stamp(__0, value);
        }
    }
}
