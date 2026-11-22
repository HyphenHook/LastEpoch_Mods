using System;
using System.Collections.Generic;
using Il2Cpp;
using LastEpoch_Hud.Scripts.Core.Character;
using LastEpoch_Hud.Scripts.Core.Factions;
using LastEpoch_Hud.Scripts.Core.ModUI;
using LastEpoch_Hud.Scripts.Core.ModUI.Sliders;
using LastEpoch_Hud.Scripts.Mods.Factions;
using LastEpoch_Hud.Scripts.ModUI.Settings;
using LastEpoch_Hud.Scripts.ModUI.Shell;
using UnityEngine;
using UnityEngine.UI;

namespace LastEpoch_Hud.Scripts.ModUI.Pages;

/// <summary>
/// Utilities &gt; Character. The legacy Character, Skills, Data, Blessings, and
/// Factions controls are represented here; factions use their own controls and edit through Core rules.
/// The page re-builds on HUD re-bind (Build); faction inputs reset then.
/// </summary>
internal static class UtilitiesCharacterPage
{
    private static HudFormPage _page;
    private static int _factionSelection;
    private static float _factionFavor;
    private static float _factionRank;
    private static float _factionReputation;
    private static Dropdown _factionDropdown;
    private static Dictionary<string, string> _factionOptionsLocale;
    private static Dropdown _classDropdown;
    private static Dictionary<string, string> _classOptionsLocale;
    private static bool _classOptionsFromGame;

    public static void Build(HudPageId pageId, GameObject parent, GameObject hud, Font font)
    {
        _page = HudFormPage.Build(parent, hud, font, pageId);
        if (_page == null)
        {
            return;
        }

        _page.AddButtonGrid(
            "CharacterActions",
            new[] { "Level Up Once", "Level Up To Level 100", "Skip Campaign", "Reset Mastery" },
            new System.Action[]
            {
                Mods.Character.Character_Level.LevelUpOnce,
                Mods.Character.Character_Level.LevelUptoMax,
                Mods.Character.Character_MainQuest.Complete,
                Mods.Character.Character_Masteries.ResetChooseMasterie,
            }
        );
        BuildCheats();
        BuildData();
        BuildBlessings();
        BuildFactions();
    }

    public static void Show()
    {
        RefreshFactionOptions();
        RefreshClassOptions();
        _page?.Show();
    }

    public static void Hide() => _page?.Hide();

    public static void Refresh()
    {
        RefreshFactionOptions();
        RefreshClassOptions();
        _page?.RefreshValues();
    }

    // The two out-of-mana modes exclude each other: turning one on turns the other off.
    private static void SetManaMode(BoolSetting setting, bool value)
    {
        setting.Set(value);
        if (value)
        {
            BoolSetting other =
                setting == ModSettings.SkillsMana.CastWhenOutOfMana
                    ? ModSettings.SkillsMana.AllowNegativeMana
                    : ModSettings.SkillsMana.CastWhenOutOfMana;
            other.Set(false);
            _page?.RefreshValues();
        }
    }

