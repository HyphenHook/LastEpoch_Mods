using LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter.Bar;
using UnityEngine;
using UnityEngine.UI;

namespace LastEpoch_Hud.Scripts.Mods.Items.CustomItems.Headhunter.Bar;

/// <summary>One icon entry of the buff bar: framed icon plus countdown digits.</summary>
internal sealed class HeadhunterBarSlot
{
    private static readonly Color _outlineColor = new(0f, 0f, 0f, 1f);
    private static readonly SecondsTextCache _numberTexts = new();
    private static readonly Vector2 _badgeSize = new(24f, 20f);

    private readonly GameObject _root;
    private readonly Image _icon;
    private readonly Image _timer;
    private readonly Text _text;
    private readonly GameObject _stackPanel;
    private readonly Text _stackText;
    private int _statId = -1;
    private int _tags;
    private bool _iconFinal;
    private int _row = -1;
    private int _stacks;
    private int _seconds = -1;
    private float _elapsed = -1f;

    public HeadhunterBarSlot(GameObject entry)
    {
        _root = entry;
        BuffSlotStyle.FitEntry(entry.GetComponent<RectTransform>());
        GameObject iconPanel = Functions.GetChild(entry, "Panel_Icon");
        _icon = Functions.GetChild(iconPanel, "Icon").GetComponent<Image>();
        _timer = Functions.GetChild(iconPanel, "Timer").GetComponent<Image>();
        _text = Functions.GetChild(iconPanel, "Timer_Text").GetComponent<Text>();
        _stackPanel = Functions.GetChild(entry, "Panel_Stack");
        _stackText = _stackPanel.GetComponentInChildren<Text>(true);
        PlaceStackBadge(_stackPanel, iconPanel);
        BuffSlotStyle.ApplyFrame(Functions.GetChild(iconPanel, "Background"), _icon);
        ApplyTextStyle(_text);
        ApplyTextStyle(_stackText);
        _timer.gameObject.SetActive(true);
    }

    public int Row => _row;

    public int Stacks => _stacks;

    public Font TextFont => _text.font;

    public void Show(HeadhunterBarEntry entry)
    {
        _root.SetActive(true);
        ApplyIcon(entry.StatId, entry.Tags);
        _row = entry.Row;
        ApplyStacks(entry.Stacks);
        if (entry.SecondsLeft != _seconds)
        {
            _seconds = entry.SecondsLeft;
            _text.text = _numberTexts.Get(_seconds);
        }

        if (entry.Elapsed != _elapsed)
        {
            _elapsed = entry.Elapsed;
            _timer.fillAmount = _elapsed;
        }
    }

    public void Hide()
    {
        _root.SetActive(false);
    }

    /// <summary>Moves the slot to a cell (canvas units from the panel bottom-center).</summary>
    public void PlaceAt(float centerX, float bottom)
    {
        _root.GetComponent<RectTransform>().anchoredPosition = new Vector2(centerX, bottom);
    }

    private static void PlaceStackBadge(GameObject stackPanel, GameObject iconPanel)
    {
        stackPanel.transform.SetParent(iconPanel.transform, false);
        RectTransform rect = stackPanel.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.right;
        rect.anchorMax = Vector2.right;
        rect.pivot = Vector2.right;
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = _badgeSize;
        stackPanel.SetActive(false);
    }

    private void ApplyStacks(int stacks)
    {
        if (stacks == _stacks)
        {
            return;
        }

        _stacks = stacks;
        _stackPanel.SetActive(stacks > 1);
        _stackText.text = _numberTexts.Get(stacks);
    }

    private static void ApplyTextStyle(Text text)
    {
        text.color = Color.white;
        Outline outline = text.gameObject.AddComponent<Outline>();
        outline.effectColor = _outlineColor;
    }

    private void ApplyIcon(int statId, int tags)
    {
        bool same = statId == _statId && tags == _tags;
        if (same && _iconFinal && !_icon.sprite.IsNullOrDestroyed())
        {
            return;
        }

        _statId = statId;
        _tags = tags;
        Sprite sprite = HeadhunterBuffIcons.For(statId, tags, out _iconFinal);
        if (sprite.IsNullOrDestroyed())
        {
            _iconFinal = false;
            return;
        }

        if (sprite == _icon.sprite)
        {
            return;
        }

        _icon.overrideSprite = null;
        _icon.sprite = sprite;
    }
}
