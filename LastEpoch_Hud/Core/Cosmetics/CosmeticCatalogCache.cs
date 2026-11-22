using System;

namespace LastEpoch_Hud.Scripts.Core.Cosmetics;

/// <summary>
/// Holds one built catalog per registry instance. A build with no kept ids fails and is not retried
/// until the next scene load. Never builds from <see cref="OnSceneLoaded" />.
/// </summary>
public sealed class CosmeticCatalogCache
{
    private readonly Func<CosmeticCatalog> _build;
    private CosmeticCatalog _catalog;
    private long _cachedKey;
    private bool _failed;
    private bool _broken;

    public CosmeticCatalogCache(Func<CosmeticCatalog> build)
    {
        _build = build;
    }

    /// <summary>True after a build with no kept ids, until the next scene load.</summary>
    public bool Failed => _failed || _broken;

    /// <summary>True after <see cref="MarkBroken" />, for the rest of the session.</summary>
    public bool Broken => _broken;

    /// <summary>True while a catalog is cached.</summary>
    public bool HasCatalog => _catalog != null;

    /// <summary>True with the catalog when it is cached or just built; false when not ready.</summary>
    public bool TryGet(long registryKey, bool cachedRegistryAlive, out CosmeticCatalog catalog)
    {
        catalog = null;
        if (_broken)
        {
            return false;
        }
        if (_catalog != null && cachedRegistryAlive)
        {
            catalog = _catalog;
            return true;
        }
        if (_failed)
        {
            return false;
        }
        CosmeticCatalog built = _build();
        if (built == null || built.KeptCount == 0)
        {
            _catalog = null;
            _failed = true;
            return false;
        }
        _catalog = built;
        _cachedKey = registryKey;
        catalog = built;
        return true;
    }

    /// <summary>Drops the catalog and blocks every build for the session; a scene load does not undo it.</summary>
    public void MarkBroken()
    {
        _broken = true;
        _catalog = null;
    }

    /// <summary>Allows one retry after a failure and drops the cache when the registry changed or is gone.</summary>
    public void OnSceneLoaded(long registryKey, bool registryAvailable)
    {
        _failed = false;
        if (_catalog == null)
        {
            return;
        }
        if (registryAvailable && registryKey == _cachedKey)
        {
            return;
        }
        _catalog = null;
    }
}
