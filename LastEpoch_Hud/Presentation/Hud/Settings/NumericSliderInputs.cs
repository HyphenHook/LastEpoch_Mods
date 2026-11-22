using System;
using System.Collections.Generic;
using System.Globalization;
using Il2CppTMPro;
using LastEpoch_Hud.Scripts.ModUI.Shell;
using UnityEngine;
using UnityEngine.UI;

namespace LastEpoch_Hud.Scripts.ModUI.Settings;

// One shared adapter for legacy and dynamically-created slider rows.
// The boxes edit the same numbers the old labels showed. They do not
// rescale a slider to 0-100 just because its label contains "%".
public static class NumericSliderInputs
{
    enum Unit
    {
        Raw,
        PercentOf255, // (value / 255) * 100, shown as "N %"
        Times100, // value * 100, shown as "+ N %" (move speed reaches 2000)
        StatPercent, // the slider value itself, shown as "+ N %"
        ChancePercent, // the slider value itself, shown as "N %"
        CritChance, // (value * 100) + 1 when the value is above 0
        Tier, // stored 0-based, shown 1-based
        PrefixX, // "x N"
        SuffixX, // "Nx"
        Seconds, // "All N sec"
    }

    sealed class Entry
    {
        public Slider slider;
        public Unit unit;
        public bool plus;
        public Text label;
        public TMP_InputField input;
        public UnityEngine.Events.UnityAction<string> submit;
    }

    static readonly Dictionary<int, Entry> entries = new Dictionary<int, Entry>();
    static GameObject root;
    static float nextScan;

    public static void Tick(GameObject hud)
    {
        if (hud.IsNullOrDestroyed())
        {
            return;
        }
        if (root != hud)
        {
            entries.Clear();
            root = hud;
            nextScan = 0f;
        }

        if (Time.unscaledTime >= nextScan)
        {
            nextScan = Time.unscaledTime + 1f;
            Scan(hud);
        }

        foreach (var entry in entries.Values)
        {
            if (entry.slider.IsNullOrDestroyed() || entry.input.IsNullOrDestroyed())
            {
                continue;
            }
            bool visible = entry.slider.gameObject.activeInHierarchy;
            entry.input.gameObject.SetActive(visible);
            if (!visible)
            {
                continue;
            }
            if (!entry.label.IsNullOrDestroyed())
            {
                entry.label.gameObject.SetActive(false);
            }
            entry.input.interactable = entry.slider.interactable;
            if (!entry.input.isFocused)
            {
                string value = Format(entry);
                if (entry.input.text != value)
                {
                    entry.input.SetTextWithoutNotify(value);
                }
            }
        }
    }

    static void Scan(GameObject hud)
    {
        TMP_InputField template = null;
        foreach (var candidate in hud.GetComponentsInChildren<TMP_InputField>(true))
        {
            if (
                !candidate.IsNullOrDestroyed()
                && candidate.name == "InputField"
                && candidate.transform.parent != null
                && candidate.transform.parent.name == "Name"
            )
            {
                template = candidate;
                break;
            }
        }
        if (template.IsNullOrDestroyed())
        {
            return;
        }

        foreach (var slider in hud.GetComponentsInChildren<Slider>(true))
        {
            if (slider.IsNullOrDestroyed() || entries.ContainsKey(slider.GetInstanceID()))
            {
                continue;
            }
            // Existing amount-only controls intentionally hide their sliders.
            if (!slider.gameObject.activeSelf)
            {
                continue;
            }
            Text valueLabel = FindValueLabel(slider);
            if (valueLabel.IsNullOrDestroyed())
            {
                continue;
            }
            try
            {
                Attach(slider, valueLabel, template);
            }
            catch (Exception ex)
            {
                Main.logger_instance?.Warning(
                    "Numeric input setup failed for " + slider.name + ": " + ex.Message
                );
            }
        }
    }

