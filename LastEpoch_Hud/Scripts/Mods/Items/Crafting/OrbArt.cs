using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace LastEpoch_Hud.Scripts.Mods.Items.Crafting;

/// <summary>
/// The art of the advanced crafting materials. The images are drawn by <c>scripts/Generate-OrbArt.py</c> and built into
/// the mod; they are read once and kept alive, because Unity throws away textures nobody in the game refers to.
/// </summary>
internal static class OrbArt
{
    private static readonly Dictionary<string, Sprite> Cache = new();

    /// <summary>The orb's sprite. The colours are unused now that the image is drawn ahead of time.</summary>
    public static Sprite Get(string key, Color core, Color glow)
    {
        string resource = key == AdvancedMaterials.EncapsulationOrb ? "orb_encapsulation" : "orb_encapsulation";
        return Load(resource);
    }

    /// <summary>A named image of the mod: orb_slot_empty, forge_slot_frame, forge_button_frame, glow_violet and so on.</summary>
    public static Sprite Named(string name) => Load(name);

    /// <summary>The orb, drained of colour, for when there are none left.</summary>
    public static Sprite OrbGrey() => Load("orb_encapsulation_grey");

    /// <summary>The small blue diamond that marks a fractured affix.</summary>
    public static Sprite Diamond() => Load("diamond_fractured");

    private static Sprite Load(string name)
    {
        if (Cache.TryGetValue(name, out Sprite cached) && cached != null)
        {
            return cached;
        }
        Sprite sprite = Read(name);
        Cache[name] = sprite;
        return sprite;
    }

    private static Sprite Read(string name)
    {
        try
        {
            using Stream stream = typeof(OrbArt).Assembly.GetManifestResourceStream("Generated." + name + ".png");
            if (stream == null)
            {
                Main.logger_instance?.Warning("Orb art: the image " + name + " is not in the mod.");
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
                Main.logger_instance?.Warning("Orb art: the image " + name + " could not be read.");
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
            Main.logger_instance?.Warning("Orb art: " + ex.Message);
            return null;
        }
    }
}
