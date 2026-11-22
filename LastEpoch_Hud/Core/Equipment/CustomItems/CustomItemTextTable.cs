using System;
using System.Collections.Generic;

namespace LastEpoch_Hud.Scripts.Core.CustomItems;

/// <summary>Maps game localization keys to custom item text resolvers.</summary>
public sealed class CustomItemTextTable
{
    private readonly Dictionary<
        string,
        Func<IReadOnlyDictionary<string, string>, string>
    > _resolvers = new(StringComparer.Ordinal);

    public void Register(string gameKey, Func<IReadOnlyDictionary<string, string>, string> resolve)
    {
        if (string.IsNullOrEmpty(gameKey) || resolve == null)
        {
            return;
        }

        _resolvers[gameKey] = resolve;
    }

    public void RegisterLocaleKey(string gameKey, string localeKey)
    {
        if (string.IsNullOrEmpty(localeKey))
        {
            return;
        }

        Register(gameKey, texts => LocaleText.Get(texts, localeKey));
    }

    public string Resolve(string gameKey, IReadOnlyDictionary<string, string> texts)
    {
        if (texts == null || gameKey == null)
        {
            return null;
        }

        if (
            !_resolvers.TryGetValue(
                gameKey,
                out Func<IReadOnlyDictionary<string, string>, string> resolve
            )
        )
        {
            return null;
        }

        string text = resolve(texts);
        return string.IsNullOrEmpty(text) ? null : text;
    }
}
