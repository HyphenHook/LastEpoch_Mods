namespace LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter.Config.Defaults;

/// <summary>A default stat row plus the defaults version that introduced it.</summary>
public readonly record struct HeadhunterVersionedStat(HeadhunterStatEntry Entry, int Since);
