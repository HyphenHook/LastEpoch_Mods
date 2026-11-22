using HarmonyLib;
using Il2Cpp;

namespace LastEpoch_Hud.Scripts.Mods.Mobs;

public class Mobs_Density
{
    public static bool CanRun()
    {
        if ((Scenes.IsGameScene()) && (!Save_Manager.instance.IsNullOrDestroyed()))
        {
            if (!Save_Manager.instance.data.IsNullOrDestroyed())
            {
                return Save_Manager.instance.data.Character.Cheats.Enable_DensityMultiplier;
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

    public static void Apply(SpawnerPlacementManager manager)
    {
        if ((!CanRun()) || (manager.IsNullOrDestroyed()))
        {
            return;
        }
        manager.defaultSpawnerDensity = Save_Manager
            .instance
            .data
            .Character
            .Cheats
            .DensityMultiplier;
        manager.alwaysRollSpawnerDensity = false;
    }

    [HarmonyPatch(typeof(SpawnerPlacementManager), "RollSpawners", new System.Type[] { })]
    public class RollSpawners_NoArgs
    {
        [HarmonyPrefix]
        static void Prefix(SpawnerPlacementManager __instance)
        {
            Apply(__instance);
        }
    }

    [HarmonyPatch(
        typeof(SpawnerPlacementManager),
        "RollSpawners",
        new System.Type[] { typeof(SpawnerPlacementRoom.SpawnerRuntimeConfig) }
    )]
    public class RollSpawners_Config
    {
        [HarmonyPrefix]
        static void Prefix(SpawnerPlacementManager __instance)
        {
            Apply(__instance);
        }
    }

    [HarmonyPatch(typeof(SpawnerPlacementManager), "rollSpawnersNPerFrame")]
    public class RollSpawners_PerFrame
    {
        [HarmonyPrefix]
        static void Prefix(SpawnerPlacementManager __instance)
        {
            Apply(__instance);
        }
    }
}
