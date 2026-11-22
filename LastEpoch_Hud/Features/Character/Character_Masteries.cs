using LastEpoch_Hud.Scripts.Core.Character;
using LastEpoch_Hud.Scripts.ModUI.Shell.Host;

namespace LastEpoch_Hud.Scripts.Mods.Character;

internal static class Character_Masteries
{
    public static void ResetChooseMasterie()
    {
        if (!CanRun())
        {
            return;
        }

        if (MasteryButtonRule.ShouldReset(Refs_Manager.player_treedata.chosenMastery))
        {
            ResetMasterie();
            return;
        }

        ChooseMasterie();
    }

    private static void ResetMasterie()
    {
        Refs_Manager.player_treedata.chosenMastery = 0;
        Refs_Manager.player_data_tracker.charData.ChosenMastery = 0;
        Refs_Manager.player_data_tracker.charData.SaveData();
    }

    private static void ChooseMasterie()
    {
        GamePauseMenu.Resume(); //Close Hud
        if (!Refs_Manager.game_uibase.IsNullOrDestroyed())
        {
            Refs_Manager.game_uibase.openMasteryPanel(false);
        }
    }

    private static bool CanRun()
    {
        return Scenes.IsGameScene()
            && !Refs_Manager.player_treedata.IsNullOrDestroyed()
            && !Refs_Manager.player_data_tracker.IsNullOrDestroyed();
    }
}
