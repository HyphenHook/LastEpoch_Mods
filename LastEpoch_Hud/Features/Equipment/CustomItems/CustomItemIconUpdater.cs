using MelonLoader;
using UnityEngine;

namespace LastEpoch_Hud.Scripts.Mods.Items.CustomItems;

/// <summary>Loads the custom item icons and keeps them on the views each frame.</summary>
[RegisterTypeInIl2Cpp]
public class CustomItemIconUpdater : MonoBehaviour
{
    public CustomItemIconUpdater(System.IntPtr ptr)
        : base(ptr) { }

    private void Update()
    {
        CustomItemIcons.LoadOnce();
    }

    private void LateUpdate()
    {
        CustomItemInventoryIcons.Refresh();
        CustomItemTooltipIcons.Refresh();
    }
}
