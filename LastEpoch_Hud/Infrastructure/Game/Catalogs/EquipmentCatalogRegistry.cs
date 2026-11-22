using LastEpoch_Hud.Scripts;
using System;
using System.Linq;
using Il2Cpp;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using LastEpoch_Hud.Core.Equipment;

namespace LastEpoch_Hud.Infrastructure.Game.Catalogs;

/// <summary>Native catalog adapter. Definitions and classification policies stay in their features.</summary>
internal static class EquipmentCatalogRegistry
{
    internal static bool CanAppend(int id) => !Refs_Manager.item_list.IsNullOrDestroyed()
        && Refs_Manager.item_list.EquippableItems != null
        && Refs_Manager.item_list.EquippableItems.Length >= id;

    internal static void Append(int id, ItemList.BaseEquipmentItem category)
    {
        if (category == null) throw new ArgumentNullException(nameof(category));
        var list = Refs_Manager.item_list;
        var original = list.EquippableItems;
        int nativeMaximum = Enum.GetValues(typeof(EquipmentType)).Cast<EquipmentType>().Max(x => (int)x);
        bool occupied = list.nonEquippableItems != null && id >= 0 && id < list.nonEquippableItems.Length
            && list.nonEquippableItems[id] != null;
        CatalogAppendRules.Validate(id, original.Length, nativeMaximum, occupied);
        if (category.baseTypeID != id || (int)category.type != id)
            throw new InvalidOperationException("The equipment definition does not match its reserved base ID.");
        var extended = new Il2CppReferenceArray<ItemList.BaseEquipmentItem>(id + 1);
        for (int i = 0; i < original.Length; i++) extended[i] = original[i];
        extended[id] = category;
        list.EquippableItems = extended;
    }
}
