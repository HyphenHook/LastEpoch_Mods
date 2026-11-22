namespace LastEpoch_Hud.Scripts.Core.CustomItems.Mageblood.Config;

/// <summary>Which config rule a value broke.</summary>
public enum MagebloodConfigProblemCode
{
    EmptyFile,
    InvalidJson,
    RootNotObject,
    NotWholeNumber,
    UnsupportedVersion,
    NotList,
    NotObject,
    MissingName,
    EmptyOrNotText,
    DuplicateFlask,
    MissingStat,
    NotFiniteNumber,
    NoValue,
    DuplicateStat,
    NoStats,
    UnknownStat,
    UnknownTag,
    Negative,
}
