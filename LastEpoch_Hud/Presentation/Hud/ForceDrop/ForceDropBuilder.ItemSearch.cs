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

    static void RefreshItems()
    {
        nativeItems = NativeItemNames.Items(FD.ItemsDropdown, FD.ItemType, FD.ItemRarity);
        itemIndexes.Clear();
        itemMatches.Clear();
        if (SearchAllItems)
        {
            string key = NativeItemNames.CatalogKey;
            if (allItems == null || lastCatalogKey != key)
            {
                allItems = NativeItemNames.SearchCatalog(nativeCategories);
                lastCatalogKey = NativeItemNames.CatalogKey;
            }
            foreach (var item in allItems)
                if (ForceDropItemSearch.Matches(search.text, item.item.name, item.aliases))
                    itemMatches.Add(item);
        }
        else
        {
            for (int i = 1; i < FD.ItemsDropdown.options.Count; i++)
            {
                itemIndexes.Add(i);
            }
        }
        itemSearchStatus.text =
            SearchAllItems && VisibleItemCount == 0 ? L("No matching items") : "";
        itemScrollStart = -1;
        RefreshItemScrollRows();
    }

    static void ResetItemScroll()
    {
        if (itemScroll.IsNullOrDestroyed())
            return;
        itemScroll.StopMovement();
        itemScroll.content.anchoredPosition = Vector2.zero;
        itemScrollStart = -1;
    }

    static void RefreshItemScrollRows()
    {
        if (itemScroll.IsNullOrDestroyed())
            return;
        float height = Math.Max(1f, itemScroll.viewport.rect.height);
        itemScroll.content.sizeDelta = new Vector2(
            0,
            Math.Max(height, VisibleItemCount * AffixRowHeight)
        );
        int start = Math.Min(
            Math.Max(0, (int)(itemScroll.content.anchoredPosition.y / AffixRowHeight)),
            Math.Max(0, VisibleItemCount - 1)
        );
        if (start == itemScrollStart)
            return;
        itemScrollStart = start;
        var identity = new ForceDropItemIdentity(
            FD.ItemType,
            FD.ItemSubtype,
            FD.ItemRarity,
            FD.ItemUniqueId
        );
        for (int slot = 0; slot < itemButtons.Count; slot++)
        {
            int index = itemScrollStart + slot;
            itemButtons[slot].gameObject.SetActive(index < VisibleItemCount);
            if (index < VisibleItemCount)
            {
                var rect = itemButtons[slot].GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0, 1);
                rect.anchorMax = new Vector2(1, 1);
                rect.pivot = new Vector2(.5f, 1);
                rect.offsetMin = new Vector2(0, -(index + 1) * AffixRowHeight + 4);
                rect.offsetMax = new Vector2(0, -index * AffixRowHeight);
            }
            if (index < VisibleItemCount)
            {
                var match = SearchAllItems ? itemMatches[index] : null;
                bool selected =
                    FD.ItemsDropdown.value > 0
                    && (
                        match == null
                            ? itemIndexes[index] == FD.ItemsDropdown.value
                            : match.Identity == identity
                    );
                string name =
                    match == null
                        ? ItemName(itemIndexes[index])
                        : match.item.name
                            + " — "
                            + match.category.name
                            + " / "
                            + NativeItemNames.RarityName(match.RarityLabel);
                Caption(itemButtons[slot], (selected ? "Selected: " : "") + name);
                var label = itemButtons[slot].GetComponentInChildren<Text>(true);
                label.resizeTextForBestFit = SearchAllItems;
                label.resizeTextMinSize = 10;
                label.resizeTextMaxSize = 15;
                StyleSelection(itemButtons[slot], selected);
            }
        }
    }

    static void ChooseItem(int slot)
    {
        int i = itemScrollStart + slot;
        if (i >= VisibleItemCount)
            return;
        bool selected = true;
        if (SearchAllItems)
            selected = NativeItemNames.SelectSearchChoice(itemMatches[i]);
        else
        {
            int option = itemIndexes[i];
            FD.ItemsDropdown.SetValueWithoutNotify(option);
            FD.SelectItem();
        }
        ResetRage();
        RefreshItems();
        foreach (var row in rows)
        {
            row.id = -1;
            row.name = "None";
            Caption(row.select, "None");
        }
        corruptionId = -1;
        corruptionName = "None";
        Caption(
            corruptionSelect,
            CorruptedAffixAdapter.IsSupported
                ? "Corrupted affix: None"
                : "Corrupted affix API unavailable"
        );
        result = selected ? "" : "Item unavailable. Search again or choose an item manually.";
    }

    // The picker search is shared by affixes, corruption, unique variants,
    // categories and rarity. Focus it as soon as any picker is opened.
    static void FocusSearch(TMP_InputField target)
    {
        if (target.IsNullOrDestroyed() || !target.gameObject.activeInHierarchy)
            return;
        target.Select();
        target.ActivateInputField();
        target.caretPosition = target.text.Length;
    }

    // Unity's inputString preserves the first typed character even if a picker
    // button/scrollbar was selected instead of its text field. Redirect only when
    // Force Drop is visible, and never take focus from another edited input.
    static void RedirectTypingToSearch()
    {
        if (root.IsNullOrDestroyed() || !root.activeInHierarchy)
            return;
        bool pickerOpen = !picker.IsNullOrDestroyed() && picker.activeInHierarchy;
        TMP_InputField target = pickerOpen ? pickerSearch : search;
        if (
            target.IsNullOrDestroyed()
            || !target.gameObject.activeInHierarchy
            || !target.interactable
            || target.isFocused
        )
            return;

        // Avoid per-frame event-system and component queries until typing occurs.
        string typed = ForceDropSearchTyping.Redirected(
            UnityEngine.Input.inputString,
            UnityEngine.Input.GetKeyDown(KeyCode.Return)
                || UnityEngine.Input.GetKeyDown(KeyCode.KeypadEnter)
        );
        if (typed.Length == 0)
            return;

        var eventSystem = EventSystem.current;
        if (!eventSystem.IsNullOrDestroyed())
        {
            var selected = eventSystem.currentSelectedGameObject;
            if (!selected.IsNullOrDestroyed())
            {
                var tmp = selected.GetComponent<TMP_InputField>();
                if (!tmp.IsNullOrDestroyed() && tmp.isFocused)
                    return;
                var legacy = selected.GetComponent<InputField>();
                if (!legacy.IsNullOrDestroyed() && legacy.isFocused)
                    return;
            }
        }

        // Do not hijack hotkeys such as Ctrl+A/C/V, Alt+F4, or the Windows key.
        // AltGr (RightAlt+Ctrl) is allowed for locale-specific printable text.
        bool control =
            UnityEngine.Input.GetKey(KeyCode.LeftControl)
            || UnityEngine.Input.GetKey(KeyCode.RightControl);
        bool alt =
            UnityEngine.Input.GetKey(KeyCode.LeftAlt) || UnityEngine.Input.GetKey(KeyCode.RightAlt);
        bool altGr = UnityEngine.Input.GetKey(KeyCode.RightAlt) && control;
        if (
            ((control || alt) && !altGr)
            || UnityEngine.Input.GetKey(KeyCode.LeftWindows)
            || UnityEngine.Input.GetKey(KeyCode.RightWindows)
        )
            return;

        FocusSearch(target);
        target.text += typed;
        target.caretPosition = target.text.Length;
    }

    static void RefreshNativeLocale()
    {
        string locale = NativeItemNames.Locale;
        object modLocale = Locales.current_dictionary;
        int categoryCount = FD.TypeDropdown.options.Count;
        if (
            lastNativeLocale == locale
            && ReferenceEquals(lastModLocale, modLocale)
            && lastCategoryCount == categoryCount
        )
            return;
        lastNativeLocale = locale;
        lastModLocale = modLocale;
        lastCategoryCount = categoryCount;
        nativeCategories = NativeItemNames.Categories(FD.TypeDropdown);
        allItems = null;
        lastItems = "";
        lastSearch = "";
        ResetItemScroll();
        // Picker actions keep stable ids; reopen to rebuild its translated labels.
        if (!picker.IsNullOrDestroyed())
            picker.SetActive(false);
        foreach (var row in rows)
        {
            row.name = row.id < 0 ? "None" : NativeItemNames.AffixName(row.id, row.name);
            Caption(row.select, row.name);
        }
        corruptionName =
            corruptionId < 0 ? "None" : NativeItemNames.AffixName(corruptionId, corruptionName);
        Caption(corruptionSelect, corruptionId < 0 ? "Corrupted affix: None" : corruptionName);
        for (int slot = 0; slot < variantIds.Length; slot++)
        {
            if (variantIds[slot] < 0)
                continue;
            variantNames[slot] = NativeItemNames.AffixName(variantIds[slot], variantNames[slot]);
            Caption(variantSelect[slot], variantNames[slot]);
        }
    }

    static string ItemName(int index)
    {
        if (nativeItems.TryGetValue(index, out var item))
            return item.name;
        return index > 0 && index < FD.ItemsDropdown.options.Count
            ? FD.ItemsDropdown.options[index].text
            : L("Choose an item");
    }

    static string CategoryName(int index, string fallback)
    {
        if (!nativeCategories.TryGetValue(index, out var category))
            return L(CategoryLabel(fallback));
        string label = CategoryLabel(category.raw);
        if (label == "Runes" || label == "Glyphs")
            return L(label);
        return category.name;
    }

    static string SelectedCategoryName(string fallback)
    {
        return FD.TypeDropdown.value > 0
            ? CategoryName(FD.TypeDropdown.value, Selected(FD.TypeDropdown, fallback))
            : L(fallback);
    }

    static string CategoryLabel(string name)
    {
        if (name.IndexOf("crafting modifier", StringComparison.OrdinalIgnoreCase) >= 0)
            return "Runes";
        if (name.IndexOf("crafting support", StringComparison.OrdinalIgnoreCase) >= 0)
            return "Glyphs";
        // Keep the lens family visible together; retain the subtype so each button is distinct.
        if (name.EndsWith(" Lens", StringComparison.OrdinalIgnoreCase))
            return "Lens: " + name.Substring(0, name.Length - 5);
        return name;
    }

    static int CategoryGroup(string name)
    {
        string n = name.ToLowerInvariant();
        foreach (
            string token in new[]
            {
                "axe",
                "bow",
                "dagger",
                "mace",
                "scepter",
                "sceptre",
                "staff",
                "staves",
                "sword",
                "wand",
                "spear",
                "quiver",
                "fist",
                "polearm",
            }
        )
            if (n.Contains(token))
                return 0;
        foreach (
            string token in new[]
            {
                "helmet",
                "body armor",
                "body armour",
                "belt",
                "boot",
                "glove",
                "shield",
            }
        )
            if (n.Contains(token))
                return 1;
        foreach (string token in new[] { "ring", "amulet", "relic" })
            if (n.Contains(token))
                return 2;
        if (n.Contains("idol"))
            return 3;
        return 4;
    }
}
