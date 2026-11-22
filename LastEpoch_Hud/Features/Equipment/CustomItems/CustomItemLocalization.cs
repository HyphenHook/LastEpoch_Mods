using LastEpoch_Hud.Scripts.Core.CustomItems;

namespace LastEpoch_Hud.Scripts.Mods.Items.CustomItems;

/// <summary>Shared text table for custom items, resolved against the current mod locale.</summary>
public static class CustomItemLocalization
{
    public static CustomItemTextTable Table { get; } = new();

    public static string Resolve(string gameKey)
    {
        return Table.Resolve(gameKey, Locales.current_dictionary);
    }

    public static string Text(string gameKey)
    {
        return Resolve(gameKey) ?? "";
    }
}
