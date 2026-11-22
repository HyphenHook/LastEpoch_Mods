namespace LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter.Bar;

/// <summary>One visible icon of the Headhunter buff bar.</summary>
public readonly record struct HeadhunterBarEntry(
    int StatId,
    int SecondsLeft,
    float Elapsed,
    int Row,
    int Stacks,
    int Tags = 0
)
{
    public HeadhunterStatKey Key => new(StatId, Tags);
}
