namespace LastEpoch_Hud.Scripts.Core.Cosmetics;

/// <summary>Decides from game facts whether a cosmetic has a released store icon.</summary>
public static class CosmeticStoreIconRules
{
    /// <summary>No reference or an invalid GUID: false. Unknown bundles: true. Else the bundle lookup answers.</summary>
    public static bool HasStoreIcon(
        bool hasSoftRef,
        bool guidValid,
        bool bundlesKnown,
        bool foundInBundles
    )
    {
        if (!hasSoftRef || !guidValid)
        {
            return false;
        }
        return !bundlesKnown || foundInBundles;
    }
}
