using System;
using HarmonyLib;
using Il2Cpp;
using Il2CppLE.Factions;
using LastEpoch_Hud.Scripts.Core;
using LastEpoch_Hud.Scripts.ModUI;
using ModSaveManager = LastEpoch_Hud.Scripts.ModUI.Settings.SaveManager;

namespace LastEpoch_Hud.Scripts.Mods.Items;

internal static class ProphecyRewards
{
    // Do NOT patch SpawnRewardForPlayer: its Nullable<LensType> parameter crosses
    // an IL2CPP detour that crashed both previous implementations. Leave that
    // call entirely native, including its original lens and double-reward logic.
    [HarmonyPatch(typeof(ProphecySlot), "TryTriggerReward")]
    internal static class Trigger
    {
        [HarmonyPrefix]
        static void Prefix(ProphecySlot __instance, Actor __2, out ProphecyRewardCount __state)
        {
            __state = null;
            var setting = ModSettings.ProphecyRewards.Multiplier;
            if (
                !setting.Enabled
                || ProphecyRewardCount.Factor(setting.Value) <= 1
                || ModSaveManager.instance.IsNullOrDestroyed()
                || !ModSaveManager.instance.initialized
                || !Scenes.IsGameScene()
                || __instance.IsNullOrDestroyed()
                || __2.IsNullOrDestroyed()
                || Refs_Manager.player_actor.IsNullOrDestroyed()
                || __2.Pointer != Refs_Manager.player_actor.Pointer
            )
                return;

            try
            {
                if (!__instance.IsCharged)
                    return;
                var reward = __instance.Reward;
                if (reward.IsNullOrDestroyed())
                    return;
                int original = reward.itemsDropped;
                __state = ProphecyRewardCount.Begin(
                    reward.Pointer.ToInt64(),
                    original,
                    setting.Value,
                    count =>
                    {
                        if (!reward.IsNullOrDestroyed())
                            reward.itemsDropped = count;
                    }
                );
                if (__state != null)
                    ModSettings.Trace(
                        $"Prophecy trigger: x{ProphecyRewardCount.Factor(setting.Value)}, base count {original}."
                    );
            }
            catch (Exception ex)
            {
                Main.logger_instance?.Warning(
                    "Prophecy count override failed; using native reward flow: " + ex.Message
                );
            }
        }

        [HarmonyFinalizer]
        static void Finalizer(ProphecyRewardCount __state)
        {
            // Restore after every exit, including unsuccessful triggers and native
            // managed exceptions. Do not replace/suppress the game's exception.
            try
            {
                __state?.Dispose();
            }
            catch (Exception ex)
            {
                Main.logger_instance?.Error(
                    "Prophecy base count could not be restored: " + ex.Message
                );
            }
        }
    }
}
