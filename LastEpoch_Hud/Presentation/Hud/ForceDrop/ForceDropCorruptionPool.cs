using System;
using System.Collections.Generic;
using Il2Cpp;
using LastEpoch_Hud.Scripts.Core.ForceDrop;

namespace LastEpoch_Hud.Scripts.ModUI.ForceDrop;

// Built once per immutable, uncorrupted selection. Native outcome eligibility
// and level-dependent tier checks are not repeated for every affix or frame.
public sealed class ForceDropCorruptionPool
{
    readonly Dictionary<int, int> masks = new Dictionary<int, int>();
    readonly Dictionary<int, WeightedCorruptionOutcome[]> outcomes =
        new Dictionary<int, WeightedCorruptionOutcome[]>();
    public string Error { get; }

    public ForceDropCorruptionPool(ItemDataUnpacked item)
    {
        try
        {
            if (
                item.IsNullOrDestroyed()
                || item.corrupted
                || !item.CanBeCorrupted()
                || !item.TryGetCorruptionConfig(out CorruptionCategoryConfig category)
                || category.IsNullOrDestroyed()
                || category.positiveChance <= 0
            )
            {
                Error = "The game has no positive corruption route for this item";
                return;
            }
            var ids = new List<int>();
            foreach (var affix in item.affixes)
                if (
                    !affix.IsNullOrDestroyed()
                    && !UniqueVariantAdapter.IsVariant(ForceDropCatalog.Find(affix.affixId))
                )
                    ids.Add(affix.affixId);
            var context = new ForceDropLegalAffixes(item.itemType, item.subType, item.rarity, ids);
            var candidates = new List<WeightedCorruptionOutcome>();
            if (!category.positiveOutcomes.IsNullOrDestroyed())
                foreach (var outcome in category.positiveOutcomes)
                    AddOutcome(item, outcome, candidates);
            if (!category.cannotCombineWithType)
            {
                var list = ItemList.get();
                var config = list.GetCorruptionOutcomeConfig();
                if (
                    !config.IsNullOrDestroyed()
                    && !config.corruptionEquipmentTypeConfig.IsNullOrDestroyed()
                )
                    foreach (var equipment in config.corruptionEquipmentTypeConfig)
                        if (
                            !equipment.IsNullOrDestroyed()
                            && equipment.type == list.GetEquipmentTypeForBaseType(item.itemType)
                            && !equipment.positiveOutcomes.IsNullOrDestroyed()
                        )
                            foreach (var outcome in equipment.positiveOutcomes)
                                AddOutcome(item, outcome, candidates);
            }
            int nativeMask = 0;
            int runeMaximum = ForceDropLegalTiers.RuneMaximumDisplayTier(
                context.IsEquipment && item.rarity < 7,
                item.LvlReq
            );
            for (int tier = 0; tier < 7; tier++)
                if (
                    tier < runeMaximum
                    && (
                        runeMaximum == 5 ? item.TierValidForCorruptionLowLevel(tier)
                        : runeMaximum == 6 ? item.TierValidForCorruptionNoT7(tier)
                        : item.TierValidForCorruptionNoRestrictions(tier)
                    )
                )
                    nativeMask |= 1 << tier;
            foreach (var definition in ForceDropCatalog.Definitions())
            {
                if (
                    definition.IsNullOrDestroyed()
                    || UniqueVariantAdapter.IsVariant(definition)
                    || ids.Contains(definition.affixId)
                    || !context.Fits(definition, false)
                )
                    continue;
                bool conflictingFamily = false;
                if (
                    definition.specialAffixType == AffixList.SpecialAffixType.Set
                    || definition.specialAffixType == AffixList.SpecialAffixType.Experimental
                )
                    foreach (int id in ids)
                    {
                        var existing = ForceDropCatalog.Find(id);
                        if (
                            !existing.IsNullOrDestroyed()
                            && existing.specialAffixType == definition.specialAffixType
                        )
                            conflictingFamily = true;
                    }
                if (conflictingFamily)
                    continue;
                var byTier = new WeightedCorruptionOutcome[7];
                int mask = 0;
                foreach (var candidate in candidates)
                {
                    if (
                        !Matches(
                            definition,
                            candidate.corruptionOutcome,
                            context.IsChampion(definition)
                        )
                    )
                        continue;
                    var weights = new List<float>();
                    if (!candidate.tierWeights.IsNullOrDestroyed())
                        foreach (float weight in candidate.tierWeights)
                            weights.Add(weight);
                    int candidateMask = ForceDropLegalTiers.FromWeights(
                        weights,
                        definition.tiers.IsNullOrDestroyed() ? 0 : definition.tiers.Count,
                        nativeMask
                    );
                    for (int tier = 0; tier < 7; tier++)
                        if (ForceDropLegalTiers.Supports(candidateMask, tier))
                            byTier[tier] = candidate;
                    mask |= candidateMask;
                }
                if (mask != 0)
                {
                    masks[definition.affixId] = mask;
                    outcomes[definition.affixId] = byTier;
                }
            }
            Error =
                masks.Count == 0 ? "No compatible additive corruption affixes for this item" : "";
        }
        catch (Exception ex)
        {
            masks.Clear();
            outcomes.Clear();
            Error = "Native corruption eligibility unavailable: " + ex.Message;
        }
    }

    static void AddOutcome(
        ItemDataUnpacked item,
        WeightedCorruptionOutcome outcome,
        List<WeightedCorruptionOutcome> target
    )
    {
        if (
            !outcome.IsNullOrDestroyed()
            && outcome.weight > 0
            && (outcome.maximumChanceType.ToString() != "Custom" || outcome.maximumChance > 0)
            && !outcome.replacesAffix
            && outcome.extraCorruptions == 0
            && item.CorruptionOutcomeCanApplyToItem(outcome)
        )
            target.Add(outcome);
    }

    static bool Matches(AffixList.Affix definition, CorruptionOutcome outcome, bool champion)
    {
        string route = outcome.ToString();
        switch (definition.specialAffixType)
        {
            case AffixList.SpecialAffixType.Corrupted:
                return route == "AddsCorruptedAffix" || route == "AddsLowTierCorruptedAffix";
            case AffixList.SpecialAffixType.Standard:
                return definition.uniqueId == 0
                    && (route == "AddStandardAffix" || route == "AddLowTierStandardAffix");
            case AffixList.SpecialAffixType.Experimental:
                return route == "AddExperimentalAffix";
            case AffixList.SpecialAffixType.Personal:
                return champion && route == "AddChampionAffix";
            case AffixList.SpecialAffixType.Set:
                return route == "AddSetAffix";
            default:
                return false;
        }
    }

    public int TierMask(int id) => masks.TryGetValue(id, out int mask) ? mask : 0;

    public WeightedCorruptionOutcome Outcome(int id, int tier) =>
        ForceDropLegalTiers.Supports(TierMask(id), tier) ? outcomes[id][tier] : null;

    public IEnumerable<AffixList.Affix> Definitions()
    {
        foreach (int id in masks.Keys)
            yield return ForceDropCatalog.Find(id);
    }
}
