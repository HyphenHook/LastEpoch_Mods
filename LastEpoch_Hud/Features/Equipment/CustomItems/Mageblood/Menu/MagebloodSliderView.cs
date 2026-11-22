using UnityEngine.UI;

namespace LastEpoch_Hud.Scripts.Mods.Items.CustomItems.Mageblood.Menu;

/// <summary>A built stat slider, where it reads from, and whether its unit is known.</summary>
internal readonly record struct MagebloodSliderView(
    Slider Control,
    int Slot,
    int Index,
    bool AddedAsPercent,
    bool Resolved
);