    static Text FindValueLabel(Slider slider)
    {
        Transform parent = slider.transform.parent;
        for (int depth = 0; depth < 4 && parent != null; depth++, parent = parent.parent)
        {
            // Stop before crossing into another row or an entire settings panel.
            if (parent.GetComponentsInChildren<Slider>(true).Length != 1)
            {
                return null;
            }
            foreach (var text in parent.GetComponentsInChildren<Text>(true))
            {
                if (!text.IsNullOrDestroyed() && text.name == "Value")
                {
                    return text;
                }
            }
        }
        return null;
    }

    static void Attach(Slider slider, Text label, TMP_InputField template)
    {
        Unit unit = Classify(slider, out bool plus);
        // Only regular, unsealed tier controls are capped; sealed controls retain their range.
        if (
            unit == Unit.Tier
            && slider.name.IndexOf("Seal", StringComparison.OrdinalIgnoreCase) < 0
            && !HasName(slider, "SealTier")
        )
        {
            slider.maxValue = Mathf.Min(slider.maxValue, 6f);
            slider.wholeNumbers = true;
        }

        GameObject clone = UnityEngine.Object.Instantiate(
            template.gameObject,
            label.transform.parent
        );
        clone.name = "NumericInput_" + slider.GetInstanceID();
        TMP_InputField input = clone.GetComponent<TMP_InputField>();
        RectTransform source = label.GetComponent<RectTransform>();
        RectTransform rect = clone.GetComponent<RectTransform>();
        // Fixed right-aligned dimensions avoid inheriting narrow label anchors.
        rect.anchorMin = new Vector2(1f, source.anchorMin.y);
        rect.anchorMax = new Vector2(1f, source.anchorMax.y);
        rect.pivot = new Vector2(1f, source.pivot.y);
        rect.anchoredPosition = new Vector2(-6f, source.anchoredPosition.y);
        rect.localScale = Vector3.one;
        rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 100f);
        rect.SetSizeWithCurrentAnchors(
            RectTransform.Axis.Vertical,
            Mathf.Max(24f, source.rect.height)
        );

        LayoutElement layout = clone.GetComponent<LayoutElement>();
        if (!layout.IsNullOrDestroyed())
        {
            layout.ignoreLayout = true;
        }
        Image background = clone.GetComponent<Image>();
        if (!background.IsNullOrDestroyed())
        {
            background.sprite = null;
            background.type = Image.Type.Simple;
            background.color = HudTheme.InputBackground;
        }
        ColorBlock colors = input.colors;
        colors.normalColor = HudTheme.SelectableTint;
        colors.highlightedColor = HudTheme.TextPrimary;
        colors.selectedColor = colors.highlightedColor;
        colors.pressedColor = HudTheme.TextSecondary;
        input.colors = colors;
        if (!input.placeholder.IsNullOrDestroyed())
        {
            input.placeholder.gameObject.SetActive(false);
        }
        if (!input.textViewport.IsNullOrDestroyed())
        {
            input.textViewport.anchorMin = Vector2.zero;
            input.textViewport.anchorMax = Vector2.one;
            input.textViewport.offsetMin = new Vector2(4f, 1f);
            input.textViewport.offsetMax = new Vector2(-4f, -1f);
        }
        input.onValueChanged.RemoveAllListeners();
        input.onEndEdit = new TMP_InputField.SubmitEvent();
        input.enabled = true;
        input.readOnly = false;
        input.interactable = slider.interactable;
        // Fractional rows (camera zoom, difficulty) must accept decimals and negatives.
        input.contentType = slider.wholeNumbers
            ? TMP_InputField.ContentType.IntegerNumber
            : TMP_InputField.ContentType.DecimalNumber;
        input.characterLimit = 16;
        // Populate after the display units have been recorded.
        if (!input.targetGraphic.IsNullOrDestroyed())
        {
            input.targetGraphic.raycastTarget = true;
        }
        if (!input.textComponent.IsNullOrDestroyed())
        {
            input.textComponent.raycastTarget = false;
            input.textComponent.fontSize = Mathf.Max(14f, label.fontSize);
            input.textComponent.enableAutoSizing = true;
            input.textComponent.fontSizeMin = 10f;
            input.textComponent.fontSizeMax = Mathf.Max(14f, label.fontSize);
            input.textComponent.color = label.color;
            input.textComponent.margin = Vector4.zero;
            RectTransform textRect = input.textComponent.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            input.textComponent.horizontalAlignment = HorizontalAlignmentOptions.Center;
            input.textComponent.verticalAlignment = VerticalAlignmentOptions.Middle;
        }
        CanvasGroup group = input.GetComponent<CanvasGroup>();
        if (!group.IsNullOrDestroyed())
        {
            group.interactable = true;
            group.blocksRaycasts = true;
        }

