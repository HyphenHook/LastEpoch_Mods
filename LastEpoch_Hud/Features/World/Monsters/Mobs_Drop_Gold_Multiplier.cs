using HarmonyLib;
using Il2Cpp;

namespace LastEpoch_Hud.Scripts.Mods.Mobs;

public class Mobs_Drop_Gold_Multiplier
{
    public static bool CanRun()
    {
        if ((Scenes.IsGameScene()) && (!Save_Manager.instance.IsNullOrDestroyed()))
        {
            if (!Save_Manager.instance.data.IsNullOrDestroyed())
            {
                return Save_Manager.instance.data.Character.Cheats.Enable_GoldDropMultiplier;
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

    [HarmonyPatch(typeof(DeathItemDrop), "Start")]
    public class DeathItemDrop_Start
    {
        [HarmonyPrefix]
        static void Prefix(ref DeathItemDrop __instance)
        {
            if (CanRun())
            {
                __instance.goldMultiplier = Save_Manager
                    .instance
                    .data
                    .Character
                    .Cheats
                    .GoldDropMultiplier;
            }
        }
    }

    [HarmonyPatch(
        typeof(ItemDrop),
        "DropItem",
        new System.Type[]
        {
            typeof(int),
            typeof(UnityEngine.Vector3),
            typeof(float),
            typeof(bool),
            typeof(float),
            typeof(ItemDrop.BaseDropRates),
            typeof(bool),
            typeof(float),
            typeof(float),
            typeof(float),
            typeof(ItemDrop.DropFlags),
            typeof(UnityEngine.SceneManagement.Scene),
            typeof(bool),
            typeof(bool),
            typeof(bool),
            typeof(ItemDrop.GoldDropType),
            typeof(CorruptionOutcome),
            typeof(float),
        }
    )]
    public class ItemDrop_DropItem
    {
        [HarmonyPrefix]
        static void Prefix(ref float __7, ItemDrop.BaseDropRates __5)
        {
            if (
                (CanRun())
                && ((__5 == ItemDrop.BaseDropRates.Enemy) || (__5 == ItemDrop.BaseDropRates.Chest))
            )
            {
                __7 = Save_Manager.instance.data.Character.Cheats.GoldDropMultiplier;
            }
        }
    }
}
