using System;
using System.Collections.Generic;
using LastEpoch_Hud.Scripts.Core.CustomItems.Mageblood.Config;

namespace LastEpoch_Hud.Scripts.Core.CustomItems.Mageblood;

/// <summary>Locale keys and shown names of the default Mageblood flasks.</summary>
public static class MagebloodFlaskNames
{
    public const string KeyPrefix = "Mageblood.Flask.";

    private static readonly Dictionary<string, string> _keyByName = BuildKeys();

    public static IReadOnlyList<string> Keys { get; } = new List<string>(_keyByName.Values);

    public static string KeyFor(string flaskName)
    {
        if (string.IsNullOrEmpty(flaskName))
        {
            return null;
        }

        return _keyByName.TryGetValue(flaskName, out string key) ? key : null;
    }

    public static string Display(IReadOnlyDictionary<string, string> texts, string flaskName)
    {
        return LocaleText.Get(texts, KeyFor(flaskName)) ?? flaskName;
    }

    private static Dictionary<string, string> BuildKeys()
    {
        var keys = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (MagebloodFlaskEntry flask in MagebloodConfigDefaults.Flasks)
        {
            keys[flask.Name] = KeyPrefix + flask.Name;
        }

        return keys;
    }
}
