using System.Collections.Generic;

namespace LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter.Kills;

/// <summary>Accepts each (frame, actor) kill once, so a double-delivered event counts once.</summary>
public sealed class KillDeduper
{
    private readonly HashSet<int> _claimed = new();
    private int _frame = -1;

    public bool TryClaim(int frame, int actorId)
    {
        if (frame != _frame)
        {
            _claimed.Clear();
            _frame = frame;
        }

        return _claimed.Add(actorId);
    }
}
