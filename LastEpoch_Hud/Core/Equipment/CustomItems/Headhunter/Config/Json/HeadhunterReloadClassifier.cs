using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter.Config.Json;

/// <summary>Decides if a config edit touched only visuals (bar layout, aura).</summary>
public static class HeadhunterReloadClassifier
{
    public static HeadhunterReloadKind Classify(string previousJson, string nextJson)
    {
        if (!TryParseWithoutVisuals(previousJson, out JObject previous))
        {
            return HeadhunterReloadKind.Full;
        }
        if (!TryParseWithoutVisuals(nextJson, out JObject next))
        {
            return HeadhunterReloadKind.Full;
        }
        if (!JToken.DeepEquals(previous, next))
        {
            return HeadhunterReloadKind.Full;
        }

        return HeadhunterReloadKind.VisualOnly;
    }

    private static bool TryParseWithoutVisuals(string json, out JObject root)
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
        if (root == null)
        {
            return false;
        }

        root.Remove(HeadhunterConfigKeys.Bar);
        root.Remove(HeadhunterConfigKeys.Aura);
        return true;
    }
}
