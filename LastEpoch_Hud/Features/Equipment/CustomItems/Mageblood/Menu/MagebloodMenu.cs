using System;
using System.Collections.Generic;
using LastEpoch_Hud.Scripts.Core.CustomItems.Mageblood.Menu;
using LastEpoch_Hud.Scripts.Core.ModUI;
using LastEpoch_Hud.Scripts.ModUI;
using LastEpoch_Hud.Scripts.ModUI.Settings;
using LastEpoch_Hud.Scripts.ModUI.Shell;
using UnityEngine;
using UnityEngine.UI;

namespace LastEpoch_Hud.Scripts.Mods.Items.CustomItems.Mageblood.Menu;

/// <summary>The Mageblood card of the Custom Items page.</summary>
internal static class MagebloodMenu
{
    private const double ConfirmWindowSeconds = 3.0;
    private const float StatusRowHeight = 64f;
    private const float NoteRowHeight = 48f;

    private static readonly ConfirmPress _confirm = new(ConfirmWindowSeconds);
    private static readonly MagebloodValueTarget _maxResistances =
        MagebloodValueTarget.ForMaxResistances();

    private static HudFormPage _page;
    private static Text _statusText;
    private static Text _warningText;
    private static Text _restoreText;
    private static Slider _slider;

    private static bool _backupSaved;
    private static bool _stale = true;
    private static MagebloodMenuStatus _shownStatus;
    private static int _shownVersion;
    private static bool _shownArmed;
    private static bool _shownBackup;
    private static object _shownTexts;

    public static void Build(HudFormPage page)
    {
        _page = page;
        _confirm.Reset();
        _backupSaved = false;
        _stale = true;

        HudFormPage.Card card = page.AddCard("Mageblood", MagebloodMenuTexts.CardTitle);
        _statusText = page.AddText(card, "MagebloodStatus", string.Empty, StatusRowHeight);
        _warningText = page.AddText(card, "MagebloodWarning", string.Empty, StatusRowHeight);
        page.SetVisibleWhen(_warningText, () => _shownStatus.Warning != MagebloodFileWarning.None);

        MagebloodSliderRange range = MagebloodSliderRanges.ForMaxResistances(
            MagebloodConfigLoader.Current.MaxResistances
        );
        _slider = page.AddSlider(
            card,
            "MagebloodMaxResistances",
            MagebloodMenuTexts.MaxResistances,
            "%",
            range.Min,
            range.Max,
            range.WholeNumbers,
            ReadMaxResistances,
            WriteMaxResistances
        );

        Text tooltipNote = page.AddText(
            card,
            "MagebloodTooltipRestart",
            string.Empty,
            NoteRowHeight
        );
        LocaleRegistry.Apply(tooltipNote, MagebloodMenuTexts.TooltipRestart);

        Button restore = page.AddButton(
            card,
            "MagebloodRestore",
            MagebloodMenuTexts.RestoreDefaults,
            OnRestoreClicked
        );
        _restoreText = restore.GetComponentInChildren<Text>(true);

        Text backupNote = page.AddText(card, "MagebloodBackupSaved", string.Empty, NoteRowHeight);
        LocaleRegistry.Apply(backupNote, MagebloodMenuTexts.BackupSaved);
        page.SetVisibleWhen(backupNote, () => _backupSaved);
    }

    public static void OnShow()
    {
        _confirm.Reset();
        _backupSaved = false;
        _stale = true;
    }

    /// <summary>True when the card changed and the page must refresh its values.</summary>
    public static bool Refresh(double now)
    {
        var status = MagebloodMenuStatus.From(
            MagebloodConfigLoader.IsReadable,
            MagebloodConfigLoader.ProblemCount,
            MagebloodBuffs.Worn,
            MagebloodBuffs.ActiveFlasks
        );
        bool armed = _confirm.IsArmed(now);
        Dictionary<string, string> texts = Locales.current_dictionary;
        if (!IsChanged(status, armed, texts))
        {
            return false;
        }

        _shownStatus = status;
        _shownVersion = MagebloodConfigLoader.Version;
        _shownArmed = armed;
        _shownBackup = _backupSaved;
        _shownTexts = texts;
        _stale = false;
        Apply(status, armed);
        return true;
    }

    private static bool IsChanged(MagebloodMenuStatus status, bool armed, object texts)
    {
        return _stale
            || status != _shownStatus
            || MagebloodConfigLoader.Version != _shownVersion
            || armed != _shownArmed
            || _backupSaved != _shownBackup
            || !ReferenceEquals(texts, _shownTexts);
    }

    private static void Apply(MagebloodMenuStatus status, bool armed)
    {
        Dictionary<string, string> texts = Locales.current_dictionary;
        _statusText.text = MagebloodMenuTexts.Status(texts, status) ?? string.Empty;
        _warningText.text = MagebloodMenuTexts.Warning(texts, status.Warning) ?? string.Empty;
        LocaleRegistry.Apply(_restoreText, MagebloodMenuTexts.RestoreLabel(armed));

        MagebloodSliderRange range = MagebloodSliderRanges.ForMaxResistances(ReadMaxResistances());
        _page.SetSliderRange(_slider, range.Min, range.Max, range.WholeNumbers);
        _page.SetInteractable(_slider, status.Editable);
    }

    private static float ReadMaxResistances()
    {
        return MagebloodMenuWriter.TryPeek(_maxResistances, out float pending)
            ? pending
            : MagebloodConfigLoader.Current.MaxResistances;
    }

    private static void WriteMaxResistances(float value)
    {
        // Unity calls set_value on a plain handle click: skip writes that change nothing.
        if (Mathf.Approximately(value, ReadMaxResistances()))
        {
            return;
        }

        MagebloodMenuWriter.Queue(
            new MagebloodValueEdit(_maxResistances, value),
            Time.unscaledTime
        );
    }

    private static void OnRestoreClicked()
    {
        try
        {
            if (!_confirm.Press(Time.unscaledTime))
            {
                return;
            }

            _backupSaved = MagebloodMenuWriter.Restore();
        }
        catch (Exception ex)
        {
            ErrorLog.Report(ex, "MagebloodMenu.Restore");
        }
    }
}
