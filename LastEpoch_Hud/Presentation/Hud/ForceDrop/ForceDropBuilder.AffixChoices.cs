using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using HarmonyLib;
using Il2Cpp;
using Il2CppTMPro;
using LastEpoch_Hud.Scripts.Core.ForceDrop;
using LastEpoch_Hud.Scripts.Core.Items;
using LastEpoch_Hud.Scripts.ModUI.Settings;
using LastEpoch_Hud.Scripts.ModUI.Shell;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using FD = LastEpoch_Hud.Scripts.ModUI.ForceDrop.ForceDropModel;

namespace LastEpoch_Hud.Scripts.ModUI.ForceDrop;

public static partial class ForceDropBuilder
{

    static void RagePicker(int slot)
    {
        choices.Clear();
        foreach (var definition in UniqueVariantAdapter.Catalog(SelectedRageEntry()))
        {
            int id = definition.affixId;
            if (
                variantIds[1 - slot] == id
                || Array.Exists(rows, row => row.id == id)
                || (corrupted && corruptionId == id)
            )
                continue;
            string name = NativeItemNames.AffixName(definition);
            choices.Add(
                new Choice
                {
                    id = id,
                    name = name,
                    aliases = NativeItemNames.AffixAliases(definition),
                    select = () =>
                    {
                        variantIds[slot] = id;
                        variantNames[slot] = name;
                        Caption(variantSelect[slot], name);
                    },
                }
            );
        }
        choices.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase));
        OpenPicker(
            UniqueVariantAdapter.IsUnsated(SelectedRageEntry())
                ? "Unsated Rage — exclusive ring modifier"
            : slot == 0 ? "Withstand the Elements — modifier 1"
            : "Withstand the Elements — modifier 2"
        );
    }

    static void CorruptionPicker()
    {
        choices.Clear();
        choices.Add(
            new Choice
            {
                id = -1,
                name = "None",
                select = () =>
                {
                    corruptionId = -1;
                    corruptionName = "None";
                    Caption(corruptionSelect, "Corrupted affix: None");
                },
            }
        );
        var pool = allowIllegal ? null : CorruptionPool();
        if (!allowIllegal && (pool == null || corruptionPoolError.Length > 0))
            result = corruptionPoolError;
        var definitions =
            allowIllegal ? ForceDropCatalog.Definitions()
            : pool == null ? Array.Empty<AffixList.Affix>()
            : pool.Definitions();
        foreach (var a in definitions)
        {
            if (a.IsNullOrDestroyed() || ForceDropCatalog.MaximumTier(a, RouteMaximum) == 0)
                continue;
            int id = a.affixId;
            if (Array.Exists(rows, row => row.id == id) || Array.IndexOf(variantIds, id) >= 0)
                continue;
            string name = NativeItemNames.AffixName(a);
            bool champion = LegalContext(-1).IsChampion(a);
            string family = champion ? "Champion" : a.specialAffixType.ToString();
            choices.Add(
                new Choice
                {
                    id = id,
                    name =
                        name
                        + " · "
                        + L(
                            CorruptedAffixAdapter.IsCorruption(a) ? "Corruption-exclusive" : family
                        ),
                    aliases = NativeItemNames.AffixAliases(a) + "\n" + family,
                    classMask = AffixClassMask(a),
                    affixFamily = ForceDropLegalAffixes.Family(a),
                    champion = champion,
                    suffix = a.type == AffixList.AffixType.SUFFIX,
                    select = () =>
                    {
                        corruptionId = id;
                        corruptionName = name;
                        corrupted = true;
                        Caption(corruptButton, "Corrupted: Yes");
                        Caption(corruptionSelect, name);
                        RefreshTierLimits();
                    },
                }
            );
        }
        choices.Sort(CompareAffixChoices);
        Main.logger_instance.Msg(
            (
                allowIllegal
                    ? "Force Drop illegal corruption pool: base="
                    : "Force Drop legal corruption pool: base="
            )
                + FD.ItemType
                + ", subtype="
                + FD.ItemSubtype
                + ", unique="
                + FD.ItemUniqueId
                + ", choices="
                + (choices.Count - 1)
                + ", status="
                + corruptionPoolError
        );
        OpenPicker("Corrupted affix", true);
    }

    static void CatalogPicker(Dropdown catalog, Action changed, bool skipPlaceholder)
    {
        choices.Clear();
        for (int i = skipPlaceholder ? 1 : 0; i < catalog.options.Count; i++)
        {
            string label = catalog.options[i].text;
            if (string.IsNullOrWhiteSpace(label))
                continue;
            if (
                catalog == FD.TypeDropdown
                && label.IndexOf("blessing", StringComparison.OrdinalIgnoreCase) >= 0
            )
                continue;
            nativeCategories.TryGetValue(i, out var category);
            string display =
                catalog == FD.TypeDropdown
                    ? CategoryName(i, label)
                    : NativeItemNames.RarityName(label);
            int index = i;
            choices.Add(
                new Choice
                {
                    group = CategoryGroup(label),
                    name = display,
                    aliases = label + "\n" + CategoryLabel(label),
                    select = () =>
                    {
                        catalog.SetValueWithoutNotify(index);
                        if (catalog == FD.TypeDropdown && category != null)
                        {
                            NativeItemNames.SelectCategory(category);
                        }
                        else
                        {
                            changed();
                        }
                        ResetRage();
                        foreach (var r in rows)
                        {
                            r.id = -1;
                            r.name = "None";
                            Caption(r.select, "None");
                        }
                        corruptionId = -1;
                        corruptionName = "None";
                        lastItems = "";
                    },
                }
            );
        }
        OpenPicker(catalog == FD.TypeDropdown ? "Category" : "Rarity");
        categoryPicker = catalog == FD.TypeDropdown;
        rarityPicker = !categoryPicker;
        RefreshPicker();
    }

    static void AffixPicker(int slot)
    {
        choices.Clear();
        choices.Add(
            new Choice
            {
                id = -1,
                name = "None",
                select = () =>
                {
                    rows[slot].id = -1;
                    rows[slot].name = "None";
                    Caption(rows[slot].select, "None");
                },
            }
        );
        var list = AffixList.get();
        if (list.IsNullOrDestroyed())
            return;
        var exclusions = new Dictionary<string, int>();
        foreach (var a in ForceDropCatalog.Definitions())
        {
            string reason = AddAffixChoice(a, slot);
            if (reason.Length > 0)
            {
                exclusions.TryGetValue(reason, out int count);
                exclusions[reason] = count + 1;
            }
        }
        var excluded = new StringBuilder();
        foreach (var pair in exclusions)
            excluded.Append(pair.Key).Append('=').Append(pair.Value).Append(';');
        choices.Sort(CompareAffixChoices);
        Main.logger_instance.Msg(
            (
                allowIllegal
                    ? "Force Drop illegal affix pool: base="
                    : "Force Drop legal affix pool: base="
            )
                + FD.ItemType
                + ", subtype="
                + FD.ItemSubtype
                + ", unique="
                + FD.ItemUniqueId
                + ", slot="
                + slot
                + ", choices="
                + (choices.Count - 1)
                + ", excluded="
                + excluded
        );
        bool idolEnchantment = LegalContext(slot).IsHereticalIdol && (slot == 1 || slot == 3);
        OpenPicker(
            idolEnchantment ? "Idol enchantment"
                : slot == AffixLimits.SealedSlot ? "Sealed affix"
                : slot == AffixLimits.PrimordialSlot ? "Primordial affix"
                : "Affix " + (slot + 1),
            true,
            // Any affix fits any slot, so the picker always lists prefixes and suffixes together.
            -1
        );
    }

    static string AddAffixChoice(AffixList.Affix a, int slot)
    {
        var context = LegalContext(slot);
        if (
            !allowIllegal
            && !ForceDropLegalRules.SlotAllowed(
                slot,
                context.IsIdol,
                context.IsUnique,
                context.IsSet,
                context.IsHereticalIdol,
                context.BaseType
            )
        )
            return "Unavailable slot";
        string reason = context.OrdinaryReason(
            a,
            AffixLimits.IsSealedSlot(slot),
            context.IsHereticalIdol && (slot == 1 || slot == 3)
        );
        if (!allowIllegal && reason.Length > 0)
            return reason;
        if (a.IsNullOrDestroyed() || ForceDropCatalog.MaximumTier(a, RouteMaximum) == 0)
            return "Definition/tier unavailable";
        if (
            slot == AffixLimits.PrimordialSlot
            && !ForceDropModeRules.CanSealPrimordial(
                FD.ItemType,
                FD.ItemRarity,
                ForceDropLegalAffixes.Family(a),
                ForceDropCatalog.MaximumTier(a, 8),
                allowIllegal ? ForceDropMode.Illegal : ForceDropMode.Legal
            )
        )
            return "Not eligible for a T8 Primordial seal";
        int id = a.affixId;
        for (int other = 0; other < rows.Length; other++)
            if (other != slot && rows[other].id == id)
                return "Placement or duplicate exclusion";
        if (corrupted && corruptionId == id)
            return "Placement or duplicate exclusion";
        if (Array.IndexOf(variantIds, id) >= 0)
            return "Already selected as a unique modifier";
        if (choices.Exists(x => x.id == id))
            return "Placement or duplicate exclusion";
        string name = NativeItemNames.AffixName(a);
        choices.Add(
            new Choice
            {
                id = id,
                name =
                    a.specialAffixType == AffixList.SpecialAffixType.Standard
                        ? name
                        : name
                            + " · "
                            + L(
                                context.IsChampion(a) ? "Champion"
                                : a.specialAffixType == AffixList.SpecialAffixType.IdolWeaver
                                    ? "Weaver idol"
                                : a.specialAffixType == AffixList.SpecialAffixType.IdolEnchantment
                                    ? "Idol enchantment"
                                : a.specialAffixType.ToString()
                            ),
                aliases = NativeItemNames.AffixAliases(a) + "\n" + a.specialAffixType,
                classMask = AffixClassMask(a),
                affixFamily = ForceDropLegalAffixes.Family(a),
                champion = context.IsChampion(a),
                suffix = a.type == AffixList.AffixType.SUFFIX,
                select = () =>
                {
                    rows[slot].id = id;
                    rows[slot].name = name;
                    Caption(rows[slot].select, name);
                },
            }
        );
        return "";
    }

    // Search by the definition's native class specificity, not CanRollOn for
    // the currently selected base/subtype. The latter wrongly hides affixes in
    // Illegal mode (even +Skeletons while browsing an Acolyte-unrelated item).
    static int AffixClassMask(AffixList.Affix affix)
    {
        if (affix.IsNullOrDestroyed())
            return ForceDropClassSearch.AllClasses;
        try
        {
            return ForceDropClassSearch.MaskFromSpecificity(affix.classSpecificity.ToString());
        }
        catch (Exception)
        {
            // Unknown metadata must not silently shrink the unrestricted pool.
            return ForceDropClassSearch.AllClasses;
        }
    }

    static bool MatchesPickerSearch(Choice choice, string query) =>
        ForceDropClassSearch.Matches(
            query,
            choice.classMask,
            text => NativeItemNames.Matches(text, choice.name, choice.aliases)
        );
}
