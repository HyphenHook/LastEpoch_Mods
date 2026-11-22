using System;
using System.Collections.Generic;

namespace LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter.Pause.RewardMenu;

/// <summary>Which game panels are reward choices that pause HH timers.</summary>
public static class HeadhunterRewardPanels
{
    private static readonly HashSet<string> _names = new(StringComparer.Ordinal)
    {
        "ChooseBlessingPanel",
        "ChooseEchoRewardPanel",
        "NemesisPanel",
        "RageOfMorditasRewardPanel",
        "GreaterWindowPanel",
        "TimeBeastEvolutionPanel",
        "VaultsOfUncertainFatePanel",
        "SanctuaryOfEterraPanel",
        "IdolEnchantmentPanel",
        "GlyphOfInsightReplacementPanel",
        "RandomizeLegendaryPotentialPanel",
        "ShatterSetItemsPanel",
        "SwapAttributesPanel",
        "RerollUniqueAffixesPanel",
        "ExchangeWovenEchoesPanel",
    };

    public static IReadOnlyCollection<string> TypeNames => _names;

    public static bool IsRewardChoice(string typeName)
    {
        return typeName != null && _names.Contains(typeName);
    }
}
