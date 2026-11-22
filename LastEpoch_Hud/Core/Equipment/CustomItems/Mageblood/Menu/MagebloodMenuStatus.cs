namespace LastEpoch_Hud.Scripts.Core.CustomItems.Mageblood.Menu;

/// <summary>What the Mageblood card shows.</summary>
public readonly record struct MagebloodMenuStatus(
    bool Worn,
    int ActiveFlasks,
    MagebloodFileWarning Warning,
    bool Editable
)
{
    public static MagebloodMenuStatus From(
        bool readable,
        int problemCount,
        bool worn,
        int activeFlasks
    )
    {
        int active = worn && activeFlasks > 0 ? activeFlasks : 0;
        return new MagebloodMenuStatus(worn, active, WarningFor(readable, problemCount), readable);
    }

    /// <summary>True when a belt is worn and this slot is past its flask count.</summary>
    public bool IsSlotInactive(int slot)
    {
        return Worn && slot >= ActiveFlasks;
    }

    /// <summary>True when the file is readable and the value's unit is known.</summary>
    public bool CanEdit(bool unitKnown)
    {
        return Editable && unitKnown;
    }

    private static MagebloodFileWarning WarningFor(bool readable, int problemCount)
    {
        if (!readable)
        {
            return MagebloodFileWarning.FileError;
        }

        return problemCount > 0 ? MagebloodFileWarning.SomeSkipped : MagebloodFileWarning.None;
    }
}
