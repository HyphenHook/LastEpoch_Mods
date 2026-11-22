using System;
using System.Collections.Generic;
using LastEpoch_Hud.Scripts.Core.CustomItems;
using LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter.Bar;
using LastEpoch_Hud.Scripts.Core.CustomItems.Mageblood;
using LastEpoch_Hud.Scripts.Core.CustomItems.Mageblood.Config;
using LastEpoch_Hud.Scripts.Core.CustomItems.Mageblood.Menu;
using LastEpoch_Hud.Scripts.Mods.Items.CustomItems.Headhunter;
using LastEpoch_Hud.Scripts.ModUI;
using LastEpoch_Hud.Scripts.ModUI.Settings;
using LastEpoch_Hud.Scripts.ModUI.Shell;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LastEpoch_Hud.Scripts.Mods.Items.CustomItems.Mageblood.Menu;

/// <summary>The Mageblood Flasks card of the Custom Items page: slot dropdowns and one slider per stat value.</summary>
internal static class MagebloodFlasksCard
{
    private const float ProbeSeconds = 1f;
    private const float HelpRowHeight = 72f;
    private const float NoteRowHeight = 48f;

    private static readonly List<Dropdown> _slots = new();
    private static readonly List<MagebloodSliderView> _sliders = new();

    private static HudFormPage _page;
    private static HudFormPage.Card _card;
    private static MagebloodMenuLayout _layout;

    private static int _rebuildCount;
    private static bool _stale = true;
    private static int _shownVersion;
    private static MagebloodMenuStatus _shownStatus;
    private static object _shownTexts;

    private static bool _unresolved;
    private static int _probeStatId;
    private static float _nextProbe;

    public static void Build(HudFormPage page)
    {
        _page = page;
        _layout = null;
        _rebuildCount = 0;
        _slots.Clear();
        _sliders.Clear();
        _stale = true;
        ResetProbe();

        _card = page.AddCard("MagebloodFlasks", MagebloodFlasksTexts.CardTitle);
    }

    public static void OnShow()
    {
        ModSettings.Trace("Mageblood flasks: card shown");
        _stale = true;
    }

    /// <summary>True when the card changed and the page must refresh its values.</summary>
    public static bool Refresh(float now)
    {
        if (_page == null || _card == null)
        {
            return false;
        }

        if (ProbeUnresolved(now))
        {
            ModSettings.Trace("Mageblood flasks: stat name resolved");
            _stale = true;
        }

        var status = MagebloodMenuStatus.From(
            MagebloodConfigLoader.IsReadable,
            MagebloodConfigLoader.ProblemCount,
            MagebloodBuffs.Worn,
            MagebloodBuffs.ActiveFlasks
        );
        Dictionary<string, string> texts = Locales.current_dictionary;
        bool layoutChanged =
            _stale
            || MagebloodConfigLoader.Version != _shownVersion
            || !ReferenceEquals(texts, _shownTexts);
        if (!layoutChanged && status == _shownStatus)
        {
            return false;
        }

        if (layoutChanged)
        {
            UpdateLayout(texts);
        }

        _shownStatus = status;
        ApplyStatus(status, texts);
        return true;
    }

    private static void UpdateLayout(Dictionary<string, string> texts)
    {
        var layout = MagebloodMenuLayout.Build(MagebloodConfigLoader.Flasks);
        bool keep = !_stale && ReferenceEquals(texts, _shownTexts) && layout.SameShape(_layout);
        TraceLayout(texts, keep);
        _layout = layout;
        _shownVersion = MagebloodConfigLoader.Version;
        _shownTexts = texts;
        _stale = false;
        if (keep)
        {
            UpdateRanges();
            return;
        }

        Rebuild(texts);
    }

    private static void TraceLayout(Dictionary<string, string> texts, bool keep)
    {
        if (!ModSettings.Debug.Enabled.Value)
        {
            return;
        }

        _rebuildCount += keep ? 0 : 1;
        ModSettings.Trace(
            "Mageblood flasks layout: rebuild #"
                + _rebuildCount
                + " | "
                + (keep ? "ranges only" : "full")
                + ", stale "
                + _stale
                + ", version "
                + _shownVersion
                + "->"
                + MagebloodConfigLoader.Version
                + ", locale changed "
                + !ReferenceEquals(texts, _shownTexts)
        );
    }

    private static void Rebuild(Dictionary<string, string> texts)
    {
        int focus = FocusedSlot();
        ClearRows();
        AddRows(texts);
        HudFormPage.StyleCard(_card);
        Reselect(focus);
    }

