namespace LastEpoch_Hud.Scripts.Core.Skills;

/// <summary>Result of classifying a health drain: not yet decidable, skip it, or keep it running.</summary>
public enum FormDrainVerdict
{
    NotReady,
    Skip,
    Keep,
}
