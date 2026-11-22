namespace LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter.Config;

/// <summary>One stat row. Increased is a percent (10 = 10 %), Added is the raw flat game value. Tag is a game ability tag name, null = untagged.</summary>
public readonly record struct HeadhunterStatEntry(
    string Stat,
    float Added,
    float Increased,
    bool Enabled,
    string Tag = null
)
{
    public string RowText => Tag == null ? Stat : Stat + "_" + Tag;
}
