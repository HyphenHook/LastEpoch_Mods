using HarmonyLib;
using Il2CppLE.UI.Minimap;
using UnityEngine;

namespace LastEpoch_Hud.Scripts.Mods.Minimap;

internal class Minimap_FogOfWar
{
    private const float FallbackRevealRadius = 10000f;

    public static bool CanRun()
    {
        if ((Scenes.IsGameScene()) && (!Save_Manager.instance.IsNullOrDestroyed()))
        {
            if (!Save_Manager.instance.data.IsNullOrDestroyed())
            {
                return Save_Manager.instance.data.Scenes.Minimap.Enable_RemoveFogOfWar;
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

    private static float GetRevealRadius(Il2CppLE.UI.Minimap.Minimap minimap)
    {
        if (minimap.IsNullOrDestroyed())
        {
            return FallbackRevealRadius;
        }

        try
        {
            if (!minimap.minimapInformation.IsNullOrDestroyed())
            {
                Vector3 size = minimap.minimapInformation.LevelBounds.size;
                float diagonal = Mathf.Sqrt((size.x * size.x) + (size.z * size.z));
                if (diagonal > 0f)
                {
                    // Cover the entire level plus a little margin without feeding
                    // extreme values into the FoW compute shader.
                    return Mathf.Max(1000f, diagonal + 256f);
                }
            }
        }
        catch { }

        return FallbackRevealRadius;
    }

    private static void ApplyRevealAll(Il2CppLE.UI.Minimap.Minimap minimap)
    {
        if (!CanRun() || minimap.IsNullOrDestroyed())
        {
            return;
        }

        float radius = GetRevealRadius(minimap);
        minimap.RevealRadius = radius;

        try
        {
            if (
                !minimap.minimapInformation.IsNullOrDestroyed()
                && !minimap.minimapInformation.LiveSdf.IsNullOrDestroyed()
            )
            {
                // maxDistance is shader/SDF distance, so size it from the actual
                // FoW texture rather than using int.MaxValue.
                minimap.maxDistance = Mathf.Max(
                    minimap.minimapInformation.LiveSdf.width,
                    minimap.minimapInformation.LiveSdf.height
                );
            }
        }
        catch { }
    }

    [HarmonyPatch(typeof(Il2CppLE.UI.Minimap.Minimap), "Awake")]
    public class Minimap_Awake
    {
        [HarmonyPostfix]
        static void Postfix(Il2CppLE.UI.Minimap.Minimap __instance)
        {
            ApplyRevealAll(__instance);
        }
    }

    [HarmonyPatch(typeof(Il2CppLE.UI.Minimap.Minimap), "OnFoWDataReady")]
    public class Minimap_OnFoWDataReady
    {
        [HarmonyPostfix]
        static void Postfix(Il2CppLE.UI.Minimap.Minimap __instance)
        {
            ApplyRevealAll(__instance);
        }
    }

    [HarmonyPatch(typeof(Il2CppLE.UI.Minimap.Minimap), "OnMapLoaded")]
    public class Minimap_OnMapLoaded
    {
        [HarmonyPostfix]
        static void Postfix(Il2CppLE.UI.Minimap.Minimap __instance)
        {
            ApplyRevealAll(__instance);
        }
    }

    [HarmonyPatch(typeof(Il2CppLE.UI.Minimap.Minimap), "UpdateFoW_Singular")]
    public class Minimap_UpdateFoW_Singular
    {
        [HarmonyPrefix]
        static void Prefix(Il2CppLE.UI.Minimap.Minimap __instance, ref float __1)
        {
            if (!CanRun())
            {
                return;
            }
            __1 = GetRevealRadius(__instance);
        }
    }

    [HarmonyPatch(typeof(Il2CppLE.UI.Minimap.Minimap), "UpdateFoW")]
    public class Minimap_UpdateFoW
    {
        [HarmonyPrefix]
        static void Prefix(Il2CppLE.UI.Minimap.Minimap __instance, ref float __2)
        {
            if (!CanRun())
            {
                return;
            }
            __2 = GetRevealRadius(__instance);
        }
    }
}
