using System;
using Il2Cpp;
using LastEpoch_Hud.Scripts.Core.Monoliths;

namespace LastEpoch_Hud.Scripts.Mods.Monoliths;

/// <summary>Timeline names in the game's language.</summary>
internal static class MonolithTimelineNames
{
    /// <summary>The game's localized name; the catalog's English label when the game has none.</summary>
    public static string Of(MonolithTimelineOption option)
    {
        string name;
        try
        {
            name = TimelineIDExtensions.GetTimelineName((TimelineID)option.TimelineId);
        }
        catch (Exception ex)
        {
            ErrorLog.Report(ex, "Monoliths.GetTimelineName");
            return option.Label;
        }

        return string.IsNullOrWhiteSpace(name) ? option.Label : name;
    }
}
