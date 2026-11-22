namespace LastEpoch_Hud.Scripts.Core.CustomItems.Mageblood.Config;

/// <summary>One stat row of a flask as written in the file. Increased and More are percents.</summary>
public readonly record struct MagebloodStatEntry(
    string Stat,
    float Added = 0f,
    float Increased = 0f,
    float More = 0f,
    string Tag = null
)
{
    public string RowText => Tag == null ? Stat : Stat + "_" + Tag;
}
