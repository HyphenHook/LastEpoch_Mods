using System;
using HarmonyLib;
using Il2Cpp;
using Il2CppEHG.Multiplayer;
using Il2CppLE.Networking.Cosmetics;
using Il2CppLE.Services.Cosmetics;

namespace LastEpoch_Hud.Scripts.Mods.Cosmetics;

/// <summary>
/// Offline: skill effect (ability) cosmetics were equipped but never shown. The ability list preloads
/// the effect visuals once, in Awake, before the offline player has a UserIdentity ("UserIdentity not
/// yet initialized"), so the preload is skipped and never retried. Re-run the game's own preload once
/// the identity exists and whenever a skill effect is equipped.
/// </summary>
internal static class SkillEffectPreload
{
    public static void Refresh(string reason)
    {
        if (GameplayEnvironment.IsOnlinePlay)
        {
            return;
        }
        try
        {
            Actor player = Scripts.Refs_Manager.player_actor;
            if (player.IsNullOrDestroyed())
            {
                return;
            }
            int refreshed = 0;
            foreach (AbilityList list in player.GetComponentsInChildren<AbilityList>(true))
            {
                if (list.IsNullOrDestroyed())
                {
                    continue;
                }
                list.UpdatePreloader(true);
                refreshed++;
            }
            Main.logger_instance?.Msg(
                "Skill effect preload refresh (" + reason + "): " + refreshed + " ability list(s)"
            );
        }
        catch (Exception ex)
        {
            Main.logger_instance?.Warning("Skill effect preload refresh failed: " + ex.Message);
        }
    }
}

[HarmonyPatch(
    typeof(ClientCosmeticsNetworkManager),
    "OfflineLocalPlayerInitHandler",
    new[] { typeof(PlayerActorSync) }
)]
public class SkillEffectPreloadOnInitPatch
{
    [HarmonyPostfix]
    private static void Postfix() => SkillEffectPreload.Refresh("player init");
}

[HarmonyPatch(
    typeof(ClientCosmeticsNetworkManager),
    "AddToAbilityEquips",
    new[] { typeof(CosmeticAbility), typeof(string) }
)]
public class SkillEffectPreloadOnEquipPatch
{
    [HarmonyPostfix]
    private static void Postfix() => SkillEffectPreload.Refresh("skill effect equipped");
}
