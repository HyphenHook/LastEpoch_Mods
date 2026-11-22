using System.Collections.Generic;

namespace LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter.Config;

/// <summary>One affix map entry as written in the file. Rows are stat row texts: Stat or Stat_Tag.</summary>
public readonly record struct HeadhunterAffixEntry(
    int ModKey,
    string Note,
    IReadOnlyList<string> Rows
);