    private static void BuildCheats()
    {
        HudFormPage.Card card = _page.AddCard("Cheats", "Cheats");
        // Preserve native lens/double-reward behavior; only the transient item count changes.
        _page.AddToggleSlider(
            card,
            "ProphecyRewardMultiplier",
            "Prophecy Reward Multiplier",
            "x",
            1f,
            10f,
            true,
            () => ModSettings.ProphecyRewards.Multiplier.Enabled,
            enabled => ModSettings.ProphecyRewards.Multiplier.SetEnabled(enabled),
            () => ModSettings.ProphecyRewards.Multiplier.Value,
            value => ModSettings.ProphecyRewards.Multiplier.SetValue(value)
        );

        _page.AddToggle(
            card,
            "UnlockAllIdols",
            "Unlock All Idol Slots",
            () =>
                HasSave() && Save_Manager.instance.data.Character.Cheats.Enable_UnlockAllIdolsSlots,
            value =>
            {
                if (!HasSave())
                {
                    return;
                }

                Save_Manager.instance.data.Character.Cheats.Enable_UnlockAllIdolsSlots = value;
                Mods.Character.Character_UnlockAllIdols.Update();
            }
        );
        _page.AddToggle(
            card,
            "IdolRerollFreeAmber",
            "No Memory Amber Cost",
            () => ModSettings.IdolReroll.FreeMemoryAmber.Value,
            value => ModSettings.IdolReroll.FreeMemoryAmber.Set(value)
        );
        _page.AddToggle(
            card,
            "IdolRerollUnlimitedUses",
            "Unlimited Idol Altar Uses",
            () => ModSettings.IdolReroll.UnlimitedUses.Value,
            value => ModSettings.IdolReroll.UnlimitedUses.Set(value)
        );

        _page.AddToggle(
            card,
            "GodMode",
            "God Mode",
            () => HasSave() && Save_Manager.instance.data.Character.Cheats.Enable_GodMode,
            value =>
            {
                if (HasSave())
                {
                    Save_Manager.instance.data.Character.Cheats.Enable_GodMode = value;
                }
            }
        );
        _page.AddToggle(
            card,
            "NoTransformHealthDecay",
            "No Transform Health Decay",
            () => ModSettings.TransformForms.NoHealthDecay.Value,
            value => ModSettings.TransformForms.NoHealthDecay.Set(value)
        );
        _page.AddToggle(
            card,
            "LowLife",
            "Force Low Life",
            () => HasSave() && Save_Manager.instance.data.Character.Cheats.Enable_LowLife,
            value =>
            {
                if (HasSave())
                {
                    Save_Manager.instance.data.Character.Cheats.Enable_LowLife = value;
                }
            }
        );
        _page.AddToggle(
            card,
            "WeaponRestrictions",
            "Ignore Weapon Restrictions",
            () =>
                HasSave() && Save_Manager.instance.data.Character.Cheats.Enable_TwoHandedWithShield,
            value =>
            {
                if (HasSave())
                {
                    Save_Manager.instance.data.Character.Cheats.Enable_TwoHandedWithShield = value;
                }
            }
        );
        _page.AddToggle(
            card,
            "ManaCost",
            "Remove Mana Cost",
            () => HasSave() && Save_Manager.instance.data.Skills.Enable_RemoveManaCost,
            value =>
            {
                if (HasSave())
                {
                    Save_Manager.instance.data.Skills.Enable_RemoveManaCost = value;
                }
            }
        );
        _page.AddToggle(
            card,
            "ChannelCost",
            "Remove Mana Cost While Channeling",
            () => HasSave() && Save_Manager.instance.data.Skills.Enable_RemoveChannelCost,
            value =>
            {
                if (HasSave())
                {
                    Save_Manager.instance.data.Skills.Enable_RemoveChannelCost = value;
                }
            }
        );
        _page.AddToggle(
            card,
            "ChannelRegen",
            "Mana Regeneration While Channeling",
            () => HasSave() && Save_Manager.instance.data.Skills.Enable_NoManaRegenWhileChanneling,
            value =>
            {
                if (HasSave())
                {
                    Save_Manager.instance.data.Skills.Enable_NoManaRegenWhileChanneling = value;
                }
            }
        );
        _page.AddToggle(
            card,
            "ContinueOutOfMana",
            "Don't Stop When Out Of Mana Channeling",
            () => HasSave() && Save_Manager.instance.data.Skills.Enable_StopWhenOutOfMana,
            value =>
            {
                if (HasSave())
                {
                    Save_Manager.instance.data.Skills.Enable_StopWhenOutOfMana = value;
                }
            }
        );
        _page.AddToggle(
            card,
            "CastWhenOutOfMana",
            "Cast When Out Of Mana",
            () => ModSettings.SkillsMana.CastWhenOutOfMana.Value,
            value => SetManaMode(ModSettings.SkillsMana.CastWhenOutOfMana, value)
        );
        _page.AddToggle(
            card,
            "AllowNegativeMana",
            "Allow Negative Mana",
            () => ModSettings.SkillsMana.AllowNegativeMana.Value,
            value => SetManaMode(ModSettings.SkillsMana.AllowNegativeMana, value)
        );
        _page.AddToggle(
            card,
            "Cooldown",
            "No Cooldown",
            () => HasSave() && Save_Manager.instance.data.Skills.Enable_RemoveCooldown,
            value =>
            {
                if (HasSave())
                {
                    Save_Manager.instance.data.Skills.Enable_RemoveCooldown = value;
                }
            }
        );
        _page.AddToggle(
            card,
            "NodeRequirements",
            "Remove Node Requirements",
            () => HasSave() && Save_Manager.instance.data.Skills.Disable_NodeRequirement,
            value =>
            {
                if (HasSave())
                {
                    Save_Manager.instance.data.Skills.Disable_NodeRequirement = value;
                }
            }
        );

        _page.AddSlider(
            card,
            "PassivePoints",
            "Passive Points",
            string.Empty,
            0f,
            255f,
            true,
            () =>
                HasSave() && Save_Manager.instance.data.Skills.Enable_PassivePoints
                    ? Save_Manager.instance.data.Skills.PassivePoints
                    : 0f,
            value =>
            {
                if (!HasSave())
                {
                    return;
                }
                bool enabled = value > 0.0001f;
                Save_Manager.instance.data.Skills.Enable_PassivePoints = enabled;
                if (enabled)
                {
                    Save_Manager.instance.data.Skills.PassivePoints = value;
                }
                Mods.Skills.Passives_Points.Sync();
            }
        );
        _page.AddSlider(
            card,
            "PassiveMultiplier",
            "Passive Point Multiplier Per Level",
            "x",
            0f,
            10f,
            true,
            () =>
                HasSave() && Save_Manager.instance.data.Skills.Enable_PassivePointMultiplier
                    ? Save_Manager.instance.data.Skills.PassivePointMultiplier
                    : 0f,
            value =>
            {
                if (!HasSave())
                {
                    return;
                }
                bool enabled = value > 0.0001f;
                Save_Manager.instance.data.Skills.Enable_PassivePointMultiplier = enabled;
                if (enabled)
                {
                    Save_Manager.instance.data.Skills.PassivePointMultiplier =
                        PointMultiplier.Clamp(value);
                }
                Mods.Skills.Passives_Points.Sync();
            }
        );
        _page.AddSlider(
            card,
            "SkillLevel",
            "Skill Level",
            string.Empty,
            0f,
            255f,
            true,
            () =>
                HasSave() && Save_Manager.instance.data.Skills.Enable_SkillLevel
                    ? Save_Manager.instance.data.Skills.SkillLevel
                    : 0f,
            value =>
            {
                if (!HasSave())
                {
                    return;
                }
                bool enabled = value > 0.0001f;
                Save_Manager.instance.data.Skills.Enable_SkillLevel = enabled;
                if (enabled)
                {
                    Save_Manager.instance.data.Skills.SkillLevel = value;
                }
                Mods.Skills.Skills_Level.Sync();
            }
        );
        _page.AddSlider(
            card,
            "SkillMultiplier",
            "Skill Point Multiplier",
            "x",
            0f,
            10f,
            true,
            () =>
                HasSave() && Save_Manager.instance.data.Skills.Enable_SkillLevelMultiplier
                    ? Save_Manager.instance.data.Skills.SkillLevelMultiplier
                    : 0f,
            value =>
            {
                if (!HasSave())
                {
                    return;
                }
                bool enabled = value > 0.0001f;
                Save_Manager.instance.data.Skills.Enable_SkillLevelMultiplier = enabled;
                if (enabled)
                {
                    Save_Manager.instance.data.Skills.SkillLevelMultiplier = PointMultiplier.Clamp(
                        value
                    );
                }
                Mods.Skills.Skills_Level.Sync();
            }
        );
        _page.AddSlider(
            card,
            "WeaverPoints",
            "Weaver Tree Points",
            string.Empty,
            0f,
            Mods.Factions.TheWoven.Faction_Woven_TreePoints.SliderMax,
            true,
            () =>
                HasSave() && Save_Manager.instance.data.Factions.TheWoven.Enable_TreePoints
                    ? Save_Manager.instance.data.Factions.TheWoven.TreePoints
                    : 0f,
            value =>
            {
                if (!HasSave())
                {
                    return;
                }
                bool enabled = value > 0.0001f;
                Save_Manager.instance.data.Factions.TheWoven.Enable_TreePoints = enabled;
                if (enabled)
                {
                    Save_Manager.instance.data.Factions.TheWoven.TreePoints = (int)value;
                    Mods.Factions.TheWoven.Faction_Woven_TreePoints.ApplyToPlayer();
                }
                else
                {
                    Mods.Factions.TheWoven.Faction_Woven_TreePoints.ReleaseToRealPoints();
                }
            }
        );
        _page.AddSlider(
            card,
            "WeaverMultiplier",
            "Weaver Tree Points Multiplier",
            "x",
            0f,
            10f,
            true,
            () =>
                HasSave() && Save_Manager.instance.data.Factions.TheWoven.Enable_PointMultiplier
                    ? Save_Manager.instance.data.Factions.TheWoven.PointMultiplier
                    : 0f,
            value =>
            {
                if (!HasSave())
                {
                    return;
                }
                bool enabled = value > 0.0001f;
                Save_Manager.instance.data.Factions.TheWoven.Enable_PointMultiplier = enabled;
                if (enabled)
                {
                    Save_Manager.instance.data.Factions.TheWoven.PointMultiplier =
                        PointMultiplier.Clamp(value);
                }
                Mods.Factions.TheWoven.Faction_Woven_TreePoints.ApplyToPlayer();
            }
        );
        _page.AddToggle(
            card,
            "FreeWeaverRespec",
            "Free Weaver Respec",
            () => HasSave() && Save_Manager.instance.data.Factions.TheWoven.Enable_FreeRespe,
            value =>
            {
                if (HasSave())
                {
                    Save_Manager.instance.data.Factions.TheWoven.Enable_FreeRespe = value;
                }
            }
        );
    }

