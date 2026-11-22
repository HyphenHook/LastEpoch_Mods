using System;
using System.Collections.Generic;

namespace LastEpoch_Hud.Scripts.Core.Cosmetics;

/// <summary>Deduped set of cosmetic ids to unlock, with removal counts per reason.</summary>
public sealed class CosmeticCatalog
{
    private readonly Dictionary<string, CosmeticVerdict> _verdicts = new(StringComparer.Ordinal);
    private readonly HashSet<string> _kept = new(StringComparer.Ordinal);
    private readonly List<string> _keptIds = new();
    private readonly int[] _removedBy = new int[Enum.GetValues(typeof(CosmeticVerdict)).Length];

    public int KeptCount => _keptIds.Count;

    public int RemovedCount { get; private set; }

    public IReadOnlyList<string> KeptIds => _keptIds;

    /// <summary>Judges an entry on first sighting; a repeat returns the stored verdict.</summary>
    public CosmeticVerdict Add(string id, string displayName, bool hasStoreIcon, bool isTitle)
    {
        id ??= "";
        if (_verdicts.TryGetValue(id, out CosmeticVerdict known))
        {
            return known;
        }
        CosmeticVerdict verdict = CosmeticCatalogRules.Judge(
            id,
            displayName,
            hasStoreIcon,
            isTitle
        );
        _verdicts[id] = verdict;
        Count(id, verdict);
        return verdict;
    }

    /// <summary>Stored verdict for a seen id only; no rule fallback, stores nothing.</summary>
    public bool TryGetVerdict(string id, out CosmeticVerdict verdict) =>
        _verdicts.TryGetValue(id ?? "", out verdict);

    public bool Contains(string id) => id != null && _kept.Contains(id);

    /// <summary>Distinct ids removed for a reason; Keep gives 0.</summary>
    public int RemovedBy(CosmeticVerdict reason) =>
        reason == CosmeticVerdict.Keep ? 0 : _removedBy[(int)reason];

    public string SummaryLine() =>
        $"Offline cosmetics: {KeptCount} unlocked, {RemovedCount} removed "
        + $"(EmptyId {RemovedBy(CosmeticVerdict.EmptyId)}, UnknownPrefix {RemovedBy(CosmeticVerdict.UnknownPrefix)}, "
        + $"Blocklisted {RemovedBy(CosmeticVerdict.Blocklisted)}, TestEntry {RemovedBy(CosmeticVerdict.TestEntry)}, "
        + $"NoStoreIcon {RemovedBy(CosmeticVerdict.NoStoreIcon)})";

    private void Count(string id, CosmeticVerdict verdict)
    {
        if (verdict != CosmeticVerdict.Keep)
        {
            _removedBy[(int)verdict]++;
            RemovedCount++;
            return;
        }
        _kept.Add(id);
        _keptIds.Add(id);
    }
}