    private static void ClearRows()
    {
        _page.ClearCard(_card);
        _slots.Clear();
        _sliders.Clear();
        ResetProbe();
    }

    private static void AddRows(Dictionary<string, string> texts)
    {
        Text help = _page.AddText(_card, "MagebloodLeftmostHelp", string.Empty, HelpRowHeight);
        LocaleRegistry.Apply(help, MagebloodFlasksTexts.LeftmostHelp);
        if (_layout.Slots.Count == 0)
        {
            Text none = _page.AddText(_card, "MagebloodNoFlasks", string.Empty, NoteRowHeight);
            LocaleRegistry.Apply(none, MagebloodFlasksTexts.NoFlasks);
            return;
        }

        string[] shown = ShownOptions(texts);
        for (int slot = 0; slot < _layout.Slots.Count; slot++)
        {
            AddSlot(slot, shown);
        }
    }

    private static string[] ShownOptions(Dictionary<string, string> texts)
    {
        string[] shown = new string[_layout.Options.Count];
        for (int i = 0; i < shown.Length; i++)
        {
            shown[i] = MagebloodFlaskNames.Display(texts, _layout.Options[i]);
        }

        return shown;
    }

    private static void AddSlot(int slot, IReadOnlyList<string> optionNames)
    {
        Dropdown dropdown = _page.AddOptionsDropdown(
            _card,
            "MagebloodSlot" + slot,
            string.Empty,
            optionNames,
            () => ReadSlot(slot),
            index => OnSlotPicked(slot, index)
        );
        _slots.Add(dropdown);

        for (int index = 0; index < _layout.Slots[slot].Sliders.Count; index++)
        {
            AddStatSlider(slot, index);
        }
    }

    private static void AddStatSlider(int slot, int index)
    {
        MagebloodSlotLayout slotLayout = _layout.Slots[slot];
        MagebloodSliderSlot spec = slotLayout.Sliders[index];
        MagebloodStatField field = spec.Target.Field;
        MagebloodBuffStat stat = slotLayout.Flask.Stats[spec.RowIndex];

        bool known = TryStatLabel(stat, out string label, out bool addedAsPercent);
        if (!known)
        {
            MarkUnresolved(stat.StatId);
        }

        bool resolved = MagebloodValueScale.IsUnitKnown(field, known);
        int view = _sliders.Count;
        MagebloodSliderRange range = RangeOf(slot, index, addedAsPercent);
        bool percent =
            MagebloodValueScale.Unit(field, addedAsPercent) == MagebloodValueUnit.Percent;
        Slider slider = _page.AddSlider(
            _card,
            "MagebloodStat" + slot + "_" + index,
            string.Empty,
            percent ? "%" : string.Empty,
            range.Min,
            range.Max,
            range.WholeNumbers,
            () => ReadDisplay(view),
            value => WriteDisplay(view, value)
        );
        _page.SetLabel(slider, label);
        _sliders.Add(new MagebloodSliderView(slider, slot, index, addedAsPercent, resolved));
    }

    private static bool TryStatLabel(
        MagebloodBuffStat stat,
        out string label,
        out bool addedAsPercent
    )
    {
        bool known = HeadhunterStatNames.TryRead(
            stat.StatId,
            out string gameName,
            out addedAsPercent
        );
        label = HeadhunterBuffLabel.Format(
            known ? gameName : null,
            HeadhunterStatNames.EnumName(stat.StatId),
            0f,
            0f,
            addedAsPercent,
            1,
            HeadhunterStatNames.GameTagName(stat.Tags),
            stat.Tags == 0 ? null : HeadhunterStatNames.TagEnumName(stat.Tags)
        );
        return known;
    }

    private static void MarkUnresolved(int statId)
    {
        if (_unresolved)
        {
            return;
        }

        _unresolved = true;
        _probeStatId = statId;
        _nextProbe = Time.unscaledTime + ProbeSeconds;
    }

    private static void ResetProbe()
    {
        _unresolved = false;
        _probeStatId = 0;
        _nextProbe = 0f;
    }

    private static bool ProbeUnresolved(float now)
    {
        if (!_unresolved || now < _nextProbe)
        {
            return false;
        }

        _nextProbe = now + ProbeSeconds;
        return HeadhunterStatNames.TryRead(_probeStatId, out _, out _);
    }

    private static MagebloodSliderRange RangeOf(int slot, int index, bool addedAsPercent)
    {
        MagebloodSlotLayout slotLayout = _layout.Slots[slot];
        return slotLayout.Range(slotLayout.Sliders[index], addedAsPercent);
    }

