using System.Collections.Generic;
using LastEpoch_Hud.Scripts.Core.ModUI;
using LastEpoch_Hud.Scripts.ModUI.Shell;
using UnityEngine;

namespace LastEpoch_Hud.Scripts.ModUI.Pages;

/// <summary>
/// Save bindings for Utilities &gt; Buffs. Values are shown in the same percentage
/// units as the legacy labels while retaining every original raw slider range.
/// State resets on HUD re-bind (Build).
/// </summary>
internal static class UtilitiesBuffsPage
{
    private static HudSliderCard page;

    public static void Build(HudPageId pageId, GameObject parent, GameObject hud, Font font)
    {
        page = HudSliderCard.Build(
            parent,
            hud,
            font,
            pageId,
            "Buffs",
            new List<HudSliderCard.Definition>
            {
                Scaled(
                    "MoveSpeed",
                    "Move Speed",
                    20f,
                    () => Save_Manager.instance.data.Character.PermanentBuffs.Enable_MoveSpeed_Buff,
                    () => Save_Manager.instance.data.Character.PermanentBuffs.MoveSpeed_Buff_Value,
                    (enabled, value) =>
                    {
                        Save_Manager.instance.data.Character.PermanentBuffs.Enable_MoveSpeed_Buff =
                            enabled;
                        Save_Manager.instance.data.Character.PermanentBuffs.MoveSpeed_Buff_Value =
                            value;
                    }
                ),
                Scaled(
                    "Damage",
                    "Damage",
                    255f,
                    () => Save_Manager.instance.data.Character.PermanentBuffs.Enable_Damage_Buff,
                    () => Save_Manager.instance.data.Character.PermanentBuffs.Damage_Buff_Value,
                    (enabled, value) =>
                    {
                        Save_Manager.instance.data.Character.PermanentBuffs.Enable_Damage_Buff =
                            enabled;
                        Save_Manager.instance.data.Character.PermanentBuffs.Damage_Buff_Value =
                            value;
                    }
                ),
                Scaled(
                    "AreaOfEffect",
                    "Area of Effect",
                    100f,
                    () => Save_Manager.instance.data.Character.PermanentBuffs.Enable_AoE_Buff,
                    () => Save_Manager.instance.data.Character.PermanentBuffs.AoE_Buff_Value,
                    (enabled, value) =>
                    {
                        Save_Manager.instance.data.Character.PermanentBuffs.Enable_AoE_Buff =
                            enabled;
                        Save_Manager.instance.data.Character.PermanentBuffs.AoE_Buff_Value = value;
                    }
                ),
                Scaled(
                    "AttackSpeed",
                    "Attack Speed",
                    10f,
                    () =>
                        Save_Manager.instance.data.Character.PermanentBuffs.Enable_AttackSpeed_Buff,
                    () =>
                        Save_Manager.instance.data.Character.PermanentBuffs.AttackSpeed_Buff_Value,
                    (enabled, value) =>
                    {
                        Save_Manager
                            .instance
                            .data
                            .Character
                            .PermanentBuffs
                            .Enable_AttackSpeed_Buff = enabled;
                        Save_Manager.instance.data.Character.PermanentBuffs.AttackSpeed_Buff_Value =
                            value;
                    }
                ),
                Scaled(
                    "CastingSpeed",
                    "Casting Speed",
                    10f,
                    () => Save_Manager.instance.data.Character.PermanentBuffs.Enable_CastSpeed_Buff,
                    () => Save_Manager.instance.data.Character.PermanentBuffs.CastSpeed_Buff_Value,
                    (enabled, value) =>
                    {
                        Save_Manager.instance.data.Character.PermanentBuffs.Enable_CastSpeed_Buff =
                            enabled;
                        Save_Manager.instance.data.Character.PermanentBuffs.CastSpeed_Buff_Value =
                            value;
                    }
                ),
                CriticalChance(),
                Scaled(
                    "CriticalMultiplier",
                    "Critical Multiplier",
                    255f,
                    () =>
                        Save_Manager
                            .instance
                            .data
                            .Character
                            .PermanentBuffs
                            .Enable_CriticalMultiplier_Buff,
                    () =>
                        Save_Manager
                            .instance
                            .data
                            .Character
                            .PermanentBuffs
                            .CriticalMultiplier_Buff_Value,
                    (enabled, value) =>
                    {
                        Save_Manager
                            .instance
                            .data
                            .Character
                            .PermanentBuffs
                            .Enable_CriticalMultiplier_Buff = enabled;
                        Save_Manager
                            .instance
                            .data
                            .Character
                            .PermanentBuffs
                            .CriticalMultiplier_Buff_Value = value;
                    }
                ),
                Scaled(
                    "HealthRegen",
                    "Health Regeneration",
                    10f,
                    () =>
                        Save_Manager.instance.data.Character.PermanentBuffs.Enable_HealthRegen_Buff,
                    () =>
                        Save_Manager.instance.data.Character.PermanentBuffs.HealthRegen_Buff_Value,
                    (enabled, value) =>
                    {
                        Save_Manager
                            .instance
                            .data
                            .Character
                            .PermanentBuffs
                            .Enable_HealthRegen_Buff = enabled;
                        Save_Manager.instance.data.Character.PermanentBuffs.HealthRegen_Buff_Value =
                            value;
                    }
                ),
                Scaled(
                    "ManaRegen",
                    "Mana Regeneration",
                    10f,
                    () => Save_Manager.instance.data.Character.PermanentBuffs.Enable_ManaRegen_Buff,
                    () => Save_Manager.instance.data.Character.PermanentBuffs.ManaRegen_Buff_Value,
                    (enabled, value) =>
                    {
                        Save_Manager.instance.data.Character.PermanentBuffs.Enable_ManaRegen_Buff =
                            enabled;
                        Save_Manager.instance.data.Character.PermanentBuffs.ManaRegen_Buff_Value =
                            value;
                    }
                ),
                Direct(
                    "Strength",
                    "Strength",
                    () => Save_Manager.instance.data.Character.PermanentBuffs.Enable_Str_Buff,
                    () => Save_Manager.instance.data.Character.PermanentBuffs.Str_Buff_Value,
                    (enabled, value) =>
                    {
                        Save_Manager.instance.data.Character.PermanentBuffs.Enable_Str_Buff =
                            enabled;
                        Save_Manager.instance.data.Character.PermanentBuffs.Str_Buff_Value = value;
                    }
                ),
                Direct(
                    "Intelligence",
                    "Intelligence",
                    () => Save_Manager.instance.data.Character.PermanentBuffs.Enable_Int_Buff,
                    () => Save_Manager.instance.data.Character.PermanentBuffs.Int_Buff_Value,
                    (enabled, value) =>
                    {
                        Save_Manager.instance.data.Character.PermanentBuffs.Enable_Int_Buff =
                            enabled;
                        Save_Manager.instance.data.Character.PermanentBuffs.Int_Buff_Value = value;
                    }
                ),
                Direct(
                    "Dexterity",
                    "Dexterity",
                    () => Save_Manager.instance.data.Character.PermanentBuffs.Enable_Dex_Buff,
                    () => Save_Manager.instance.data.Character.PermanentBuffs.Dex_Buff_Value,
                    (enabled, value) =>
                    {
                        Save_Manager.instance.data.Character.PermanentBuffs.Enable_Dex_Buff =
                            enabled;
                        Save_Manager.instance.data.Character.PermanentBuffs.Dex_Buff_Value = value;
                    }
                ),
                Direct(
                    "Vitality",
                    "Vitality",
                    () => Save_Manager.instance.data.Character.PermanentBuffs.Enable_Vit_Buff,
                    () => Save_Manager.instance.data.Character.PermanentBuffs.Vit_Buff_Value,
                    (enabled, value) =>
                    {
                        Save_Manager.instance.data.Character.PermanentBuffs.Enable_Vit_Buff =
                            enabled;
                        Save_Manager.instance.data.Character.PermanentBuffs.Vit_Buff_Value = value;
                    }
                ),
                Direct(
                    "Attunement",
                    "Attunement",
                    () => Save_Manager.instance.data.Character.PermanentBuffs.Enable_Att_Buff,
                    () => Save_Manager.instance.data.Character.PermanentBuffs.Att_Buff_Value,
                    (enabled, value) =>
                    {
                        Save_Manager.instance.data.Character.PermanentBuffs.Enable_Att_Buff =
                            enabled;
                        Save_Manager.instance.data.Character.PermanentBuffs.Att_Buff_Value = value;
                    }
                ),
            }
        );
    }

