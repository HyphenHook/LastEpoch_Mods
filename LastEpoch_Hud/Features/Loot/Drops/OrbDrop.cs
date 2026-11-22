using System;
using HarmonyLib;
using Il2Cpp;
using Il2CppTMPro;
using LastEpoch_Hud.Scripts.Mods.Items.AutoLoot;
using LastEpoch_Hud.Scripts.Mods.Items.Crafting;
using UnityEngine;

using LastEpoch_Hud.Scripts.Core.Tuning;
namespace LastEpoch_Hud.Scripts.Mods.Items.Drop;

/// <summary>
/// Advanced crafting orbs use marked Harbinger's Eye carriers. All ground pickups go to the
/// Advance Materials tab stacks, including orbs withdrawn and dropped again.
/// </summary>
internal static class OrbDrop
{
    /// <summary>No real item has this individual id.</summary>
    public const uint Marker = 0x0EC0C0DE;
    private const uint ReminiscenceMarker = 0x0EC0C0DF;
    private const uint EchoMarker = 0x0EC0C0E0;
    private const uint ReverieMarker = 0x0EC0C0E1;
    private const uint ChaosMarker = 0x0EC0C0E2;
    private const uint AttunementMarker = 0x0EC0C0E3;

    private const uint PhysicalMarkerOffset = 16;
    private static (byte Type, ushort Subtype)? eyeType;
    private static readonly System.Random Rng = new();

    private static double ReminiscenceDropChance => LastEpoch_Hud.Scripts.Core.Tuning.Tunables.Get("OrbDrop.ReminiscenceChance", 0.5);

    internal static bool IsPhysical(ItemData item) => !item.IsNullOrDestroyed() && Item.isKey(item.itemType)
        && item.individualID >= Marker + PhysicalMarkerOffset && item.individualID <= AttunementMarker + PhysicalMarkerOffset;

    public static string KeyOf(ItemData item) => item.IsNullOrDestroyed()
        || (!Item.isKey(item.itemType) && item.itemType != Item.runeItemType) ? null
        : (IsPhysical(item) ? item.individualID - PhysicalMarkerOffset : item.individualID) switch
    {
        Marker => AdvancedMaterials.EncapsulationOrb,
        ReminiscenceMarker => AdvancedMaterials.ReminiscenceOrb,
        EchoMarker => AdvancedMaterials.EchoOrb,
        ReverieMarker => AdvancedMaterials.ReverieOrb,
        ChaosMarker => AdvancedMaterials.ChaosOrb,
        AttunementMarker => AdvancedMaterials.AttunementOrb,
        _ => null,
    };

    public static bool IsOrb(ItemData item) => KeyOf(item) != null;

    private static readonly System.Collections.Generic.Dictionary<string, ItemData> tooltipCarriers = new();

    internal static ItemData TooltipCarrier(string key)
    {
        if (!tooltipCarriers.TryGetValue(key, out ItemData item) || item.IsNullOrDestroyed())
        {
            item = Carrier(key);
            tooltipCarriers[key] = item;
        }
        return item;
    }

    private static (byte Type, ushort Subtype)? HarbingerEyeType()
    {
        if (eyeType.HasValue) return eyeType;
        ItemContainersManager manager = ItemContainersManager.Instance;
        var slot = manager.IsNullOrDestroyed() ? null : manager.harbingerPinnacleKey;
        if (!slot.IsNullOrDestroyed() && slot.subTypes != null)
        {
            foreach (var family in slot.subTypes)
            {
                if (!Item.isKey(family.Key) || family.Value == null || family.Value.Count != 1) continue;
                foreach (int subtype in family.Value)
                {
                    eyeType = ((byte)family.Key, (ushort)subtype);
                    return eyeType;
                }
            }
        }

        ItemList catalog = ItemList.get();
        if (catalog.IsNullOrDestroyed() || catalog.nonEquippableItems == null) return null;
        foreach (var family in catalog.nonEquippableItems)
        {
            if (family == null || !Item.isKey(family.baseTypeID) || family.subItems == null) continue;
            foreach (var subtype in family.subItems)
            {
                if (subtype == null || !(IsHarbingerEyeName(subtype.name) || IsHarbingerEyeName(subtype.displayName))) continue;
                eyeType = ((byte)family.baseTypeID, (ushort)subtype.subTypeID);
                return eyeType;
            }
        }
        return null;
    }

    private static bool IsHarbingerEyeName(string name) => !string.IsNullOrEmpty(name)
        && name.Contains("Harbinger", StringComparison.OrdinalIgnoreCase)
        && (name.Contains("Eye", StringComparison.OrdinalIgnoreCase) || name.Contains("Key", StringComparison.OrdinalIgnoreCase));

