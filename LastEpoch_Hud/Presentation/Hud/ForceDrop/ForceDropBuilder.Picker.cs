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

    static void BuildPicker()
    {
        picker = Panel(root, "Search picker", .15f, .08f, .85f, .89f);
        pickerTitle = Label(picker, "Select", .03f, .90f, .83f, .98f, 20);
        Button(picker, "Close", .84f, .90f, .97f, .98f, () => picker.SetActive(false));
        pickerSearch = Input(picker, "Search choices", .03f, .81f, .97f, .88f, "", false);
        for (int i = 0; i < 75; i++)
        {
            int slot = i;
            pickButtons.Add(
                Button(
                    picker,
                    "",
                    .03f,
                    .735f - i * .063f,
                    .97f,
                    .79f - i * .063f,
                    () =>
                    {
                        int index = slot;
                        if (index >= visiblePicks.Count)
                            return;
                        visiblePicks[index].select();
                        picker.SetActive(false);
                    }
                )
            );
        }
        pickerPrevious = Button(
            picker,
            "Previous",
            .03f,
            .03f,
            .48f,
            .085f,
            () =>
            {
                pickerPage = Math.Max(0, pickerPage - 1);
                RefreshPicker();
            }
        );
        pickerNext = Button(
            picker,
            "Next",
            .52f,
            .03f,
            .97f,
            .085f,
            () =>
            {
                if (HasNextPickerPage())
                    pickerPage++;
                RefreshPicker();
            }
        );
        for (int i = 0; i < 2; i++)
        {
            int column = i;
            float left = column == 0 ? .03f : .51f;
            columnTitles[column] = Label(
                picker,
                column == 0 ? "Prefix" : "Suffix",
                left,
                .74f,
                left + .46f,
                .795f,
                15
            );
            columnClear[column] = Button(picker, "None", left, .68f, left + .46f, .735f, () => { });
            var viewport = Panel(picker, "Affix scroll viewport", left, .10f, left + .435f, .67f);
            viewport.AddComponent<RectMask2D>();
            var content = new GameObject("Affix scroll content");
            var contentRect = content.AddComponent<RectTransform>();
            content.transform.SetParent(viewport.transform, false);
            contentRect.anchorMin = new Vector2(0, 1);
            contentRect.anchorMax = new Vector2(1, 1);
            contentRect.pivot = new Vector2(.5f, 1);
            contentRect.sizeDelta = Vector2.zero;
            var scroll = viewport.AddComponent<ScrollRect>();
            scroll.viewport = viewport.GetComponent<RectTransform>();
            scroll.content = contentRect;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = AffixRowHeight;
            var track = Panel(picker, "Affix scrollbar", left + .44f, .10f, left + .46f, .67f);
            var handle = Panel(track, "Handle", 0, 0, 1, 1);
            handle.GetComponent<Image>().color = gold;
            var scrollbar = track.AddComponent<Scrollbar>();
            scrollbar.handleRect = handle.GetComponent<RectTransform>();
            scrollbar.targetGraphic = handle.GetComponent<Image>();
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            scroll.verticalScrollbar = scrollbar;
            columnScrolls[column] = scroll;
        }
        for (int i = 0; i < groups.Length; i++)
            pickerHeaders.Add(
                Label(picker, groups[i], .03f + i * .19f, .74f, .21f + i * .19f, .795f, 15)
            );
        picker.SetActive(false);
    }

    static void OpenPicker(string title, bool scrollAffixes = false, int affixColumn = -1)
    {
        categoryPicker = rarityPicker = false;
        scrollAffixPicker = scrollAffixes;
        affixPickerColumn = affixColumn;
        LocaleRegistry.Apply(pickerTitle, title);
        pickerSearch.SetTextWithoutNotify("");
        lastPickerSearch = "";
        pickerPage = 0;
        ResetAffixScrolls();
        picker.SetActive(true);
        picker.transform.SetAsLastSibling();
        RefreshPicker();
        FocusSearch(pickerSearch);
    }

    static bool HasNextPickerPage() =>
        !categoryPicker && !rarityPicker && (pickerPage + 1) * 20 < filtered.Count;

    static void RefreshPicker()
    {
        filtered.Clear();
        visiblePicks.Clear();
        foreach (var choice in choices)
            if (MatchesPickerSearch(choice, pickerSearch.text))
                filtered.Add(choice);
        pickerPrevious.gameObject.SetActive(!categoryPicker && !rarityPicker && !scrollAffixPicker);
        pickerNext.gameObject.SetActive(!categoryPicker && !rarityPicker && !scrollAffixPicker);
        for (int column = 0; column < 2; column++)
        {
            bool visible =
                scrollAffixPicker && (affixPickerColumn < 0 || affixPickerColumn == column);
            columnTitles[column].gameObject.SetActive(visible);
            columnClear[column].gameObject.SetActive(visible);
            columnScrolls[column].gameObject.SetActive(visible);
            columnScrolls[column].verticalScrollbar.gameObject.SetActive(visible);
            if (!visible)
                continue;
            float left = affixPickerColumn < 0 && column == 1 ? .51f : .03f;
            float right = affixPickerColumn < 0 ? left + .46f : .97f;
            Rect(columnTitles[column].gameObject, left, .74f, right, .795f);
            Rect(columnClear[column].gameObject, left, .68f, right, .735f);
            Rect(columnScrolls[column].gameObject, left, .10f, right - .025f, .67f);
            Rect(
                columnScrolls[column].verticalScrollbar.gameObject,
                right - .02f,
                .10f,
                right,
                .67f
            );
        }
        foreach (var header in pickerHeaders)
            header.gameObject.SetActive(categoryPicker);
        if (categoryPicker)
            while (pickButtons.Count < filtered.Count)
            {
                int slot = pickButtons.Count;
                pickButtons.Add(
                    Button(
                        picker,
                        "",
                        0,
                        0,
                        1,
                        1,
                        () =>
                        {
                            if (slot >= visiblePicks.Count)
                                return;
                            visiblePicks[slot].select();
                            picker.SetActive(false);
                        }
                    )
                );
            }
        foreach (var button in pickButtons)
            button.gameObject.SetActive(false);
        if (scrollAffixPicker)
        {
            RefreshAffixColumns();
            return;
        }
        if (categoryPicker)
        {
            int rowCount = 15;
            for (int group = 0; group < groups.Length; group++)
                rowCount = Math.Max(rowCount, filtered.FindAll(x => x.group == group).Count);
            float step = .62f / rowCount;
            for (int group = 0; group < groups.Length; group++)
            {
                var column = filtered.FindAll(x => x.group == group);
                column.Sort(
                    (x, y) => string.Compare(x.name, y.name, StringComparison.OrdinalIgnoreCase)
                );
                for (int row = 0; row < column.Count; row++)
                {
                    int index = row;
                    if (index >= column.Count)
                        break;
                    var button = pickButtons[visiblePicks.Count];
                    Rect(
                        button.gameObject,
                        .03f + group * .19f,
                        .73f - (row + 1) * step,
                        .21f + group * .19f,
                        .73f - row * step - .004f
                    );
                    Caption(button, column[index].name);
                    button.GetComponentInChildren<Text>(true).color = gold;
                    button.gameObject.SetActive(true);
                    visiblePicks.Add(column[index]);
                }
            }
        }
        else
        {
            int count = rarityPicker ? Math.Min(4, filtered.Count) : 20;
            for (int slot = 0; slot < count; slot++)
            {
                int index = rarityPicker ? slot : pickerPage * 20 + slot;
                if (index >= filtered.Count)
                    break;
                int col = rarityPicker ? slot : slot % 2,
                    row = rarityPicker ? 0 : slot / 2;
                float width = rarityPicker ? .235f : .475f;
                var button = pickButtons[visiblePicks.Count];
                Rect(
                    button.gameObject,
                    .03f + col * width,
                    .69f - row * .058f,
                    .03f + col * width + width - .015f,
                    .739f - row * .058f
                );
                Caption(button, filtered[index].name);
                button.GetComponentInChildren<Text>(true).color = ChoiceColor(filtered[index]);
                button.gameObject.SetActive(true);
                visiblePicks.Add(filtered[index]);
            }
        }
    }

    static void RefreshAffixColumns()
    {
        foreach (var column in columnChoices)
            column.Clear();
        Choice clear = null;
        foreach (var choice in filtered)
            if (choice.id < 0)
                clear = choice;
            else
                columnChoices[choice.suffix ? 1 : 0].Add(choice);

        for (int column = 0; column < 2; column++)
        {
            if (affixPickerColumn >= 0 && affixPickerColumn != column)
                continue;
            columnTitles[column].text =
                L(column == 0 ? "Prefix" : "Suffix") + " · " + columnChoices[column].Count;
            columnClear[column].gameObject.SetActive(clear != null);
            if (clear != null)
                clicks[columnClear[column].GetInstanceID()] = () =>
                {
                    clear.select();
                    picker.SetActive(false);
                };
            columnStarts[column] = columnEnds[column] = -1;
            RefreshAffixScrollRows(column);
        }
    }

    static void ResetAffixScrolls()
    {
        for (int column = 0; column < 2; column++)
        {
            var scroll = columnScrolls[column];
            if (scroll.IsNullOrDestroyed())
                continue;
            scroll.StopMovement();
            scroll.content.anchoredPosition = Vector2.zero;
            columnStarts[column] = columnEnds[column] = -1;
        }
    }

    // Pool only visible rows rather than building thousands of native UI objects.
    // ScrollRect owns wheel/drag input and smooth movement in each independent column.
    static void RefreshAffixScrollRows(int column)
    {
        var scroll = columnScrolls[column];
        if (!scroll.gameObject.activeSelf)
            return;
        var list = columnChoices[column];
        float height = Math.Max(1f, scroll.viewport.rect.height);
        scroll.content.sizeDelta = new Vector2(0, Math.Max(height, list.Count * AffixRowHeight));
        int start = Math.Min(
            Math.Max(0, (int)(scroll.content.anchoredPosition.y / AffixRowHeight)),
            Math.Max(0, list.Count - 1)
        );
        int end = Math.Min(list.Count, start + (int)Math.Ceiling(height / AffixRowHeight) + 1);
        if (start == columnStarts[column] && end == columnEnds[column])
            return;
        columnStarts[column] = start;
        columnEnds[column] = end;
        var buttons = columnButtons[column];
        while (buttons.Count < end - start)
            buttons.Add(Button(scroll.content.gameObject, "", 0, 0, 1, 1, () => { }));
        for (int slot = 0; slot < buttons.Count; slot++)
        {
            var button = buttons[slot];
            bool visible = slot < end - start;
            button.gameObject.SetActive(visible);
            if (!visible)
                continue;
            int index = start + slot;
            var choice = list[index];
            var rect = button.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(1, 1);
            rect.pivot = new Vector2(.5f, 1);
            rect.offsetMin = new Vector2(0, -(index + 1) * AffixRowHeight + 4);
            rect.offsetMax = new Vector2(0, -index * AffixRowHeight);
            Caption(button, choice.name);
            button.GetComponentInChildren<Text>(true).color = ChoiceColor(choice);
            clicks[button.GetInstanceID()] = () =>
            {
                // Both columns retain the original action for the opened slot.
                choice.select();
                picker.SetActive(false);
            };
        }
    }
}
