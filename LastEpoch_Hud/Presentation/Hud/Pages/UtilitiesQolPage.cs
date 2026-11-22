using System;
using LastEpoch_Hud.Scripts.Core.ModUI;
using LastEpoch_Hud.Scripts.Core.ModUI.Sliders;
using LastEpoch_Hud.Scripts.ModUI.Shell;
using UnityEngine;

namespace LastEpoch_Hud.Scripts.ModUI.Pages;

/// <summary>
/// Utilities &gt; QOL. This replaces the mixed legacy Pickup/Requirements panel
/// with small, focused cards while keeping the same Save_Manager fields.
/// State resets on HUD re-bind (Build).
/// </summary>
internal static class UtilitiesQolPage
{
    private static HudFormPage page;

    public static void Build(HudPageId pageId, GameObject parent, GameObject hud, Font font)
    {
        page = HudFormPage.Build(parent, hud, font, pageId);
        if (page == null)
            return;

        BuildAutoPickup();
        BuildAutoStore();
        BuildAutoShatter();
        BuildAutoSell();
        BuildMisc();
        BuildRequirements();
    }

    public static void Show() => page?.Show();

    public static void Hide() => page?.Hide();

    public static void Refresh() => page?.RefreshValues();

    private static void BuildAutoPickup()
    {
        var card = page.AddCard("AutoPickup", "Auto-Pickup");
        AddToggle(
            card,
            "Gold",
            "Gold",
            () => Save_Manager.instance.data.Items.Pickup.Enable_AutoPickup_Gold,
            value => Save_Manager.instance.data.Items.Pickup.Enable_AutoPickup_Gold = value
        );
        AddToggle(
            card,
            "Keys",
            "Keys",
            () => Save_Manager.instance.data.Items.Pickup.Enable_AutoPickup_Keys,
            value => Save_Manager.instance.data.Items.Pickup.Enable_AutoPickup_Keys = value
        );
        AddToggle(
            card,
            "Potions",
            "Potions",
            () => Save_Manager.instance.data.Items.Pickup.Enable_AutoPickup_Potions,
            value => Save_Manager.instance.data.Items.Pickup.Enable_AutoPickup_Potions = value
        );
        AddToggle(
            card,
            "ExperienceTomes",
            "Experience Tomes",
            () => Save_Manager.instance.data.Items.Pickup.Enable_AutoPickup_XpTome,
            value => Save_Manager.instance.data.Items.Pickup.Enable_AutoPickup_XpTome = value
        );
        AddToggle(
            card,
            "FavorTomes",
            "Favor Tomes",
            () => Save_Manager.instance.data.Items.Pickup.Enable_AutoPickup_FavorTome,
            value => Save_Manager.instance.data.Items.Pickup.Enable_AutoPickup_FavorTome = value
        );
        AddToggle(
            card,
            "MemoryAmber",
            "Memory Amber",
            () => Save_Manager.instance.data.Items.Pickup.Enable_AutoPickup_MemoryAmber,
            value => Save_Manager.instance.data.Items.Pickup.Enable_AutoPickup_MemoryAmber = value
        );
        AddToggle(
            card,
            "Materials",
            "Materials",
            () => Save_Manager.instance.data.Items.Pickup.Enable_AutoPickup_Materials,
            value => Save_Manager.instance.data.Items.Pickup.Enable_AutoPickup_Materials = value
        );
        AddToggle(
            card,
            "WovenEchoes",
            "Woven Echoes",
            () => Save_Manager.instance.data.Items.Pickup.Enable_AutoPickup_WovenEchoes,
            value => Save_Manager.instance.data.Items.Pickup.Enable_AutoPickup_WovenEchoes = value
        );
        AddToggle(
            card,
            "Filter",
            "Items In Filter",
            () => Save_Manager.instance.data.Items.Pickup.Enable_AutoPickup_FromFilter,
            value => Save_Manager.instance.data.Items.Pickup.Enable_AutoPickup_FromFilter = value
        );
    }

    private static void BuildAutoStore()
    {
        var card = page.AddCard("AutoStore", "Auto-Store");
        AddToggle(
            card,
            "OnDrop",
            "On Drop",
            () => Save_Manager.instance.data.Items.Pickup.Enable_AutoStore_OnDrop,
            value => Save_Manager.instance.data.Items.Pickup.Enable_AutoStore_OnDrop = value
        );
        AddToggle(
            card,
            "OnInventoryOpen",
            "On Inventory Open",
            () => Save_Manager.instance.data.Items.Pickup.Enable_AutoStore_OnInventoryOpen,
            value =>
                Save_Manager.instance.data.Items.Pickup.Enable_AutoStore_OnInventoryOpen = value
        );
        AddToggle(
            card,
            "EveryTenSeconds",
            "Every 10s",
            () => Save_Manager.instance.data.Items.Pickup.Enable_AutoStore_Timer,
            value =>
            {
                Save_Manager.instance.data.Items.Pickup.Enable_AutoStore_Timer = value;
                Save_Manager.instance.data.Items.Pickup.AutoStore_Timer = 10;
            }
        );
        page.AddToggle(
            card,
            "OnFullInventory",
            "On Full Inventory",
            () => ModSettings.AutoStore.OnFullInventory.Value,
            ModSettings.AutoStore.OnFullInventory.Set
        );
        page.AddText(
            card,
            "OnFullInventoryNote",
            "Stores materials in your stash when a pickup doesn't fit, then picks the item up.",
            58f
        );
    }

