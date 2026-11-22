using System;

namespace LastEpoch_Hud.Scripts.Core.CustomItems.Mageblood.Bar;

/// <summary>Built-in Headhunter buff icon for a flask whose stats have no matching icon.</summary>
public static class MagebloodDefaultIcons
{
    public static string BuffIconFor(string flaskName)
    {
        return string.Equals(flaskName, "Basalt", StringComparison.OrdinalIgnoreCase)
            ? "Armour"
            : null;
    }
}