    internal static ItemData Carrier(string key, bool physical = false)
    {
        var eye = HarbingerEyeType();
        if (!eye.HasValue) return null;
        var item = new ItemDataUnpacked
        {
            LvlReq = 0,
            classReq = ItemList.ClassRequirement.Any,
            itemType = eye.Value.Type,
            subType = eye.Value.Subtype,
            rarity = 0,
            sockets = 0,
            uniqueID = 0,
        };
        uint marker = key switch
        {
            AdvancedMaterials.EncapsulationOrb => Marker,
            AdvancedMaterials.ReminiscenceOrb => ReminiscenceMarker,
            AdvancedMaterials.EchoOrb => EchoMarker,
            AdvancedMaterials.ReverieOrb => ReverieMarker,
            AdvancedMaterials.ChaosOrb => ChaosMarker,
            AdvancedMaterials.AttunementOrb => AttunementMarker,
            _ => 0u,
        };
        if (marker == 0) return null;
        item.individualID = marker + (physical ? PhysicalMarkerOffset : 0u);
        item.RefreshIDAndValues();
        return KeyOf(item) == key ? item : null;
    }

    internal static Color LootColor(string key) => key switch
    {
        AdvancedMaterials.EncapsulationOrb => new Color(1f, 0.73f, 0.29f),
        AdvancedMaterials.ReminiscenceOrb => new Color(0.85f, 0.54f, 1f),
        AdvancedMaterials.EchoOrb => new Color(0.31f, 1f, 0.84f),
        AdvancedMaterials.ReverieOrb => new Color(0.63f, 0.81f, 1f),
        AdvancedMaterials.ChaosOrb => new Color(1f, 0.36f, 0.27f),
        AdvancedMaterials.AttunementOrb => new Color(0.88f, 0.62f, 1f),
        _ => Color.white,
    };

    /// <summary>Drops an orb on the ground at the position, for the local player.</summary>
    public static bool Drop(Vector3 position, string key = AdvancedMaterials.EncapsulationOrb)
    {
        try
        {
            Actor player = Refs_Manager.player_actor;
            GroundItemManager ground = Refs_Manager.ground_item_manager;
            if (player.IsNullOrDestroyed() || ground.IsNullOrDestroyed() || Refs_Manager.item_list.IsNullOrDestroyed())
            {
                return false;
            }
            ItemData carrier = Carrier(key);
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
                if (Rng.NextDouble() < ReminiscenceDropChance)
                {
                    Drop(enemy.position(), AdvancedMaterials.ReminiscenceOrb);
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
            bool isOrb = false;
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
                    isOrb = true;
                    string key = KeyOf(found.itemData);
                    if (!AdvancedMaterials.TryCreditStack(key, 1)) return false;
                    __0.items.RemoveAt(i);
                    GroundItemVisualsManager visuals = UnityEngine.Object.FindObjectOfType<GroundItemVisualsManager>();
                    visuals?.poolGroundItemVisuals(__2, true);
                    AdvancedMaterial material = AdvancedMaterials.All.Find(m => m.Key == key);
                    if (material != null) Notifications.KeyOrWovenEchoAddedNotification(material.Name, 1);
                    Main.logger_instance?.Msg((material?.Name ?? key) + " picked up (" + AdvancedMaterials.Count(key) + ").");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Main.logger_instance?.Warning("Orb pickup failed: " + ex.Message);
            }
            return !isOrb;
        }
    }

    // ------------------------------------------------------------------ its name on the ground

    [HarmonyPatch(typeof(GroundItemVisuals), nameof(GroundItemVisuals.initialise))]
    private static class BeamPatch
    {
        [HarmonyPostfix]
        private static void Postfix(ItemDataUnpacked __0, GroundItemRarityVisualsV2 __3)
        {
            string key = KeyOf(__0);
            if (key == null || __3.IsNullOrDestroyed()) return;
            try
            {
                Color color = LootColor(key);
                __3.ConfigureBaseVisualPrefab(EpochColor.BeamSize.LARGE, color, DMMapWorldIcon.iconType.legendaryOrKeyIcon, color);
            }
            catch (Exception ex)
            {
                Main.logger_instance?.Warning("Orb drop beam: " + ex.Message);
            }
        }
    }

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
                    OrbItemVisuals.TrackGroundLabel(__instance, null);
                    return;
                }
                foreach (TextMeshProUGUI text in __instance.GetComponentsInChildren<TextMeshProUGUI>(true))
                {
                    if (text != null && !string.IsNullOrEmpty(text.text))
                    {
                        AdvancedMaterial orb = AdvancedMaterials.All.Find(m => m.Key == KeyOf(data));
                        text.text = "<color=#" + ColorUtility.ToHtmlStringRGB(LootColor(orb.Key)) + ">" + orb.Name + "</color>";
                        OrbItemVisuals.TrackGroundLabel(__instance, text.rectTransform);
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
