namespace LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter.Bar;

/// <summary>Says when the buff bar prefabs must be loaded. A bundle that lacked them is not scanned again until the bundle id changes.</summary>
public sealed class HeadhunterAssetLoadGate
{
    private long _missingBundle;

    /// <summary>True unless there is no bundle (id 0) or this bundle was already found lacking the prefabs.</summary>
    public bool ShouldLoad(long bundleId)
    {
        return bundleId != 0 && bundleId != _missingBundle;
    }

    /// <summary>Remembers a bundle that lacks the prefabs.</summary>
    public void MarkMissing(long bundleId)
    {
        _missingBundle = bundleId;
    }
}
