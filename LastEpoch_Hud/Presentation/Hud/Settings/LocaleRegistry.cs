using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Il2CppTMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LastEpoch_Hud.Scripts.ModUI.Settings;

// Tracks (Text, canonical English label) pairs. On language change we re-translate
// from the canonical key instead of the current Text content (which has already been
// translated once and so is no longer a key in any locale dictionary).
internal static class LocaleRegistry
{
    private static readonly List<(Text Text, string EnglishLabel)> entries = new();
    private static System.Collections.Generic.Dictionary<string, string> lastDict;

    private static readonly List<(TMP_Text Text, string EnglishLabel)> tmpEntries = new();
    private static readonly string[] formats =
    {
        "Implicit {0}",
        "Affix {0} Tier",
        "Affix {0} Value",
        "Unique Mod {0}",
        "Roll {0}",
        "Prefix {0}",
        "Suffix {0}",
        "Selected: {0}",
        "Lens: {0}",
        "Corrupted: {0}",
        "Corrupted affix: {0}",
        "Choose a regular affix for {0}.",
        "Dropped {0} item(s).",
        "Drop failed: {0}",
    };

    public static string Translate(string english)
    {
        if (string.IsNullOrEmpty(english))
            return english;
        string canonical = Locales.CanonicalKey(english);
        if (Locales.TryGetTranslation(canonical, out string translated))
            return translated;
        if (canonical.EndsWith(" %", StringComparison.Ordinal))
            return Translate(canonical.Substring(0, canonical.Length - 2)) + " %";
        if (canonical.EndsWith(" (%)", StringComparison.Ordinal))
            return Translate(canonical.Substring(0, canonical.Length - 4)) + " (%)";
        foreach (var format in formats)
        {
            string pattern = "^" + Regex.Escape(format).Replace(Regex.Escape("{0}"), "(.*?)") + "$";
            var match = Regex.Match(canonical, pattern);
            if (!match.Success)
                continue;
            string value = match.Groups[1].Value;
            if (Locales.TryGetTranslation(value, out string localizedValue))
                value = localizedValue;
            string template = Locales.TryGetTranslation(format, out string localizedFormat)
                ? localizedFormat
                : format;
            try
            {
                return string.Format(template, value);
            }
            catch (FormatException)
            {
                return string.Format(format, value);
            }
        }
        return canonical;
    }

    public static void Apply(Text text, string english)
    {
        if (text.IsNullOrDestroyed())
            return;
        if (string.IsNullOrEmpty(english))
            entries.RemoveAll(e => e.Text == text);
        else
            Register(text, english);
        text.text = Translate(english);
    }

    public static void Apply(TMP_Text text, string english)
    {
        if (text.IsNullOrDestroyed())
            return;
        if (string.IsNullOrEmpty(english))
        {
            tmpEntries.RemoveAll(e => e.Text == text);
            text.text = english;
            return;
        }
        int index = tmpEntries.FindIndex(e => e.Text == text);
        if (index < 0)
            tmpEntries.Add((text, english));
        else
            tmpEntries[index] = (text, english);
        text.text = Translate(english);
    }

    public static void Register(Text text, string englishLabel)
    {
        if (text == null || string.IsNullOrEmpty(englishLabel))
            return;
        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i].Text == text)
            {
                entries[i] = (text, englishLabel);
                return;
            }
        }
        entries.Add((text, englishLabel));
    }

    // Legacy prefab labels must join the same canonical-key registry as runtime
    // controls before their first translation. Never use a translated caption as
    // the next locale's lookup key, and leave dynamic values/item names alone.
    public static void RefreshTree(GameObject root)
    {
        if (root.IsNullOrDestroyed())
            return;
        SweepDead();
        foreach (var text in root.GetComponentsInChildren<Text>(true))
        {
            int index = entries.FindIndex(e => e.Text == text);
            if (index >= 0)
                text.text = Translate(entries[index].EnglishLabel);
            else if (Locales.TryGetTranslation(text.text, out _))
                Apply(text, Locales.CanonicalKey(text.text));
        }
        foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
        {
            int index = tmpEntries.FindIndex(e => e.Text == text);
            if (index >= 0)
                text.text = Translate(tmpEntries[index].EnglishLabel);
            else if (Locales.TryGetTranslation(text.text, out _))
                Apply(text, Locales.CanonicalKey(text.text));
        }
    }

    public static void TickIfLocaleChanged()
    {
        var current = Locales.current_dictionary;
        if (ReferenceEquals(current, lastDict))
            return;
        lastDict = current;
        ReapplyAll();
    }

    public static void SweepDead()
    {
        for (int i = tmpEntries.Count - 1; i >= 0; i--)
            if (tmpEntries[i].Text.IsNullOrDestroyed())
                tmpEntries.RemoveAt(i);
        for (int i = entries.Count - 1; i >= 0; i--)
        {
            var t = entries[i].Text;
            if (t == null || t.IsNullOrDestroyed())
                entries.RemoveAt(i);
        }
    }

    private static void ReapplyAll()
    {
        for (int i = tmpEntries.Count - 1; i >= 0; i--)
        {
            var entry = tmpEntries[i];
            if (entry.Text.IsNullOrDestroyed())
            {
                tmpEntries.RemoveAt(i);
                continue;
            }
            entry.Text.text = Translate(entry.EnglishLabel);
        }
        for (int i = entries.Count - 1; i >= 0; i--)
        {
            var entry = entries[i];
            if (entry.Text == null || entry.Text.IsNullOrDestroyed())
            {
                entries.RemoveAt(i);
                continue;
            }
            entry.Text.text = Translate(entry.EnglishLabel);
        }
    }
}
