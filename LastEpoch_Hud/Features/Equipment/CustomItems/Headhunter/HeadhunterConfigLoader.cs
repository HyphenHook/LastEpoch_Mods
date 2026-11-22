using System;
using System.Collections.Generic;
using Il2Cpp;
using LastEpoch_Hud.Scripts.Core.CustomItems;
using LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter.Buffs;
using LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter.Config;
using LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter.Config.Defaults;
using LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter.Config.Json;
using LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter.Config.Resolve;
using LastEpoch_Hud.Scripts.Mods.Items.CustomItems.Headhunter.Bar;
using LastEpoch_Hud.Scripts.Mods.Items.CustomItems.Headhunter.Buffs;

namespace LastEpoch_Hud.Scripts.Mods.Items.CustomItems.Headhunter;

/// <summary>Exports the default Headhunter config when missing, then reads and holds it.</summary>
internal static class HeadhunterConfigLoader
{
    private static readonly CustomItemConfigStore _store = new("headhunter.json");

    private static readonly Dictionary<string, int> _statIds = EnumIdMap.Build(typeof(SP));
    private static readonly Dictionary<string, int> _tagIds = EnumIdMap.Build(typeof(AT));

    private static readonly HashSet<string> _knownStats = new(
        _statIds.Keys,
        StringComparer.Ordinal
    );

    private static readonly ConfigChangeDetector _changes = new(1.0);
    private static readonly HeadhunterSystemRandom _random = new();

    private static string _appliedText;

    public static HeadhunterConfig Current { get; private set; } = HeadhunterConfigDefaults.Config;
    public static HeadhunterResolvedConfig Resolved { get; private set; }
    public static HeadhunterStackState Stacks { get; private set; }
    public static RareModsMechanic Mechanic { get; private set; }

    public static void Load()
    {
        ExportDefaultsIfMissing();
        string text = MergeNewDefaults(_store.Read());
        _appliedText = text;
        HeadhunterConfigParseResult result = HeadhunterConfigParser.Parse(text, _knownStats);
        LogProblems(result.Problems);
        Current = result.Config;
        ResolveAndCreateMechanic();
        Main.logger_instance?.Msg(
            "Headhunter config loaded: "
                + Current.Stats.Count
                + " stat(s), "
                + Resolved.AffixCount
                + " affix mapping(s) from "
                + _store.FilePath
        );
        _changes.Remember(_store.LastWriteUtc());
    }

    public static void ReloadIfChanged(double now)
    {
        if (!_changes.IsCheckDue(now))
        {
            return;
        }
        if (!_changes.HasChanged(_store.LastWriteUtc()))
        {
            return;
        }

        Reload();
    }

    private static void Reload()
    {
        string text = _store.Read();
        HeadhunterConfigParseResult result = Parse(text);
        LogProblems(result.Problems);
        if (!result.IsReadable)
        {
            return;
        }

        HeadhunterReloadKind kind = HeadhunterReloadClassifier.Classify(_appliedText, text);
        _appliedText = text;
        if (kind == HeadhunterReloadKind.VisualOnly)
        {
            ApplyVisualOnly(result.Config);
            return;
        }

        HeadhunterBuffClearer.ClearAll(Resolved);
        Current = result.Config;
        ResolveAndCreateMechanic();
        Main.logger_instance?.Msg(
            "Headhunter config reloaded: "
                + Current.Stats.Count
                + " stat(s), "
                + Resolved.AffixCount
                + " affix mapping(s)"
        );
    }

    /// <summary>Swaps in a config whose only change is bar placement or aura; buffs stay.</summary>
    private static void ApplyVisualOnly(HeadhunterConfig config)
    {
        Current = config;
        HeadhunterBuffBar.MarkDirty();
        Main.logger_instance?.Msg("Headhunter config reloaded: visuals only");
    }

    private static HeadhunterConfigParseResult Parse(string text)
    {
        return HeadhunterConfigParser.Parse(text, _knownStats);
    }

    private static void ExportDefaultsIfMissing()
    {
        if (_store.Exists())
        {
            return;
        }
        _store.Write(HeadhunterConfigWriter.Write(HeadhunterConfigDefaults.Config));
    }

    private static string MergeNewDefaults(string text)
    {
        HeadhunterMergeResult merge = HeadhunterConfigMerger.Merge(
            text,
            HeadhunterConfigDefaults.MergeDefaults
        );
        if (!merge.Changed)
        {
            return text;
        }
        _store.Write(merge.Text);
        Main.logger_instance?.Msg(
            "Headhunter config: merged " + merge.Added + " new default(s) into " + _store.FilePath
        );
        return merge.Text;
    }

    private static void ResolveAndCreateMechanic()
    {
        var problems = new List<HeadhunterConfigProblem>();
        HeadhunterResolvedConfig resolved = HeadhunterConfigResolver.Resolve(
            Current,
            _statIds,
            _tagIds,
            problems
        );
        Stacks = new HeadhunterStackState(resolved.Stats.Count);
        Mechanic = new RareModsMechanic(resolved, Stacks, _random);
        Resolved = resolved;
        LogProblems(problems);
    }

    private static void LogProblems(IReadOnlyList<HeadhunterConfigProblem> problems)
    {
        foreach (HeadhunterConfigProblem problem in problems)
        {
            Main.logger_instance?.Warning(
                "Headhunter config: " + problem.Path + ": " + problem.Message
            );
        }
    }
}
