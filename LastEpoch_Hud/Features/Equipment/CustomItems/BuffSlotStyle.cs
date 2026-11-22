using LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter.Bar;
using UnityEngine;
using UnityEngine.UI;

namespace LastEpoch_Hud.Scripts.Mods.Items.CustomItems;

/// <summary>Shared look of one buff bar entry: size, dark fill and orange outline.</summary>
internal static class BuffSlotStyle
{
    private static readonly Color _frameColor = new(1f, 0.55f, 0.1f, 1f);
    private static readonly Color _backingColor = new(0.06f, 0.04f, 0.02f, 0.85f);
    private static readonly Vector2 _frameDistance = new(3f, 3f);
    private static readonly Vector2 _bottomCenter = new(0.5f, 0f);

    public static void FitEntry(RectTransform entry)
    {
        entry.anchorMin = _bottomCenter;
        entry.anchorMax = _bottomCenter;
        entry.pivot = _bottomCenter;
        entry.sizeDelta = new Vector2(HeadhunterBarLayout.EntrySize, HeadhunterBarLayout.EntrySize);
    }

    public static void ApplyFrame(GameObject background, Image icon)
    {
        Image image = background.GetComponent<Image>();
        image.sprite = null;
        image.color = _backingColor;
        background.transform.SetAsFirstSibling();
        background.GetComponent<RectTransform>().sizeDelta =
            icon.GetComponent<RectTransform>().sizeDelta;
        Outline outline = background.AddComponent<Outline>();
        outline.effectColor = _frameColor;
        outline.effectDistance = _frameDistance;
    }
}
