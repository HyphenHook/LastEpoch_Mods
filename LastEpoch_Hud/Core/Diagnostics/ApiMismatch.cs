using System;

namespace LastEpoch_Hud.Scripts.Core.Diagnostics;

/// <summary>Tells a permanent game API mismatch (missing member or type after an update) from a transient failure.</summary>
public static class ApiMismatch
{
    /// <summary>True when the exception means the game no longer has a member or type the mod calls.</summary>
    public static bool Is(Exception ex)
    {
        return ex is MissingMemberException or TypeLoadException;
    }
}
