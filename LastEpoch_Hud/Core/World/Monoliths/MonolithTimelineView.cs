using System.Collections.Generic;

namespace LastEpoch_Hud.Scripts.Core.Monoliths;

/// <summary>What the editor shows for one timeline; default shows nothing.</summary>
public readonly record struct MonolithTimelineView(
    bool HasBasic,
    int BasicStability,
    bool HasEmpowered,
    int EmpoweredStability,
    bool HasEchoWeb,
    int Corruption,
    int Gaze
)
{
    /// <summary>Value of a field that is not shown.</summary>
    public const int Hidden = -1;

    /// <summary>True when the timeline has a saved run.</summary>
    public bool HasRun => HasBasic || HasEmpowered;

    /// <summary>View of the first saved basic and empowered runs of a timeline.</summary>
    public static MonolithTimelineView Of(IReadOnlyList<MonolithRunSnapshot> runs, int timelineId)
    {
        if (timelineId < 1)
        {
            return default;
        }

        bool hasBasic = TryFirst(
            runs,
            timelineId,
            MonolithFields.BasicDifficulty,
            out MonolithRunSnapshot basic
        );
        bool hasEmpowered = TryFirst(
            runs,
            timelineId,
            MonolithFields.EmpoweredDifficulty,
            out MonolithRunSnapshot empowered
        );
        bool web = hasEmpowered && empowered.HasEchoWeb;
        return new MonolithTimelineView(
            hasBasic,
            basic.Stability,
            hasEmpowered,
            empowered.Stability,
            web,
            empowered.Corruption,
            empowered.Gaze
        );
    }

    /// <summary>True when the editor shows the field.</summary>
    public bool Shows(MonolithField field)
    {
        switch (field)
        {
            case MonolithField.BasicStability:
                return HasBasic;
            case MonolithField.EmpoweredStability:
                return HasEmpowered;
            default:
                return HasEchoWeb;
        }
    }

    /// <summary>Shown value, or Hidden.</summary>
    public int ValueOf(MonolithField field)
    {
        if (!Shows(field))
        {
            return Hidden;
        }

        switch (field)
        {
            case MonolithField.BasicStability:
                return BasicStability;
            case MonolithField.EmpoweredStability:
                return EmpoweredStability;
            case MonolithField.Corruption:
                return Corruption;
            default:
                return Gaze;
        }
    }

    private static bool TryFirst(
        IReadOnlyList<MonolithRunSnapshot> runs,
        int timelineId,
        int difficulty,
        out MonolithRunSnapshot found
    )
    {
        for (int i = 0; i < runs.Count; i++)
        {
            if (runs[i].TimelineId != timelineId || runs[i].Difficulty != difficulty)
            {
                continue;
            }

            found = runs[i];
            return true;
        }

        found = default;
        return false;
    }
}
