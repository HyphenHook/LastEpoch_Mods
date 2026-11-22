using System;
using System.Collections.Generic;
using Il2Cpp;
using LastEpoch_Hud.Scripts.Core.ForceDrop;

namespace LastEpoch_Hud.Scripts.ModUI.ForceDrop;

// Dedicated variant ids come from the native fixed pool. Illegal extras use
// ordinary slots and must not be mistaken for that serialized unique prefix.
public static class UniqueVariantAdapter
{
    public static bool IsUnsated(UniqueList.Entry entry)
    {
        return !entry.IsNullOrDestroyed()
            && string.Equals(
                (entry.name ?? "").Replace(" ", "").Replace("_", ""),
                "UnsatedRage",
                StringComparison.OrdinalIgnoreCase
            );
    }

    public static int VariantCount(UniqueList.Entry entry)
    {
        if (
            entry.IsNullOrDestroyed()
            || !entry.dropsSpecificLegendaryAffixes
            || entry.droppableLegendaryAffixes.IsNullOrDestroyed()
            || entry.droppableLegendaryAffixes.Count == 0
        )
            return 0;
        // Fixed FakeUniqueMod pools use the same native representation. Exulis
        // instead has tiered Corrupted modifiers and needs its own selection
        // route; do not misrepresent those as fixed T1 unique modifiers.
        foreach (int id in entry.droppableLegendaryAffixes)
        {
            var definition = ForceDropCatalog.Find(id);
            if (
                definition.IsNullOrDestroyed()
                || definition.specialAffixType != AffixList.SpecialAffixType.FakeUniqueMod
            )
                return 0;
        }
        return entry.droppableLegendaryAffixCount >= 1 && entry.droppableLegendaryAffixCount <= 2
            ? entry.droppableLegendaryAffixCount
            : 0;
    }

    public static bool HasVariants(UniqueList.Entry entry)
    {
        int count = VariantCount(entry);
        return count > 0
            && !entry.droppableLegendaryAffixes.IsNullOrDestroyed()
            && entry.droppableLegendaryAffixes.Count >= count;
    }

    public static bool HasSingleVariant(UniqueList.Entry entry)
    {
        // These flags also govern legendary drop generation; they are not a
        // capability test for selecting a variant from an explicit native pool.
        return VariantCount(entry) == 1 && HasVariants(entry);
    }

    public static bool IsVariant(AffixList.Affix definition)
    {
        return !definition.IsNullOrDestroyed()
            && definition.specialAffixType == AffixList.SpecialAffixType.FakeUniqueMod;
    }

    public static List<AffixList.Affix> Catalog(UniqueList.Entry entry)
    {
        var result = new List<AffixList.Affix>();
        if (!HasVariants(entry))
            return result;
        foreach (var id in entry.droppableLegendaryAffixes)
        {
            var definition = ForceDropCatalog.Find(id);
            if (
                !definition.IsNullOrDestroyed()
                && !CorruptedAffixAdapter.IsCorruption(definition)
                && !result.Exists(a => a.affixId == id)
            )
                result.Add(definition);
        }
        return result;
    }

    public static void Apply(ItemDataUnpacked item, params int[] ids) => Apply(item, false, ids);

