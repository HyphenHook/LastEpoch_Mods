using System;
using System.Collections.Generic;
using Il2Cpp;
using LastEpoch_Hud.Scripts.Core.CustomItems;
using LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter.Kills;
using LastEpoch_Hud.Scripts.ModUI;
using MelonLoader;

namespace LastEpoch_Hud.Scripts.Mods.Items.CustomItems.Headhunter.Kills;

/// <summary>Writes every monster prefix and suffix to a TSV once per session while Debug is on.</summary>
internal static class MonsterModDump
{
    private static readonly IntervalGate _gate = new(1.0);
    private static readonly CustomItemConfigStore _store = new("monster_mods.tsv");
    private static readonly OneShotAttempt _attempt = new();

    public static void Tick(double now)
    {
        if (_attempt.IsDone)
        {
            return;
        }

        if (!_gate.IsDue(now))
        {
            return;
        }

        if (!ModSettings.Debug.Enabled.Value)
        {
            return;
        }

        if (!IsGameReady())
        {
            return;
        }

        TryDump();
    }

    private static bool IsGameReady()
    {
        return Scenes.IsGameScene() && !Refs_Manager.player_actor.IsNullOrDestroyed();
    }

    private static void TryDump()
    {
        try
        {
            Dump();
        }
        catch (Exception ex)
        {
            ReportFailure(ex);
        }
    }

    private static void ReportFailure(Exception ex)
    {
        if (!_attempt.ShouldReportFailure())
        {
            return;
        }

        ErrorLog.Report(ex, "MonsterModDump");
    }

    private static void Dump()
    {
        var manager = MonsterRarityManager.getInstance();
        if (manager.IsNullOrDestroyed())
        {
            return;
        }

        List<MonsterModRow> rows = ReadRows(manager);
        if (rows.Count == 0)
        {
            return;
        }

        _attempt.MarkDone();
        _store.Write(MonsterModTsv.Build(rows));
        Main.logger_instance?.Msg(MonsterModDumpLog.Line(rows, _store.FilePath));
    }

    private static List<MonsterModRow> ReadRows(MonsterRarityManager manager)
    {
        var rows = new List<MonsterModRow>();
        AddRows(rows, MonsterModRow.PrefixList, manager.prefixList);
        AddRows(rows, MonsterModRow.SuffixList, manager.suffixList);
        return rows;
    }

    private static void AddRows(
        List<MonsterModRow> rows,
        string list,
        Il2CppSystem.Collections.Generic.List<MonsterModRef> refs
    )
    {
        if (refs == null)
        {
            return;
        }

        foreach (MonsterModRef modRef in refs)
        {
            rows.Add(ReadRow(list, modRef));
        }
    }

    private static MonsterModRow ReadRow(string list, MonsterModRef modRef)
    {
        MonsterMod mod = modRef.GetMonsterMod();
        if (mod.IsNullOrDestroyed())
        {
            return new MonsterModRow
            {
                List = list,
                ModKey = modRef.key,
                ClassName = "missing",
            };
        }

        var row = new MonsterModRow
        {
            List = list,
            ModKey = mod.modKey,
            Title = mod.title,
            ModType = mod.modType.ToString(),
            RarityRequirement = mod.rarityRequirement.ToString(),
            DisplayGroup = mod.displayGroup.ToString(),
            ClassName = mod.GetIl2CppType().Name,
        };
        FillStats(row, mod.TryCast<StatsMonsterMod>());
        return row;
    }

    private static void FillStats(MonsterModRow row, StatsMonsterMod statsMod)
    {
        if (statsMod.IsNullOrDestroyed())
        {
            return;
        }

        row.RareModifier = statsMod.rareModifier;
        row.ScalingType = statsMod.scalingType.ToString();
        row.Stats = ReadStats(statsMod.stats);
    }

    private static List<MonsterModStat> ReadStats(
        Il2CppSystem.Collections.Generic.List<Stats.Stat> stats
    )
    {
        var result = new List<MonsterModStat>();
        if (stats == null)
        {
            return result;
        }

        foreach (Stats.Stat stat in stats)
        {
            result.Add(ReadStat(stat));
        }

        return result;
    }

    private static MonsterModStat ReadStat(Stats.Stat stat)
    {
        var more = new List<float>();
        if (stat.moreValues != null)
        {
            foreach (float value in stat.moreValues)
            {
                more.Add(value);
            }
        }

        return new MonsterModStat(
            stat.property.ToString(),
            stat.addedValue,
            stat.increasedValue,
            more,
            stat.tags.ToString()
        );
    }
}
