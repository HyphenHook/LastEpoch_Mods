using HarmonyLib;
using Il2CppLE.Services.Models.Items;
using Il2CppLE.Services.Visuals;
using LastEpoch_Hud.Scripts.Core.CustomItems;

namespace LastEpoch_Hud.Scripts.Mods.Items.CustomItems;

[HarmonyPatch(typeof(ClientVisualsService), "GetItemVisual")]
public class CustomItemVisualPatch
{
    [HarmonyPrefix]
    private static void Prefix(ref ItemVisualKey __0)
    {
        CustomItemVisualSource source = CustomUniqueLookup.VisualSource(
            (int)__0.EquipmentType,
            __0.SubType,
            __0.UniqueID,
            CustomUniqueItems.Subtypes
        );
        if (source == null)
        {
            return;
        }

        __0.SubType = source.SubType;
        __0.UniqueID = source.UniqueId;
    }
}
