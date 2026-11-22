namespace LastEpoch_Hud.Scripts.Core.CustomItems.Mageblood.Menu;

/// <summary>One editable number in the config file.</summary>
public readonly record struct MagebloodValueTarget(
    MagebloodValueKind Kind,
    string Flask,
    string RowText,
    MagebloodStatField Field
)
{
    public static MagebloodValueTarget ForMaxResistances()
    {
        return new MagebloodValueTarget(
            MagebloodValueKind.MaxResistances,
            null,
            null,
            MagebloodStatField.Added
        );
    }

    public static MagebloodValueTarget ForStat(
        string flask,
        string rowText,
        MagebloodStatField field
    )
    {
        return new MagebloodValueTarget(MagebloodValueKind.StatValue, flask, rowText, field);
    }
}