    public static void Show()
    {
        if (!Save_Manager.instance.IsNullOrDestroyed())
            UpdateMasterEnable();
        page?.Show();
    }

    public static void Hide() => page?.Hide();

    private static HudSliderCard.Definition Scaled(
        string id,
        string label,
        float rawMaximum,
        System.Func<bool> enabled,
        System.Func<float> read,
        System.Action<bool, float> write
    )
    {
        return Buff(
            id,
            label,
            rawMaximum * 100f,
            enabled,
            () => read() * 100f,
            (isEnabled, percentage) => write(isEnabled, percentage / 100f)
        );
    }

    private static HudSliderCard.Definition Direct(
        string id,
        string label,
        System.Func<bool> enabled,
        System.Func<float> read,
        System.Action<bool, float> write
    ) => Buff(id, label, 255f, enabled, read, write);

    private static HudSliderCard.Definition Buff(
        string id,
        string label,
        float maximum,
        System.Func<bool> enabled,
        System.Func<float> read,
        System.Action<bool, float> write
    )
    {
        return new HudSliderCard.Definition
        {
            Id = id,
            Label = label,
            Unit = "%",
            Minimum = 0f,
            Maximum = maximum,
            WholeNumbers = true,
            Read = () => Save_Manager.instance.IsNullOrDestroyed() ? 0f : read(),
            Write = value =>
            {
                if (Save_Manager.instance.IsNullOrDestroyed())
                    return;
                write(enabled(), value);
                UpdateMasterEnable();
            },
            ReadEnabled = () =>
                !Save_Manager.instance.IsNullOrDestroyed()
                && Save_Manager.instance.data.Character.PermanentBuffs.Enable_Mod
                && enabled(),
            WriteEnabled = isEnabled =>
            {
                if (Save_Manager.instance.IsNullOrDestroyed())
                    return;
                write(isEnabled, read());
                UpdateMasterEnable();
            },
        };
    }

