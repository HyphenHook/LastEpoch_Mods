using System;
using System.Collections.Generic;
using Il2Cpp;
using LastEpoch_Hud.Scripts.Core.ForceDrop;
using LastEpoch_Hud.Scripts.ModUI.Settings;
using UnityEngine.UI;
using FD = LastEpoch_Hud.Scripts.ModUI.ForceDrop.ForceDropModel;

namespace LastEpoch_Hud.Scripts.ModUI.ForceDrop;

// Native translations are presentation only. Selection uses catalog indices and ids.
internal static class NativeItemNames
{
    internal sealed class Category
    {
        public int baseType;
        public string raw,
            name;
    }

    internal sealed class ItemChoice
    {
        public int subType;
        public ushort uniqueId;
        public UniqueList.LegendaryType legendaryType;
        public string raw,
            name,
            aliases;
    }

    internal sealed class SearchChoice
    {
        public Category category;
        public ItemChoice item;
        public int rarity;
        public ForceDropItemIdentity Identity =>
            new(category.baseType, item.subType, rarity, item.uniqueId);
        public string RarityLabel =>
            rarity == 7 ? "Unique"
            : rarity == 8 ? "Set"
            : "Base Item";
        public string aliases;
    }

    public static string CatalogKey
    {
        get
        {
            var list = ItemList.get();
            return list.IsNullOrDestroyed()
                ? ""
                : list.Pointer.ToString()
                    + ":"
                    + (
                        UniqueList.instance.IsNullOrDestroyed()
                            ? ""
                            : UniqueList.instance.Pointer.ToString()
                    );
        }
    }

    public static List<SearchChoice> SearchCatalog(Dictionary<int, Category> categories)
    {
        var result = new List<SearchChoice>();
        var list = ItemList.get();
        if (list.IsNullOrDestroyed())
            return result;
        var byType = new Dictionary<int, Category>();
        foreach (var category in categories.Values)
            if (category.raw.IndexOf("blessing", StringComparison.OrdinalIgnoreCase) < 0)
                byType[category.baseType] = category;
        foreach (var type in list.EquippableItems)
            if (byType.TryGetValue(type.baseTypeID, out var category))
                foreach (var item in type.subItems)
                    result.Add(
                        new SearchChoice
                        {
                            category = category,
                            item = BaseItem(
                                category.baseType,
                                item.subTypeID,
                                item.displayName,
                                item.name
                            ),
                            rarity = 0,
                        }
                    );
        foreach (var type in list.nonEquippableItems)
            if (byType.TryGetValue(type.baseTypeID, out var category))
                foreach (var item in type.subItems)
                    result.Add(
                        new SearchChoice
                        {
                            category = category,
                            item = BaseItem(
                                category.baseType,
                                item.subTypeID,
                                item.displayName,
                                item.name
                            ),
                            rarity = 0,
                        }
                    );
        if (UniqueList.instance.IsNullOrDestroyed())
            UniqueList.getUnique(0);
        if (!UniqueList.instance.IsNullOrDestroyed())
            foreach (var unique in UniqueList.instance.uniques)
                if (
                    byType.TryGetValue(unique.baseType, out var category)
                    && !unique.subTypes.IsNullOrDestroyed()
                    && unique.subTypes.Count > 0
                )
                    result.Add(
                        new SearchChoice
                        {
                            category = category,
                            item = UniqueItem(unique),
                            rarity = unique.isSetItem ? 8 : 7,
                        }
                    );
        var rarityNames = new Dictionary<int, string>
        {
            [0] = RarityName("Base Item"),
            [7] = RarityName("Unique"),
            [8] = RarityName("Set"),
        };
        foreach (var choice in result)
            choice.aliases =
                choice.item.aliases
                + "\n"
                + choice.category.raw
                + "\n"
                + choice.category.name
                + "\n"
                + choice.RarityLabel
                + "\n"
                + rarityNames[choice.rarity];
        return result;
    }

    public static bool SelectSearchChoice(SearchChoice choice)
    {
        int categoryIndex = -1;
        foreach (KeyValuePair<int, Category> entry in Categories(FD.TypeDropdown))
        {
            if (entry.Value.baseType == choice.category.baseType)
            {
                categoryIndex = entry.Key;
            }
        }
        if (categoryIndex < 1)
            return false;
        // Clear the old identity before rebuilding dependent catalogs. Failure
        // cannot leave a previous item's id attached to the new category.
        FD.ItemSubtype = -1;
        FD.ItemUniqueId = 0;
        FD.TypeDropdown.SetValueWithoutNotify(categoryIndex);
        SelectCategory(choice.category);
        int rarityIndex = -1;
        for (int i = 1; i < FD.RarityDropdown.options.Count; i++)
        {
            if (FD.RarityDropdown.options[i].text == choice.RarityLabel)
            {
                rarityIndex = i;
            }
        }
        if (rarityIndex < 1)
            return false;
        FD.RarityDropdown.SetValueWithoutNotify(rarityIndex);
        FD.SelectRarity();
        Dictionary<int, ItemChoice> items = Items(
            FD.ItemsDropdown,
            choice.category.baseType,
            choice.rarity
        );
        var identities = new List<(int, ForceDropItemIdentity)>();
        foreach (var entry in items)
            identities.Add(
                (
                    entry.Key,
                    new ForceDropItemIdentity(
                        choice.category.baseType,
                        entry.Value.subType,
                        choice.rarity,
                        entry.Value.uniqueId
                    )
                )
            );
        int option = ForceDropItemSearch.FindOption(choice.Identity, identities);
        if (option < 1)
            return false;
        FD.ItemsDropdown.SetValueWithoutNotify(option);
        SelectItem(items[option]);
        return true;
    }

