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

// A new view over the existing catalog and item creation code. No asset bundle rebuild.
public static class ForceDropBuilder
{
    static Color gold => HudTheme.Accent;
    static Color foreground => HudTheme.TextPrimary;
    static Color dark => HudTheme.Surface;
    static Color setGreen => HudTheme.ForceDropSetText;
    static Color corruptionPurple => HudTheme.ForceDropCorruptionText;
    static Color idolCyan => HudTheme.ForceDropIdolText;
    static readonly Dictionary<int, Action> clicks = new Dictionary<int, Action>();
    static GameObject root,
        basePage,
        affixPage,
        uniquePage,
        ragePage,
        picker;
    static TMP_InputField template,
        search,
        pickerSearch;
    static bool categoryPicker,
        rarityPicker,
        scrollAffixPicker;

    // Legal prefix/suffix slots use one full-width column; mixed pools use both.
    static int affixPickerColumn = -1;
    const float AffixRowHeight = 40f;
    static readonly ScrollRect[] columnScrolls = new ScrollRect[2];
    static readonly Button[] columnClear = new Button[2];
    static readonly List<Button>[] columnButtons = { new List<Button>(), new List<Button>() };
    static readonly int[] columnStarts = { -1, -1 };
    static readonly int[] columnEnds = { -1, -1 };
    static readonly List<Choice>[] columnChoices = { new List<Choice>(), new List<Choice>() };
    static readonly Text[] columnTitles = new Text[2];
    static readonly List<Choice> visiblePicks = new List<Choice>();
    static readonly List<Text> pickerHeaders = new List<Text>();
    static readonly string[] groups = { "Weapons", "Armour", "Accessories", "Idols", "Other" };
    static Font font;
    static Text preview,
        status,
        pickerTitle,
        corruptionSelectedLabel,
        itemSearchStatus;
    static Button typeButton,
        rarityButton,
        dropButton,
        corruptButton,
        corruptionSelect,
        pickerPrevious,
        pickerNext;
    static int corruptionId = -1;
    static readonly int[] variantIds = { -1, -1 };
    static readonly string[] variantNames = { "Choose modifier 1", "Choose modifier 2" };
    static readonly Button[] variantSelect = new Button[2];
    static Text variantHeader,
        variantDescription;
    static ushort rageUniqueId;
    static int rageMetadataId = -1;

    static string corruptionName = "None";
    static Number corruptionTier,
        corruptionRoll;
    static readonly List<Button> itemButtons = new List<Button>();
    static readonly List<Button> pickButtons = new List<Button>();
    static readonly List<Choice> choices = new List<Choice>();
    static readonly List<Choice> filtered = new List<Choice>();
    static readonly List<int> itemIndexes = new List<int>();
    static List<NativeItemNames.SearchChoice> allItems;
    static readonly List<NativeItemNames.SearchChoice> itemMatches = new();
    static string lastCatalogKey = "";
    static bool SearchAllItems => !string.IsNullOrWhiteSpace(search.text);
    static int VisibleItemCount => SearchAllItems ? itemMatches.Count : itemIndexes.Count;
    static readonly List<Number> numbers = new List<Number>();
    static readonly AffixRow[] rows = new AffixRow[AffixLimits.ForceDropRows];
    static GameObject affixContent;
    static Number forging,
        quantity,
        lp,
        ww;
    static readonly Number[] implicits = new Number[3],
        uniqueRolls = new Number[8];
    static int pickerPage;
    static ScrollRect itemScroll;
    static int itemScrollStart = -1;
    static string lastSearch = "",
        lastPickerSearch = "",
        lastItems = "";
    static string lastNativeLocale;
    static object lastModLocale;
    static int lastCategoryCount;
    static Dictionary<int, NativeItemNames.Category> nativeCategories =
        new Dictionary<int, NativeItemNames.Category>();
    static Dictionary<int, NativeItemNames.ItemChoice> nativeItems =
        new Dictionary<int, NativeItemNames.ItemChoice>();
    static bool corrupted,
        allowIllegal,
        metadataLogged;
    static float nextCorruptionCheck;
    static string result = "";
    static Button illegalModeButton;
    private static readonly ForceDropBuildGate _buildGate = new();
    static int RouteMaximum => allowIllegal ? 8 : 7;
    public static bool IsReady => !root.IsNullOrDestroyed();

    static readonly Dictionary<int, ForceDropLegalAffixes> legalContexts =
        new Dictionary<int, ForceDropLegalAffixes>();
    static string legalContextKey = "",
        corruptionPoolKey = "";
    static ForceDropCorruptionPool corruptionPool;
    static string corruptionPoolError = "";

