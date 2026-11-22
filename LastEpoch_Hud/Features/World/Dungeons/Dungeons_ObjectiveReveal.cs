using System;
using System.Collections.Generic;
using HarmonyLib;
using Il2Cpp;
using LastEpoch_Hud.Scripts.ModUI;
using ModSaveManager = LastEpoch_Hud.Scripts.ModUI.Settings.SaveManager;

namespace LastEpoch_Hud.Scripts.Mods.Dungeons;

internal static class Dungeons_ObjectiveReveal
{
    static DelayedZoneObjectivePulse pulse;
    static readonly HashSet<IntPtr> requested = new HashSet<IntPtr>();

    static bool Enabled =>
        Scenes.IsGameScene()
        && !ModSaveManager.instance.IsNullOrDestroyed()
        && ModSaveManager.instance.initialized
        && ModSettings.DungeonReveal.Enabled.Value;

    public static void Apply(bool enabled)
    {
        if (
            !enabled
            || !Enabled
            || pulse.IsNullOrDestroyed()
            || pulse.activated
            || pulse.dungeonZoneManager.IsNullOrDestroyed()
        )
            return;
        var manager = pulse.dungeonZoneManager;
        // The dungeon's entry-unlock state is not a reveal-readiness signal.
        // Native pulse Start has finished and the dungeon manager is present.
        IntPtr pointer = pulse.Pointer;
        // Mark BEFORE the native call: activation can synchronously invoke other
        // native callbacks before its activated field has been updated.
        if (!requested.Add(pointer))
            return;
        Main.logger_instance?.Msg(
            "Dungeon objective reveal: requesting pulse "
                + pointer
                + " in "
                + manager.gameObject.scene.name
                + " (type="
                + manager.zoneType
                + ", state="
                + manager.zoneState
                + ")"
        );
        pulse.activate();
        Main.logger_instance?.Msg(
            "Dungeon objective reveal: completed request for pulse " + pointer
        );
    }

    static System.Collections.IEnumerator ApplyWhenReady(DelayedZoneObjectivePulse target)
    {
        // Let native Start finish, then wait for the floor and saved settings.
        // Never invoke activation from inside the native score-change callback.
        yield return null;
        yield return null;
        for (int i = 0; i < 120; i++)
        {
            yield return new UnityEngine.WaitForSeconds(0.25f);
            if (target.IsNullOrDestroyed())
                yield break;
            if (target.dungeonZoneManager.IsNullOrDestroyed())
                continue;
            pulse = target;
            Apply(ModSettings.DungeonReveal.Enabled.Value);
            if (requested.Contains(target.Pointer) || target.activated)
                yield break;
        }
    }

    [HarmonyPatch(typeof(DelayedZoneObjectivePulse), "Start")]
    static class PulseStart
    {
        [HarmonyPostfix]
        static void Postfix(DelayedZoneObjectivePulse __instance)
        {
            MelonLoader.MelonCoroutines.Start(ApplyWhenReady(__instance));
        }
    }

    [HarmonyPatch(typeof(DelayedZoneObjectivePulse), "OnDestroy")]
    static class PulseDestroyed
    {
        [HarmonyPrefix]
        static void Prefix(DelayedZoneObjectivePulse __instance)
        {
            requested.Remove(__instance.Pointer);
            if (!pulse.IsNullOrDestroyed() && pulse.Pointer == __instance.Pointer)
                pulse = null;
        }
    }
}
