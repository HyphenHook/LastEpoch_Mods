using System;

namespace LastEpoch_Hud.Scripts.Core.QualityOfLife;

public static class TravelSceneRules
{
    // Keep the original picker's exclusions for menu, utility and generated scenes.
    static readonly string[] excluded =
    {
        "ClientSplash",
        "PersistentUI",
        "CharacterSelectScene",
        "Login",
        "MonolithHub",
        "A_Reward",
        "Mastery",
        "Neutral",
        "PCG_Dev",
    };

    public static bool IsSceneName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name != name.Trim())
            return false;
        foreach (char c in name)
            if (char.IsControl(c) || c == '/' || c == '\\')
                return false;
        return true;
    }

    // Dungeon scenes need the game's dungeon transition/setup, not generic travel.
    public static bool IsDungeonScene(string name) =>
        name != null
        && name.Length > 3
        && name.StartsWith("Dun", StringComparison.OrdinalIgnoreCase)
        && char.IsDigit(name[3]);

    public static bool IsDestination(string name)
    {
        if (!IsSceneName(name) || IsDungeonScene(name))
            return false;
        foreach (string blocked in excluded)
            if (string.Equals(name, blocked, StringComparison.OrdinalIgnoreCase))
                return false;
        return name.IndexOf("PCG", StringComparison.OrdinalIgnoreCase) < 0
            && name.IndexOf("Arena", StringComparison.OrdinalIgnoreCase) < 0;
    }
}
