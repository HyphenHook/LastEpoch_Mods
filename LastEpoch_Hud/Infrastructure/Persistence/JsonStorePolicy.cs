using Newtonsoft.Json;

namespace LastEpoch_Hud.Scripts.Infrastructure.Persistence;

/// <summary>Explicit per-file wire format and backup behavior. Backups are never loaded automatically.</summary>
public sealed class JsonStorePolicy
{
    public Formatting Formatting { get; init; } = Formatting.None;
    public bool KeepBackup { get; init; }
}
