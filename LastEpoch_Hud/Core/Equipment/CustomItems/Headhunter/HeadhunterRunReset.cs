using System;

namespace LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter;

/// <summary>Says which scene loads end a character's run: login and character select. Zone loads never do.</summary>
public static class HeadhunterRunReset
{
    public const string CharacterSelectScene = "CharacterSelectScene";
    public const string LoginScene = "Login";

    public static bool IsCharacterExit(string sceneName)
    {
        return string.Equals(sceneName, CharacterSelectScene, StringComparison.Ordinal)
            || string.Equals(sceneName, LoginScene, StringComparison.Ordinal);
    }
}
