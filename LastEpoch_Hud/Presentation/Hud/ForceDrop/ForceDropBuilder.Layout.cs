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

    static void Build()
    {
        if (!FD.TypeInitialized || FD.ContentObject.IsNullOrDestroyed())
        {
            return;
        }
        foreach (
            var candidate in Hud_Manager.hud_object.GetComponentsInChildren<TMP_InputField>(true)
        )
            if (
                candidate.name == "InputField"
                && candidate.transform.parent != null
                && candidate.transform.parent.name == "Name"
            )
            {
                template = candidate;
                break;
            }
        if (template.IsNullOrDestroyed())
            throw new InvalidOperationException("Input template is unavailable");
        foreach (Text t in FD.ContentObject.GetComponentsInChildren<Text>(true))
        {
            if (!t.font.IsNullOrDestroyed())
            {
                font = t.font;
                break;
            }
        }
        if (font.IsNullOrDestroyed())
            throw new InvalidOperationException("Menu font is unavailable");
        clicks.Clear();
        numbers.Clear();
        itemButtons.Clear();
        pickButtons.Clear();
        pickerHeaders.Clear();
        foreach (var buttons in columnButtons)
            buttons.Clear();
        root = Panel(FD.ContentObject, "ForceDropBuilder", 0, 0, 1, 1, false);
        root.GetComponent<Image>().color = HudTheme.Background;
        Label(root, "Force Drop", 0.02f, 0.955f, 0.64f, 0.995f, HudTheme.SliderCardTitleFontSize);
        // Reserve a footer beneath the panels so panel outlines do not cross status text.
        var left = Panel(root, "Choose item", 0.01f, 0.06f, 0.29f, 0.945f);
        var middle = Panel(root, "Customize", 0.30f, 0.06f, 0.73f, 0.945f);
        var right = Panel(root, "Preview", 0.74f, 0.06f, 0.99f, 0.945f);
        Label(left, "Choose item", .03f, .95f, .97f, .99f, HudTheme.CardTitleFontSize);
        Divider(left, .03f, .945f, .97f);
        Label(left, "Search all items", .03f, .90f, .97f, .94f);
        search = Input(left, "Item search", .03f, .84f, .97f, .89f, "", false);
        typeButton = Button(
            left,
            "Choose category",
            .03f,
            .77f,
            .97f,
            .825f,
            () => CatalogPicker(FD.TypeDropdown, FD.SelectType, true)
        );
        rarityButton = Button(
            left,
            "Choose rarity",
            .03f,
            .705f,
            .97f,
            .76f,
            () => CatalogPicker(FD.RarityDropdown, FD.SelectRarity, true)
        );
        var itemViewport = Panel(left, "Item scroll viewport", .03f, .16f, .925f, .695f);
        itemViewport.AddComponent<RectMask2D>();
        var itemContent = new GameObject("Item scroll content");
        var itemContentRect = itemContent.AddComponent<RectTransform>();
        itemContent.transform.SetParent(itemViewport.transform, false);
        itemContentRect.anchorMin = new Vector2(0, 1);
        itemContentRect.anchorMax = new Vector2(1, 1);
        itemContentRect.pivot = new Vector2(.5f, 1);
        itemContentRect.sizeDelta = Vector2.zero;
        itemScroll = itemViewport.AddComponent<ScrollRect>();
        itemScroll.viewport = itemViewport.GetComponent<RectTransform>();
        itemScroll.content = itemContentRect;
        itemScroll.horizontal = false;
        itemScroll.vertical = true;
        itemScroll.movementType = ScrollRect.MovementType.Clamped;
        itemScroll.scrollSensitivity = AffixRowHeight;
        var itemTrack = Panel(left, "Item scrollbar", .94f, .16f, .97f, .695f);
        var itemHandle = Panel(itemTrack, "Handle", 0, 0, 1, 1);
        itemHandle.GetComponent<Image>().color = gold;
        var itemScrollbar = itemTrack.AddComponent<Scrollbar>();
        itemScrollbar.handleRect = itemHandle.GetComponent<RectTransform>();
        itemScrollbar.targetGraphic = itemHandle.GetComponent<Image>();
        itemScrollbar.direction = Scrollbar.Direction.BottomToTop;
        itemScroll.verticalScrollbar = itemScrollbar;
        for (int i = 0; i < 12; i++)
        {
            int slot = i;
            itemButtons.Add(Button(itemContent, "", 0, 0, 1, 1, () => ChooseItem(slot)));
        }
        itemSearchStatus = Label(left, "", .03f, .10f, .97f, .15f, 12);
        Label(middle, "Customize", .03f, .955f, .55f, .99f, HudTheme.CardTitleFontSize);
        Divider(middle, .03f, .952f, .97f);
        Button(middle, "Random", .03f, .905f, .245f, .948f, () => Preset(true));
        Button(middle, "Maximum", .27f, .905f, .485f, .948f, () => Preset(false));
        Button(
            middle,
            "Custom",
            .51f,
            .905f,
            .725f,
            .948f,
            () =>
            {
                foreach (var n in numbers)
                    n.random = false;
                RefreshModes();
            }
        );
        illegalModeButton = Button(
            middle,
            "Illegal: Off",
            .75f,
            .905f,
            .97f,
            .948f,
            () => ChangeMode(!allowIllegal)
        );
        RefreshIllegalButton();
        basePage = Panel(middle, "Base properties", .02f, .735f, .98f, .895f);
        forging = NumericGrid(basePage, "Forging potential", 0, 1, 0, 255, 100, false);
        for (int i = 0; i < 3; i++)
            implicits[i] = NumericGrid(
                basePage,
                "Implicit " + (i + 1),
                (i + 1) % 2,
                i < 1 ? 1 : 0,
                0,
                100,
                100,
                true
            );
        affixPage = Panel(middle, "Affixes", .02f, .345f, .98f, .725f);
        Label(affixPage, "Affix", .03f, .93f, .55f, .99f, 13);
        Label(affixPage, "Tier", .59f, .93f, .72f, .99f, 13);
        Label(affixPage, "Roll %", .75f, .93f, .88f, .99f, 13);
        // Twelve rows do not fit in the panel, so they scroll inside a masked viewport.
        var affixViewport = Panel(affixPage, "Affix scroll viewport", 0f, 0f, 1f, .92f, false);
        affixViewport.GetComponent<Image>().color = Color.clear;
        affixViewport.AddComponent<RectMask2D>();
        affixContent = new GameObject("Affix scroll content");
        var affixContentRect = affixContent.AddComponent<RectTransform>();
        affixContent.transform.SetParent(affixViewport.transform, false);
        affixContentRect.anchorMin = new Vector2(0, 1);
        affixContentRect.anchorMax = new Vector2(1, 1);
        affixContentRect.pivot = new Vector2(.5f, 1);
        affixContentRect.sizeDelta = new Vector2(0, rows.Length * AffixRowHeight);
        var affixScroll = affixViewport.AddComponent<ScrollRect>();
        affixScroll.viewport = affixViewport.GetComponent<RectTransform>();
        affixScroll.content = affixContentRect;
        affixScroll.horizontal = false;
        affixScroll.vertical = true;
        affixScroll.movementType = ScrollRect.MovementType.Clamped;
        affixScroll.scrollSensitivity = AffixRowHeight;
        float rowStep = 1f / rows.Length;
        for (int i = 0; i < rows.Length; i++)
        {
            int index = i;
            float top = 1f - i * rowStep;
            float y = top - rowStep + rowStep * .06f;
            float y1 = top - rowStep * .06f;
            var row = new AffixRow();
            rows[i] = row;
            string slot =
                i == AffixLimits.SealedSlot ? "Sealed"
                : i == AffixLimits.PrimordialSlot ? "Primordial"
                : "Affix " + (i + 1);
            row.slotLabel = Label(affixContent, slot, .03f, y, .17f, y1, 12);
            row.select = Button(affixContent, "None", .18f, y, .57f, y1, () => AffixPicker(index));
            row.selectedLabel = row.select.GetComponentInChildren<Text>(true);
            row.tier = NumericCompact(
                affixContent,
                .59f,
                y,
                .72f,
                y1,
                i == AffixLimits.PrimordialSlot ? 8 : 1,
                i == AffixLimits.PrimordialSlot ? 8 : 7,
                i == AffixLimits.PrimordialSlot ? 8 : 7
            );
            row.roll = NumericCompact(affixContent, .75f, y, .86f, y1, 0, 100, 100);
            row.roll.mode = Button(
                affixContent,
                "Fixed",
                .88f,
                y,
                .98f,
                y1,
                () =>
                {
                    row.roll.random = !row.roll.random;
                    RefreshModes();
                }
            );
        }
        var corruptionPanel = Panel(middle, "Corruption", .02f, .235f, .98f, .335f);
        corruptButton = Button(
            corruptionPanel,
            "Corrupted: No",
            .02f,
            .25f,
            .24f,
            .80f,
            () =>
            {
                corrupted = !corrupted;
                Caption(corruptButton, corrupted ? "Corrupted: Yes" : "Corrupted: No");
            }
        );
        corruptionSelect = Button(
            corruptionPanel,
            "Corrupted affix: None",
            .26f,
            .25f,
            .61f,
            .80f,
            CorruptionPicker
        );
        corruptionSelectedLabel = corruptionSelect.GetComponentInChildren<Text>(true);
        corruptionTier = NumericCompact(corruptionPanel, .64f, .25f, .77f, .80f, 1, 7, 7);
        corruptionRoll = NumericCompact(corruptionPanel, .80f, .25f, .96f, .80f, 0, 100, 100);
        Label(corruptionPanel, "Tier", .64f, .81f, .77f, .99f, 11);
        Label(corruptionPanel, "Roll %", .80f, .81f, .96f, .99f, 11);
        corruptionSelect.interactable = CorruptedAffixAdapter.IsSupported;
        if (!CorruptedAffixAdapter.IsSupported)
            Caption(corruptionSelect, "Corruption data unavailable");
        uniquePage = Panel(middle, "Unique properties", .02f, .025f, .98f, .225f);
        lp = NumericGrid(uniquePage, "Legendary Potential", 0, 2, 0, 4, 0, false, 3);
        ww = NumericGrid(uniquePage, "Weaver's Will", 1, 2, 0, 28, 0, false, 3);
        for (int i = 0; i < 8; i++)
            uniqueRolls[i] = NumericGrid(
                uniquePage,
                "Roll " + (i + 1),
                i % 4,
                i < 4 ? 1 : 0,
                0,
                100,
                100,
                true,
                3,
                4
            );
        ragePage = Panel(right, "Unique variants", .04f, .28f, .96f, .45f);
        variantHeader = Label(ragePage, "Unsated Rage modifier", .03f, .72f, .97f, .96f, 15);
        variantSelect[0] = Button(
            ragePage,
            "Choose Rage",
            .03f,
            .29f,
            .97f,
            .69f,
            () => RagePicker(0)
        );
        variantSelect[1] = Button(
            ragePage,
            "Choose modifier 2",
            .03f,
            .25f,
            .97f,
            .49f,
            () => RagePicker(1)
        );
        variantSelect[1].gameObject.SetActive(false);
        variantDescription = Label(
            ragePage,
            "Exclusive ring modifier · separate from LP",
            .03f,
            .04f,
            .97f,
            .25f,
            11
        );
        ragePage.SetActive(false);
        Label(right, "Item preview", .04f, .92f, .96f, .99f, HudTheme.CardTitleFontSize);
        Divider(right, .04f, .915f, .96f);
        preview = Label(right, "Choose an item", .04f, .28f, .96f, .90f, 15);
        quantity = Numeric(right, "Quantity", .20f, 1, 99, 1, false, false);
        dropButton = Button(right, "Drop Item", .04f, .105f, .96f, .18f, Drop);
        Button(right, "Reset", .04f, .03f, .96f, .09f, Reset);
        status = Label(root, "", .30f, .01f, .99f, .045f, 12);
        BuildPicker();
        HudStyler.NormalizeSelectableGraphics(root);
        HudStyler.ApplyFontScale(root);
        RefreshItems();
        LogCorruptionMetadata();
    }
}
