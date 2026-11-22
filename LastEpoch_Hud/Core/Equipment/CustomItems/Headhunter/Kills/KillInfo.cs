using System.Collections.Generic;

namespace LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter.Kills;

/// <summary>One kill as Core sees it. ModStats may be null (no mods). Mods may be null: then all ModStats are own stats.</summary>
public readonly record struct KillInfo(
    KillKind Kind,
    bool ByMinion,
    IReadOnlyList<HeadhunterStatKey> ModStats,
    IReadOnlyList<KillMod> Mods = null
);
