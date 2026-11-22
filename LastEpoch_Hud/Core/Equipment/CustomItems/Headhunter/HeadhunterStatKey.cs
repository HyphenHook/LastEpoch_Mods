namespace LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter;

/// <summary>Identity of a stat row or monster stat: game stat id plus ability tag id (0 = none).</summary>
public readonly record struct HeadhunterStatKey(int StatId, int Tags);
