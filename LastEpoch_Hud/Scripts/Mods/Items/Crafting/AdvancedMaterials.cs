using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace LastEpoch_Hud.Scripts.Mods.Items.Crafting;

/// <summary>One kind of advanced crafting material: what it is called and how it looks in the Advanced Crafting tab.</summary>
internal sealed class AdvancedMaterial
{
    public string Key;
    public string Name;
    public string Description;
    public Color Tint = Color.white;
}

/// <summary>The advanced crafting materials the player holds, saved with the mod. Each is designed one at a time.</summary>
internal static class AdvancedMaterials
{
    public const string EncapsulationOrb = "EncapsulationOrb";

    public static readonly List<AdvancedMaterial> All = new()
    {
        new AdvancedMaterial
        {
            Key = EncapsulationOrb,
            Name = "Encapsulation Orb",
            Description = "Fractures one random affix of the item in the forge. A fractured affix is part of the item: no "
                + "crafting can change or remove it. The item needs at least four affixes, and can only be fractured once.",
            Tint = new Color(0.55f, 0.85f, 1f),
        },
    };

    private static Dictionary<string, int> _counts;
    private static string Folder => Path.Combine(Directory.GetCurrentDirectory(), "Mods", Main.mod_name);
    private static string SavePath => Path.Combine(Folder, "AdvancedMaterials.json");

    private static Dictionary<string, int> Counts
    {
        get
        {
            if (_counts == null)
            {
                try
                {
                    _counts = File.Exists(SavePath)
                        ? JsonConvert.DeserializeObject<Dictionary<string, int>>(File.ReadAllText(SavePath)) ?? new()
                        : new();
                }
                catch (Exception ex)
                {
                    Main.logger_instance?.Warning("Advanced materials: could not read the save: " + ex.Message);
                    _counts = new();
                }
            }
            return _counts;
        }
    }

    public static int Count(string key) => Counts.TryGetValue(key, out int n) ? n : 0;

    public static void Add(string key, int amount)
    {
        Counts[key] = Math.Max(0, Count(key) + amount);
        Save();
    }

    public static bool TryTake(string key, int amount = 1)
    {
        if (Count(key) < amount)
        {
            return false;
        }
        Counts[key] = Count(key) - amount;
        Save();
        return true;
    }

    private static void Save()
    {
        try
        {
            Directory.CreateDirectory(Folder);
            File.WriteAllText(SavePath, JsonConvert.SerializeObject(Counts));
        }
        catch (Exception ex)
        {
            Main.logger_instance?.Warning("Advanced materials: could not save: " + ex.Message);
        }
    }
}
