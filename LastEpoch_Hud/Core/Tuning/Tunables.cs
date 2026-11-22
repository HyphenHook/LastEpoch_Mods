using System;
using System.Collections.Generic;
using System.Globalization;
using LastEpoch_Hud.Scripts.Core.IO;

namespace LastEpoch_Hud.Scripts.Core.Tuning;

/// <summary>
/// The adjustable numbers of the crafting features (chances, costs, ranges), read from Data/CraftingNumbers.csv, which
/// is embedded in the mod at compile time. Every caller passes its built-in default, so a number missing from the file
/// keeps its default.
/// </summary>
public static class Tunables
{
    private static IReadOnlyDictionary<string, double> values;
    private static readonly Dictionary<string, double[]> arrays = new();
    private static readonly object gate = new();
    [ThreadStatic] private static IReadOnlyDictionary<string, double> scoped;

    public static void Set(IReadOnlyDictionary<string, double> table)
    {
        lock (gate)
        {
            values = table;
            arrays.Clear();
        }
    }

    /// <summary>Overrides the numbers for the current thread only, until disposed. Used by tests.</summary>
    public static IDisposable Scope(IReadOnlyDictionary<string, double> table)
    {
        var previous = scoped;
        scoped = table;
        return new Restore(previous);
    }

    private sealed class Restore : IDisposable
    {
        private readonly IReadOnlyDictionary<string, double> previous;
        public Restore(IReadOnlyDictionary<string, double> previous) => this.previous = previous;
        public void Dispose() => scoped = previous;
    }

    private static bool TryGet(string name, out double value)
    {
        var local = scoped;
        if (local != null && local.TryGetValue(name, out value) && double.IsFinite(value)) return true;
        var global = values;
        if (global != null && global.TryGetValue(name, out value) && double.IsFinite(value)) return true;
        value = 0;
        return false;
    }

    public static double Get(string name, double fallback) => TryGet(name, out double value) ? value : fallback;
    public static int GetInt(string name, int fallback) => (int)Math.Round(Get(name, fallback));
    public static long GetLong(string name, long fallback) => (long)Math.Round(Get(name, fallback));

    /// <summary>A tier-indexed array whose slot 0 is unused: slot i comes from the number named "prefix.i".</summary>
    public static double[] Tiers(string prefix, double[] fallback) => Build(prefix, fallback, 1, 0);

    /// <summary>A zero-based list: slot i comes from the number named "prefix.(i+1)".</summary>
    public static double[] Series(string prefix, double[] fallback) => Build(prefix, fallback, 0, 1);

    private static double[] Build(string prefix, double[] fallback, int first, int nameOffset)
    {
        if (scoped != null) return Make(prefix, fallback, first, nameOffset);
        lock (gate)
        {
            if (!arrays.TryGetValue(prefix, out var cached))
                arrays[prefix] = cached = Make(prefix, fallback, first, nameOffset);
            return cached;
        }
    }

    private static double[] Make(string prefix, double[] fallback, int first, int nameOffset)
    {
        var result = (double[])fallback.Clone();
        for (int i = first; i < result.Length; i++)
            result[i] = Get(prefix + "." + (i + nameOffset).ToString(CultureInfo.InvariantCulture), fallback[i]);
        return result;
    }
}

/// <summary>Reads CraftingNumbers.csv (Name,Value,Note).</summary>
public static class TunablesTable
{
    public static readonly string[] Columns = { "Name", "Value", "Note" };

    public static Dictionary<string, double> Parse(string text, List<string> errors)
    {
        var table = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        bool header = false;
        foreach (var (number, line) in CsvText.DataLines(text))
        {
            var f = CsvText.SplitFields(line);
            if (!header)
            {
                header = true;
                if (f.Count != Columns.Length || !string.Equals(f[0], "Name", StringComparison.OrdinalIgnoreCase))
                {
                    errors?.Add("CraftingNumbers.csv line " + number + ": the header row is missing or has the wrong columns.");
                    return table;
                }
                continue;
            }
            if (f.Count != Columns.Length) { errors?.Add("CraftingNumbers.csv line " + number + ": expected " + Columns.Length + " columns."); continue; }
            string name = f[0].Trim();
            if (name.Length == 0 || !double.TryParse(f[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double value) || !double.IsFinite(value))
            {
                errors?.Add("CraftingNumbers.csv line " + number + ": needs a Name and a numeric Value.");
                continue;
            }
            if (!table.TryAdd(name, value)) errors?.Add("CraftingNumbers.csv line " + number + ": " + name + " appears twice; the first one is used.");
        }
        return table;
    }
}
