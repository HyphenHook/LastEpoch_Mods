using System;
using System.Collections.Generic;

namespace LastEpoch_Hud.Scripts.Core.Skills;

/// <summary>Caches per drain instance whether it belongs to a transform form.</summary>
public sealed class FormDrainGate
{
    private readonly Dictionary<int, bool> _cache = new();
    private string _scene = "";

    public bool ShouldSkip<TSource>(
        string sceneName,
        int instanceId,
        TSource source,
        Func<TSource, FormDrainVerdict> classify
    )
    {
        SyncScene(sceneName);
        if (_cache.TryGetValue(instanceId, out bool cached))
        {
            return cached;
        }

        FormDrainVerdict verdict = classify(source);
        if (verdict == FormDrainVerdict.NotReady)
        {
            return false;
        }

        bool skip = verdict == FormDrainVerdict.Skip;
        _cache[instanceId] = skip;
        return skip;
    }

    public void Reset()
    {
        _cache.Clear();
        _scene = "";
    }

    private void SyncScene(string sceneName)
    {
        if (string.Equals(_scene, sceneName, StringComparison.Ordinal))
        {
            return;
        }

        _cache.Clear();
        _scene = sceneName;
    }
}
