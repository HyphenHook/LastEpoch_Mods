using Il2Cpp;
using LastEpoch_Hud.Scripts.Core.Items;
using MelonLoader;
using UnityEngine;

namespace LastEpoch_Hud.Scripts.Mods.Items.Drop;

[RegisterTypeInIl2Cpp]
public class Items_SocketsNb : MonoBehaviour
{
    public static Items_SocketsNb instance { get; private set; }

    public Items_SocketsNb(System.IntPtr ptr)
        : base(ptr) { }

    public int items_nb_sockets = 4;
    public int idols_nb_sockets = 2;

    private int default_items_nb_sockets = 4;
    private int backup_items_nb_sockets = 4;
    private int default_idols_nb_sockets = 2;
    private int backup_idols_nb_sockets = 2;

    void Awake()
    {
        instance = this;
    }

    void Update()
    {
        if (
            (!Refs_Manager.item_list.IsNullOrDestroyed())
            && (!Save_Manager.instance.IsNullOrDestroyed())
        )
        {
            if (Save_Manager.instance.data.Items.Drop.Enable_AffixCount)
            {
                items_nb_sockets = DropAffixCount.ToWhole(
                    Save_Manager.instance.data.Items.Drop.AffixCount_Max
                );
            }
            else
            {
                items_nb_sockets = default_items_nb_sockets;
            }
            if (items_nb_sockets != backup_items_nb_sockets)
            {
                backup_items_nb_sockets = items_nb_sockets;
                foreach (
                    ItemList.BaseEquipmentItem base_item in Refs_Manager.item_list.EquippableItems
                )
                {
                    if (Core.Items.EquipmentTypeRules.IsEquipment(base_item.baseTypeID))
                    {
                        base_item.maximumAffixes = base_item.baseTypeID == EquipmentTypeRules.JewelBaseType
                            ? EquipmentTypeRules.JewelTransferAffixes : items_nb_sockets;
                    }
                }
                if (items_nb_sockets == default_items_nb_sockets)
                {
                    Main.logger_instance?.Msg(
                        "Items max sockets reset to default value ("
                            + default_items_nb_sockets
                            + ")"
                    );
                }
                else
                {
                    Main.logger_instance?.Msg("Items max sockets set to " + items_nb_sockets);
                }
            }

            if (Save_Manager.instance.data.Items.Drop.Enable_IdolAffixCount)
            {
                idols_nb_sockets = (int)Save_Manager.instance.data.Items.Drop.IdolAffixCount_Max;
            }
            else
            {
                idols_nb_sockets = default_idols_nb_sockets;
            }
            if (idols_nb_sockets != backup_idols_nb_sockets)
            {
                backup_idols_nb_sockets = idols_nb_sockets;
                foreach (
                    ItemList.BaseEquipmentItem base_item in Refs_Manager.item_list.EquippableItems
                )
                {
                    if ((base_item.baseTypeID > 24) && (base_item.baseTypeID < 34))
                    {
                        base_item.maximumAffixes = idols_nb_sockets;
                    }
                }
                if (idols_nb_sockets == default_idols_nb_sockets)
                {
                    Main.logger_instance?.Msg(
                        "Idols max sockets reset to default value ("
                            + default_idols_nb_sockets
                            + ")"
                    );
                }
                else
                {
                    Main.logger_instance?.Msg("Idols max sockets set to " + idols_nb_sockets);
                }
            }
        }
    }
}
