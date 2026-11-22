using System.Collections.Generic;
using LastEpoch_Hud.Scripts.Core.ModUI;
using LastEpoch_Hud.Scripts.ModUI.Shell;
using UnityEngine;

namespace LastEpoch_Hud.Scripts.ModUI.Pages;

/// <summary>
/// Save bindings for Utilities &gt; Multipliers. The shared HudSliderCard owns all
/// visuals and interaction; these definitions preserve the original raw x values.
/// State resets on HUD re-bind (Build).
/// </summary>
internal static class UtilitiesMultipliersPage
{
    private static HudSliderCard page;

    public static void Build(HudPageId pageId, GameObject parent, GameObject hud, Font font)
    {
        page = HudSliderCard.Build(
            parent,
            hud,
            font,
            pageId,
            "Multipliers",
            new List<HudSliderCard.Definition>
            {
                Multiplier(
                    "Density",
                    "Density Multiplier",
                    () => Save_Manager.instance.data.Character.Cheats.Enable_DensityMultiplier,
                    () => Save_Manager.instance.data.Character.Cheats.DensityMultiplier,
                    (enabled, value) =>
                    {
                        Save_Manager.instance.data.Character.Cheats.Enable_DensityMultiplier =
                            enabled;
                        Save_Manager.instance.data.Character.Cheats.DensityMultiplier = value;
                    }
                ),
                Multiplier(
                    "Experience",
                    "Experience Multiplier",
                    () => Save_Manager.instance.data.Character.Cheats.Enable_ExperienceMultiplier,
                    () => Save_Manager.instance.data.Character.Cheats.ExperienceMultiplier,
                    (enabled, value) =>
                    {
                        Save_Manager.instance.data.Character.Cheats.Enable_ExperienceMultiplier =
                            enabled;
                        Save_Manager.instance.data.Character.Cheats.ExperienceMultiplier = value;
                    }
                ),
                Multiplier(
                    "AbilityExperience",
                    "Ability Experience Multiplier",
                    () => Save_Manager.instance.data.Character.Cheats.Enable_AbilityMultiplier,
                    () => Save_Manager.instance.data.Character.Cheats.AbilityMultiplier,
                    (enabled, value) =>
                    {
                        Save_Manager.instance.data.Character.Cheats.Enable_AbilityMultiplier =
                            enabled;
                        Save_Manager.instance.data.Character.Cheats.AbilityMultiplier = value;
                    }
                ),
                Multiplier(
                    "Favor",
                    "Favor Multiplier",
                    () => Save_Manager.instance.data.Character.Cheats.Enable_FavorMultiplier,
                    () => Save_Manager.instance.data.Character.Cheats.FavorMultiplier,
                    (enabled, value) =>
                    {
                        Save_Manager.instance.data.Character.Cheats.Enable_FavorMultiplier =
                            enabled;
                        Save_Manager.instance.data.Character.Cheats.FavorMultiplier = value;
                    }
                ),
                MemoryAmber(),
                Multiplier(
                    "ItemDropQuantity",
                    "Item Drop Multiplier",
                    () => Save_Manager.instance.data.Character.Cheats.Enable_ItemDropMultiplier,
                    () => Save_Manager.instance.data.Character.Cheats.ItemDropMultiplier,
                    (enabled, value) =>
                    {
                        Save_Manager.instance.data.Character.Cheats.Enable_ItemDropMultiplier =
                            enabled;
                        Save_Manager.instance.data.Character.Cheats.ItemDropMultiplier = value;
                    }
                ),
                Multiplier(
                    "ItemDropChance",
                    "Item Drop Chance",
                    () => Save_Manager.instance.data.Character.Cheats.Enable_ItemDropChance,
                    () => Save_Manager.instance.data.Character.Cheats.ItemDropChance,
                    (enabled, value) =>
                    {
                        Save_Manager.instance.data.Character.Cheats.Enable_ItemDropChance = enabled;
                        Save_Manager.instance.data.Character.Cheats.ItemDropChance = value;
                    }
                ),
                Multiplier(
                    "GoldDropQuantity",
                    "Gold Drop Multiplier",
                    () => Save_Manager.instance.data.Character.Cheats.Enable_GoldDropMultiplier,
                    () => Save_Manager.instance.data.Character.Cheats.GoldDropMultiplier,
                    (enabled, value) =>
                    {
                        Save_Manager.instance.data.Character.Cheats.Enable_GoldDropMultiplier =
                            enabled;
                        Save_Manager.instance.data.Character.Cheats.GoldDropMultiplier = value;
                    }
                ),
                Multiplier(
                    "GoldDropChance",
                    "Gold Drop Chance",
                    () => Save_Manager.instance.data.Character.Cheats.Enable_GoldDropChance,
                    () => Save_Manager.instance.data.Character.Cheats.GoldDropChance,
                    (enabled, value) =>
                    {
                        Save_Manager.instance.data.Character.Cheats.Enable_GoldDropChance = enabled;
                        Save_Manager.instance.data.Character.Cheats.GoldDropChance = value;
                    }
                ),
            }
        );
    }

    public static void Show() => page?.Show();

    public static void Hide() => page?.Hide();

    private static HudSliderCard.Definition Multiplier(
        string id,
        string label,
        System.Func<bool> enabled,
        System.Func<float> read,
        System.Action<bool, float> write
    )
    {
        return new HudSliderCard.Definition
        {
            Id = id,
            Label = label,
            Unit = "x",
            Minimum = 0f,
            Maximum = 255f,
            WholeNumbers = true,
            Read = () => Save_Manager.instance.IsNullOrDestroyed() ? 0f : read(),
            Write = value =>
            {
                if (!Save_Manager.instance.IsNullOrDestroyed())
                    write(enabled(), value);
            },
            ReadEnabled = () => !Save_Manager.instance.IsNullOrDestroyed() && enabled(),
            WriteEnabled = isEnabled =>
            {
                if (!Save_Manager.instance.IsNullOrDestroyed())
                    write(isEnabled, read());
            },
        };
    }

    private static HudSliderCard.Definition MemoryAmber()
    {
        return new HudSliderCard.Definition
        {
            Id = "MemoryAmber",
            Label = "Memory Amber Multiplier",
            Unit = "x",
            Minimum = 0f,
            Maximum = 255f,
            WholeNumbers = true,
            Read = () =>
            {
                if (Save_Manager.instance.IsNullOrDestroyed())
                    return 0f;
                uint value = Save_Manager.instance.data.Character.Cheats.MemoryAmberMultiplier;
                if (value > 255u)
                {
                    value = 255u;
                    Save_Manager.instance.data.Character.Cheats.MemoryAmberMultiplier = value;
                }
                return value;
            },
            Write = value =>
            {
                if (Save_Manager.instance.IsNullOrDestroyed())
                    return;
                Save_Manager.instance.data.Character.Cheats.MemoryAmberMultiplier = (uint)
                    Mathf.Clamp(Mathf.RoundToInt(value), 0, 255);
            },
            ReadEnabled = () =>
                !Save_Manager.instance.IsNullOrDestroyed()
                && Save_Manager.instance.data.Character.Cheats.Enable_MemoryAmberMultiplier,
            WriteEnabled = enabled =>
            {
                if (!Save_Manager.instance.IsNullOrDestroyed())
                    Save_Manager.instance.data.Character.Cheats.Enable_MemoryAmberMultiplier =
                        enabled;
            },
        };
    }
}
