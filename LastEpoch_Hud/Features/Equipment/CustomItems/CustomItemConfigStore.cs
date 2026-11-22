using System;
using System.IO;
using LastEpoch_Hud.Scripts.Core.CustomItems;

namespace LastEpoch_Hud.Scripts.Mods.Items.CustomItems;

/// <summary>One config file under the mod folder's CustomItems directory. Never throws on IO errors.</summary>
internal sealed class CustomItemConfigStore : IConfigText
{
    private static readonly string _folder = Path.Combine(
        Directory.GetCurrentDirectory(),
        "Mods",
        Main.mod_name,
        "CustomItems"
    );

    private static readonly DateTime _missingFileStamp = DateTime.FromFileTimeUtc(0);

    public CustomItemConfigStore(string fileName)
    {
        FilePath = Path.Combine(_folder, fileName);
    }

    public string FilePath { get; }

    public bool Exists()
    {
        return File.Exists(FilePath);
    }

    public DateTime? LastWriteUtc()
    {
        try
        {
            DateTime stamp = File.GetLastWriteTimeUtc(FilePath);
            return stamp == _missingFileStamp ? null : stamp;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>False after an IO error (logged).</summary>
    public bool Write(string text)
    {
        try
        {
            Directory.CreateDirectory(_folder);
            File.WriteAllText(FilePath, text);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Main.logger_instance?.Warning("Could not write " + FilePath + ": " + ex.Message);
            return false;
        }
    }

    public string Read()
    {
        try
        {
            return File.ReadAllText(FilePath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Main.logger_instance?.Warning("Could not read " + FilePath + ": " + ex.Message);
            return null;
        }
    }
}
