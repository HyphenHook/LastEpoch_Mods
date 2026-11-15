using System.Collections.Generic;
using Newtonsoft.Json;

namespace LastEpoch_Hud.Scripts.Core.Crafting;

/// <summary>
/// Which affix of which item is fractured. An item is known by its individual id, which the game saves with the
/// item, keeps through crafts and the stash, and gives a copy a new one of. At most one affix per item.
/// </summary>
public sealed class FractureRegistry
{
    private Dictionary<uint, int> _fractured = new();

    public int Count => _fractured.Count;

    /// <summary>The fractured affix id of the item, or -1.</summary>
    public int AffixOf(uint itemId) => itemId != 0 && _fractured.TryGetValue(itemId, out int affix) ? affix : -1;

    public bool Has(uint itemId) => AffixOf(itemId) >= 0;

    public bool Is(uint itemId, int affixId) => AffixOf(itemId) == affixId;

    public void Set(uint itemId, int affixId)
    {
        if (itemId != 0)
        {
            _fractured[itemId] = affixId;
        }
    }

    public void Remove(uint itemId) => _fractured.Remove(itemId);

    /// <summary>A copy of a fractured item is fractured too. Returns whether anything was copied.</summary>
    public bool Copy(uint fromItemId, uint toItemId)
    {
        if (toItemId == 0 || fromItemId == toItemId || !_fractured.TryGetValue(fromItemId, out int affix))
        {
            return false;
        }
        _fractured[toItemId] = affix;
        return true;
    }

    public string ToJson() => JsonConvert.SerializeObject(_fractured);

    public static FractureRegistry FromJson(string json)
    {
        var registry = new FractureRegistry();
        try
        {
            registry._fractured = JsonConvert.DeserializeObject<Dictionary<uint, int>>(json ?? "") ?? new();
        }
        catch (JsonException)
        {
        }
        return registry;
    }
}
