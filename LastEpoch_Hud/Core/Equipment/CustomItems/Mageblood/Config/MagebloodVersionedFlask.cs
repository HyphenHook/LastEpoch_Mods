namespace LastEpoch_Hud.Scripts.Core.CustomItems.Mageblood.Config;

/// <summary>A default flask plus the defaults version that added it.</summary>
public readonly record struct MagebloodVersionedFlask(MagebloodFlaskEntry Entry, int Since);
