namespace LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter.Config;

/// <summary>Which config rule a value broke.</summary>
public enum HeadhunterConfigProblemCode
{
    EmptyFile,
    InvalidJson,
    RootNotObject,
    UnsupportedVersion,
    NotWholeNumber,
    NotPositiveNumber,
    NotPositiveWholeNumber,
    NotFiniteNumber,
    NotBool,
    NotObject,
    NotList,
    EmptyOrNotText,
    MissingStat,
    UnknownStat,
    DuplicateStat,
    UnknownTag,
    DuplicateStatId,
    DuplicateModKey,
    UnknownRow,
    NotPercent,
    NotAuraStrength,
}