    private static void UpdateRanges()
    {
        foreach (MagebloodSliderView view in _sliders)
        {
            MagebloodSliderRange range = RangeOf(view.Slot, view.Index, view.AddedAsPercent);
            _page.SetSliderRange(view.Control, range.Min, range.Max, range.WholeNumbers);
        }
    }

    private static void ApplyStatus(MagebloodMenuStatus status, Dictionary<string, string> texts)
    {
        for (int slot = 0; slot < _slots.Count; slot++)
        {
            bool inactive = status.IsSlotInactive(slot);
            Dropdown dropdown = _slots[slot];
            if (dropdown.IsNullOrDestroyed())
            {
                continue;
            }

            _page.SetLabel(dropdown, SlotText(texts, slot, inactive));
            _page.SetDimmed(dropdown, inactive);
            dropdown.interactable = status.Editable;
        }

        foreach (MagebloodSliderView view in _sliders)
        {
            _page.SetInteractable(view.Control, status.CanEdit(view.Resolved));
            _page.SetDimmed(view.Control, status.IsSlotInactive(view.Slot));
        }
    }

    private static string SlotText(Dictionary<string, string> texts, int slot, bool inactive)
    {
        string label = MagebloodFlasksTexts.SlotLabel(texts, slot, inactive);
        if (label != null)
        {
            return label;
        }

        return TextTemplate.Fill(
            inactive ? MagebloodFlasksTexts.SlotInactive : MagebloodFlasksTexts.Slot,
            slot + 1
        );
    }

    private static float ReadDisplay(int view)
    {
        MagebloodSliderView item = _sliders[view];
        MagebloodSlotLayout slotLayout = _layout.Slots[item.Slot];
        MagebloodSliderSlot spec = slotLayout.Sliders[item.Index];
        float file = MagebloodMenuWriter.TryPeek(spec.Target, out float pending)
            ? pending
            : slotLayout.FileValue(spec);
        return MagebloodValueScale.ToDisplay(file, spec.Target.Field, item.AddedAsPercent);
    }

    private static void WriteDisplay(int view, float value)
    {
        // Unity calls set_value on a plain handle click: skip writes that change nothing.
        MagebloodSliderView item = _sliders[view];
        MagebloodSliderSlot spec = _layout.Slots[item.Slot].Sliders[item.Index];
        if (
            !MagebloodValueScale.TryEdit(
                value,
                ReadDisplay(view),
                spec.Target.Field,
                item.AddedAsPercent,
                out float file
            )
        )
        {
            return;
        }

        MagebloodMenuWriter.Queue(new MagebloodValueEdit(spec.Target, file), Time.unscaledTime);
    }

    private static int ReadSlot(int slot)
    {
        return _layout == null ? 0 : Math.Max(0, _layout.OptionIndex(slot));
    }

    private static void OnSlotPicked(int slot, int index)
    {
        try
        {
            string chosen =
                index >= 0 && index < _layout.Options.Count ? _layout.Options[index] : null;
            bool pick = MagebloodSlotOptions.TryPickSwap(
                _layout.Names,
                slot,
                chosen,
                out string current
            );
            ModSettings.Trace(
                "Mageblood slot pick: slot "
                    + slot
                    + ", index "
                    + index
                    + ", chosen "
                    + chosen
                    + ", current "
                    + current
                    + ", swap "
                    + pick
            );
            if (!pick || !MagebloodMenuWriter.Swap(current, chosen))
            {
                _stale = true;
            }
        }
        catch (Exception ex)
        {
            ErrorLog.Report(ex, "MagebloodFlasksCard.OnSlotPicked");
        }
    }

    private static int FocusedSlot()
    {
        EventSystem events = EventSystem.current;
        if (events.IsNullOrDestroyed() || events.currentSelectedGameObject.IsNullOrDestroyed())
        {
            return -1;
        }

        Transform selected = events.currentSelectedGameObject.transform;
        if (!selected.IsChildOf(_card.Body.transform))
        {
            return -1;
        }

        for (int slot = 0; slot < _slots.Count; slot++)
        {
            if (!_slots[slot].IsNullOrDestroyed() && selected.IsChildOf(_slots[slot].transform))
            {
                return slot;
            }
        }

        return 0;
    }

    private static void Reselect(int slot)
    {
        EventSystem events = EventSystem.current;
        if (slot < 0 || slot >= _slots.Count || events.IsNullOrDestroyed())
        {
            return;
        }

        if (_slots[slot].IsNullOrDestroyed())
        {
            return;
        }

        events.SetSelectedGameObject(_slots[slot].gameObject);
    }
}
