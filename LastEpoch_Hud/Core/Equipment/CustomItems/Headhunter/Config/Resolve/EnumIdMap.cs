using System;
using System.Collections.Generic;

namespace LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter.Config.Resolve;

/// <summary>Maps every name of an enum to its numeric value, whatever the underlying type.</summary>
public static class EnumIdMap
{
    public static Dictionary<string, int> Build(Type enumType)
    {
        string[] names = Enum.GetNames(enumType);
        var ids = new Dictionary<string, int>(names.Length, StringComparer.Ordinal);
        foreach (string name in names)
        {
            ids[name] = Convert.ToInt32(
                Enum.Parse(enumType, name),
                System.Globalization.CultureInfo.InvariantCulture
            );
        }

        return ids;
    }
}