    public static void Tick()
    {
        if (_buildGate.ShouldBuild(IsReady))
        {
            TryBuild();
        }
        if (!IsReady)
        {
            return;
        }
        RedirectTypingToSearch();
        RefreshNativeLocale();
        RefreshTierLimits();
        foreach (var n in numbers)
            n.Read();
        string signature =
            FD.ItemType
            + ":"
            + FD.ItemRarity
            + ":"
            + FD.ItemsDropdown.options.Count
            + ":"
            + NativeItemNames.CatalogKey;
        if (lastSearch != search.text || lastItems != signature)
        {
            lastSearch = search.text;
            lastItems = signature;
            ResetItemScroll();
            RefreshItems();
        }
        RefreshItemScrollRows();
        if (picker.activeSelf && lastPickerSearch != pickerSearch.text)
        {
            lastPickerSearch = pickerSearch.text;
            pickerPage = 0;
            ResetAffixScrolls();
            RefreshPicker();
        }
        if (picker.activeSelf && scrollAffixPicker)
            for (int column = 0; column < 2; column++)
                RefreshAffixScrollRows(column);
        Caption(typeButton, SelectedCategoryName("Choose category"));
        Caption(
            rarityButton,
            NativeItemNames.RarityName(Selected(FD.RarityDropdown, "Choose rarity"))
        );
        lp.group.SetActive(
            FD.ItemRarity > 6 && FD.ItemLegendaryType == UniqueList.LegendaryType.LegendaryPotential
        );
        ww.group.SetActive(
            FD.ItemRarity > 6 && FD.ItemLegendaryType != UniqueList.LegendaryType.LegendaryPotential
        );
        uniquePage.SetActive(FD.ItemRarity > 6);
        var rageEntry = SelectedRageEntry();
        int variantCount = UniqueVariantAdapter.VariantCount(rageEntry);
        bool showRage = variantCount > 0;
        if (rageUniqueId != FD.ItemUniqueId)
        {
            ResetRage();
            rageUniqueId = (ushort)FD.ItemUniqueId;
        }
        ragePage.SetActive(showRage);
        if (showRage && rageMetadataId != FD.ItemUniqueId)
        {
            rageMetadataId = FD.ItemUniqueId;
            Main.logger_instance.Msg(
                "Unique variant native pool: unique="
                    + rageEntry.uniqueID
                    + ", specific="
                    + rageEntry.dropsSpecificLegendaryAffixes
                    + ", count="
                    + rageEntry.droppableLegendaryAffixCount
                    + ", excludesSlotLimits="
                    + rageEntry.excludeSpecificAffixesFromPrefixSuffixLimits
                    + ", pool="
                    + (
                        rageEntry.droppableLegendaryAffixes.IsNullOrDestroyed()
                            ? "null"
                            : rageEntry.droppableLegendaryAffixes.Count.ToString()
                    )
                    + ", resolved="
                    + UniqueVariantAdapter.Catalog(rageEntry).Count
            );
        }
        // Keep the exclusive modifier next to the item preview and outside the affix grid.
        bool twoVariants = variantCount == 2;
        Rect(ragePage, .04f, .28f, .96f, twoVariants ? .54f : .45f);
        LocaleRegistry.Apply(variantHeader, "Exclusive unique modifiers");
        LocaleRegistry.Apply(
            variantDescription,
            "Native modifiers · separate from ordinary affixes"
        );
        Rect(variantHeader.gameObject, .03f, twoVariants ? .78f : .72f, .97f, .97f);
        Rect(variantDescription.gameObject, .03f, .02f, .97f, twoVariants ? .22f : .25f);
        for (int slot = 0; slot < variantSelect.Length; slot++)
        {
            variantSelect[slot].gameObject.SetActive(slot < variantCount);
            variantSelect[slot].interactable = UniqueVariantAdapter.HasVariants(rageEntry);
            Caption(
                variantSelect[slot],
                variantSelect[slot].interactable
                    ? variantNames[slot]
                    : "Unique modifier pool unavailable"
            );
        }
        Rect(
            variantSelect[0].gameObject,
            .03f,
            twoVariants ? .51f : .29f,
            .97f,
            twoVariants ? .75f : .69f
        );
        Rect(preview.gameObject, .04f, showRage ? (twoVariants ? .56f : .47f) : .28f, .96f, .90f);
        if (Time.unscaledTime >= nextCorruptionCheck)
        {
            nextCorruptionCheck = Time.unscaledTime + 2f;
            corruptionSelect.interactable = CorruptedAffixAdapter.IsSupported;
            if (corruptionSelect.interactable && corruptionId < 0)
                Caption(corruptionSelect, "Corrupted affix: None");
        }
        forging.input.interactable = !corrupted && !forging.random;
        if (forging.mode != null)
            forging.mode.interactable = !corrupted;
        RefreshPotentialControls();
        for (int slot = 0; slot < rows.Length; slot++)
        {
            var context = LegalContext(slot);
            bool enchantment = !allowIllegal && context.IsHereticalIdol && (slot == 1 || slot == 3);
            bool available = allowIllegal
                ? FD.ItemType < 100
                : ForceDropLegalRules.SlotAllowed(
                    slot,
                    context.IsIdol,
                    context.IsUnique,
                    context.IsSet,
                    context.IsHereticalIdol
                );
            LocaleRegistry.Apply(
                rows[slot].slotLabel,
                enchantment ? "Enchantment " + (slot == 1 ? 1 : 2)
                    : slot == AffixLimits.SealedSlot ? "Sealed"
                    : slot == AffixLimits.PrimordialSlot ? "Primordial"
                    : "Affix " + (slot + 1)
            );
            rows[slot].select.interactable = available;
            rows[slot].tier.input.interactable = available;
            rows[slot].roll.input.interactable = available && !rows[slot].roll.random;
            rows[slot].roll.mode.interactable = available;
        }
        RefreshPreview();
    }

    /// <summary>Builds the view; on failure reports it and shows the load error.</summary>
    private static void TryBuild()
    {
        try
        {
            Build();
        }
        catch (Exception ex)
        {
            _buildGate.MarkFailed();
            if (!root.IsNullOrDestroyed())
            {
                UnityEngine.Object.Destroy(root);
            }
            root = null;
            ErrorLog.Report(ex, "ForceDropBuilder.Build");
            ForceDropLoadError.Show(FD.ContentObject);
        }
    }

    static string SelectionKey()
    {
        var key = new StringBuilder()
            .Append(FD.ItemType)
            .Append(':')
            .Append(FD.ItemSubtype)
            .Append(':')
            .Append(FD.ItemRarity)
            .Append(':')
            .Append(FD.ItemUniqueId)
            .Append(':')
            .Append(corrupted)
            .Append(':')
            .Append(lp.value)
            .Append(':')
            .Append(ww.value);
        key.Append(':').Append(allowIllegal);
        foreach (var row in rows)
            key.Append(':').Append(row.id).Append('/').Append(row.tier.value);
        foreach (int id in variantIds)
            key.Append(':').Append(id);
        return key.ToString();
    }

