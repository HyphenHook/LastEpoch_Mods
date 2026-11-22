using System;
using System.Collections.Generic;

namespace LastEpoch_Hud.Scripts.Core.Diagnostics;

/// <summary>Decides per context whether an error is reported now: first at once, then after 1 min, 10 min, 60 min gaps.</summary>
public sealed class ErrorThrottle
{
    private static readonly long[] _backoffMs = { 60_000, 600_000, 3_600_000 };

    private readonly Dictionary<string, Entry> _entries = new();

    /// <summary>True when the error must be reported now. repeats is 0 for the first report, otherwise the occurrences since the last report, current one included.</summary>
    public bool TryReport(string context, long nowMs, out int repeats)
    {
        context ??= string.Empty;
        repeats = 0;
        if (!_entries.TryGetValue(context, out Entry entry))
        {
            _entries[context] = new Entry { LastReportMs = nowMs };
            return true;
        }

        entry.Pending++;
        if (nowMs - entry.LastReportMs < _backoffMs[entry.Step])
        {
            return false;
        }

        repeats = entry.Pending;
        entry.Pending = 0;
        entry.LastReportMs = nowMs;
        entry.Step = Math.Min(entry.Step + 1, _backoffMs.Length - 1);
        return true;
    }

    private sealed class Entry
    {
        public long LastReportMs;
        public int Step;
        public int Pending;
    }
}
