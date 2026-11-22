using System.Collections.Generic;
using LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter;
using LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter.Config;

namespace LastEpoch_Hud.Scripts.Mods.Items.CustomItems.Headhunter;

internal static class HeadhunterLocales
{
    internal static string Description(IReadOnlyDictionary<string, string> texts)
    {
        HeadhunterConfig config = HeadhunterConfigLoader.Current;
        return HeadhunterDescription.Text(texts, config.DurationSeconds, config.MaxStacks);
    }
}
