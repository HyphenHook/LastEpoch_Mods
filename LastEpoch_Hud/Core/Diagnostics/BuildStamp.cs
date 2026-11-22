using System;

namespace LastEpoch_Hud.Scripts.Core.Diagnostics;

// Formats the build line written to the log at startup.
public static class BuildStamp
{
    private const string UnknownCommit = "unknown";
    private const string DirtySuffix = "+dirty";

    // Without a commit (git missing at build time) the dirty flag carries no meaning and is ignored.
    public static string Format(string commit, bool dirty, string builtAt)
    {
        if (string.IsNullOrWhiteSpace(commit))
        {
            return Compose(UnknownCommit, builtAt);
        }

        return Compose(dirty ? commit + DirtySuffix : commit, builtAt);
    }

    private static string Compose(string version, string builtAt)
    {
        return "Build " + version + " (" + builtAt + ")";
    }
}