    private static void BuildData()
    {
        HudFormPage.Card card = _page.AddCard("Data", "Data");
        _classDropdown = _page.AddOptionsDropdown(
            card,
            "Class",
            "Class",
            ClassOptionTexts(),
            ReadClassOption,
            SetClass
        );
        _classOptionsLocale = Locales.current_dictionary;
        _classOptionsFromGame = GameClassListLoaded();
        _page.AddToggle(
            card,
            "Died",
            "Died",
            () => !Refs_Manager.player_data.IsNullOrDestroyed() && Refs_Manager.player_data.Died,
            value =>
            {
                if (!Refs_Manager.player_data.IsNullOrDestroyed())
                {
                    Refs_Manager.player_data.Died = value;
                }
            }
        );
        _page.AddSlider(
            card,
            "Deaths",
            "Deaths",
            string.Empty,
            0f,
            255f,
            true,
            () =>
                Refs_Manager.player_data.IsNullOrDestroyed() ? 0f : Refs_Manager.player_data.Deaths,
            value =>
            {
                if (!Refs_Manager.player_data.IsNullOrDestroyed())
                {
                    Refs_Manager.player_data.Deaths = (int)value;
                }
            }
        );
        _page.AddToggle(
            card,
            "Hardcore",
            "Hardcore",
            () =>
                !Refs_Manager.player_data.IsNullOrDestroyed() && Refs_Manager.player_data.Hardcore,
            value =>
            {
                if (!Refs_Manager.player_data.IsNullOrDestroyed())
                {
                    Refs_Manager.player_data.Hardcore = value;
                }
            }
        );
        _page.AddToggle(
            card,
            "Masochist",
            "Masochist",
            () =>
                !Refs_Manager.player_data.IsNullOrDestroyed() && Refs_Manager.player_data.Masochist,
            value =>
            {
                if (!Refs_Manager.player_data.IsNullOrDestroyed())
                {
                    Refs_Manager.player_data.Masochist = value;
                }
            }
        );
        _page.AddToggle(
            card,
            "Portal",
            "Portal Unlocked",
            () =>
                !Refs_Manager.player_data.IsNullOrDestroyed()
                && Refs_Manager.player_data.PortalUnlocked,
            value =>
            {
                if (!Refs_Manager.player_data.IsNullOrDestroyed())
                {
                    Refs_Manager.player_data.PortalUnlocked = value;
                }
            }
        );
        _page.AddToggle(
            card,
            "SoloChallenge",
            "Solo Challenge",
            () =>
                !Refs_Manager.player_data.IsNullOrDestroyed()
                && Refs_Manager.player_data.SoloChallenge,
            value =>
            {
                if (!Refs_Manager.player_data.IsNullOrDestroyed())
                {
                    Refs_Manager.player_data.SoloChallenge = value;
                }
            }
        );
        _page.AddSlider(
            card,
            "LanternLuminance",
            "Lantern Luminance",
            string.Empty,
            0f,
            255f,
            true,
            () =>
                Refs_Manager.player_data.IsNullOrDestroyed()
                    ? 0f
                    : Refs_Manager.player_data.LanternLuminance,
            value =>
            {
                if (!Refs_Manager.player_data.IsNullOrDestroyed())
                {
                    Refs_Manager.player_data.LanternLuminance = (int)value;
                }
            }
        );
        _page.AddButton(card, "Save", "Save Character Data", Mods.Character.Character_Data.Save);
    }

