using System;
using LastEpoch_Hud.Scripts.Core.CustomItems.Mageblood.Config;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LastEpoch_Hud.Scripts.Core.CustomItems.Mageblood.Menu;

/// <summary>In-place edits of the Mageblood config text. Every other token stays as it was.</summary>
public static class MagebloodFileEditor
{
    public static bool TrySetValue(
        string json,
        MagebloodValueTarget target,
        float fileValue,
        out string text
    )
    {
        text = null;
        if (!float.IsFinite(fileValue) || !ConfigJson.TryParseObject(json, out JObject root))
        {
            return false;
        }
        if (!TryApply(root, target, fileValue))
        {
            return false;
        }
        text = root.ToString(Formatting.Indented);
        return true;
    }

    public static bool TrySwapFlasks(string json, string first, string second, out string text)
    {
        text = null;
        if (string.Equals(first, second, StringComparison.Ordinal))
        {
            return false;
        }
        if (!ConfigJson.TryParseObject(json, out JObject root))
        {
            return false;
        }
        JArray list = FlaskList(root);
        JObject a = list == null ? null : MagebloodConfigParser.FindFlask(list, first);
        JObject b = list == null ? null : MagebloodConfigParser.FindFlask(list, second);
        if (a == null || b == null)
        {
            return false;
        }

        int i = list.IndexOf(a);
        int j = list.IndexOf(b);
        list[i] = b;
        list[j] = a;
        if (!StillKept(list, first, j) || !StillKept(list, second, i))
        {
            return false;
        }
        text = root.ToString(Formatting.Indented);
        return true;
    }

    private static bool StillKept(JArray list, string name, int index)
    {
        JObject kept = MagebloodConfigParser.FindFlask(list, name);
        return kept != null && list.IndexOf(kept) == index;
    }

    private static bool TryApply(JObject root, MagebloodValueTarget target, float value)
    {
        return target.Kind switch
        {
            MagebloodValueKind.MaxResistances => TrySetMaxResistances(root, value),
            MagebloodValueKind.StatValue => TrySetStat(root, target, value),
            _ => false,
        };
    }

    private static bool TrySetMaxResistances(JObject root, float value)
    {
        if (value < 0f)
        {
            return false;
        }
        root[MagebloodConfigKeys.MaxResistances] = value;
        return true;
    }

    private static bool TrySetStat(JObject root, MagebloodValueTarget target, float value)
    {
        string key = FieldKey(target.Field);
        JArray flasks = FlaskList(root);
        if (value == 0f || key == null || flasks == null)
        {
            return false;
        }
        JObject flask = MagebloodConfigParser.FindFlask(flasks, target.Flask);
        if (flask?[MagebloodConfigKeys.Stats] is not JArray stats)
        {
            return false;
        }
        JObject row = MagebloodConfigParser.FindRow(stats, target.RowText);
        if (row == null)
        {
            return false;
        }
        row[key] = value;
        return true;
    }

    private static string FieldKey(MagebloodStatField field)
    {
        return field switch
        {
            MagebloodStatField.Added => MagebloodConfigKeys.Added,
            MagebloodStatField.Increased => MagebloodConfigKeys.Increased,
            MagebloodStatField.More => MagebloodConfigKeys.More,
            _ => null,
        };
    }

    private static JArray FlaskList(JObject root)
    {
        return root[MagebloodConfigKeys.Flasks] as JArray;
    }
}