    private static HudSliderCard.Definition CriticalChance()
    {
        return Buff(
            "CriticalChance",
            "Critical Chance",
            96f,
            () => Save_Manager.instance.data.Character.PermanentBuffs.Enable_CriticalChance_Buff,
            () =>
            {
                float raw = Save_Manager
                    .instance
                    .data
                    .Character
                    .PermanentBuffs
                    .CriticalChance_Buff_Value;
                return raw > 0f ? raw * 100f + 1f : 0f;
            },
            (enabled, percentage) =>
            {
                Save_Manager.instance.data.Character.PermanentBuffs.Enable_CriticalChance_Buff =
                    enabled;
                Save_Manager.instance.data.Character.PermanentBuffs.CriticalChance_Buff_Value =
                    Mathf.Clamp((percentage - 1f) / 100f, 0f, 0.95f);
            }
        );
    }

    private static void UpdateMasterEnable()
    {
        var buffs = Save_Manager.instance.data.Character.PermanentBuffs;
        Save_Manager.instance.data.Character.PermanentBuffs.Enable_Mod =
            buffs.Enable_MoveSpeed_Buff
            || buffs.Enable_CastSpeed_Buff
            || buffs.Enable_AttackSpeed_Buff
            || buffs.Enable_Damage_Buff
            || buffs.Enable_ManaRegen_Buff
            || buffs.Enable_HealthRegen_Buff
            || buffs.Enable_CriticalChance_Buff
            || buffs.Enable_CriticalMultiplier_Buff
            || buffs.Enable_Str_Buff
            || buffs.Enable_Int_Buff
            || buffs.Enable_Dex_Buff
            || buffs.Enable_Vit_Buff
            || buffs.Enable_Att_Buff
            || buffs.Enable_AoE_Buff;
    }
}
