using System;
using System.Collections.Generic;

namespace LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter.Buffs;

/// <summary>Stack count per table row. Allocated once per config resolve.</summary>
public sealed class HeadhunterStackState
{
    private readonly int[] _counts;

    public HeadhunterStackState(int rowCount)
    {
        _counts = new int[rowCount];
    }

    public int Total
    {
        get
        {
            int sum = 0;
            for (int i = 0; i < _counts.Length; i++)
            {
                sum += _counts[i];
            }

            return sum;
        }
    }

    /// <summary>Drops rows that are not live and lifts live rows without a count to 1.</summary>
    public void Sync(IReadOnlySet<int> liveRows)
    {
        for (int i = 0; i < _counts.Length; i++)
        {
            _counts[i] = SyncedCount(_counts[i], liveRows.Contains(i));
        }
    }

    public int Get(int row)
    {
        return InRange(row) ? _counts[row] : 0;
    }

    /// <summary>Adds one stack unless the row is at the cap or out of range.</summary>
    public bool TryAdd(int row, int cap)
    {
        if (!InRange(row) || _counts[row] >= cap)
        {
            return false;
        }

        _counts[row]++;
        return true;
    }

    public void Reset()
    {
        Array.Clear(_counts, 0, _counts.Length);
    }

    private static int SyncedCount(int count, bool live)
    {
        if (!live)
        {
            return 0;
        }

        return count > 0 ? count : 1;
    }

    private bool InRange(int row)
    {
        return row >= 0 && row < _counts.Length;
    }
}