    private static void BuildBlessings()
    {
        HudFormPage.Card card = _page.AddCard("Blessings", "Blessings");
        _page.AddButton(
            card,
            "Choose",
            "Choose Blessings",
            Mods.Character.Character_Blessings.ChooseBlessings
        );
        _page.AddButton(
            card,
            "Discover",
            "Discover All Blessings",
            Mods.Character.Character_Blessings.DiscoverAllBlessings
        );
        _page.AddButton(
            card,
            "Max",
            "Max Out Blessings",
            Mods.Character.Character_Blessings.MaxOutBlessings
        );
        _page.AddButton(
            card,
            "Slots",
            "Unlock Blessing Slots",
            Mods.Character.Character_Blessings.UnlockBlessingSlots
        );
    }

    private static void BuildFactions()
    {
        _factionSelection = 0;
        _factionFavor = 0f;
        _factionRank = 0f;
        _factionReputation = 0f;

        HudFormPage.Card card = _page.AddCard("Factions", "Factions");
        _factionDropdown = _page.AddOptionsDropdown(
            card,
            "Faction",
            FactionCardLabels.Faction,
            FactionOptionTexts(),
            () => _factionSelection,
            value => _factionSelection = value
        );
        _factionOptionsLocale = Locales.current_dictionary;

        AddFactionSlider(
            card,
            "Favor",
            FactionCardLabels.Favor,
            CharacterSliderLimits.FactionFavor,
            () => _factionFavor,
            value => _factionFavor = value
        );
        _page.AddButton(card, "GainFavor", FactionCardLabels.GainFavor, GainFavorClicked);

        AddFactionSlider(
            card,
            "Rank",
            FactionCardLabels.Rank,
            CharacterSliderLimits.FactionRank,
            () => _factionRank,
            value => _factionRank = value
        );
        _page.AddButton(card, "SetRank", FactionCardLabels.SetRank, SetRankClicked);

        AddFactionSlider(
            card,
            "Reputation",
            FactionCardLabels.Reputation,
            CharacterSliderLimits.FactionReputation,
            () => _factionReputation,
            value => _factionReputation = value
        );
        _page.AddButton(
            card,
            "SetReputation",
            FactionCardLabels.SetReputation,
            SetReputationClicked
        );
    }

