using System.Collections.Generic;

namespace LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter.Config.Defaults;

/// <summary>What a config merge may add to an older file.</summary>
public sealed class HeadhunterMergeDefaults
{
    public int Version { get; init; }
    public IReadOnlyList<HeadhunterVersionedStat> Stats { get; init; }
    public IReadOnlyList<HeadhunterVersionedField> Fields { get; init; }
    public IReadOnlyList<HeadhunterVersionedAffix> Affixes { get; init; }
    public IReadOnlyList<HeadhunterVersionedAffixRow> AffixRows { get; init; }
}
