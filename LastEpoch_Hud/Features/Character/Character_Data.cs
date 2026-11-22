namespace LastEpoch_Hud.Scripts.Mods.Character;

/// <summary>Saves the player's character data.</summary>
public static class Character_Data
{
    public static void Save()
    {
        Main.logger_instance.Msg("Save Character Data");
        if (Refs_Manager.player_data.IsNullOrDestroyed())
        {
            return;
        }

        Refs_Manager.player_data.SaveData();
    }
}
