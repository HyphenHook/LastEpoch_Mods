using System;
using HarmonyLib;
using Il2Cpp;
using Il2CppTMPro;
using LastEpoch_Hud.Scripts.Core.Crafting;
using LastEpoch_Hud.Scripts.Mods.Items.AutoLoot;
using LastEpoch_Hud.Scripts.Mods.Items.Crafting;
using UnityEngine;

namespace LastEpoch_Hud.Scripts.Mods.Items.Drop;

/// <summary>
/// The Encapsulation Orb as ground loot. The game knows no such item, so the orb drops as an ordinary rune that carries a
/// marker in its individual id. Picking the marked rune up gives an orb to the Advanced Crafting tab instead of a rune,
/// and its label reads "Encapsulation Orb".
/// </summary>
internal static class OrbDrop
{
    /// <summary>No real item has this individual id.</summary>
    public const uint Marker = 0x0EC0C0DE;

    private const int RuneType = 102;
    private const string OrbName = "<color=#8fd8ff>Encapsulation Orb</color>";
    private static readonly System.Random Rng = new();

    // TESTING: every enemy has this chance to drop an orb. Set to null for the real chances.
    private static readonly double? TestChance = 0.5;

    public static bool IsOrb(ItemData item) => !item.IsNullOrDestroyed() && item.individualID == Marker;

    private static ItemData tooltipCarrier;

    /// <summary>An orb-marked item for tooltips, so the game's own tooltip (rewritten for orbs) can show it anywhere.</summary>
    internal static ItemData TooltipCarrier()
    {
        if (tooltipCarrier.IsNullOrDestroyed())
        {
            tooltipCarrier = Carrier();
        }
        return tooltipCarrier;
    }

    private static bool _carrierLogged;

    /// <summary>
    /// The rune the orb stands in for. The ground beam, colour and sound come from it, so it is one of the golden runes:
    /// Ascendance, or failing that Creation.
    /// </summary>
    private static ushort CarrierSubtype(ItemList.BaseNonEquipmentItem runes)
    {
        foreach (string wanted in new[] { "Ascendance", "Creation" })
        {
            for (int i = 0; i < runes.subItems.Count; i++)
            {
                string name = Refs_Manager.item_list.GetItemName(RuneType, runes.subItems[i].subTypeID);
                if (name != null && name.IndexOf(wanted, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    if (!_carrierLogged)
                    {
                        _carrierLogged = true;
                        Main.logger_instance?.Msg("Encapsulation Orb: dropping as " + name);
                    }
                    return (ushort)runes.subItems[i].subTypeID;
                }
            }
        }
        return (ushort)runes.subItems[0].subTypeID;
    }

    private static ItemData Carrier()
    {
        ItemList.BaseNonEquipmentItem runes = null;
        foreach (ItemList.BaseNonEquipmentItem list in Refs_Manager.item_list.nonEquippableItems)
        {
            if (list.baseTypeID == RuneType)
            {
                runes = list;
                break;
            }
        }
        if (runes == null || runes.subItems.Count == 0)
        {
            return null;
        }
        var item = new ItemDataUnpacked
        {
            LvlReq = 0,
            classReq = ItemList.ClassRequirement.Any,
            itemType = RuneType,
            subType = CarrierSubtype(runes),
            rarity = 0,
            sockets = 0,
            uniqueID = 0,
        };
        item.individualID = Marker;
        item.RefreshIDAndValues();
        return item.TryCast<ItemData>();
    }

    /// <summary>Drops an orb on the ground at the position, for the local player.</summary>
    public static bool Drop(Vector3 position)
    {
        try
        {
            Actor player = Refs_Manager.player_actor;
            GroundItemManager ground = Refs_Manager.ground_item_manager;
            if (player.IsNullOrDestroyed() || ground.IsNullOrDestroyed() || Refs_Manager.item_list.IsNullOrDestroyed())
            {
                return false;
            }
            ItemData carrier = Carrier();
            if (carrier.IsNullOrDestroyed())
            {
                return false;
            }
            ModItemDrop.Drop(ground, player, carrier, position);
            return true;
        }
        catch (Exception ex)
        {
            Main.logger_instance?.Warning("Orb drop failed: " + ex.Message);
            return false;
        }
    }

    // ------------------------------------------------------------------ dying enemies

    [HarmonyPatch(typeof(DeathItemDrop), nameof(DeathItemDrop.OnDeath))]
    private static class DeathPatch
    {
        [HarmonyPostfix]
        private static void Postfix(DeathItemDrop __instance)
        {
            try
            {
                if (!Scenes.IsGameScene())
                {
                    return;
                }
                Actor enemy = null;
                if (!__instance.TryGetActor(out enemy) || enemy.IsNullOrDestroyed() || enemy.isPlayerActor || enemy.IsMinionOfPlayer())
                {
                    return;
                }
                double chance = TestChance ?? OrbDropMath.Chance((int)enemy.getRarity(), enemy.isBossOrMiniboss());
                if (Rng.NextDouble() < chance)
                {
                    Drop(enemy.position());
                }
            }
            catch (Exception ex)
            {
                Main.logger_instance?.Warning("Orb drop roll failed: " + ex.Message);
            }
        }
    }

    // ------------------------------------------------------------------ picking it up

    [HarmonyPatch(typeof(GroundItemManager), nameof(GroundItemManager.pickupItem),
        new[] { typeof(GroundItemManager.GroundItemList), typeof(Actor), typeof(uint), typeof(StackableItemFlags) })]
    private static class PickupPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(GroundItemManager.GroundItemList __0, uint __2)
        {
            try
            {
                if (__0.IsNullOrDestroyed() || __0.items.IsNullOrDestroyed())
                {
                    return true;
                }
                for (int i = 0; i < __0.items.Count; i++)
                {
                    GroundItemManager.GroundItem found = __0.items[i];
                    if (found.IsNullOrDestroyed() || found.id != __2 || !IsOrb(found.itemData))
                    {
                        continue;
                    }
                    AdvancedMaterials.Add(AdvancedMaterials.EncapsulationOrb, 1);
                    __0.items.RemoveAt(i);
                    GroundItemVisualsManager visuals = UnityEngine.Object.FindObjectOfType<GroundItemVisualsManager>();
                    visuals?.poolGroundItemVisuals(__2, true);
                    Main.logger_instance?.Msg("Encapsulation Orb picked up (" + AdvancedMaterials.Count(AdvancedMaterials.EncapsulationOrb) + ").");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Main.logger_instance?.Warning("Orb pickup failed: " + ex.Message);
            }
            return true;
        }
    }

    // ------------------------------------------------------------------ its name on the ground

    [HarmonyPatch(typeof(GroundItemLabel), nameof(GroundItemLabel.SetLabelText))]
    private static class LabelPatch
    {
        [HarmonyPostfix]
        private static void Postfix(GroundItemLabel __instance)
        {
            try
            {
                ItemData data = __instance.getItemData()?.TryCast<ItemData>();
                if (!IsOrb(data))
                {
                    return;
                }
                foreach (TextMeshProUGUI text in __instance.GetComponentsInChildren<TextMeshProUGUI>(true))
                {
                    if (text != null && !string.IsNullOrEmpty(text.text))
                    {
                        text.text = OrbName;
                        break;
                    }
                }
            }
            catch (Exception)
            {
            }
        }
    }
}
