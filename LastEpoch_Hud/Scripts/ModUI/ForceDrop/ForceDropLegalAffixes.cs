using System;
using System.Collections.Generic;
using Il2Cpp;
using LastEpoch_Hud.Scripts.Core.ForceDrop;
using LastEpoch_Hud.Scripts.Mods.Items.Requirements;

namespace LastEpoch_Hud.Scripts.ModUI.ForceDrop;

// Shared by ordinary pickers, corruption pickers and the creation boundary.
// Native catalog metadata owns compatibility. Never use player class or the
// class/weapon-restriction cheat's modified item requirements for legal pools.
public sealed class ForceDropLegalAffixes
{
    readonly List<AffixList.Affix> existing = new List<AffixList.Affix>();
    readonly Dictionary<int, string> ordinaryReasons = new Dictionary<int, string>();
    readonly List<Func<AffixList.Affix, bool>> donors = new List<Func<AffixList.Affix, bool>>();
    readonly HashSet<int> champions = new HashSet<int>();
    readonly ItemList.ClassRequirement requirement;
    readonly EquipmentType equipmentType;
    readonly bool known;
    readonly int compatibleClasses;
    public int BaseType { get; }
    public int SubType { get; }
    public int Rarity { get; }
    public bool IsIdol => BaseType >= 25 && BaseType <= 33;
    public bool IsAltar => BaseType == 41;
    public bool IsWeaverIdol { get; }
    public bool IsHereticalIdol { get; }
    public bool IsUnique => Rarity >= 7;
    public bool IsSet => Rarity == 8;
    public bool IsEquipment => BaseType >= 0 && BaseType <= 24;
    public int BaseLevel { get; }

    public ForceDropLegalAffixes(
        int baseType,
        int subType,
        int rarity,
        IEnumerable<int> existingIds
    )
    {
        BaseType = baseType;
        SubType = subType;
        Rarity = rarity;
        var list = ItemList.get();
        requirement = ItemList.ClassRequirement.Any;
        if (!list.IsNullOrDestroyed())
            foreach (var type in list.EquippableItems)
                if (type.baseTypeID == baseType)
                {
                    equipmentType = type.type;
                    foreach (var sub in type.subItems)
                        if (sub.subTypeID == subType)
                        {
                            known = Items_Req_Class.TryGetOriginalRequirement(
                                baseType,
                                subType,
                                sub.classRequirement,
                                out var original
                            );
                            requirement = Normalize(original);
                            BaseLevel = sub.levelRequirement;
                            // Use the internal native subtype name, never its
                            // translated display label. Current wrappers do not
                            // expose these classifications in the catalog dump.
                            IsWeaverIdol =
                                IsIdol
                                && (sub.name ?? "").IndexOf("Weaver", StringComparison.Ordinal)
                                    >= 0;
                            IsHereticalIdol =
                                IsIdol
                                && (sub.name ?? "").StartsWith(
                                    "Heretical ",
                                    StringComparison.Ordinal
                                );
                            break;
                        }
                    break;
                }
        var data = ChampionDataList.Instance;
        if (!data.IsNullOrDestroyed() && !data.mods.IsNullOrDestroyed())
            foreach (var mod in data.mods)
                if (!mod.IsNullOrDestroyed())
                    champions.Add(mod.affixId);
        int classMask = RequirementMask(requirement);
        foreach (int id in existingIds ?? Array.Empty<int>())
        {
            var definition = ForceDropCatalog.Find(id);
            if (!definition.IsNullOrDestroyed())
            {
                existing.Add(definition);
                classMask &= ClassMask(definition);
            }
        }
        compatibleClasses = classMask;
        if (!known || !IsUnique || IsIdol || IsSet || list.IsNullOrDestroyed())
            return;
        // A Legendary donor matches equipment type, not the unique-only subtype.
        // Keep actual donor subtypes so all selected affixes must coexist on ONE
        // donor. CanRollOnNormalSubTypes alone could accept different donors.
        foreach (var type in list.EquippableItems)
            if (type.baseTypeID == baseType)
                foreach (var sub in type.subItems)
                {
                    if (sub.IsUniqueOnlyBase || sub.isCorruptedSubtype || sub.obsoleteItem)
                        continue;
                    if (
                        !Items_Req_Class.TryGetOriginalRequirement(
                            baseType,
                            sub.subTypeID,
                            sub.classRequirement,
                            out var original
                        )
                    )
                        continue;
                    var donorRequirement = Normalize(original);
                    if ((RequirementMask(donorRequirement) & compatibleClasses) == 0)
                        continue;
                    int donorSubType = sub.subTypeID;
                    bool valid = true;
                    foreach (var definition in existing)
                        if (!definition.CanRollOn(baseType, donorSubType, donorRequirement))
                        {
                            valid = false;
                            break;
                        }
                    if (valid)
                        donors.Add(definition =>
                            definition.CanRollOn(baseType, donorSubType, donorRequirement)
                        );
                }
    }

    static ItemList.ClassRequirement Normalize(ItemList.ClassRequirement value) =>
        value == ItemList.ClassRequirement.None ? ItemList.ClassRequirement.Any : value;

    static int AllClasses
    {
        get
        {
            int mask = 0;
            foreach (string name in new[] { "Primalist", "Mage", "Sentinel", "Acolyte", "Rogue" })
                if (Enum.TryParse(name, out ItemList.ClassRequirement value))
                    mask |= (int)value;
            return mask;
        }
    }