    private static void BuildAutoShatter()
    {
        var card = page.AddCard("AutoShatter", "Auto-Shatter");
        AddToggle(
            card,
            "NotInFilter",
            "Items Not In Filter",
            () => Save_Manager.instance.data.Items.Pickup.Enable_AutoShatter_FromFilter,
            value => Save_Manager.instance.data.Items.Pickup.Enable_AutoShatter_FromFilter = value
        );
        AddToggle(
            card,
            "UseRune",
            "Use Rune of Shattering",
            () => Save_Manager.instance.data.Items.Pickup.Enable_AutoShatter_UseRune,
            value => Save_Manager.instance.data.Items.Pickup.Enable_AutoShatter_UseRune = value
        );
        AddPercentSlider(
            card,
            "Chance",
            "Shatter Chance",
            ItemSliderLimits.AutoShatterChance,
            () => Save_Manager.instance.data.Items.Pickup.AutoShatter_Chance,
            value => Save_Manager.instance.data.Items.Pickup.AutoShatter_Chance = (int)value
        );
        AddPercentSlider(
            card,
            "AffixChance",
            "Affix Obtained From Shatter Chance",
            ItemSliderLimits.AutoShatterAffixChance,
            () => Save_Manager.instance.data.Items.Pickup.AutoShatter_AffixChance,
            value => Save_Manager.instance.data.Items.Pickup.AutoShatter_AffixChance = (int)value
        );
        AddPercentSlider(
            card,
            "QuantityChance",
            "Affix Quantity Chance",
            ItemSliderLimits.AutoShatterQuantityChance,
            () => Save_Manager.instance.data.Items.Pickup.AutoShatter_QuantityChance,
            value => Save_Manager.instance.data.Items.Pickup.AutoShatter_QuantityChance = (int)value
        );
    }

    private static void BuildAutoSell()
    {
        var card = page.AddCard("AutoSell", "Auto-Sell");
        AddToggle(
            card,
            "NotInFilter",
            "Items Not In Filter",
            () => Save_Manager.instance.data.Items.Pickup.Enable_AutoSell_FromFilter,
            value => Save_Manager.instance.data.Items.Pickup.Enable_AutoSell_FromFilter = value
        );
    }

    private static void BuildMisc()
    {
        var card = page.AddCard("Misc", "Misc");
        AddToggle(
            card,
            "RangePickup",
            "Range Pickup",
            () => Save_Manager.instance.data.Items.Pickup.Enable_RangePickup,
            value => Save_Manager.instance.data.Items.Pickup.Enable_RangePickup = value
        );
        AddToggle(
            card,
            "HideNotifications",
            "Hide Material Notifications",
            () => Save_Manager.instance.data.Items.Pickup.Enable_HideMaterialsNotifications,
            value =>
                Save_Manager.instance.data.Items.Pickup.Enable_HideMaterialsNotifications = value
        );
    }

    private static void BuildRequirements()
    {
        var card = page.AddCard("Requirements", "Requirements");
        AddToggle(
            card,
            "Class",
            "Class",
            () => Save_Manager.instance.data.Items.Req.classe,
            value => Save_Manager.instance.data.Items.Req.classe = value
        );
        AddToggle(
            card,
            "Level",
            "Level",
            () => Save_Manager.instance.data.Items.Req.level,
            value => Save_Manager.instance.data.Items.Req.level = value
        );
        AddToggle(
            card,
            "Set",
            "Set",
            () => Save_Manager.instance.data.Items.Req.set,
            value => Save_Manager.instance.data.Items.Req.set = value
        );
    }

    private static void AddToggle(
        HudFormPage.Card card,
        string id,
        string label,
        Func<bool> read,
        Action<bool> write
    )
    {
        page.AddToggle(
            card,
            id,
            label,
            () => HasSave() && read(),
            value =>
            {
                if (HasSave())
                    write(value);
            }
        );
    }

    private static void AddPercentSlider(
        HudFormPage.Card card,
        string id,
        string label,
        SliderRange range,
        Func<int> read,
        Action<float> write
    )
    {
        page.AddSlider(
            card,
            id,
            label,
            "%",
            range.Min,
            range.Max,
            range.WholeNumbers,
            () => HasSave() ? read() : 0f,
            value =>
            {
                if (HasSave())
                    write(value);
            }
        );
    }

    private static bool HasSave() =>
        !Save_Manager.instance.IsNullOrDestroyed() && Save_Manager.instance.initialized;
}
