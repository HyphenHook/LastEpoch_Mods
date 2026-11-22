using UnityEngine;
using UnityEngine.UI;

namespace LastEpoch_Hud.Scripts.Mods.Items.CustomItems.Mageblood.Bar;

/// <summary>One framed flask icon of the Mageblood bar: no timer, no stack badge.</summary>
internal sealed class MagebloodBarSlot
{
    private readonly GameObject _root;
    private readonly Image _icon;

    public MagebloodBarSlot(GameObject entry)
    {
        _root = entry;
        BuffSlotStyle.FitEntry(entry.GetComponent<RectTransform>());
        GameObject iconPanel = Functions.GetChild(entry, "Panel_Icon");
        _icon = Functions.GetChild(iconPanel, "Icon").GetComponent<Image>();
        _icon.preserveAspect = true;
        BuffSlotStyle.ApplyFrame(Functions.GetChild(iconPanel, "Background"), _icon);
        Functions.GetChild(iconPanel, "Timer").SetActive(false);
        GameObject timerText = Functions.GetChild(iconPanel, "Timer_Text");
        TextFont = timerText.GetComponent<Text>().font;
        timerText.SetActive(false);
        Functions.GetChild(entry, "Panel_Stack").SetActive(false);
    }

    public Font TextFont { get; }

    public void Show(Sprite sprite)
    {
        _root.SetActive(true);
        _icon.overrideSprite = null;
        _icon.sprite = sprite;
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
}
