using System.Collections.Generic;
using LastEpoch_Hud.Scripts.Core.CustomItems.Mageblood.Config;

namespace LastEpoch_Hud.Scripts.Core.CustomItems.Mageblood.Bar;

/// <summary>Says when the flask bar must rebuild its icons and labels.</summary>
public sealed class MagebloodBarContent
{
    private IReadOnlyList<MagebloodFlask> _flasks;
    private int _count = -1;
    private bool _complete;

    public int Version { get; private set; }

    public bool NeedsRebuild(IReadOnlyList<MagebloodFlask> flasks, int count)
    {
        return !_complete || !ReferenceEquals(flasks, _flasks) || count != _count;
    }

    public void Built(IReadOnlyList<MagebloodFlask> flasks, int count, bool complete)
    {
        _flasks = flasks;
        _count = count;
        _complete = complete;
        Version++;
    }

    public void Reset()
    {
        _flasks = null;
        _count = -1;
        _complete = false;
    }
}