    public static string Locale
    {
        get
        {
            try
            {
                return Il2Cpp.Localization.Locale ?? "";
            }
            catch
            {
                return "";
            }
        }
    }

    static string Name(Func<string> translated, string fallback)
    {
        try
        {
            string value = translated();
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }
        catch { }
        return fallback ?? "";
    }

    static string Raw(string display, string internalName)
    {
        return ForceDropSelection.CatalogLabel(display, internalName);
    }

    public static string RarityName(string raw)
    {
        string fallback = LocaleRegistry.Translate(raw);
        if (raw == "Unique")
            return Name(() => Il2Cpp.Localization.Items.GetRarityName(7, false), fallback);
        if (raw == "Set")
            return Name(() => Il2Cpp.Localization.Items.GetRarityName(8, false), fallback);
        return fallback;
    }

    public static string AffixName(AffixList.Affix definition)
    {
        if (definition.IsNullOrDestroyed())
            return "None";
        string fallback = Raw(definition.affixDisplayName, definition.affixName);
        return Name(
            () => Il2Cpp.Localization.Items.GetAffixDisplayName(definition.affixId, fallback),
            fallback
        );
    }

    public static string AffixName(int id, string fallback)
    {
        return Name(() => Il2Cpp.Localization.Items.GetAffixDisplayName(id, fallback), fallback);
    }

    public static string AffixAliases(AffixList.Affix definition)
    {
        return definition.IsNullOrDestroyed()
            ? ""
            : definition.affixName + "\n" + definition.affixDisplayName;
    }

    public static Dictionary<int, Category> Categories(Dropdown dropdown)
    {
        var result = new Dictionary<int, Category>();
        var list = ItemList.get();
        if (list.IsNullOrDestroyed() || dropdown.IsNullOrDestroyed())
            return result;
        var entries = new List<Category>();
        foreach (var type in list.EquippableItems)
            entries.Add(new Category { baseType = type.baseTypeID, raw = type.BaseTypeName });
        foreach (var type in list.nonEquippableItems)
            entries.Add(new Category { baseType = type.baseTypeID, raw = type.BaseTypeName });
        // Refuse an index map if the legacy catalog has changed order or contents.
        if (dropdown.options.Count != entries.Count + 1)
            return result;
        for (int i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            if (!string.Equals(dropdown.options[i + 1].text, entry.raw, StringComparison.Ordinal))
                return new Dictionary<int, Category>();
            entry.name = Name(
                () => Il2Cpp.Localization.Items.GetBaseTypeName(entry.baseType),
                entry.raw
            );
            result.Add(i + 1, entry);
        }
        return result;
    }

    public static Dictionary<int, ItemChoice> Items(Dropdown dropdown, int baseType, int rarity)
    {
        var result = new Dictionary<int, ItemChoice>();
        var list = ItemList.get();
        if (list.IsNullOrDestroyed() || dropdown.IsNullOrDestroyed())
            return result;
        var entries = new List<ItemChoice>();
        if (rarity == 0)
        {
            bool equipment = false;
            foreach (var type in list.EquippableItems)
                if (type.baseTypeID == baseType)
                {
                    equipment = true;
                    foreach (var item in type.subItems)
                        entries.Add(
                            BaseItem(baseType, item.subTypeID, item.displayName, item.name)
                        );
                }
            if (!equipment)
                foreach (var type in list.nonEquippableItems)
                    if (type.baseTypeID == baseType)
                        foreach (var item in type.subItems)
                            entries.Add(
                                BaseItem(baseType, item.subTypeID, item.displayName, item.name)
                            );
        }
        else if ((rarity == 7 || rarity == 8) && !UniqueList.instance.IsNullOrDestroyed())
            foreach (var unique in UniqueList.instance.uniques)
                if (unique.baseType == baseType && unique.isSetItem == (rarity == 8))
                {
                    if (unique.subTypes.IsNullOrDestroyed() || unique.subTypes.Count == 0)
                        continue;
                    entries.Add(UniqueItem(unique));
                }
        if (dropdown.options.Count != entries.Count + 1)
            return result;
        for (int i = 0; i < entries.Count; i++)
        {
            if (
                !string.Equals(
                    dropdown.options[i + 1].text,
                    entries[i].raw,
                    StringComparison.Ordinal
                )
            )
                return new Dictionary<int, ItemChoice>();
            result.Add(i + 1, entries[i]);
        }
        return result;
    }

    static ItemChoice UniqueItem(UniqueList.Entry unique)
    {
        string raw = Raw(unique.displayName, unique.name);
        ushort id = unique.uniqueID;
        return new ItemChoice
        {
            subType = unique.subTypes[0],
            uniqueId = id,
            legendaryType = unique.legendaryType,
            raw = raw,
            name = Name(() => Il2Cpp.Localization.Items.GetUniqueName(id, true, false), raw),
            aliases = raw + "\n" + unique.name + "\n" + unique.alternativeSearchName,
        };
    }

    static ItemChoice BaseItem(int baseType, int subType, string display, string internalName)
    {
        string raw = Raw(display, internalName);
        return new ItemChoice
        {
            subType = subType,
            raw = raw,
            name = Name(() => Il2Cpp.Localization.Items.GetSubTypeName(baseType, subType), raw),
            aliases = raw + "\n" + internalName,
        };
    }

    public static void SelectCategory(Category category)
    {
        FD.ItemType = category.baseType;
        FD.UpdateRarity();
        FD.UpdateItems();
    }

    public static void SelectItem(ItemChoice item)
    {
        FD.ItemSubtype = item.subType;
        FD.ItemUniqueId = item.uniqueId;
        FD.ItemLegendaryType = item.legendaryType;
    }

    public static bool Matches(string query, string translated, string aliases)
    {
        return ForceDropItemSearch.Matches(query, translated, aliases);
    }
}