    public static void Apply(ItemDataUnpacked item, bool allowAdditionalVariants, params int[] ids)
    {
        var entry = UniqueList.getUnique(item.uniqueID);
        var catalog = Catalog(entry);
        if (
            !HasVariants(entry)
            || item.itemType != entry.baseType
            || !entry.subTypes.Contains((byte)item.subType)
            || !item.isUniqueOrLegendary()
        )
            throw new InvalidOperationException("Unique variant does not match this item");
        if (ids == null || ids.Length != VariantCount(entry))
            throw new InvalidOperationException("Choose every exclusive unique modifier");
        var selected = new HashSet<int>();
        var additions = new List<ItemAffix>();
        foreach (int id in ids)
        {
            var definition = catalog.Find(a => a.affixId == id);
            if (definition.IsNullOrDestroyed() || !selected.Add(id))
                throw new InvalidOperationException(
                    "Choose distinct modifiers from this unique's native fixed pool"
                );
            var affix = new ItemAffix((ushort)id, 0, 255, item.itemType, SealedAffixType.None);
            if (
                affix.affixId != id
                || affix.specialAffixType != definition.specialAffixType
                || affix.sealedAffixType != SealedAffixType.None
            )
                throw new InvalidOperationException(
                    "Native unique variant constructor rejected the modifier"
                );
            additions.Add(affix);
        }
        var original = new List<string>();
        foreach (var existing in item.affixes)
        {
            if (existing.IsNullOrDestroyed())
                throw new InvalidOperationException("Invalid existing affix; no item was dropped");
            if (
                selected.Contains(existing.affixId)
                || (
                    !allowAdditionalVariants
                    && (
                        existing.specialAffixType == AffixList.SpecialAffixType.FakeUniqueMod
                        || entry.droppableLegendaryAffixes.Contains(existing.affixId)
                    )
                )
            )
                throw new InvalidOperationException("The item already has a variant modifier");
            original.Add(Signature(existing, allowAdditionalVariants));
        }
        original.Sort(StringComparer.Ordinal);
        byte rarity = item.rarity,
            potential = item.legendaryPotential,
            weaversWill = item.weaversWill;
        ushort uniqueId = item.uniqueID;
        // Variants are additional fixed-pool modifiers, not LP-transferred affixes.
        // Add both glove variants before packing, without spending LP.
        foreach (var affix in additions)
            item.affixes.Add(affix);
        item.sockets = (byte)item.affixes.Count;
        PrepareForPacking(item, ids, allowAdditionalVariants);
        item.RefreshIDAndValues();
        VerifySelection(item, allowAdditionalVariants, ids);
        var remaining = new List<string>();
        foreach (var saved in item.affixes)
            if (!selected.Contains(saved.affixId))
                remaining.Add(Signature(saved, allowAdditionalVariants));
        remaining.Sort(StringComparer.Ordinal);
        if (
            item.rarity != rarity
            || item.legendaryPotential != potential
            || item.weaversWill != weaversWill
            || item.uniqueID != uniqueId
            || remaining.Count != original.Count
        )
            throw new InvalidOperationException(
                "Unique variant storage verification failed (unique="
                    + item.uniqueID
                    + ", rarity="
                    + rarity
                    + "->"
                    + item.rarity
                    + ", lp="
                    + potential
                    + "->"
                    + item.legendaryPotential
                    + ", ww="
                    + weaversWill
                    + "->"
                    + item.weaversWill
                    + ", sockets="
                    + item.sockets
                    + ", affixes="
                    + item.affixes.Count
                    + ", expectedOrdinary=["
                    + string.Join(",", original)
                    + "], actualOrdinary=["
                    + string.Join(",", remaining)
                    + "]); no item was dropped"
            );
        for (int i = 0; i < original.Count; i++)
            if (original[i] != remaining[i])
                throw new InvalidOperationException(
                    "Unique variant changed an existing affix (unique="
                        + item.uniqueID
                        + ", expectedOrdinary=["
                        + string.Join(",", original)
                        + "], actualOrdinary=["
                        + string.Join(",", remaining)
                        + "]); no item was dropped"
                );
    }