    static ForceDropLegalAffixes LegalContext(int editingSlot)
    {
        string key = SelectionKey();
        if (key != legalContextKey)
        {
            legalContextKey = key;
            legalContexts.Clear();
        }
        if (!legalContexts.TryGetValue(editingSlot, out var context))
        {
            var ids = new List<int>();
            for (int slot = 0; slot < rows.Length; slot++)
                if (slot != editingSlot && rows[slot].id >= 0)
                    ids.Add(rows[slot].id);
            context = new ForceDropLegalAffixes(FD.ItemType, FD.ItemSubtype, FD.ItemRarity, ids);
            legalContexts[editingSlot] = context;
        }
        return context;
    }

    static ForceDropCorruptionPool CorruptionPool()
    {
        string key = SelectionKey();
        if (key == corruptionPoolKey)
            return corruptionPool;
        corruptionPoolKey = key;
        corruptionPool = null;
        corruptionPoolError = "";
        try
        {
            var request = ResolveItem(true, false);
            corruptionPool = new ForceDropCorruptionPool(
                ForceDropItemCreator.CreateBeforeCorruption(request)
            );
            corruptionPoolError = corruptionPool.Error;
        }
        catch (Exception ex)
        {
            corruptionPoolError = ex.Message;
        }
        return corruptionPool;
    }

    sealed class Choice
    {
        public int id;
        public int group;
        public string name;
        public string aliases;

        // Five-bit class compatibility mask for affix choices; -1 for other choices.
        public int classMask = -1;
        public Action select;
        public ForceDropAffixFamily affixFamily;
        public bool champion;
        public bool suffix;
    }

    // Sort by native family, never translated labels. None remains the first
    // choice regardless of language; names are alphabetical within each family.
    static int AffixGroup(Choice choice)
    {
        if (choice.id < 0)
            return -1;
        return choice.affixFamily switch
        {
            ForceDropAffixFamily.Standard => 0,
            ForceDropAffixFamily.Experimental => 1,
            ForceDropAffixFamily.Personal => choice.champion ? 2 : 3,
            ForceDropAffixFamily.Set => 4,
            ForceDropAffixFamily.IdolWeaver => 5,
            ForceDropAffixFamily.IdolEnchantment => 6,
            ForceDropAffixFamily.Corrupted => 7,
            ForceDropAffixFamily.UniqueModifier => 8,
            _ => 9,
        };
    }

