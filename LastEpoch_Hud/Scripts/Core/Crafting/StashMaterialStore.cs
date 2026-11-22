using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace LastEpoch_Hud.Scripts.Core.Crafting;

/// <summary>Orb balances keyed by the game's persistent stash identity.</summary>
public sealed class StashMaterialStore
{
    private sealed class SaveData
    {
        [JsonProperty(Required = Required.Always)]
        public int Version = 1;

        [JsonProperty(Required = Required.Always)]
        public string LegacyImportedTo;

        [JsonProperty(Required = Required.Always)]
        public Dictionary<string, Dictionary<string, int>> Groups = new();
    }

    private readonly string savePath;
    private readonly string legacyPath;
    private readonly object gate = new();
    private SaveData data;

    public StashMaterialStore(string savePath, string legacyPath)
    {
        this.savePath = savePath;
        this.legacyPath = legacyPath;
    }

    public static string ScopeKey(bool offline, string accountId, string stashId)
    {
        if (string.IsNullOrWhiteSpace(stashId) || (!offline && string.IsNullOrWhiteSpace(accountId))) return null;
        return JsonConvert.SerializeObject(new[] { offline ? "offline" : "online", offline ? "local" : accountId, stashId });
    }

    public void Initialize(string scope)
    {
        if (string.IsNullOrWhiteSpace(scope)) return;
        lock (gate) Load(scope);
    }

    public int Count(string scope, string material)
    {
        if (string.IsNullOrWhiteSpace(scope) || string.IsNullOrWhiteSpace(material)) return 0;
        lock (gate)
        {
            Load(scope);
            return Balance(data, scope, material);
        }
    }

    public bool TryChange(string scope, string material, int change)
    {
        if (string.IsNullOrWhiteSpace(scope) || string.IsNullOrWhiteSpace(material) || change == 0) return false;
        lock (gate)
        {
            Load(scope);
            long total = (long)Balance(data, scope, material) + change;
            if (total < 0 || total > int.MaxValue) return false;
            SaveData next = Copy();
            var balances = CopyBalances(scope);
            balances[material] = (int)total;
            next.Groups[scope] = balances;
            Save(next);
            return true;
        }
    }

    /// <summary>Transfers resources when the native game merges a retired stash. Safe to retry.</summary>
    public void Merge(string source, string target)
    {
        if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(target) || source == target) return;
        lock (gate)
        {
            Load(target);
            if (!data.Groups.ContainsKey(source)) return;
            var remaining = CopyBalances(source);
            var combined = CopyBalances(target);
            bool changed = false;
            foreach (var entry in data.Groups[source])
            {
                int current = combined.TryGetValue(entry.Key, out int count) ? count : 0;
                int moved = Math.Min(entry.Value, int.MaxValue - current);
                if (moved == 0) continue;
                combined[entry.Key] = current + moved;
                remaining[entry.Key] -= moved;
                changed = true;
            }
            if (!changed) return;
            SaveData next = Copy();
            next.Groups[source] = remaining;
            next.Groups[target] = combined;
            Save(next);
        }
    }

    private static int Balance(SaveData state, string scope, string material) =>
        state.Groups.TryGetValue(scope, out var balances) && balances.TryGetValue(material, out int count) ? count : 0;

    private SaveData Copy() => new()
    {
        LegacyImportedTo = data.LegacyImportedTo,
        Groups = new Dictionary<string, Dictionary<string, int>>(data.Groups),
    };

    private Dictionary<string, int> CopyBalances(string scope) =>
        data.Groups.TryGetValue(scope, out var balances) ? new Dictionary<string, int>(balances) : new();

    private void Load(string scope)
    {
        if (data != null) return;
        if (File.Exists(savePath))
        {
            var loaded = JsonConvert.DeserializeObject<SaveData>(File.ReadAllText(savePath));
            if (loaded == null || loaded.Version != 1 || string.IsNullOrWhiteSpace(loaded.LegacyImportedTo) || loaded.Groups == null)
                throw new JsonSerializationException("Unsupported or incomplete Advanced Materials save.");
            foreach (var group in loaded.Groups)
            {
                if (string.IsNullOrWhiteSpace(group.Key)) throw new JsonSerializationException("Missing stash identity.");
                ValidateBalances(group.Value);
            }
            data = loaded;
            return;
        }

        var legacy = File.Exists(legacyPath)
            ? JsonConvert.DeserializeObject<Dictionary<string, int>>(File.ReadAllText(legacyPath))
            : new Dictionary<string, int>();
        ValidateBalances(legacy);
        var migrated = new SaveData { LegacyImportedTo = scope };
        migrated.Groups[scope] = legacy;
        // The imported balance and its migration marker become durable together.
        Save(migrated);
    }

    private static void ValidateBalances(Dictionary<string, int> balances)
    {
        if (balances == null) throw new JsonSerializationException("Missing Advanced Materials balances.");
        foreach (var entry in balances)
        {
            if (string.IsNullOrWhiteSpace(entry.Key) || entry.Value < 0)
                throw new JsonSerializationException("Invalid Advanced Materials balance.");
        }
    }

    private void Save(SaveData next)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(savePath)));
        string temporary = savePath + ".tmp";
        File.WriteAllText(temporary, JsonConvert.SerializeObject(next, Formatting.Indented));
        File.Move(temporary, savePath, true);
        data = next;
    }
}
