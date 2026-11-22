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
public static partial class ForceDropBuilder
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
            FD.ItemType == Core.Items.EquipmentTypeRules.JewelBaseType
            || (FD.ItemRarity > 6 && FD.ItemLegendaryType == UniqueList.LegendaryType.LegendaryPotential)
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
                    context.IsHereticalIdol,
                    context.BaseType
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
}