    private static void AddFactionSlider(
        HudFormPage.Card card,
        string id,
        string label,
        SliderRange range,
        Func<float> read,
        Action<float> write
    )
    {
        _page.AddSlider(
            card,
            id,
            label,
            string.Empty,
            range.Min,
            range.Max,
            range.WholeNumbers,
            read,
            write
        );
    }

    private static string[] FactionOptionTexts()
    {
        string[] texts = new string[FactionSelection.OptionLabels.Count];
        for (int i = 0; i < texts.Length; i++)
        {
            texts[i] = LocaleRegistry.Translate(FactionSelection.OptionLabels[i]);
        }

        return texts;
    }

    /// <summary>Re-translates the faction options after a language change.</summary>
    private static void RefreshFactionOptions()
    {
        if (_factionDropdown.IsNullOrDestroyed())
        {
            return;
        }

        if (ReferenceEquals(_factionOptionsLocale, Locales.current_dictionary))
        {
            return;
        }

        SettingsBuilder.ApplyDropdownOptions(_factionDropdown, FactionOptionTexts());
        // ClearOptions resets the shown value to 0; keep the stored faction selected.
        _factionDropdown.SetValueWithoutNotify(_factionSelection);
        _factionOptionsLocale = Locales.current_dictionary;
    }

    private static void GainFavorClicked()
    {
        if (!TrySelectedKind(out FactionKind kind))
        {
            return;
        }
        FactionStanding.GainFavor(kind, (int)MathF.Round(_factionFavor));
    }

