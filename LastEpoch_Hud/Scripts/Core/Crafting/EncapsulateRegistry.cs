using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace LastEpoch_Hud.Scripts.Core.Crafting;

/// <summary>
/// Which affix of which item is encapsulated. An item is known by its individual id, which the game saves with the
/// item, keeps through crafts and the stash, and gives a copy a new one of. At most one affix per item.
/// </summary>
public sealed class EncapsulateRegistry
{
    private Dictionary<uint, int> _encapsulated = new();

    public int Count => _encapsulated.Count;

    /// <summary>The encapsulated affix id of the item, or -1.</summary>
    public int AffixOf(uint itemId) => itemId != 0 && _encapsulated.TryGetValue(itemId, out int affix) ? affix : -1;

    public bool Has(uint itemId) => AffixOf(itemId) >= 0;

    public bool Is(uint itemId, int affixId) => AffixOf(itemId) == affixId;

    public void Set(uint itemId, int affixId)
    {
        if (itemId != 0)
        {
            _encapsulated[itemId] = affixId;
        }
    }

    public void Remove(uint itemId) => _encapsulated.Remove(itemId);

    /// <summary>A copy of an encapsulated item is encapsulated too. Returns whether anything was copied.</summary>
    public bool Copy(uint fromItemId, uint toItemId)
    {
        if (toItemId == 0 || fromItemId == toItemId || !_encapsulated.TryGetValue(fromItemId, out int affix))
        {
            return false;
        }
        _encapsulated[toItemId] = affix;
        return true;
    }

    public string ToJson() => JsonConvert.SerializeObject(_encapsulated);

    public static EncapsulateRegistry Load(string savePath, string legacyPath)
    {
        string path = File.Exists(savePath) ? savePath : legacyPath;
        return File.Exists(path) ? FromJson(File.ReadAllText(path)) : new EncapsulateRegistry();
    }

    public static EncapsulateRegistry FromJson(string json)
    {
        var registry = new EncapsulateRegistry();
        try
        {
            registry._encapsulated = JsonConvert.DeserializeObject<Dictionary<uint, int>>(json ?? "") ?? new();
        }
        catch (JsonException)
        {
        }
        return registry;
    }
}
