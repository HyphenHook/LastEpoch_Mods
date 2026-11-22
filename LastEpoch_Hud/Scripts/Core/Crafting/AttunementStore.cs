using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace LastEpoch_Hud.Scripts.Core.Crafting;

/// <summary>Persists base-item attunements atomically and keeps failed writes out of the live cache.</summary>
public sealed class AttunementStore
{
    private readonly string path;
    private readonly object gate = new();
    private Dictionary<uint, AttunementState> records;

    public AttunementStore(string path) => this.path = path;

    /// <summary>The stored state is read-only to callers. Copy it before making a new crafting plan.</summary>
    public AttunementState Find(uint id)
    {
        if (id == 0) return null;
        lock (gate)
        {
            Load();
            return records.TryGetValue(id, out var state) ? state : null;
        }
    }

    public void Set(uint id, AttunementState state)
    {
        if (id == 0) throw new ArgumentOutOfRangeException(nameof(id));
        lock (gate)
        {
            Load();
            var next = new Dictionary<uint, AttunementState>(records);
            if (state == null) next.Remove(id);
            else
            {
                Validate(state);
                next[id] = state.Copy();
            }
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            string temporary = path + ".tmp";
            File.WriteAllText(temporary, JsonConvert.SerializeObject(next));
            File.Move(temporary, path, true);
            records = next;
        }
    }

    private void Load()
    {
        if (records != null) return;
        var loaded = File.Exists(path)
            ? JsonConvert.DeserializeObject<Dictionary<uint, AttunementState>>(File.ReadAllText(path))
            : new Dictionary<uint, AttunementState>();
        if (loaded == null) throw new JsonSerializationException("Missing Attunement data.");
        foreach (var entry in loaded)
        {
            if (entry.Key == 0) throw new JsonSerializationException("Missing Attunement item identity.");
            Validate(entry.Value);
        }
        records = loaded;
    }

    private static void Validate(AttunementState state)
    {
        if (state == null || !ValidMultiplier(state.Multiplier) || state.Implicits == null || state.Unique == null || state.Affixes == null)
            throw new JsonSerializationException("Invalid Attunement state.");
        foreach (var affix in state.Affixes)
        {
            if (affix == null || !ValidMultiplier(affix.Multiplier))
                throw new JsonSerializationException("Invalid attuned affix.");
        }
    }

    private static bool ValidMultiplier(double multiplier) =>
        double.IsFinite(multiplier) && multiplier >= AttunementState.MinMultiplier && multiplier <= AttunementState.MaxMultiplier;
}
