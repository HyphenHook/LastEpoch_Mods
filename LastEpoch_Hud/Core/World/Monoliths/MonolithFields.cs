namespace LastEpoch_Hud.Scripts.Core.Monoliths;

/// <summary>Which run a monolith field lives on.</summary>
public static class MonolithFields
{
    public const int BasicDifficulty = 0;
    public const int EmpoweredDifficulty = 1;

    /// <summary>Difficulty of the run that holds the field.</summary>
    public static int DifficultyOf(MonolithField field)
    {
        return field == MonolithField.BasicStability ? BasicDifficulty : EmpoweredDifficulty;
    }

    /// <summary>True when the run holds the field; Corruption and Gaze also need an echo web.</summary>
    public static bool AppliesTo(MonolithField field, MonolithRunSnapshot run)
    {
        if (run.Difficulty != DifficultyOf(field))
        {
            return false;
        }

        bool needsWeb = field == MonolithField.Corruption || field == MonolithField.Gaze;
        return !needsWeb || run.HasEchoWeb;
    }
}