    static int CompareAffixChoices(Choice a, Choice b)
    {
        int comparison = AffixGroup(a).CompareTo(AffixGroup(b));
        if (comparison == 0)
            comparison = string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase);
        return comparison != 0 ? comparison : a.id.CompareTo(b.id);
    }

    static Color AffixColor(ForceDropAffixFamily family) =>
        family == ForceDropAffixFamily.Set ? setGreen
        : family == ForceDropAffixFamily.Corrupted ? corruptionPurple
        : gold;

    static Color ChoiceColor(Choice choice) =>
        allowIllegal && choice.id >= 0 && ForceDropLegalAffixes.IsIdolAffix(FindAffix(choice.id))
            ? idolCyan
            : AffixColor(choice.affixFamily);

    static Color SelectedAffixColor(int id) =>
        id < 0 ? gold
        : allowIllegal && ForceDropLegalAffixes.IsIdolAffix(FindAffix(id)) ? idolCyan
        : AffixColor(ForceDropLegalAffixes.Family(FindAffix(id)));

    sealed class Number
    {
        public TMP_InputField input;
        public GameObject group;
        public int min,
            max,
            value;
        public bool random;
        public Button mode;

        public void Read()
        {
            if (
                int.TryParse(
                    input.text,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out int n
                )
            )
                value = Math.Max(min, Math.Min(max, n));
            if (!input.isFocused)
                input.SetTextWithoutNotify(value.ToString(CultureInfo.InvariantCulture));
        }
    }

    private sealed class AffixRow
    {
        public int id = -1;
        public string name = "None";
        public Button select;
        public Text slotLabel;
        public Text selectedLabel;
        public Number tier,
            roll;
    }

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

    static void Preset(bool random)
    {
        foreach (var n in numbers)
        {
            if (n == quantity || n == corruptionTier || n == corruptionRoll)
                continue;
            n.random = random && n.mode != null;
            if (!random)
            {
                n.value = n.max;
                n.input.SetTextWithoutNotify(n.value.ToString());
            }
        }
        RefreshModes();
    }

    static void RefreshModes()
    {
        foreach (var n in numbers)
            if (n.mode != null)
            {
                Caption(n.mode, n.random ? "Random" : "Fixed");
                n.input.interactable = !n.random;
            }
        RefreshPotentialControls();
    }

    static bool CreatesLegendary()
    {
        int selected = 0;
        foreach (var row in rows)
            if (row != null && row.id >= 0)
                selected++;
        return ForceDropPotentialRules.CreatesLegendary(FD.ItemRarity, selected);
    }

    static void RefreshPotentialControls()
    {
        if (lp == null || ww == null)
            return;
        bool legendary = CreatesLegendary();
        if (legendary)
        {
            lp.value = 0;
            lp.random = false;
            lp.input.SetTextWithoutNotify("0");
            if (lp.mode != null)
                Caption(lp.mode, "Fixed");
        }
        lp.input.interactable = !corrupted && !legendary && !lp.random;
        if (lp.mode != null)
            lp.mode.interactable = !corrupted && !legendary;
        ww.input.interactable = !corrupted && !ww.random;
        if (ww.mode != null)
            ww.mode.interactable = !corrupted;
    }

    static void Reset()
    {
        foreach (var r in rows)
        {
            r.id = -1;
            r.name = "None";
            Caption(r.select, "None");
        }
        corrupted = false;
        corruptionId = -1;
        corruptionName = "None";
        Caption(corruptButton, "Corrupted: No");
        quantity.value = 1;
        quantity.input.SetTextWithoutNotify("1");
        lp.value = ww.value = 0;
        lp.input.SetTextWithoutNotify("0");
        ww.input.SetTextWithoutNotify("0");
        ResetRage();
        Preset(true);
        result = "";
    }

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

    static UniqueList.Entry SelectedRageEntry()
    {
        if (
            FD.ItemRarity < 7
            || FD.ItemsDropdown.value <= 0
            || UniqueList.instance.IsNullOrDestroyed()
        )
            return null;
        return UniqueList.getUnique((ushort)FD.ItemUniqueId);
    }

    static void ResetRage()
    {
        bool ring = UniqueVariantAdapter.IsUnsated(SelectedRageEntry());
        for (int slot = 0; slot < variantIds.Length; slot++)
        {
            variantIds[slot] = -1;
            variantNames[slot] =
                ring && slot == 0 ? "Choose Rage"
                : slot == 0 ? "Choose modifier 1"
                : "Choose modifier 2";
            if (!variantSelect[slot].IsNullOrDestroyed())
                Caption(variantSelect[slot], variantNames[slot]);
        }
    }

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

    static int[] SelectedVariantIds()
    {
        return UniqueVariantAdapter.VariantCount(SelectedRageEntry()) == 2
            ? new[] { variantIds[0], variantIds[1] }
            : new[] { variantIds[0] };
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
                context.IsHereticalIdol
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

    static AffixList.Affix FindAffix(int id) => ForceDropCatalog.Find(id);

    static int RowMaximum(int slot, AffixList.Affix definition)
    {
        // Native storage has independent regular and Primordial seals.
        // A regular seal stays within T1-T7; the Primordial slot is T8 only.
        if (slot == AffixLimits.SealedSlot)
            return 7;
        if (slot == AffixLimits.PrimordialSlot)
            return 8;
        return RouteMaximum;
    }

    static void RefreshTierLimits()
    {
        for (int slot = 0; slot < rows.Length; slot++)
        {
            var row = rows[slot];
            var definition = FindAffix(row.id);
            int maximum = RowMaximum(slot, definition);
            if (slot == AffixLimits.PrimordialSlot)
            {
                // Primordial is an evolution to T8, not another ordinary tier slider.
                row.tier.min = 8;
                row.tier.max = 8;
                row.tier.value = 8;
                if (!row.tier.input.isFocused)
                    row.tier.input.SetTextWithoutNotify("8");
            }
            else
            {
                row.tier.min = 1;
                SetTierLimit(
                    row.tier,
                    row.id < 0 ? maximum : ForceDropCatalog.MaximumTier(definition, maximum)
                );
            }
            row.selectedLabel.color = SelectedAffixColor(row.id);
        }
        corruptionSelectedLabel.color = SelectedAffixColor(corruptionId);
        if (corruptionId < 0)
            SetTierLimit(corruptionTier, RouteMaximum);
        else if (allowIllegal)
            SetTierLimit(corruptionTier, ForceDropCatalog.MaximumTier(FindAffix(corruptionId), 8));
        else
        {
            var pool = CorruptionPool();
            int mask = pool == null ? 0 : pool.TierMask(corruptionId);
            SetTierLimit(corruptionTier, ForceDropLegalTiers.MaximumDisplayTier(mask));
            int value = ForceDropLegalTiers.ClampDisplayTier(mask, corruptionTier.value);
            if (value > 0 && value != corruptionTier.value)
            {
                corruptionTier.value = value;
                corruptionTier.input.SetTextWithoutNotify(
                    value.ToString(CultureInfo.InvariantCulture)
                );
            }
        }
    }

    static void SetTierLimit(Number number, int maximum)
    {
        number.max = Math.Max(1, maximum);
        if (number.value > number.max)
        {
            number.value = number.max;
            number.input.SetTextWithoutNotify(number.value.ToString(CultureInfo.InvariantCulture));
        }
    }

    static bool ValidSelectedItem()
    {
        var list = ItemList.get();
        if (list.IsNullOrDestroyed() || FD.ItemType < 0 || FD.ItemSubtype < 0)
        {
            return false;
        }
        bool baseExists = false;
        foreach (var type in list.EquippableItems)
        {
            if (type.baseTypeID == FD.ItemType)
            {
                foreach (var item in type.subItems)
                {
                    if (item.subTypeID == FD.ItemSubtype)
                    {
                        baseExists = true;
                        break;
                    }
                }
            }
        }
        foreach (var type in list.nonEquippableItems)
        {
            if (type.baseTypeID == FD.ItemType)
            {
                foreach (var item in type.subItems)
                {
                    if (item.subTypeID == FD.ItemSubtype)
                    {
                        baseExists = true;
                        break;
                    }
                }
            }
        }
        if (!baseExists)
        {
            return false;
        }
        if (FD.ItemRarity == 0)
        {
            return true;
        }
        if (UniqueList.instance.IsNullOrDestroyed())
        {
            return false;
        }
        foreach (var entry in UniqueList.instance.uniques)
        {
            if (
                entry.uniqueID == FD.ItemUniqueId
                && entry.baseType == FD.ItemType
                && entry.isSetItem == (FD.ItemRarity == 8)
            )
            {
                foreach (var subType in entry.subTypes)
                {
                    if (subType == FD.ItemSubtype)
                    {
                        return true;
                    }
                }
            }
        }
        return false;
    }

    static string Validate()
    {
        if (
            FD.TypeDropdown.value <= 0
            || FD.RarityDropdown.value <= 0
            || FD.ItemsDropdown.value <= 0
            || FD.ItemSubtype < 0
        )
            return "Choose a category, rarity and item.";
        if (
            Refs_Manager.player_actor.IsNullOrDestroyed()
            || Refs_Manager.ground_item_manager.IsNullOrDestroyed()
        )
            return "Enter the game before dropping items.";
        var ids = new HashSet<int>();
        foreach (var r in rows)
            if (r.id >= 0 && !ids.Add(r.id))
                return "Each affix must be different, including the sealed affix.";
        if (!ValidSelectedItem())
            return "The selected item does not match its category. Choose it again.";
        var rageEntry = SelectedRageEntry();
        int variantCount = UniqueVariantAdapter.VariantCount(rageEntry);
        if (variantCount > 0)
        {
            if (!UniqueVariantAdapter.HasVariants(rageEntry))
                return "Unique modifier pool unavailable";
            var variantCatalog = UniqueVariantAdapter.Catalog(rageEntry);
            for (int slot = 0; slot < variantCount; slot++)
            {
                if (!variantCatalog.Exists(a => a.affixId == variantIds[slot]))
                    return "Choose every exclusive unique modifier.";
                if (!ids.Add(variantIds[slot]))
                    return "Exclusive unique modifiers must be different and separate from ordinary affixes.";
            }
        }
        if (ids.Count + (corrupted && corruptionId >= 0 ? 1 : 0) > AffixLimits.MaxTotal)
            return "An item holds at most "
                + AffixLimits.MaxTotal
                + " affixes, including sealed, Primordial, corruption and unique modifiers.";
        for (int slot = 0; slot < rows.Length; slot++)
        {
            var row = rows[slot];
            if (row.id < 0)
                continue;
            var definition = FindAffix(row.id);
            var context = LegalContext(slot);
            if (
                !allowIllegal
                && !ForceDropLegalRules.SlotAllowed(
                    slot,
                    context.IsIdol,
                    context.IsUnique,
                    context.IsSet,
                    context.IsHereticalIdol
                )
            )
                return "This item has no legal affix slot here. Clear the selection.";
            string reason = context.OrdinaryReason(
                definition,
                AffixLimits.IsSealedSlot(slot),
                context.IsHereticalIdol && (slot == 1 || slot == 3)
            );
            if (!allowIllegal && reason.Length > 0)
                return NativeItemNames.AffixName(row.id, row.name) + ": " + reason;
            if (
                slot == AffixLimits.PrimordialSlot
                && (
                    row.tier.value != 8
                    || !ForceDropModeRules.CanSealPrimordial(
                        FD.ItemType,
                        FD.ItemRarity,
                        ForceDropLegalAffixes.Family(definition),
                        ForceDropCatalog.MaximumTier(definition, 8),
                        allowIllegal ? ForceDropMode.Illegal : ForceDropMode.Legal
                    )
                )
            )
                return "The Primordial slot requires an eligible T8 affix.";
            if (
                row.tier.value
                > ForceDropCatalog.MaximumTier(definition, RowMaximum(slot, definition))
            )
                return "The selected tier does not exist for this affix.";
        }
        var itemContext = LegalContext(-1);
        if (!allowIllegal && itemContext.IsWeaverIdol)
        {
            bool hasWeaver = false;
            foreach (var row in rows)
            {
                var definition = FindAffix(row.id);
                hasWeaver |=
                    !definition.IsNullOrDestroyed()
                    && definition.specialAffixType == AffixList.SpecialAffixType.IdolWeaver;
            }
            if (!hasWeaver)
                return "A Weaver idol requires at least one Weaver affix.";
        }
        if (corrupted && corruptionId >= 0)
        {
            var pool = allowIllegal ? null : CorruptionPool();
            if (!CorruptedAffixAdapter.IsSupported || (!allowIllegal && pool == null))
                return "Corruption selection unavailable: " + corruptionPoolError;
            if (
                !allowIllegal
                && !ForceDropLegalTiers.Supports(
                    pool.TierMask(corruptionId),
                    corruptionTier.value - 1
                )
            )
                return "The chosen corruption affix or tier is not permitted by this item's native outcomes.";
            if (
                allowIllegal
                && corruptionTier.value > ForceDropCatalog.MaximumTier(FindAffix(corruptionId), 8)
            )
                return "The selected tier does not exist for this affix.";
            if (!ids.Add(corruptionId))
                return "Corruption cannot duplicate another affix.";
        }
        foreach (var n in numbers)
            if (!int.TryParse(n.input.text, out int value) || value < n.min || value > n.max)
                return "Use whole numbers within each field's range.";
        if (FD.ItemType >= 100 && corrupted)
        {
            return "Corruption is available for equipment only.";
        }
        return "";
    }

    static void RefreshPreview()
    {
        var s = new StringBuilder(
            FD.ItemsDropdown.value > 0 ? ItemName(FD.ItemsDropdown.value) : L("Choose an item")
        );
        s.Append("\n\n")
            .Append(SelectedCategoryName(""))
            .Append("\n")
            .Append(NativeItemNames.RarityName(Selected(FD.RarityDropdown, "")));
        if (FD.ItemType < 100)
        {
            if (FD.ItemRarity < 7)
            {
                s.Append("\n")
                    .Append(L("Forging potential"))
                    .Append(": ")
                    .Append(
                        corrupted ? "0"
                        : forging.random ? L("Random")
                        : forging.value.ToString()
                    );
            }
            foreach (var r in rows)
                if (r.id >= 0)
                    s.Append("\n\n")
                        .Append(
                            r == rows[AffixLimits.SealedSlot] ? L("Sealed") + ": "
                            : r == rows[AffixLimits.PrimordialSlot] ? L("Primordial") + ": "
                            : ""
                        )
                        .Append(r.name)
                        .Append("\nT")
                        .Append(r.tier.value)
                        .Append(" · ")
                        .Append((r.roll.random ? L("Random") : r.roll.value.ToString()))
                        .Append(" %");
            if (FD.ItemRarity > 6)
            {
                s.Append("\n\n")
                    .Append(
                        FD.ItemLegendaryType == UniqueList.LegendaryType.LegendaryPotential
                            ? L("LP")
                                + ": "
                                + (
                                    corrupted || CreatesLegendary() ? "0"
                                    : lp.random ? L("Random")
                                    : lp.value.ToString()
                                )
                            : L("Weaver's Will")
                                + ": "
                                + (
                                    corrupted ? "0"
                                    : ww.random ? L("Random")
                                    : ww.value.ToString()
                                )
                    );
            }
        }
        int variantCount = UniqueVariantAdapter.VariantCount(SelectedRageEntry());
        for (int slot = 0; slot < variantCount; slot++)
            s.Append("\n\n")
                .Append(
                    L(
                        variantCount == 1 ? "Ring variant"
                        : slot == 0 ? "Glove modifier 1"
                        : "Glove modifier 2"
                    )
                )
                .Append(": ")
                .Append(L(variantNames[slot]));
        s.Append("\n\n").Append(L("Corrupted")).Append(": ").Append(corrupted ? L("Yes") : L("No"));
        if (corrupted && corruptionId >= 0)
            s.Append("\n")
                .Append(corruptionName)
                .Append("\nT")
                .Append(corruptionTier.value)
                .Append(" · ")
                .Append(corruptionRoll.value)
                .Append(" %");
        preview.text = s.ToString();
        string problem = Validate();
        status.text = L(
            problem.Length > 0 ? problem
            : result.Length > 0 ? result
            : "Drops at your character."
        );
        dropButton.interactable = problem.Length == 0;
    }

    static int Sample(Number n) => n.random ? UnityEngine.Random.Range(n.min, n.max + 1) : n.value;

    static int Roll(Number n) =>
        n.random ? UnityEngine.Random.Range(0, 256) : Mathf.RoundToInt(n.value / 100f * 255f);

    static ResolvedForceDrop ResolveItem(bool previewOnly = false, bool includeCorruption = true)
    {
        bool equipment = FD.ItemType < 100;
        bool unique = FD.ItemRarity >= 7;
        int ResolvedRoll(Number n) => previewOnly ? 255 : Roll(n);
        int ResolvedNumber(Number n) => previewOnly ? n.value : Sample(n);
        var selected = new List<ResolvedForceDropAffix>();
        if (equipment)
            for (int slot = 0; slot < rows.Length; slot++)
            {
                var row = rows[slot];
                if (row.id >= 0)
                    selected.Add(
                        new ResolvedForceDropAffix(
                            row.id,
                            row.tier.value - 1,
                            ResolvedRoll(row.roll),
                            slot == AffixLimits.SealedSlot ? ForceDropSeal.Regular
                                : slot == AffixLimits.PrimordialSlot ? ForceDropSeal.Primordial
                                : ForceDropSeal.None
                        )
                    );
            }
        var implicitValues = new int[implicits.Length];
        for (int i = 0; i < implicitValues.Length; i++)
            implicitValues[i] = equipment ? ResolvedRoll(implicits[i]) : 0;
        var uniqueValues = new int[uniqueRolls.Length];
        if (unique)
            for (int i = 0; i < uniqueValues.Length; i++)
                uniqueValues[i] = ResolvedRoll(uniqueRolls[i]);
        bool usesLP = unique && FD.ItemLegendaryType == UniqueList.LegendaryType.LegendaryPotential;
        return new ResolvedForceDrop(
            FD.ItemType,
            FD.ItemSubtype,
            unique ? FD.ItemUniqueId : 0,
            FD.ItemRarity,
            equipment && !unique && !corrupted ? ResolvedNumber(forging) : 0,
            usesLP
            && !corrupted
            && !ForceDropPotentialRules.CreatesLegendary(FD.ItemRarity, selected.Count)
                ? ResolvedNumber(lp)
                : 0,
            unique && !usesLP && !corrupted ? ResolvedNumber(ww) : 0,
            corrupted,
            implicitValues,
            uniqueValues,
            selected,
            UniqueVariantAdapter.VariantCount(SelectedRageEntry()) > 0
                ? SelectedVariantIds()
                : Array.Empty<int>(),
            includeCorruption && corrupted && corruptionId >= 0
                ? new ResolvedForceDropAffix(
                    corruptionId,
                    corruptionTier.value - 1,
                    ResolvedRoll(corruptionRoll),
                    ForceDropSeal.Corruption
                )
                : null,
            allowIllegal ? ForceDropMode.Illegal : ForceDropMode.Legal
        );
    }

    static void ChangeMode(bool illegal)
    {
        allowIllegal = illegal;
        // Selections made under one rule set must not leak into the other.
        foreach (var row in rows)
        {
            row.id = -1;
            row.name = "None";
            Caption(row.select, "None");
        }
        corruptionId = -1;
        corruptionName = "None";
        Caption(corruptionSelect, "Corrupted affix: None");
        legalContextKey = corruptionPoolKey = "";
        legalContexts.Clear();
        corruptionPool = null;
        corruptionPoolError = result = "";
        if (!picker.IsNullOrDestroyed())
            picker.SetActive(false);
        RefreshTierLimits();
        RefreshIllegalButton();
    }

    static void RefreshIllegalButton()
    {
        if (illegalModeButton.IsNullOrDestroyed())
            return;
        Caption(illegalModeButton, allowIllegal ? "Illegal: On" : "Illegal: Off");
        StyleSelection(illegalModeButton, allowIllegal);
    }

    static void Drop()
    {
        RefreshTierLimits();
        foreach (var n in numbers)
            n.Read();
        RefreshPotentialControls();
        string problem = Validate();
        if (problem.Length != 0)
        {
            result = problem;
            return;
        }
        int requested = quantity.value;
        int dropped = 0;
        try
        {
            for (int copy = 0; copy < requested; copy++)
            {
                // Each copy gets one immutable request and independently resolved
                // rolls. Construction and verification never sample them again.
                ForceDropItemCreator.Drop(ResolveItem());
                dropped++;
            }
            result = "Dropped " + dropped + " item(s).";
        }
        catch (Exception ex)
        {
            result =
                "Dropped " + dropped + " of " + requested + " item(s). Drop failed: " + ex.Message;
            Main.logger_instance.Error(result);
        }
    }

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

    static Number NumericGrid(
        GameObject parent,
        string name,
        int col,
        int row,
        int min,
        int max,
        int value,
        bool percent,
        int rowCount = 2,
        int columns = 2
    )
    {
        float width = .96f / columns,
            height = .92f / rowCount;
        var cell = Panel(
            parent,
            name,
            .02f + col * width,
            .04f + row * height,
            .02f + (col + 1) * width - .01f,
            .04f + (row + 1) * height - .01f
        );
        Label(cell, name + (percent ? " %" : ""), .025f, .60f, .97f, .97f, 12);
        var n = NumericCompact(cell, .03f, .08f, .55f, .57f, min, max, value);
        n.group = cell;
        n.mode = Button(
            cell,
            "Fixed",
            .60f,
            .08f,
            .97f,
            .57f,
            () =>
            {
                n.random = !n.random;
                RefreshModes();
            }
        );
        return n;
    }

    static GameObject Panel(
        GameObject parent,
        string name,
        float x0,
        float y0,
        float x1,
        float y1,
        bool bordered = true
    )
    {
        var go = new GameObject(name);
        go.AddComponent<RectTransform>();
        go.transform.SetParent(parent.transform, false);
        Rect(go, x0, y0, x1, y1);
        go.AddComponent<Image>().color = dark;
        if (bordered)
            HudStyler.AddPrimaryBorder(go, HudTheme.BorderWidth);
        return go;
    }

    static void Divider(GameObject parent, float x0, float y, float x1)
    {
        var divider = new GameObject("TitleDivider");
        divider.AddComponent<RectTransform>();
        divider.transform.SetParent(parent.transform, false);
        Rect(divider, x0, y, x1, y + .002f);
        var image = divider.AddComponent<Image>();
        image.color = HudTheme.CardDivider;
        image.raycastTarget = false;
    }

    static void Rect(GameObject go, float x0, float y0, float x1, float y1)
    {
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(x0, y0);
        rect.anchorMax = new Vector2(x1, y1);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
    }

    static Text Label(
        GameObject parent,
        string text,
        float x0,
        float y0,
        float x1,
        float y1,
        int size = 15
    )
    {
        var go = new GameObject("Label");
        go.AddComponent<RectTransform>();
        go.transform.SetParent(parent.transform, false);
        Rect(go, x0, y0, x1, y1);
        var label = go.AddComponent<Text>();
        label.font = font;
        label.fontSize = size;
        label.color = foreground;
        LocaleRegistry.Apply(label, text);
        label.raycastTarget = false;
        label.alignment = TextAnchor.UpperLeft;
        label.horizontalOverflow = HorizontalWrapMode.Wrap;
        label.verticalOverflow = VerticalWrapMode.Truncate;
        return label;
    }

    static Button Button(
        GameObject parent,
        string text,
        float x0,
        float y0,
        float x1,
        float y1,
        Action action
    )
    {
        var go = Panel(parent, "Button", x0, y0, x1, y1);
        var button = go.AddComponent<Button>();
        button.targetGraphic = go.GetComponent<Image>();
        button.targetGraphic.color = HudTheme.SelectableTint;
        button.colors = HudTheme.ActionButtonColors(HudTheme.Surface, HudTheme.Surface);
        var label = Label(go, text, .025f, .04f, .975f, .96f);
        label.alignment = TextAnchor.MiddleLeft;
        var accent = new GameObject("LEHUD_SelectedAccent");
        accent.AddComponent<RectTransform>();
        accent.transform.SetParent(go.transform, false);
        Rect(accent, 0f, 0f, .012f, 1f);
        var accentImage = accent.AddComponent<Image>();
        accentImage.color = HudTheme.Accent;
        accentImage.raycastTarget = false;
        accent.SetActive(false);
        clicks[button.GetInstanceID()] = action;
        return button;
    }

    static void StyleSelection(Button button, bool selected)
    {
        if (button.IsNullOrDestroyed())
            return;
        var image = button.GetComponent<Image>();
        if (!image.IsNullOrDestroyed())
            image.color = HudTheme.SelectableTint;
        button.colors = HudTheme.ActionButtonColors(
            selected ? HudTheme.Selection : HudTheme.Surface,
            selected ? HudTheme.Selection : HudTheme.Surface
        );
        var accent = button.transform.Find("LEHUD_SelectedAccent");
        if (!accent.IsNullOrDestroyed())
            accent.gameObject.SetActive(selected);
    }

    static void Caption(Button button, string text)
    {
        LocaleRegistry.Apply(button.GetComponentInChildren<Text>(true), text);
    }

    static string L(string text) => LocaleRegistry.Translate(text);

    static string Selected(Dropdown catalog, string fallback) =>
        catalog.value > 0 && catalog.value < catalog.options.Count
            ? catalog.options[catalog.value].text
            : fallback;

    static TMP_InputField Input(
        GameObject parent,
        string name,
        float x0,
        float y0,
        float x1,
        float y1,
        string value,
        bool numeric
    )
    {
        var go = UnityEngine.Object.Instantiate(template.gameObject, parent.transform);
        go.name = name;
        Rect(go, x0, y0, x1, y1);
        var layout = go.GetComponent<LayoutElement>();
        if (!layout.IsNullOrDestroyed())
            layout.ignoreLayout = true;
        var input = go.GetComponent<TMP_InputField>();
        input.onValueChanged.RemoveAllListeners();
        input.onEndEdit.RemoveAllListeners();
        input.enabled = true;
        input.readOnly = false;
        input.interactable = true;
        input.characterLimit = numeric ? 8 : 100;
        input.contentType = numeric
            ? TMP_InputField.ContentType.IntegerNumber
            : TMP_InputField.ContentType.Standard;
        input.SetTextWithoutNotify(value);
        input.targetGraphic.raycastTarget = true;
        var background = go.GetComponent<Image>();
        if (!background.IsNullOrDestroyed())
        {
            background.sprite = null;
            background.color = HudTheme.InputBackground;
        }
        HudStyler.AddPrimaryBorder(go, HudTheme.ControlBorderWidth);
        if (!input.placeholder.IsNullOrDestroyed())
            input.placeholder.gameObject.SetActive(false);
        input.textComponent.color = foreground;
        input.textComponent.fontSize = 15;
        input.textComponent.enableAutoSizing = true;
        input.textComponent.fontSizeMin = 10;
        input.textComponent.fontSizeMax = 15;
        input.textComponent.raycastTarget = false;
        input.textComponent.margin = Vector4.zero;
        if (!input.textViewport.IsNullOrDestroyed())
            Rect(input.textViewport.gameObject, .04f, .05f, .96f, .95f);
        Rect(input.textComponent.gameObject, 0, 0, 1, 1);
        var group = go.GetComponent<CanvasGroup>();
        if (!group.IsNullOrDestroyed())
        {
            group.alpha = 1f;
            group.interactable = true;
            group.blocksRaycasts = true;
        }
        go.SetActive(true);
        return input;
    }

    static Number NumericCompact(
        GameObject parent,
        float x0,
        float y0,
        float x1,
        float y1,
        int min,
        int max,
        int value
    )
    {
        var n = new Number
        {
            min = min,
            max = max,
            value = value,
            input = Input(parent, "Number", x0, y0, x1, y1, value.ToString(), true),
        };
        numbers.Add(n);
        return n;
    }

    static Number Numeric(
        GameObject parent,
        string name,
        float y,
        int min,
        int max,
        int value,
        bool percent,
        bool mode
    )
    {
        Label(parent, name + (percent ? " (%)" : ""), .03f, y - .015f, .56f, y + .055f, 14);
        var n = NumericCompact(
            parent,
            .57f,
            y - .025f,
            mode ? .77f : .96f,
            y + .055f,
            min,
            max,
            value
        );
        if (mode)
            n.mode = Button(
                parent,
                "Fixed",
                .79f,
                y - .025f,
                .97f,
                y + .055f,
                () =>
                {
                    n.random = !n.random;
                    RefreshModes();
                }
            );
        return n;
    }

    static void LogCorruptionMetadata()
    {
        if (metadataLogged)
            return;
        metadataLogged = true;
        foreach (var nested in typeof(AffixList).GetNestedTypes())
            if (
                nested.IsEnum
                && nested.Name.IndexOf("special", StringComparison.OrdinalIgnoreCase) >= 0
            )
                Main.logger_instance.Msg(
                    "Force Drop " + nested.Name + ": " + string.Join(", ", Enum.GetNames(nested))
                );
        Main.logger_instance.Msg(
            "Force Drop affix types: "
                + string.Join(", ", Enum.GetNames(typeof(AffixList.AffixType)))
        );
        foreach (var member in typeof(AffixList).GetMembers())
            if (
                member.MemberType == System.Reflection.MemberTypes.Field
                || member.MemberType == System.Reflection.MemberTypes.Property
            )
                Main.logger_instance.Msg("Force Drop catalog API: " + member);
        foreach (
            var type in new[]
            {
                typeof(ItemData),
                typeof(ItemDataUnpacked),
                typeof(ItemAffix),
                typeof(AffixList),
                typeof(AffixList.Affix),
                typeof(AffixList.SingleAffix),
                typeof(AffixList.MultiAffix),
            }
        )
        foreach (var member in type.GetMembers())
            if (
                member.Name.IndexOf("corrupt", StringComparison.OrdinalIgnoreCase) >= 0
                || member.Name.IndexOf("special", StringComparison.OrdinalIgnoreCase) >= 0
            )
                Main.logger_instance.Msg("Force Drop corruption API: " + type.Name + "." + member);
    }

    [HarmonyPatch(typeof(Button), "Press")]
    public class ButtonPress
    {
        [HarmonyPostfix]
        static void Postfix(Button __instance)
        {
            if (!__instance.interactable || !__instance.gameObject.activeInHierarchy)
                return;
            if (clicks.TryGetValue(__instance.GetInstanceID(), out var action))
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    Main.logger_instance.Error("Force Drop action: " + ex.Message);
                }
        }
    }
}
