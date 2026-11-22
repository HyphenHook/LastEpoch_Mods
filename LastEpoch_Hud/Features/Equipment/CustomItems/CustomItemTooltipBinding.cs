using Il2Cpp;
using UnityEngine.UI;

namespace LastEpoch_Hud.Scripts.Mods.Items.CustomItems;

/// <summary>One tooltip image set we keep a custom icon on.</summary>
internal sealed class CustomItemTooltipBinding
{
    public UITooltipItem Owner { get; init; }
    public bool Comparison { get; init; }
    public Image[] Images { get; init; }
    public int IconIndex { get; init; }
}
