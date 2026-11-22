using System.Collections.Generic;
using Il2Cpp;
using LastEpoch_Hud.Scripts.Core.CustomItems;
using LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter.Config.Resolve;
using LastEpoch_Hud.Scripts.Core.CustomItems.Mageblood.Config;

namespace LastEpoch_Hud.Scripts.Mods.Items.CustomItems.Mageblood;

/// <summary>Exports the default Mageblood config when missing, then reads and holds it.</summary>
internal static class MagebloodConfigLoader
{
    private static readonly CustomItemConfigStore _store = new("mageblood.json");

    private static readonly Dictionary<string, int> _statIds = EnumIdMap.Build(typeof(SP));
    private static readonly Dictionary<string, int> _tagIds = EnumIdMap.Build(typeof(AT));

    private static readonly ConfigChangeDetector _changes = new(1.0);

    /// <summary>Counts every read of the file, so a card can tell its view is stale.</summary>
    public static int Version { get; private set; }
    public static bool IsReadable { get; private set; } = true;

    /// <summary>Parser plus resolver problems of the last read.</summary>
    public static int ProblemCount { get; private set; }
    public static CustomItemConfigStore Store => _store;

    public static MagebloodConfig Current { get; private set; } = MagebloodConfigDefaults.Config;
    public static IReadOnlyList<MagebloodFlask> Flasks { get; private set; } =
        new List<MagebloodFlask>();

    public static void Load()
    {
        ExportDefaultsIfMissing();
        string text = MergeNewDefaults(_store.Read());
        MagebloodConfigParseResult result = MagebloodConfigParser.Parse(text);
        LogProblems(result.Problems);
        RecordRead(result.IsReadable, result.Problems.Count + Resolve(result.Config));
        Main.logger_instance?.Msg(
            "Mageblood config loaded: " + Flasks.Count + " flask(s) from " + _store.FilePath
        );
        _changes.Remember(_store.LastWriteUtc());
    }

    /// <summary>Makes the next <see cref="ReloadIfChanged"/> read the file, even with the same stamp.</summary>
    public static void RequestReload()
    {
        _changes.ForceNext();
    }

    /// <summary>True when the flasks were replaced.</summary>
    public static bool ReloadIfChanged(double now)
    {
        if (!_changes.IsCheckDue(now))
        {
            return false;
        }
        if (!_changes.HasChanged(_store.LastWriteUtc()))
        {
            return false;
        }

        return Reload();
    }

    private static bool Reload()
    {
        MagebloodConfigParseResult result = MagebloodConfigParser.Parse(_store.Read());
        LogProblems(result.Problems);
        if (!result.IsReadable)
        {
            RecordRead(false, result.Problems.Count);
            return false;
        }

        RecordRead(true, result.Problems.Count + Resolve(result.Config));
        Main.logger_instance?.Msg("Mageblood config reloaded: " + Flasks.Count + " flask(s)");
        return true;
    }

    private static void ExportDefaultsIfMissing()
    {
        if (_store.Exists())
        {
            return;
        }
        _store.Write(MagebloodConfigWriter.Write(MagebloodConfigDefaults.Config));
    }

    private static string MergeNewDefaults(string text)
    {
        MagebloodMergeResult merge = MagebloodConfigMerger.Merge(
            text,
            MagebloodConfigDefaults.VersionedFlasks,
            MagebloodConfigDefaults.MaxResistances,
            MagebloodConfigDefaults.DefaultsVersion
        );
        if (!merge.Changed)
        {
            return text;
        }
        _store.Write(merge.Text);
        Main.logger_instance?.Msg(
            "Mageblood config: merged " + merge.Added + " new default(s) into " + _store.FilePath
        );
        return merge.Text;
    }

    private static void RecordRead(bool readable, int problemCount)
    {
        IsReadable = readable;
        ProblemCount = problemCount;
        Version++;
    }

    /// <summary>Returns the number of resolver problems.</summary>
    private static int Resolve(MagebloodConfig config)
    {
        var problems = new List<MagebloodConfigProblem>();
        Flasks = MagebloodConfigResolver.Resolve(config, _statIds, _tagIds, problems);
        Current = config;
        LogProblems(problems);
        return problems.Count;
    }

    private static void LogProblems(IReadOnlyList<MagebloodConfigProblem> problems)
    {
        foreach (MagebloodConfigProblem problem in problems)
        {
            Main.logger_instance?.Warning(
                "Mageblood config: " + problem.Path + ": " + problem.Message
            );
        }
    }
}
