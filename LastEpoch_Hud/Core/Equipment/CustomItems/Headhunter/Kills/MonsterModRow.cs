using System.Collections.Generic;

namespace LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter.Kills;

/// <summary>One monster prefix or suffix, as plain data for the dump.</summary>
public sealed class MonsterModRow
{
    public const string PrefixList = "prefix";
    public const string SuffixList = "suffix";

    public string List { get; set; }
    public int ModKey { get; set; }
    public string Title { get; set; }
    public string ModType { get; set; }
    public string RarityRequirement { get; set; }
    public string DisplayGroup { get; set; }
    public string ClassName { get; set; }
    public float? RareModifier { get; set; }
    public string ScalingType { get; set; }
    public IReadOnlyList<MonsterModStat> Stats { get; set; } = new List<MonsterModStat>();
}