    private static void SetRankClicked()
    {
        if (!TrySelectedKind(out FactionKind kind))
        {
            return;
        }
        FactionStanding.SetRank(kind, (int)MathF.Round(_factionRank));
    }

    private static void SetReputationClicked()
    {
        if (!TrySelectedKind(out FactionKind kind))
        {
            return;
        }
        FactionStanding.SetReputation(kind, (int)MathF.Round(_factionReputation));
    }

    private static bool TrySelectedKind(out FactionKind kind) =>
        FactionSelection.TryKindAt(_factionSelection, out kind);

    private static bool GameClassListLoaded() =>
        !Refs_Manager.character_class_list.IsNullOrDestroyed();

    private static string[] ClassOptionTexts()
    {
        bool loaded = GameClassListLoaded();
        string[] texts = new string[CharacterClassSelection.OptionLabels.Count];
        for (int i = 0; i < texts.Length; i++)
        {
            string localized = null;
            string className = null;
            if (loaded && CharacterClassSelection.TryClassIdAt(i, out int classId))
            {
                ReadGameNames(classId, out localized, out className);
            }

            texts[i] = CharacterClassSelection.LabelAt(i, localized, className);
        }

        return texts;
    }

    private static void ReadGameNames(int classId, out string localized, out string className)
    {
        localized = null;
        className = null;
        try
        {
            localized = CharacterClassList.getLocalizedClassName(classId, 0);
            className = CharacterClassList.getClass(classId)?.className;
        }
        catch (Exception ex)
        {
            ErrorLog.Report(ex, "Character.ClassName");
        }
    }

    /// <summary>Rebuilds the class options after a language change or once the game names load.</summary>
    private static void RefreshClassOptions()
    {
        if (_classDropdown.IsNullOrDestroyed())
        {
            return;
        }

        bool localeChanged = !ReferenceEquals(_classOptionsLocale, Locales.current_dictionary);
        if (
            !CharacterClassSelection.ShouldRebuildOptions(
                localeChanged,
                _classOptionsFromGame,
                GameClassListLoaded()
            )
        )
        {
            return;
        }

        SettingsBuilder.ApplyDropdownOptions(_classDropdown, ClassOptionTexts());
        // ClearOptions resets the shown value to 0; show the current class again.
        _classDropdown.SetValueWithoutNotify(ReadClassOption());
        _classOptionsLocale = Locales.current_dictionary;
        _classOptionsFromGame = GameClassListLoaded();
    }

    private static int ReadClassOption()
    {
        if (Refs_Manager.player_data.IsNullOrDestroyed())
        {
            return 0;
        }

        return CharacterClassSelection.OptionIndexOf(Refs_Manager.player_data.CharacterClass);
    }

    private static void SetClass(int optionIndex)
    {
        if (Refs_Manager.player_data.IsNullOrDestroyed())
        {
            return;
        }

        if (!CharacterClassSelection.TryClassIdAt(optionIndex, out int classId))
        {
            return;
        }

        Refs_Manager.player_data.CharacterClass = classId;
    }

    private static bool HasSave() =>
        !Save_Manager.instance.IsNullOrDestroyed() && Save_Manager.instance.initialized;
}
