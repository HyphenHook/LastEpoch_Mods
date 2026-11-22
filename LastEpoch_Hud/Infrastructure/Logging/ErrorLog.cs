using System;
using LastEpoch_Hud.Scripts.Core.Diagnostics;

namespace LastEpoch_Hud.Scripts;

/// <summary>Backoff-throttled error log entry point for the mod.</summary>
public static class ErrorLog
{
    private static readonly ErrorThrottle _throttle = new();
    private static readonly object _gate = new();

    public static void Report(Exception ex, string context)
    {
        if (!ShouldLog(context, out int repeats))
        {
            return;
        }

        string summary = ErrorLogText.ExceptionSummary(ex?.GetType().FullName, ex?.Message);
        Write(ErrorLogText.Line(context, repeats, ex?.ToString(), summary));
    }

    public static void Report(string message, string context)
    {
        if (!ShouldLog(context, out int repeats))
        {
            return;
        }

        Write(ErrorLogText.Line(context, repeats, message, message));
    }

    private static bool ShouldLog(string context, out int repeats)
    {
        lock (_gate)
        {
            return _throttle.TryReport(context, Environment.TickCount64, out repeats);
        }
    }

    private static void Write(string line)
    {
        Main.logger_instance?.Error(line);
    }
}