    public static void PrepareForPacking(
        ItemDataUnpacked item,
        IReadOnlyList<int> dedicatedIds = null,
        bool allowAdditionalVariants = false
    )
    {
        var entry = UniqueList.getUnique(item.uniqueID);
        if (!HasVariants(entry))
            return;
        var ids = new List<int>();
        var variants = new List<int>();
        var dedicated = dedicatedIds == null ? null : new HashSet<int>(dedicatedIds);
        if (
            allowAdditionalVariants && (dedicated == null || dedicated.Count != VariantCount(entry))
        )
            throw new InvalidOperationException(
                "Choose every exclusive unique modifier before packing."
            );
        foreach (var affix in item.affixes)
        {
            if (affix.IsNullOrDestroyed())
                throw new InvalidOperationException("Missing affix before unique packing.");
            ids.Add(affix.affixId);
            bool belongsToPool = entry.droppableLegendaryAffixes.Contains(affix.affixId);
            if (
                affix.specialAffixType == AffixList.SpecialAffixType.FakeUniqueMod
                && !belongsToPool
                && !allowAdditionalVariants
            )
                throw new InvalidOperationException("The unique has an unrelated fixed modifier.");
            if (allowAdditionalVariants ? dedicated.Contains(affix.affixId) : belongsToPool)
            {
                if (!belongsToPool)
                    throw new InvalidOperationException(
                        "Dedicated modifier is outside the unique's native pool."
                    );
                variants.Add(affix.affixId);
            }
        }
        if (variants.Count != VariantCount(entry))
            throw new InvalidOperationException(
                "A fixed unique modifier is missing before packing."
            );
        var order = ForceDropPackingOrder.UniquePrefixIndices(ids, variants);
        bool changed = false;
        for (int i = 0; i < order.Count; i++)
            changed |= order[i] != i;
        if (!changed)
            return;
        var ordered = new Il2CppSystem.Collections.Generic.List<ItemAffix>();
        foreach (int index in order)
            ordered.Add(item.affixes[index]);
        item.affixes = ordered;
    }

    static string Signature(ItemAffix affix, bool allowIllegalT8 = false)
    {
        return affix.affixId
            + ":"
            + affix.affixTier
            + ":"
            + affix.affixRoll
            + ":"
            + (
                allowIllegalT8
                && affix.affixTier == 7
                && affix.sealedAffixType == SealedAffixType.None
                    ? SealedAffixType.Primordial
                    : affix.sealedAffixType
            )
            + ":"
            + affix.specialAffixType
            + ":"
            + affix.affixType;
    }

    public static void VerifySelection(ItemDataUnpacked item, params int[] ids) =>
        VerifySelection(item, false, ids);

    public static void VerifySelection(
        ItemDataUnpacked item,
        bool allowAdditionalVariants,
        params int[] ids
    )
    {
        var entry = UniqueList.getUnique(item.uniqueID);
        var catalog = Catalog(entry);
        if (!HasVariants(entry) || ids == null || ids.Length != VariantCount(entry))
            throw new InvalidOperationException(
                "Invalid unique variant selection; no item was dropped"
            );
        var expected = new HashSet<int>(ids);
        if (expected.Count != ids.Length)
            throw new InvalidOperationException("Duplicate unique variants; no item was dropped");
        int variantCount = 0;
        int index = -1;
        foreach (var saved in item.affixes)
        {
            index++;
            if (saved.IsNullOrDestroyed())
                throw new InvalidOperationException("Invalid packed affix; no item was dropped");
            if (
                saved.specialAffixType != AffixList.SpecialAffixType.FakeUniqueMod
                && !entry.droppableLegendaryAffixes.Contains(saved.affixId)
            )
                continue;
            // Only the native fixed-count prefix belongs to the dedicated
            // variant selection. Illegal extras are verified independently
            // against the request and then the complete decoded item ID.
            if (allowAdditionalVariants && !expected.Contains(saved.affixId))
            {
                if (index < ids.Length)
                    throw new InvalidOperationException(
                        "An extra modifier displaced the unique prefix."
                    );
                continue;
            }
            var definition = catalog.Find(a => a.affixId == saved.affixId);
            if (
                (allowAdditionalVariants && index >= ids.Length)
                || !expected.Remove(saved.affixId)
                || definition.IsNullOrDestroyed()
                || saved.specialAffixType != definition.specialAffixType
                || saved.sealedAffixType != SealedAffixType.None
                || saved.affixTier != 0
                || saved.affixRoll != 255
            )
                throw new InvalidOperationException(
                    "Unique variant packing changed a selected modifier; no item was dropped"
                );
            variantCount++;
        }
        if (variantCount != ids.Length || expected.Count != 0)
            throw new InvalidOperationException(
                "Unique variant missing after packing; no item was dropped"
            );
    }
}
