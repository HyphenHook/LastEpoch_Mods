using System;
using System.IO;
using System.Text;
using Il2Cpp;

namespace LastEpoch_Hud.Scripts.Dev;

/// <summary>
/// Development only. Once per session, writes every non-equipment item definition (glyphs, runes, shards and so
/// on) to glyph_dump.txt, so a custom consumable can be built from a real entry. Needs debug.enabled.
/// </summary>
internal static class GlyphDump
{
    private static bool _done;
    private static double _readySince = -1;

    public static void Tick()
    {
        if (_done || !Scenes.IsGameScene())
        {
            return;
        }
        string folder = Path.Combine(Directory.GetCurrentDirectory(), "Mods", Main.mod_name);
        if (!File.Exists(Path.Combine(folder, "debug.enabled")))
        {
            _done = true;
            return;
        }
        ItemList list = null;
        try
        {
            list = ItemList.get();
        }
        catch (Exception) { }
        if (list.IsNullOrDestroyed() || list.nonEquippableItems == null)
        {
            return;
        }
        double now = UnityEngine.Time.realtimeSinceStartupAsDouble;
        if (_readySince < 0)
        {
            _readySince = now;
        }
        if (now - _readySince < 10)
        {
            return;
        }
        _done = true;
        try
        {
            var sb = new StringBuilder();
            for (int i = 0; i < list.nonEquippableItems.Count; i++)
            {
                var entry = list.nonEquippableItems[i];
                if (entry == null)
                {
                    continue;
                }
                sb.AppendLine("==== nonEquippableItems[" + i + "] ====");
                sb.AppendLine(ObjectDumper.Dump(entry, "", 3));
            }
            File.WriteAllText(Path.Combine(folder, "glyph_dump.txt"), sb.ToString());
            Main.logger_instance?.Msg("glyph_dump.txt written (" + list.nonEquippableItems.Count + " entries).");
        }
        catch (Exception ex)
        {
            Main.logger_instance?.Warning("glyph dump failed: " + ex.Message);
        }
    }
}
