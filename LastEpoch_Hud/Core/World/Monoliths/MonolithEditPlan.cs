using System.Collections.Generic;

namespace LastEpoch_Hud.Scripts.Core.Monoliths;

/// <summary>Builds the edits of one editor action.</summary>
public static class MonolithEditPlan
{
    private static readonly MonolithField[] _fields =
    {
        MonolithField.BasicStability,
        MonolithField.EmpoweredStability,
        MonolithField.Corruption,
        MonolithField.Gaze,
    };

    /// <summary>One edit on the first run of the timeline that holds the field; none when absent.</summary>
    public static IReadOnlyList<MonolithEdit> ForValue(
        IReadOnlyList<MonolithRunSnapshot> runs,
        int timelineId,
        MonolithField field,
        int value
    )
    {
        List<MonolithEdit> edits = new();
        if (timelineId < 1)
        {
            return edits;
        }

        for (int i = 0; i < runs.Count; i++)
        {
            if (runs[i].TimelineId != timelineId || !MonolithFields.AppliesTo(field, runs[i]))
            {
                continue;
            }

            edits.Add(new MonolithEdit(i, timelineId, field, value));
            return edits;
        }

        return edits;
    }

    /// <summary>Copies every value the source timeline shows to each run that holds the field.</summary>
    public static IReadOnlyList<MonolithEdit> CopyToAll(
        IReadOnlyList<MonolithRunSnapshot> runs,
        int sourceTimelineId
    )
    {
        List<MonolithEdit> edits = new();
        var source = MonolithTimelineView.Of(runs, sourceTimelineId);
        for (int i = 0; i < runs.Count; i++)
        {
            AddRunEdits(edits, source, runs[i], i);
        }

        return edits;
    }

    /// <summary>Highest of the current max and every corruption edit.</summary>
    public static int RaisedMaxCorruption(int currentMax, IReadOnlyList<MonolithEdit> edits)
    {
        int max = currentMax;
        for (int i = 0; i < edits.Count; i++)
        {
            if (edits[i].Field == MonolithField.Corruption && edits[i].Value > max)
            {
                max = edits[i].Value;
            }
        }

        return max;
    }

    /// <summary>True when some edit targets the timeline on that difficulty.</summary>
    public static bool Touches(IReadOnlyList<MonolithEdit> edits, int timelineId, int difficulty)
    {
        for (int i = 0; i < edits.Count; i++)
        {
            if (
                edits[i].TimelineId == timelineId
                && MonolithFields.DifficultyOf(edits[i].Field) == difficulty
            )
            {
                return true;
            }
        }

        return false;
    }

    private static void AddRunEdits(
        List<MonolithEdit> edits,
        MonolithTimelineView source,
        MonolithRunSnapshot run,
        int runIndex
    )
    {
        foreach (MonolithField field in _fields)
        {
            if (!source.Shows(field) || !MonolithFields.AppliesTo(field, run))
            {
                continue;
            }

            edits.Add(new MonolithEdit(runIndex, run.TimelineId, field, source.ValueOf(field)));
        }
    }
}
