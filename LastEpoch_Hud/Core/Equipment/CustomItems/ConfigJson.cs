using System;
using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LastEpoch_Hud.Scripts.Core.CustomItems;

/// <summary>Typed reads of config JSON tokens. A wrong type is a false result, never an exception.</summary>
public static class ConfigJson
{
    public static bool TryGetInt(JToken token, out int value)
    {
        value = 0;
        if (token is not JValue { Value: long number })
        {
            return false;
        }
        if (number < int.MinValue || number > int.MaxValue)
        {
            return false;
        }
        value = (int)number;
        return true;
    }

    public static bool TryReadNumber(JToken token, out float value)
    {
        value = 0f;
        if (token is not JValue { Value: long or double } number)
        {
            return false;
        }
        value = Convert.ToSingle(number.Value, CultureInfo.InvariantCulture);
        return float.IsFinite(value);
    }

    /// <summary>Parses text to a root object. False on blank, invalid JSON or a non-object root.</summary>
    public static bool TryParseObject(string json, out JObject root)
    {
        root = null;
        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }
        try
        {
            root = JToken.Parse(json) as JObject;
        }
        catch (JsonReaderException)
        {
            return false;
        }
        return root != null;
    }

    public static bool TryReadBool(JToken token, out bool value)
    {
        value = false;
        if (token is not JValue { Value: bool flag })
        {
            return false;
        }
        value = flag;
        return true;
    }

    public static bool TryReadText(JToken token, out string value)
    {
        value = null;
        if (token is not JValue { Value: string text })
        {
            return false;
        }
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }
        value = text;
        return true;
    }
}
