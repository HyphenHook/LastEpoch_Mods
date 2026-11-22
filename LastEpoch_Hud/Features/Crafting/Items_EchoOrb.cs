using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Il2Cpp;
using Newtonsoft.Json;

namespace LastEpoch_Hud.Scripts.Mods.Items.Crafting;

internal static class Items_EchoOrb
{
    internal sealed class Rolls
    {
        public byte[] Affixes;
        public byte[] Implicits;
        public byte[] Unique;
    }

    internal sealed class Echoes
    {
        public string Shape;
        public string ItemVersion;
        public List<Rolls> Options = new();
    }

    private static readonly Random Rng = new();
    private static Dictionary<uint, Echoes> saved;
    private static string SavePath => Path.Combine(Directory.GetCurrentDirectory(), "Mods", Main.mod_name, "Echoes.json");

    private static Dictionary<uint, Echoes> Saved => saved ??= File.Exists(SavePath)
        ? JsonConvert.DeserializeObject<Dictionary<uint, Echoes>>(File.ReadAllText(SavePath)) ?? new()
        : new();

    private static void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SavePath));
        string temporary = SavePath + ".tmp";
        File.WriteAllText(temporary, JsonConvert.SerializeObject(Saved));
        File.Move(temporary, SavePath, true);
    }

    private static string Shape(ItemData item) => $"{item.itemType}/{item.subType}/{item.uniqueID}/{item.rarity}"
        + $"/{item.implicitRolls.Count}/{item.uniqueRolls.Count}/"
        + string.Join(",", item.affixes.ToArray().Select(a => $"{a.affixId}:{a.affixTier}:{a.IsSealed}:{a.IsSealedPrimordial}:{a.IsSealedCorrupted}"));

    public static string WhyNot(ItemData item)
    {
        if (item.IsNullOrDestroyed()) return "Put an item in the slot first.";
        string blocked = CraftProtection.Blocked(item);
        if (blocked.Length != 0) return blocked;
        if (item.individualID == 0) return "This item has no persistent identity.";
        if (item.affixes == null || item.implicitRolls == null || item.uniqueRolls == null
            || item.affixes.ToArray().Any(a => a == null)
            || item.affixes.Count + item.implicitRolls.Count + item.uniqueRolls.Count == 0)
        {
            return "This item has no numeric rolls to echo.";
        }

        if (item.implicitRolls.Count == 0 && item.uniqueRolls.Count == 0
            && !item.affixes.ToArray().Any(a => !CraftProtection.IsAttached(item, a)))
        {
            return "All numeric rolls on this item are protected.";
        }
        return "";
    }

    private static Rolls Read(ItemData item) => new()
    {
        Affixes = item.affixes.ToArray().Select(a => a.affixRoll).ToArray(),
        Implicits = Enumerable.Range(0, item.implicitRolls.Count).Select(i => item.implicitRolls[i]).ToArray(),
        Unique = Enumerable.Range(0, item.uniqueRolls.Count).Select(i => item.uniqueRolls[i]).ToArray(),
    };

    private static string ItemVersion(ItemData item, Rolls rolls) => Shape(item) + "/"
        + Convert.ToBase64String(rolls.Affixes) + "/" + Convert.ToBase64String(rolls.Implicits)
        + "/" + Convert.ToBase64String(rolls.Unique) + "/" + ProtectedSlots(item)
        + "/" + item.forgingPotential;

    private static string ProtectedSlots(ItemData item) =>
        string.Concat(ProtectedRolls(item).Select(isProtected => isProtected ? "1" : "0"));

    public static bool IsCurrent(ItemData item, Echoes echoes) =>
        echoes != null && WhyNot(item).Length == 0 && echoes.Shape == Shape(item)
        && (echoes.ItemVersion == null || echoes.ItemVersion == ItemVersion(item, Read(item)));

    private static bool Same(Rolls a, Rolls b) => a.Affixes.SequenceEqual(b.Affixes)
        && a.Implicits.SequenceEqual(b.Implicits) && a.Unique.SequenceEqual(b.Unique);

    private static byte[] RandomRolls(int count)
    {
        var rolls = new byte[count];
        Rng.NextBytes(rolls);
        return rolls;
    }

    public static Echoes Reveal(ItemData item, out string message)
    {
        message = WhyNot(item);
        if (message.Length > 0) return null;
        if (AdvancedMaterials.Count(AdvancedMaterials.EchoOrb) == 0)
        {
            message = "You have no Orbs of Echoes.";
            return null;
        }

        uint id = item.individualID;
        Saved.TryGetValue(id, out Echoes existing);
        Rolls current = Read(item);
        string version = ItemVersion(item, current);
        if (existing != null && existing.Shape == Shape(item))
        {
            if (existing.ItemVersion == version) return existing;
            if (existing.ItemVersion == null)
            {
                existing.ItemVersion = version;
                try
                {
                    Save();
                    return existing;
                }
                catch (Exception ex)
                {
                    existing.ItemVersion = null;
                    message = "The Echoes could not be saved.";
                    Main.logger_instance?.Warning("Orb of Echoes: " + ex.Message);
                    return null;
                }
            }
        }
        var echoes = new Echoes { Shape = Shape(item), ItemVersion = version };
        bool[] protectedRolls = ProtectedRolls(item);
        while (echoes.Options.Count < 3)
        {
            var candidate = new Rolls
            {
                Affixes = RandomRolls(current.Affixes.Length),
                Implicits = RandomRolls(current.Implicits.Length),
                Unique = RandomRolls(current.Unique.Length),
            };
            for (int i = 0; i < protectedRolls.Length; i++)
            {
                if (protectedRolls[i]) candidate.Affixes[i] = current.Affixes[i];
            }
            if (!Same(candidate, current) && !echoes.Options.Any(option => Same(option, candidate)))
            {
                echoes.Options.Add(candidate);
            }
        }

        Saved[id] = echoes;
        try
        {
            Save();
            return echoes;
        }
        catch (Exception ex)
        {
            if (existing != null) Saved[id] = existing;
            else Saved.Remove(id);
            message = "The Echoes could not be saved.";
            Main.logger_instance?.Warning("Orb of Echoes: " + ex.Message);
            return null;
        }
    }

    private static bool[] ProtectedRolls(ItemData item) =>
        item.affixes.ToArray().Select(affix => CraftProtection.IsAttached(item, affix)).ToArray();

    public static byte[] AffixPreview(ItemData item, Rolls rolls)
    {
        var values = (byte[])rolls.Affixes.Clone();
        bool[] protectedRolls = ProtectedRolls(item);
        for (int i = 0; i < values.Length; i++)
        {
            if (protectedRolls[i]) values[i] = item.affixes[i].affixRoll;
        }
        return values;
    }

    private static void Write(ItemData item, Rolls rolls, bool[] protectedRolls)
    {
        for (int i = 0; i < rolls.Affixes.Length; i++)
        {
            if (!protectedRolls[i]) item.affixes[i].affixRoll = rolls.Affixes[i];
        }
        for (int i = 0; i < rolls.Implicits.Length; i++) item.implicitRolls[i] = rolls.Implicits[i];
        for (int i = 0; i < rolls.Unique.Length; i++) item.uniqueRolls[i] = rolls.Unique[i];
        item.RefreshIDAndValues();
    }

    public static ItemData Preview(ItemData item, Rolls rolls)
    {
        if (WhyNot(item).Length != 0) return null;
        var packed = item.GetID();
        var bytes = new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<byte>(packed.Length);
        for (int i = 0; i < packed.Length; i++) bytes[i] = packed[i];
        var copy = new ItemDataUnpacked();
        copy.RebuildFromID(bytes);
        ItemData preview = copy.TryCast<ItemData>();
        Write(preview, rolls, ProtectedRolls(item));
        return preview;
    }

    public static bool Keep(ItemData item, Echoes echoes, int index, out string message)
    {
        message = WhyNot(item);
        if (message.Length > 0) return false;
        uint id = item.individualID;
        if (!IsCurrent(item, echoes) || !Saved.TryGetValue(id, out Echoes pending) || pending != echoes
            || index < 0 || index >= echoes.Options.Count)
        {
            message = "Select an Echo for this item first.";
            return false;
        }

        Rolls old = Read(item);
        bool[] protectedRolls = ProtectedRolls(item);
        if (!AdvancedMaterials.TryTake(AdvancedMaterials.EchoOrb))
        {
            message = "You have no Orbs of Echoes.";
            return false;
        }

        try
        {
            Write(item, echoes.Options[index], protectedRolls);
            Saved.Remove(id);
            Save();
        }
        catch (Exception ex)
        {
            Saved[id] = echoes;
            AdvancedMaterials.Add(AdvancedMaterials.EchoOrb, 1);
            Write(item, old, protectedRolls);
            message = "The Echo could not be applied.";
            Main.logger_instance?.Warning("Orb of Echoes: " + ex.Message);
            return false;
        }
        message = "Echo kept.";
        return true;
    }
}
