using UnityEngine;
using UnityEngine.UI;

namespace LastEpoch_Hud.Scripts.Mods.Items.CustomItems.Headhunter.Bar;

/// <summary>Runtime tooltip panel shown above a hovered bar icon.</summary>
internal sealed class HeadhunterBarTooltip
{
    private const float Padding = 8f;
    private const float Gap = 6f;
    private static readonly Color _backingColor = new(0.06f, 0.04f, 0.02f, 0.92f);
    private static readonly Color _frameColor = new(1f, 0.55f, 0.1f, 1f);

    private readonly GameObject _root;
    private readonly RectTransform _rect;
    private readonly Text _text;

    public HeadhunterBarTooltip(Transform parent, Font font)
    {
        _root = new GameObject("HeadhunterBarTooltip");
        _root.transform.SetParent(parent, false);
        _rect = _root.AddComponent<RectTransform>();
        _rect.anchorMin = new Vector2(0.5f, 0.5f);
        _rect.anchorMax = new Vector2(0.5f, 0.5f);
        _rect.pivot = new Vector2(0.5f, 0f);
        Image background = _root.AddComponent<Image>();
        background.color = _backingColor;
        background.raycastTarget = false;
        Outline outline = _root.AddComponent<Outline>();
        outline.effectColor = _frameColor;
        outline.effectDistance = new Vector2(2f, 2f);
        _text = CreateText(font);
        _root.SetActive(false);
    }

    public void Show(string text, float localX, float localY, float scale)
    {
        _text.text = text;
        _rect.sizeDelta = new Vector2(
            _text.preferredWidth + (Padding * 2f),
            _text.preferredHeight + (Padding * 2f)
        );
        _rect.anchoredPosition = new Vector2(localX, localY + (Gap * scale));
        _rect.localScale = new Vector3(scale, scale, 1f);
        _root.SetActive(true);
    }

    public void Hide()
    {
        _root.SetActive(false);
    }

    private Text CreateText(Font font)
    {
        GameObject textObject = new("Text");
        textObject.transform.SetParent(_root.transform, false);
        RectTransform rect = textObject.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(Padding, Padding);
        rect.offsetMax = new Vector2(-Padding, -Padding);
        Text text = textObject.AddComponent<Text>();
        text.font = font;
        text.fontSize = 20;
        text.color = Color.white;
        text.alignment = TextAnchor.MiddleCenter;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        return text;
    }
}
