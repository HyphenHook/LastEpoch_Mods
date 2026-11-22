namespace LastEpoch_Hud.Scripts.Core.Persistence;

/// <summary>Missing files return false; unreadable or invalid files throw. Writes commit atomically.</summary>
public interface IJsonRepository<T>
{
    bool TryLoad(out T value);
    void Save(T value);
}
