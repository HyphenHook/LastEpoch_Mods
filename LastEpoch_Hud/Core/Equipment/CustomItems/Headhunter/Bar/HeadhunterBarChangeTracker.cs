using System.Collections.Generic;

namespace LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter.Bar;

/// <summary>Detects a change in the ordered stat rows shown on the bar.</summary>
public sealed class HeadhunterBarChangeTracker
{
    private readonly List<HeadhunterStatKey> _last = new();

    public bool Update(IReadOnlyList<HeadhunterBarEntry> entries)
    {
        if (!IsDifferent(entries))
        {
            return false;
        }

        _last.Clear();
        for (int i = 0; i < entries.Count; i++)
        {
            _last.Add(entries[i].Key);
        }

        return true;
    }

    private bool IsDifferent(IReadOnlyList<HeadhunterBarEntry> entries)
    {
        if (entries.Count != _last.Count)
        {
            return true;
        }

        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i].Key != _last[i])
            {
                return true;
            }
        }

        return false;
    }
}
