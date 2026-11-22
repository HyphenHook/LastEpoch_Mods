using System;
using UnityEngine;

namespace LastEpoch_Hud.Scripts.Mods.Character;

// Currency action for the redesigned HUD; no legacy prefab button injection.
internal static class Character_Gold
{
    public static void SpawnMillion()
    {
        if (
            !Scenes.IsGameScene()
            || Refs_Manager.player_actor.IsNullOrDestroyed()
            || Refs_Manager.ground_item_manager.IsNullOrDestroyed()
        )
            return;

        try
        {
            Refs_Manager.ground_item_manager.dropGoldForPlayer(
                Refs_Manager.player_actor,
                1000000,
                Refs_Manager.player_actor.position(),
                false
            );
            Main.logger_instance?.Msg("Gold: spawned 1,000,000 for the local player.");
        }
        catch (Exception ex)
        {
            Main.logger_instance?.Error("Spawn Gold failed: " + ex.Message);
        }
    }
}
