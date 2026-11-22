namespace LastEpoch_Hud.Scripts.Core.Character;

/// <summary>Decides whether the Masteries button resets the chosen mastery or opens the mastery panel.</summary>
public static class MasteryButtonRule
{
    public static bool ShouldReset(int chosenMastery)
    {
        return chosenMastery > 0;
    }
}