        var entry = new Entry
        {
            slider = slider,
            label = label,
            input = input,
            unit = unit,
            plus = plus,
        };
        input.SetTextWithoutNotify(Format(entry));
        entry.submit = (UnityEngine.Events.UnityAction<string>)(
            text => Commit(entry, entry.input.text)
        );
        input.onEndEdit.AddListener(entry.submit);
        entries.Add(slider.GetInstanceID(), entry);
        label.gameObject.SetActive(false);
        clone.SetActive(slider.gameObject.activeInHierarchy);
    }

    static Unit Classify(Slider slider, out bool plus)
    {
        plus = false;
        string name = slider.name ?? "";
        if (
            name.IndexOf("Tier", StringComparison.OrdinalIgnoreCase) >= 0
            || HasName(slider, "SealTier")
        )
            return Unit.Tier;
        if (name.IndexOf("CriticalChance", StringComparison.OrdinalIgnoreCase) >= 0)
            return Unit.CritChance;
        if (IsTimes100(name))
            return Unit.Times100;
        if (IsStatPercent(name))
            return Unit.StatPercent;
        if (name.IndexOf("AutoShatter", StringComparison.OrdinalIgnoreCase) >= 0)
            return Unit.ChancePercent;
        if (IsSuffixMultiplier(name))
            return Unit.SuffixX;
        if (name.IndexOf("Multiplier", StringComparison.OrdinalIgnoreCase) >= 0)
            return Unit.PrefixX;
        if (
            name.IndexOf("ItemDropChance", StringComparison.OrdinalIgnoreCase) >= 0
            || name.IndexOf("GoldDropChance", StringComparison.OrdinalIgnoreCase) >= 0
        )
        {
            plus = true;
            return Unit.PercentOf255;
        }
        if (
            name.IndexOf("AutoPotion", StringComparison.OrdinalIgnoreCase) >= 0
            || name.IndexOf("Implicit", StringComparison.OrdinalIgnoreCase) >= 0
            || name.IndexOf("AffixValue", StringComparison.OrdinalIgnoreCase) >= 0
            || name.IndexOf("UniqueMod", StringComparison.OrdinalIgnoreCase) >= 0
            || name.IndexOf("SealValue", StringComparison.OrdinalIgnoreCase) >= 0
            || (name == "ValueSlider")
            || (
                name == "Slider"
                && (
                    HasName(slider, "Implicit")
                    || HasName(slider, "UniqueMod")
                    || HasName(slider, "SealValue")
                )
            )
        )
        {
            return Unit.PercentOf255;
        }
        if (name.IndexOf("AutoStore", StringComparison.OrdinalIgnoreCase) >= 0)
            return Unit.Seconds;
        return Unit.Raw;
    }

    static bool IsTimes100(string name)
    {
        // Minion move-speed rows store a raw number. Only the character buffs use value * 100.
        if (name.IndexOf("Buffs_", StringComparison.OrdinalIgnoreCase) < 0)
        {
            return false;
        }
        return name.IndexOf("MoveSpeed", StringComparison.OrdinalIgnoreCase) >= 0
            || name.IndexOf("AttackSpeed", StringComparison.OrdinalIgnoreCase) >= 0
            || name.IndexOf("CastingSpeed", StringComparison.OrdinalIgnoreCase) >= 0
            || name.IndexOf("CriticalMultiplier", StringComparison.OrdinalIgnoreCase) >= 0
            || name.IndexOf("HealthRegen", StringComparison.OrdinalIgnoreCase) >= 0
            || name.IndexOf("ManaRegen", StringComparison.OrdinalIgnoreCase) >= 0
            || name.IndexOf("Buffs_Damage", StringComparison.OrdinalIgnoreCase) >= 0
            || name.IndexOf("AreaOfEffect", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    static bool IsStatPercent(string name)
    {
        return name.IndexOf("Buffs_Strenght", StringComparison.OrdinalIgnoreCase) >= 0
            || name.IndexOf("Buffs_Intelligence", StringComparison.OrdinalIgnoreCase) >= 0
            || name.IndexOf("Buffs_Dexterity", StringComparison.OrdinalIgnoreCase) >= 0
            || name.IndexOf("Buffs_Vitality", StringComparison.OrdinalIgnoreCase) >= 0
            || name.IndexOf("Buffs_Attunement", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    static bool IsSuffixMultiplier(string name)
    {
        return name.IndexOf("SkillLevelMultiplier", StringComparison.OrdinalIgnoreCase) >= 0
            || name.IndexOf("PassivePointMultiplier", StringComparison.OrdinalIgnoreCase) >= 0
            || name.IndexOf("PointMultiplier", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    static bool HasName(Slider slider, string token)
    {
        Transform current = slider.transform;
        for (int depth = 0; depth < 5 && current != null; depth++, current = current.parent)
        {
            if (current.name.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }
        return false;
    }

    static float DisplayValue(Entry entry)
    {
        float value = entry.slider.value;
        switch (entry.unit)
        {
            case Unit.PercentOf255:
                return value / 255f * 100f;
            case Unit.Times100:
                return value * 100f;
            case Unit.CritChance:
                return value > 0f ? (int)(value * 100f) + 1f : 0f;
            case Unit.Tier:
                return value + 1f;
            default:
                return value;
        }
    }

    static string Format(Entry entry)
    {
        float shown = DisplayValue(entry);
        bool fractional = entry.unit == Unit.Raw && !entry.slider.wholeNumbers;
        string number = fractional
            ? FormatFloat(shown)
            : Mathf.Round(shown).ToString("0", CultureInfo.InvariantCulture);
        switch (entry.unit)
        {
            case Unit.Times100:
            case Unit.StatPercent:
            case Unit.CritChance:
                return "+ " + number + " %";
            case Unit.PercentOf255:
                return (entry.plus ? "+ " : "") + number + " %";
            case Unit.ChancePercent:
                return number + " %";
            case Unit.PrefixX:
                return "x " + number;
            case Unit.SuffixX:
                return number + "x";
            case Unit.Seconds:
                return "All " + number + " sec";
            default:
                return number;
        }
    }

    static string FormatFloat(float value)
    {
        float rounded = Mathf.Round(value);
        if (Mathf.Abs(value - rounded) < 0.001f)
            return rounded.ToString("0", CultureInfo.InvariantCulture);
        return value.ToString("0.##", CultureInfo.InvariantCulture);
    }

    static void Commit(Entry entry, string text)
    {
        if (entry.slider.IsNullOrDestroyed() || entry.input.IsNullOrDestroyed())
        {
            return;
        }
        if (entry.slider.interactable && TryParse(text, out float value))
        {
            switch (entry.unit)
            {
                case Unit.PercentOf255:
                    value = value / 100f * 255f;
                    break;
                case Unit.Times100:
                    value = value / 100f;
                    break;
                case Unit.CritChance:
                    value = value <= 0f ? 0f : (value - 1f) / 100f;
                    break;
                case Unit.Tier:
                    value -= 1f;
                    break;
            }
            if (entry.unit == Unit.Tier || entry.slider.wholeNumbers)
                value = Mathf.Round(value);
            value = Mathf.Clamp(value, entry.slider.minValue, entry.slider.maxValue);
            entry.slider.value = value;
        }
        entry.input.SetTextWithoutNotify(Format(entry));
    }

    static bool TryParse(string text, out float value)
    {
        value = 0f;
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }
        string cleaned = text.Replace("%", "")
            .Replace("+", "")
            .Replace("x", "")
            .Replace("X", "")
            .Replace("sec", "")
            .Replace("All", "")
            .Trim();
        return float.TryParse(cleaned, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
            || float.TryParse(cleaned, NumberStyles.Float, CultureInfo.CurrentCulture, out value);
    }
}
