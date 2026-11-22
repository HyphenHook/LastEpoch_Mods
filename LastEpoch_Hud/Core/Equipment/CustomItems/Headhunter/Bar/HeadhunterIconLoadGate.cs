using System.Collections.Generic;

namespace LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter.Bar;

/// <summary>Starts each icon load once and remembers failures until the next scene load.</summary>
public sealed class HeadhunterIconLoadGate
{
    private readonly HashSet<HeadhunterStatKey> _failed = new();

    public HeadhunterIconStep Next(HeadhunterStatKey key, HeadhunterIconLoadStatus status)
    {
        switch (status)
        {
            case HeadhunterIconLoadStatus.Loading:
                return HeadhunterIconStep.Wait;
            case HeadhunterIconLoadStatus.Loaded:
                return HeadhunterIconStep.Use;
            case HeadhunterIconLoadStatus.Failed:
                _failed.Add(key);
                return HeadhunterIconStep.Drop;
            default:
                return _failed.Contains(key) ? HeadhunterIconStep.Skip : HeadhunterIconStep.Start;
        }
    }

    public void MarkFailed(HeadhunterStatKey key)
    {
        _failed.Add(key);
    }

    public void AllowRetry()
    {
        _failed.Clear();
    }
}