    static int RequirementMask(ItemList.ClassRequirement requirement) =>
        requirement == ItemList.ClassRequirement.None
        || requirement == ItemList.ClassRequirement.Any
            ? AllClasses
            : (int)requirement;

    static int ClassMask(AffixList.Affix definition)
    {
        // These two enums have different bit values. Translate by names.
        string specificity = definition.classSpecificity.ToString();
        if (
            specificity == "None"
            || specificity.IndexOf("NonSpecific", StringComparison.Ordinal) >= 0
        )
            return AllClasses;
        int result = 0;
        foreach (string name in specificity.Split(','))
            if (Enum.TryParse(name.Trim(), out ItemList.ClassRequirement parsed))
                result |= (int)parsed;
        return result;
    }

    public bool IsChampion(AffixList.Affix definition) =>
        !definition.IsNullOrDestroyed()
        && champions.Contains(definition.affixId)
        && definition.specialAffixType == AffixList.SpecialAffixType.Personal;

    public static ForceDropAffixFamily Family(AffixList.Affix definition)
    {
        if (definition.IsNullOrDestroyed())
            return ForceDropAffixFamily.Unknown;
        if (UniqueVariantAdapter.IsVariant(definition))
            return ForceDropAffixFamily.UniqueModifier;
        return Enum.TryParse(
            definition.specialAffixType.ToString(),
            out ForceDropAffixFamily result
        )
            ? result
            : ForceDropAffixFamily.Unknown;
    }

    public static bool IsIdolAffix(AffixList.Affix definition)
    {
        if (definition.IsNullOrDestroyed())
            return false;
        if (
            definition.specialAffixType == AffixList.SpecialAffixType.IdolWeaver
            || definition.specialAffixType == AffixList.SpecialAffixType.IdolEnchantment
        )
            return true;
        var list = ItemList.get();
        if (list.IsNullOrDestroyed() || definition.canRollOn.IsNullOrDestroyed())
            return false;
        foreach (var type in list.EquippableItems)
            if (
                type.baseTypeID >= 25
                && type.baseTypeID <= 33
                && definition.canRollOn.Contains(type.type)
            )
                return true;
        return false;
    }

    public bool Fits(AffixList.Affix definition, bool transfer)
    {
        if (
            !known
            || definition.IsNullOrDestroyed()
            || compatibleClasses == 0
            || (compatibleClasses & ClassMask(definition)) == 0
        )
            return false;
        if (transfer)
        {
            foreach (var donor in donors)
                if (donor(definition))
                    return true;
            return false;
        }
        if (definition.specialAffixType == AffixList.SpecialAffixType.Set)
        {
            // uniqueId identifies the Set piece whose shard this is, not a
            // requirement that the target be that unique-only subtype.
            var piece = UniqueList.getUnique((ushort)definition.uniqueId);
            return !piece.IsNullOrDestroyed()
                && piece.isSetItem
                && piece.baseType == BaseType
                && !definition.canRollOn.IsNullOrDestroyed()
                && definition.canRollOn.Contains(equipmentType);
        }
        return definition.CanRollOn(BaseType, SubType, requirement);
    }

    public string OrdinaryReason(
        AffixList.Affix definition,
        bool sealedAffix,
        bool enchantment = false
    )
    {
        if (!known || (!IsEquipment && !IsIdol && !IsAltar))
            return "Item type unavailable";
        if (IsIdol && IsUnique)
            return "This item has no ordinary affix slots";
        if (IsSet && !sealedAffix)
            return "This item has no ordinary affix slots";
        if (sealedAffix && IsIdol)
            return "Sealed affixes do not apply to idols";
        if (definition.IsNullOrDestroyed())
            return "Definition unavailable";
        if (enchantment && !IsHereticalIdol)
            return "Enchantment requires a Heretical idol subtype";
        if (
            IsIdol
            && (definition.specialAffixType == AffixList.SpecialAffixType.IdolEnchantment)
                != enchantment
        )
            return "Affix belongs in a different idol slot";
        if (ordinaryReasons.TryGetValue(definition.affixId, out string reason))
            return reason;
        reason = EvaluateOrdinary(definition, enchantment);
        ordinaryReasons[definition.affixId] = reason;
        return reason;
    }

    string EvaluateOrdinary(AffixList.Affix definition, bool enchantment)
    {
        var family = Family(definition);
        if (
            !ForceDropLegalRules.OrdinaryFamilyAllowed(
                family,
                IsUnique,
                IsChampion(definition),
                IsIdol,
                IsWeaverIdol,
                enchantment
            )
        )
            return "Affix family belongs to another route";
        if (family == ForceDropAffixFamily.Standard && definition.uniqueId != 0)
            return "Item-exclusive modifier";
        if (ForceDropCatalog.MaximumTier(definition) == 0)
            return "No supported tiers";
        if (
            definition.type != AffixList.AffixType.PREFIX
            && definition.type != AffixList.AffixType.SUFFIX
        )
            return "No prefix or suffix placement";
        foreach (var selected in existing)
        {
            if (selected.affixId == definition.affixId)
                return "Already selected";
            if (
                family == ForceDropAffixFamily.Experimental
                && selected.specialAffixType == AffixList.SpecialAffixType.Experimental
            )
                return "An experimental affix is already selected";
            if (
                family == ForceDropAffixFamily.Set
                && selected.specialAffixType == AffixList.SpecialAffixType.Set
            )
                return "A Set affix is already selected";
        }
        return Fits(definition, IsUnique) ? "" : "Incompatible item type, subtype or class";
    }
}
