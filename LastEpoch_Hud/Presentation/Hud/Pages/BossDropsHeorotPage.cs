using LastEpoch_Hud.Scripts.Core.ModUI;
using LastEpoch_Hud.Scripts.ModUI.Shell;
using LastEpoch_Hud.Scripts.Mods.Items.Jewels;
using UnityEngine;

namespace LastEpoch_Hud.Scripts.ModUI.Pages;

internal static class BossDropsHeorotPage
{
    private static HudFormPage page;
    private static bool Ready => !Save_Manager.instance.IsNullOrDestroyed() && Save_Manager.instance.initialized;

    public static void Build(HudPageId id, GameObject parent, GameObject hud, Font font)
    {
        page = HudFormPage.Build(parent, hud, font, id);
        if (page == null) return;
        var card = page.AddCard("HeorotCore", "Heorot’s Core");
        page.AddText(card, "Rules", "Level 100 monolith Heorot only. At most one Core per encounter. Two-buff variants always have distinct buffs.", 65f);
        page.AddToggle(card, "OverrideRates", "Override CSV drop rates",
            () => Ready && Save_Manager.instance.data.Items.Drop.HeorotCoreOverride,
            value => { if (Ready) { Initialize(); Save_Manager.instance.data.Items.Drop.HeorotCoreOverride = value; } });
        page.AddToggle(card, "Testing", "Testing: bypass corruption scaling and native chance cap",
            () => Ready && Save_Manager.instance.data.Items.Drop.HeorotCoreIgnoreScaling,
            value => { if (Ready) Save_Manager.instance.data.Items.Drop.HeorotCoreIgnoreScaling = value; });
        for (int i = 0; i < 3; i++)
        {
            int variant = i;
            page.AddSlider(card, "Variant" + i, (i + 1) + " buff" + (i == 0 ? "" : "s") + " chance (%)", "%",
                0, 100, false, () => Read(variant), value => Write(variant, value));
        }
        page.AddText(card, "Scaling", "Normally these are base chances before native corruption/rarity scaling. In testing mode they are direct chances. Totals above 100% are normalized. Set one variant to 100% and the others to 0% for a guaranteed test drop. Overrides must be enabled.", 110f);
        page.AddButton(card, "Reset", "Restore CSV rates", () =>
        {
            if (!Ready) return;
            Save_Manager.instance.data.Items.Drop.HeorotCoreRatesInitialized = false;
            Initialize();
            Save_Manager.instance.data.Items.Drop.HeorotCoreOverride = false;
            Save_Manager.instance.data.Items.Drop.HeorotCoreIgnoreScaling = false;
            page.RefreshValues();
        });
    }

    private static void Initialize()
    {
        if (!Ready || Save_Manager.instance.data.Items.Drop.HeorotCoreRatesInitialized) return;
        var defaults = HeorotCore.DefaultVariantChances();
        Save_Manager.instance.data.Items.Drop.HeorotCoreSingleChance = (float)defaults[0];
        Save_Manager.instance.data.Items.Drop.HeorotCoreDoubleChance = (float)defaults[1];
        Save_Manager.instance.data.Items.Drop.HeorotCoreTripleChance = (float)defaults[2];
        Save_Manager.instance.data.Items.Drop.HeorotCoreRatesInitialized = true;
    }

    private static float Read(int variant)
    {
        if (!Ready) return (float)HeorotCore.DefaultVariantChances()[variant];
        Initialize();
        var settings = Save_Manager.instance.data.Items.Drop;
        return variant == 0 ? settings.HeorotCoreSingleChance : variant == 1 ? settings.HeorotCoreDoubleChance : settings.HeorotCoreTripleChance;
    }

    private static void Write(int variant, float value)
    {
        if (!Ready) return;
        Initialize();
        value = Mathf.Clamp(value, 0, 100);
        if (variant == 0) Save_Manager.instance.data.Items.Drop.HeorotCoreSingleChance = value;
        else if (variant == 1) Save_Manager.instance.data.Items.Drop.HeorotCoreDoubleChance = value;
        else Save_Manager.instance.data.Items.Drop.HeorotCoreTripleChance = value;
    }

    public static void Show() { Initialize(); page?.Show(); }
    public static void Hide() => page?.Hide();
    public static void Refresh() => page?.RefreshValues();
}
