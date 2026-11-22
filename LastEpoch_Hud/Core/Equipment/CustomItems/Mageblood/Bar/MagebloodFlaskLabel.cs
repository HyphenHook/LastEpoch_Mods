using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace LastEpoch_Hud.Scripts.Core.CustomItems.Mageblood.Bar;

/// <summary>Builds the hover text of a flask: its name, then one line per stat.</summary>
public static class MagebloodFlaskLabel
{
    public static string Format(string flaskName, IReadOnlyList<string> rows)
    {
        var text = new StringBuilder(flaskName);
        if (rows == null)
        {
            return text.ToString();
        }
        foreach (string row in rows)
        {
            if (!string.IsNullOrWhiteSpace(row))
            {
                text.Append('\n').Append(row);
            }
        }
        return text.ToString();
    }

    public static string WithMore(string row, float more)
    {
        if (more == 0f)
        {
            return row;
        }
        float factor = 1f + more;
        return row + " x" + factor.ToString("0.##", CultureInfo.InvariantCulture);
    }
}
