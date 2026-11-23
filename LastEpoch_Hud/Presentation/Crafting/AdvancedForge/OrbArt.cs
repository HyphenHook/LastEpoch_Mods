using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        string resource = key switch
        {
            AdvancedMaterials.AttunementOrb => "orb_attunement",
            AdvancedMaterials.ReminiscenceOrb => "orb_reminiscence",
            AdvancedMaterials.EchoOrb => "orb_echo",
            AdvancedMaterials.ReverieOrb => "orb_reverie",
            AdvancedMaterials.ChaosOrb => "orb_chaos",
            _ => "orb_encapsulation",
        };
        return Load(resource);
    }

    /// <summary>A named image of the mod: orb_slot_empty, forge_slot_frame, forge_button_frame, glow_violet and so on.</summary>
    public static Sprite Named(string name) => Load(name);

    /// <summary>A neutral silhouette of existing art for empty equipment slots.</summary>
    public static Sprite GreyNamed(string name)
    {
        string key = "grey:" + name;
        if (Cache.TryGetValue(key, out Sprite cached) && cached != null) return cached;
        var source = Load(name);
        if (source == null) return null;
        var original = source.texture;
        var pixels = original.GetPixels().ToArray();
        for (int i = 0; i < pixels.Length; i++)
        {
            var color = pixels[i];
            float grey = color.grayscale;
            pixels[i] = new Color(grey, grey, grey, color.a);
        }
        var texture = new Texture2D(original.width, original.height, TextureFormat.RGBA32, false)
        {
            name = name + "_EmptySlot", hideFlags = HideFlags.HideAndDontSave,
            wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear,
        };
        texture.SetPixels(new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<Color>(pixels));
        texture.Apply(false, true);
        var sprite = Sprite.Create(texture, source.rect, new Vector2(.5f, .5f), source.pixelsPerUnit);
        sprite.hideFlags = HideFlags.HideAndDontSave;
        Cache[key] = sprite;
        return sprite;
    }

    /// <summary>The orb, drained of colour, for when there are none left.</summary>
    public static Sprite OrbGrey() => Load("orb_encapsulation_grey");

    /// <summary>The small blue diamond that marks a encapsulated affix.</summary>
    public static Sprite Diamond() => Load("diamond_encapsulated");

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

    private static readonly Dictionary<string, Sprite[]> Sheets = new();

    /// <summary>
    /// A sheet of equal frames as sprites, left to right and then top to bottom (the first frame is the top left).
    /// </summary>
    public static Sprite[] Sheet(string name, int columns, int rows)
    {
        if (Sheets.TryGetValue(name, out Sprite[] cached) && cached.Length > 0 && cached[0] != null)
        {
            return cached;
        }
        var frames = new Sprite[columns * rows];
        Sprite whole = Load(name);
        if (whole != null)
        {
            Texture2D texture = whole.texture;
            float w = texture.width / (float)columns, h = texture.height / (float)rows;
            for (int row = 0; row < rows; row++)
            {
                for (int col = 0; col < columns; col++)
                {
                    var rect = new Rect(col * w, texture.height - (row + 1) * h, w, h);
                    Sprite frame = Sprite.Create(texture, rect, new Vector2(0.5f, 0.5f), 100f);
                    frame.hideFlags = HideFlags.HideAndDontSave;
                    frames[row * columns + col] = frame;
                }
            }
        }
        Sheets[name] = frames;
        return frames;
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
