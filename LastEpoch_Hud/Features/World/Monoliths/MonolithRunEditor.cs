using System;
using System.Collections.Generic;
using Il2Cpp;
using Il2CppLE.Data;
using LastEpoch_Hud.Scripts.Core.Monoliths;
using UnityEngine;

namespace LastEpoch_Hud.Scripts.Mods.Monoliths;

/// <summary>Applies monolith edits to the current character's saved and live runs.</summary>
internal static class MonolithRunEditor
{
    /// <summary>What the editor shows for a timeline; nothing without player data.</summary>
    public static MonolithTimelineView ViewOf(int timelineId)
    {
        CharacterData data = Refs_Manager.player_data;
        if (data.IsNullOrDestroyed())
        {
            return default;
        }

        return MonolithTimelineView.Of(Snapshot(data), timelineId);
    }

    /// <summary>Writes one value to the first matching run of the timeline.</summary>
    public static void SetValue(int timelineId, MonolithField field, int value)
    {
        CharacterData data = Refs_Manager.player_data;
        if (data.IsNullOrDestroyed())
        {
            return;
        }

        IReadOnlyList<MonolithEdit> edits = MonolithEditPlan.ForValue(
            Snapshot(data),
            timelineId,
            field,
            value
        );
        Apply(data, edits, "Monoliths.SetValue");
    }

    /// <summary>Copies the values the source timeline shows to every run that holds them.</summary>
    public static void CopyToAll(int sourceTimelineId)
    {
        CharacterData data = Refs_Manager.player_data;
        if (data.IsNullOrDestroyed())
        {
            return;
        }

        IReadOnlyList<MonolithEdit> edits = MonolithEditPlan.CopyToAll(
            Snapshot(data),
            sourceTimelineId
        );
        Apply(data, edits, "Monoliths.CopyToAll");
        Main.logger_instance?.Msg("Copied selected values to " + edits.Count + " run values");
    }

    /// <summary>First timeline to select: the first saved run's, else the first option.</summary>
    public static int InitialTimelineId()
    {
        CharacterData data = Refs_Manager.player_data;
        if (data.IsNullOrDestroyed())
        {
            return MonolithTimelineSelection.Initial(Array.Empty<MonolithRunSnapshot>());
        }

        return MonolithTimelineSelection.Initial(Snapshot(data));
    }

    private static List<MonolithRunSnapshot> Snapshot(CharacterData data)
    {
        List<MonolithRunSnapshot> runs = new();
        foreach (SavedMonolithRun run in data.MonolithRuns)
        {
            runs.Add(SnapshotOf(run));
        }

        return runs;
    }

    private static MonolithRunSnapshot SnapshotOf(SavedMonolithRun run)
    {
        if (run.IsNullOrDestroyed())
        {
            return new MonolithRunSnapshot(-1, -1, 0, false, 0, 0);
        }

        if (run.SavedEchoWeb.IsNullOrDestroyed())
        {
            return new MonolithRunSnapshot(
                run.TimelineID,
                run.DifficultyIndex,
                run.Stability,
                false,
                0,
                0
            );
        }

        return new MonolithRunSnapshot(
            run.TimelineID,
            run.DifficultyIndex,
            run.Stability,
            true,
            run.SavedEchoWeb.Corruption,
            run.SavedEchoWeb.GazeOfOrobyss
        );
    }

    private static void Apply(CharacterData data, IReadOnlyList<MonolithEdit> edits, string context)
    {
        if (edits.Count == 0)
        {
            return;
        }

        try
        {
            foreach (MonolithEdit edit in edits)
            {
                WriteSaved(data.MonolithRuns[edit.RunIndex], edit);
            }

            data.MaxCorruption = MonolithEditPlan.RaisedMaxCorruption(data.MaxCorruption, edits);
            ApplyLive(edits);
        }
        catch (Exception ex)
        {
            ErrorLog.Report(ex, context);
        }
    }

    private static void WriteSaved(SavedMonolithRun run, MonolithEdit edit)
    {
        switch (edit.Field)
        {
            case MonolithField.Corruption:
                run.SavedEchoWeb.Corruption = edit.Value;
                return;
            case MonolithField.Gaze:
                run.SavedEchoWeb.GazeOfOrobyss = edit.Value;
                return;
            default:
                run.Stability = edit.Value;
                return;
        }
    }

