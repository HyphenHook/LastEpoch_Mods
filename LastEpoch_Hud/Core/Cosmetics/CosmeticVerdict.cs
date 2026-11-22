namespace LastEpoch_Hud.Scripts.Core.Cosmetics;

/// <summary>Why a registry cosmetic is kept in or removed from the unlock list.</summary>
public enum CosmeticVerdict
{
    Keep,
    EmptyId,
    UnknownPrefix,
    Blocklisted,
    TestEntry,
    NoStoreIcon,
}
