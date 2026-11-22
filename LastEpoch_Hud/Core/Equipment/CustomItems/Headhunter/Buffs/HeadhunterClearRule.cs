using System.Collections.Generic;
using LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter.Config.Resolve;

namespace LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter.Buffs;

/// <summary>Decides which buffs go when Headhunter leaves the player: every table stat.</summary>
public sealed class HeadhunterClearRule
{
    private readonly List<BuffAction> _actions = new();

    /// <summary>One Remove per table stat. The list is reused: valid until the next call.</summary>
    public IReadOnlyList<BuffAction> RemoveAll(HeadhunterResolvedConfig config)
    {
        _actions.Clear();
        IReadOnlyList<HeadhunterBuffStat> stats = config.Stats;
        for (int i = 0; i < stats.Count; i++)
        {
            _actions.Add(
                new BuffAction(
                    BuffActionKind.Remove,
                    stats[i].BuffName,
                    stats[i].StatId,
                    stats[i].Added,
                    stats[i].Increased,
                    config.DurationSeconds,
                    0,
                    stats[i].Tags
                )
            );
        }

        return _actions;
    }
}
