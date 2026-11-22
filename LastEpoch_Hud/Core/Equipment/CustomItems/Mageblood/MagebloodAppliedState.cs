using System.Collections.Generic;
using LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter.Buffs;

namespace LastEpoch_Hud.Scripts.Core.CustomItems.Mageblood;

/// <summary>What the last Mageblood sync applied, and why to sync again.</summary>
public sealed class MagebloodAppliedState
{
    private readonly List<string> _names = new();
    private bool _dirty = true;

    public IReadOnlyList<string> Names => _names;
    public bool Worn { get; private set; }
    public int ActiveFlasks { get; private set; }

    public void MarkDirty()
    {
        _dirty = true;
    }

    public MagebloodSyncReason NeedsSync(bool worn, bool allLive)
    {
        if (worn != Worn)
        {
            return MagebloodSyncReason.WornChanged;
        }
        if (!allLive)
        {
            return MagebloodSyncReason.BuffsLost;
        }
        return _dirty ? MagebloodSyncReason.Dirty : MagebloodSyncReason.None;
    }

    public void Record(IReadOnlyList<BuffAction> actions, bool worn, int activeFlasks)
    {
        _names.Clear();
        AddNames(actions);
        Worn = worn;
        ActiveFlasks = activeFlasks;
        _dirty = false;
    }

    /// <summary>Marks a nothing-worn, nothing-applied pass clean. True when it was idle.</summary>
    public bool RecordIfIdle(bool worn)
    {
        if (worn || Worn || _names.Count > 0)
        {
            return false;
        }

        _dirty = false;
        return true;
    }

    public void Clear()
    {
        _names.Clear();
        Worn = false;
        ActiveFlasks = 0;
        _dirty = true;
    }

    private void AddNames(IReadOnlyList<BuffAction> actions)
    {
        if (actions == null)
        {
            return;
        }
        foreach (BuffAction action in actions)
        {
            if (action.Kind != BuffActionKind.Add)
            {
                continue;
            }
            if (!_names.Contains(action.BuffName))
            {
                _names.Add(action.BuffName);
            }
        }
    }
}
