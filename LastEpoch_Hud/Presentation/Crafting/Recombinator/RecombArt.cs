using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace LastEpoch_Hud.Scripts.Mods.Items.Crafting;

/// <summary>
/// The Recombinator's artwork: every image of the kit is its own PNG in a folder of its own, built into the mod under
/// Recombinator/&lt;component&gt;/&lt;effect&gt;/frame_000.png. They are read when asked for and kept alive.
/// </summary>
internal static class RecombArt
{
    private const string Prefix = "Recombinator/";
    private static Dictionary<string, string> resources; // "ItemSlot_Left/Base/Dormant" -> the resource's own name
    private static readonly Dictionary<string, Sprite> cache = new();

    private static void Index()
    {
        if (resources != null)
        {
            return;
        }
        resources = new Dictionary<string, string>();
        foreach (string name in typeof(RecombArt).Assembly.GetManifestResourceNames())
        {
            string normal = name.Replace('\\', '/');
            if (normal.StartsWith(Prefix, StringComparison.Ordinal) && normal.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            {
                resources[normal.Substring(Prefix.Length, normal.Length - Prefix.Length - 4)] = name;
            }
        }
        Main.logger_instance?.Msg("Recombinator art: " + resources.Count + " images in the mod");
    }

    /// <summary>The images of a folder such as "ItemSlot_Left/VFX_PowerUp", in order.</summary>
    public static List<string> Keys(string folder)
    {
        Index();
        string start = folder + "/";
        return resources.Keys.Where(k => k.StartsWith(start, StringComparison.Ordinal) && k.IndexOf('/', start.Length) < 0).OrderBy(k => k, StringComparer.Ordinal).ToList();
    }

    public static Sprite Get(string key)
    {
        if (cache.TryGetValue(key, out Sprite cached) && cached != null)
        {
            return cached;
        }
        Index();
        Sprite sprite = resources.TryGetValue(key, out string name) ? Read(name, key) : null;
        cache[key] = sprite;
        return sprite;
    }

    /// <summary>The dormant base of a component.</summary>
    public static Sprite Base(string component) => Get(component + "/Base/Dormant");

    /// <summary>The frames of an effect, in order.</summary>
    public static Sprite[] Frames(string folder) => Keys(folder).Select(Get).ToArray();

    /// <summary>Reads up to this many more of the images the Recombinator uses. True when there is nothing left to read.</summary>
    public static bool Preload(IEnumerable<string> keys, int atMost)
    {
        int read = 0;
        foreach (string key in keys)
        {
            if (cache.TryGetValue(key, out Sprite s) && s != null)
            {
                continue;
            }
            Get(key);
            if (++read >= atMost)
            {
                return false;
            }
        }
        return true;
    }

    private static Sprite Read(string resource, string key)
    {
        try
        {
            using Stream stream = typeof(RecombArt).Assembly.GetManifestResourceStream(resource);
            if (stream == null)
            {
                return null;
            }
            byte[] bytes;
            using (var memory = new MemoryStream())
            {
                stream.CopyTo(memory);
                bytes = memory.ToArray();
            }
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!ImageConversion.LoadImage(texture, bytes))
            {
                Main.logger_instance?.Warning("Recombinator art: " + key + " could not be read.");
                return null;
            }
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            texture.hideFlags = HideFlags.HideAndDontSave;
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }
        catch (Exception ex)
        {
            Main.logger_instance?.Warning("Recombinator art " + key + ": " + ex.Message);
            return null;
        }
    }
}
