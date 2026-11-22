using System.IO;
using System.Reflection;

namespace LastEpoch_Hud;

/// <summary>Reads the CSV tables under LastEpoch_Hud/Data, which are embedded in the mod at compile time.</summary>
internal static class EmbeddedData
{
    /// <summary>The text of an embedded data file such as "HeorotStats.csv", or null when it is not in the mod.</summary>
    internal static string Read(string fileName)
    {
        using Stream stream = typeof(EmbeddedData).Assembly.GetManifestResourceStream("Data." + fileName);
        if (stream == null) return null;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
