using Il2Cpp;

namespace LastEpoch_Hud.Scripts.Mods.Character;

internal static class Character_SoulEmbers
{
    public static DungeonRunManager GetManager()
    {
        try
        {
            DungeonRunManager manager = PlayerFinder.getDungeonRunManagerInSingleplayerOrOnClient();
            return manager.IsNullOrDestroyed() ? null : manager;
        }
        catch (System.Exception ex)
        {
            ErrorLog.Report(ex, "Character_SoulEmbers.GetManager");
            return null;
        }
    }

    public static bool Add(int amount)
    {
        if (amount <= 0)
        {
            return false;
        }

        DungeonRunManager manager = GetManager();
        if (manager.IsNullOrDestroyed())
        {
            Main.logger_instance?.Warning("Soul Embers: DungeonRunManager unavailable");
            return false;
        }

        try
        {
            manager.modifySoulEmberCountOnSingleplayerOrServer(amount, true);
            return true;
        }
        catch (System.Exception ex)
        {
            Main.logger_instance?.Warning("Soul Embers add failed: " + ex.Message);
            return false;
        }
    }
}