    private static void ApplyLive(IReadOnlyList<MonolithEdit> edits)
    {
        if (Refs_Manager.player_actor.IsNullOrDestroyed())
        {
            return;
        }

        MonolithRunsManager manager = Refs_Manager.player_actor.monolithRunsManager;
        if (manager.IsNullOrDestroyed())
        {
            return;
        }

        EnsureLiveRunsLoaded(manager, edits);
        bool written = false;
        foreach (MonolithEdit edit in edits)
        {
            if (!manager.TryGetRun(LiveId(edit), LiveDifficulty(edit), out MonolithRun run))
            {
                continue;
            }

            written |= WriteLive(run, edit);
        }

        SyncCorruption(manager, edits);
        if (written)
        {
            manager.saveRuns();
        }

        RefreshOpenPanel(manager, edits);
    }

    private static TimelineID LiveId(MonolithEdit edit)
    {
        return (TimelineID)edit.TimelineId;
    }

    private static int LiveDifficulty(MonolithEdit edit)
    {
        return MonolithFields.DifficultyOf(edit.Field);
    }

    private static void EnsureLiveRunsLoaded(
        MonolithRunsManager manager,
        IReadOnlyList<MonolithEdit> edits
    )
    {
        foreach (MonolithEdit edit in edits)
        {
            if (manager.TryGetRun(LiveId(edit), LiveDifficulty(edit), out MonolithRun _))
            {
                continue;
            }

            manager.loadRuns();
            return;
        }
    }

    private static bool WriteLive(MonolithRun run, MonolithEdit edit)
    {
        if (run.IsNullOrDestroyed())
        {
            return false;
        }

        switch (edit.Field)
        {
            case MonolithField.Corruption:
                return WriteLiveCorruption(run, edit.Value);
            case MonolithField.Gaze:
                return WriteLiveGaze(run, edit.Value);
            default:
                run.stability = edit.Value;
                return true;
        }
    }

    private static bool WriteLiveCorruption(MonolithRun run, int value)
    {
        if (run.web.IsNullOrDestroyed())
        {
            return false;
        }

        run.web.corruption = value;
        run.updateCorruptionMod();
        return true;
    }

    private static bool WriteLiveGaze(MonolithRun run, int value)
    {
        if (run.web.IsNullOrDestroyed())
        {
            return false;
        }

        run.web.gazeOfOrobyss = value;
        return true;
    }

    private static void SyncCorruption(
        MonolithRunsManager manager,
        IReadOnlyList<MonolithEdit> edits
    )
    {
        LocalPlayer localPlayer = Refs_Manager.player_actor.GetComponent<LocalPlayer>();
        bool synced = false;
        foreach (MonolithEdit edit in edits)
        {
            if (edit.Field != MonolithField.Corruption)
            {
                continue;
            }

            if (!synced)
            {
                UpdateHighestCorruption(manager);
                synced = true;
            }

            UpdateProgress(localPlayer, edit);
        }
    }

    private static void UpdateHighestCorruption(MonolithRunsManager manager)
    {
        try
        {
            manager.updateHighestCorruptionOfAnyRun();
        }
        catch (Exception ex)
        {
            ErrorLog.Report(ex, "Monoliths.updateHighestCorruptionOfAnyRun");
        }
    }

    private static void UpdateProgress(LocalPlayer localPlayer, MonolithEdit edit)
    {
        if (
            localPlayer.IsNullOrDestroyed()
            || localPlayer.monolithProgressManager.IsNullOrDestroyed()
        )
        {
            return;
        }

        localPlayer.monolithProgressManager.UpdateCorruption(LiveId(edit), edit.Value);
    }

    private static void RefreshOpenPanel(
        MonolithRunsManager manager,
        IReadOnlyList<MonolithEdit> edits
    )
    {
        MonolithTimelinePanelManager panel =
            UnityEngine.Object.FindObjectOfType<MonolithTimelinePanelManager>();
        if (panel.IsNullOrDestroyed() || panel.run.IsNullOrDestroyed())
        {
            return;
        }

        int timelineId = (int)panel.run.timelineID;
        int difficulty = panel.run.difficultyIndex;
        if (!MonolithEditPlan.Touches(edits, timelineId, difficulty))
        {
            return;
        }

        if (!manager.TryGetRun(panel.run.timelineID, difficulty, out MonolithRun run))
        {
            return;
        }

        panel.SetRunInfo(run, true, false, -1);
    }
}
