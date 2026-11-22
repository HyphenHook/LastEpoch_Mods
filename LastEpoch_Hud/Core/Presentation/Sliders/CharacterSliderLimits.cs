namespace LastEpoch_Hud.Scripts.Core.ModUI.Sliders;

/// <summary>Slider limits of the Utilities > Character sliders, copied from the legacy prefab.</summary>
public static class CharacterSliderLimits
{
    // Source: Slider_Character_Factions_Favor
    public static readonly SliderRange FactionFavor = new(0f, 1000000f, true);

    // Source: Slider_Character_Factions_Rank
    public static readonly SliderRange FactionRank = new(0f, 12f, true);

    // Source: Slider_Character_Factions_Reputation
    public static readonly SliderRange FactionReputation = new(0f, 1000000f, true);
}
