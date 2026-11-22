using System;

namespace LastEpoch_Hud.Scripts.Core.CustomItems;

/// <summary>Fills the {n} placeholders of a locale template.</summary>
public static class TextTemplate
{
    public static string Fill(string template, params object[] values)
    {
        if (string.IsNullOrEmpty(template))
        {
            return null;
        }

        try
        {
            return string.Format(template, values);
        }
        catch (FormatException)
        {
            return null;
        }
    }
}
