using System;
using HarmonyLib;
using Il2Cpp;
using Il2CppItemFiltering;
using LastEpoch_Hud.Scripts.Core.Items.AutoLoot;
using LastEpoch_Hud.Scripts.ModUI;
using UnityEngine;

namespace LastEpoch_Hud.Scripts.Mods.Items.AutoLoot;

/// <summary>Auto-pickup, auto-sell and auto-shatter of dropped items, driven by the game's loot filter; materials go to storage via the game's click pickup.</summary>
[HarmonyPatch(typeof(GroundItemManager), "dropItemForPlayer")]
public class AutoLootDropPatch
{
    [HarmonyPrefix]
    private static bool Prefix(
        Actor __0,
        ItemData __1,
        ref Vector3 __2,
        ref bool __3,
        out bool __state
    )
    {
        __state = false;
        AutoLootSettings settings = AutoLootRules.ForOrigin(
            ReadSettings(),
            ModItemDrop.CurrentOrigin
        );
        if (!settings.AnyEnabled)
        {
            return true;
        }

        try
        {
            if (!IsLootable(__0, __1))
            {
                return true;
            }

            LootKind kind = LootKindReader.Read(__1);
            LootRoute route = AutoLootRules.Route(
                kind,
                __1.MatchesStackableItemFlags(StackableItemFlags.All),
                settings
            );
            if (route == LootRoute.Filter)
            {
                return RunFilter(__0, __1, kind, settings);
            }

            if (route != LootRoute.Store)
            {
                return RunDirect(__0, __1, kind, route);
            }

            __2 = __0.position();
            __3 = false;
            __state = true;
            return true;
        }
        catch (Exception ex)
        {
            ErrorLog.Report(ex, "AutoLoot drop");
            return true;
        }
    }

    [HarmonyPostfix]
    private static void Postfix(GroundItemManager __instance, Actor __0, ItemData __1, bool __state)
    {
        if (!__state)
        {
            return;
        }

        try
        {
            bool stored = TryPickUpNewGroundItem(__instance, __0, __1);
            LogStored(__1, stored);
        }
        catch (Exception ex)
        {
            ErrorLog.Report(ex, "AutoLoot store");
        }
    }

    private static AutoLootSettings ReadSettings()
    {
        if (Save_Manager.instance.IsNullOrDestroyed())
        {
            return default;
        }

        Save_Manager.Data.Pickup pickup = Save_Manager.instance.data.Items.Pickup;
        return new AutoLootSettings(
            pickup.Enable_AutoPickup_Keys,
            pickup.Enable_AutoPickup_WovenEchoes,
            pickup.Enable_AutoPickup_Materials,
            pickup.Enable_AutoPickup_FromFilter,
            pickup.Enable_AutoSell_FromFilter,
            pickup.Enable_AutoShatter_FromFilter
        );
    }

    private static bool IsLootable(Actor actor, ItemData item)
    {
        if (actor.IsNullOrDestroyed() || item.IsNullOrDestroyed())
        {
            return false;
        }

        return Scenes.IsGameScene() && !item.isTutorialItem() && !Drop.OrbDrop.IsOrb(item);
    }

    private static bool RunDirect(Actor actor, ItemData item, LootKind kind, LootRoute route)
    {
        AutoLootAction action =
            route == LootRoute.PickUp ? PickUp(actor, item) : AutoLootAction.Kept;
        LogDirect(item, kind, action);
        return action == AutoLootAction.Kept;
    }

    private static AutoLootAction PickUp(Actor actor, ItemData item)
    {
        return TryPickUp(actor, item) ? AutoLootAction.Picked : AutoLootAction.Kept;
    }

    private static bool TryPickUpNewGroundItem(GroundItemManager ground, Actor actor, ItemData item)
    {
        if (!TryFindNewGroundItemId(ground, actor, item, out uint id))
        {
            return false;
        }

        ground.pickupItem(actor, id, StackableItemFlags.All);
        return true;
    }

    private static bool TryFindNewGroundItemId(
        GroundItemManager ground,
        Actor actor,
        ItemData item,
        out uint id
    )
    {
        id = 0;
        if (ground.IsNullOrDestroyed() || actor.IsNullOrDestroyed() || item.IsNullOrDestroyed())
        {
            return false;
        }

        if (!ground.TryGetGroundItemList(actor, out GroundItemManager.GroundItemList list))
        {
            return false;
        }

        if (list.IsNullOrDestroyed() || list.items.IsNullOrDestroyed() || list.items.Count == 0)
        {
            return false;
        }

        GroundItemManager.GroundItem last = list.items[list.items.Count - 1];
        if (last.IsNullOrDestroyed() || last.itemData.IsNullOrDestroyed())
        {
            return false;
        }

        if (last.itemData.Pointer != item.Pointer)
        {
            return false;
        }

        id = last.id;
        return true;
    }

    private static bool RunFilter(
        Actor actor,
        ItemData item,
        LootKind kind,
        AutoLootSettings settings
    )
    {
        ItemDataUnpacked unpacked = item.getAsUnpacked();
        LootVisibility visibility = LootFilterReader.Read(
            LootFilterReader.ActiveFilter(),
            unpacked
        );
        FilterLootPlan plan = AutoLootRules.ForFilter(visibility, settings);
        AutoLootAction action = ApplyPlan(actor, item, unpacked, plan);
        LogFilter(item, kind, visibility, action);
        return action == AutoLootAction.Kept;
    }

    private static AutoLootAction ApplyPlan(
        Actor actor,
        ItemData item,
        ItemDataUnpacked unpacked,
        FilterLootPlan plan
    )
    {
        if (plan.IsEmpty)
        {
            return AutoLootAction.Kept;
        }

        if (plan.PickUp)
        {
            return PickUp(actor, item);
        }

        if (plan.Shatter && AutoShatter.TryShatter(unpacked))
        {
            return AutoLootAction.Shattered;
        }

        return plan.Sell && TrySell(actor, unpacked) ? AutoLootAction.Sold : AutoLootAction.Kept;
    }

    private static void LogDirect(ItemData item, LootKind kind, AutoLootAction action)
    {
        if (!ModSettings.Debug.Enabled.Value)
        {
            return;
        }

        try
        {
            Main.logger_instance?.Msg(
                AutoLootLog.Direct(item.itemType, kind, ModItemDrop.CurrentOrigin, action)
            );
        }
        catch (Exception ex)
        {
            ErrorLog.Report(ex, "AutoLoot log");
        }
    }

    private static void LogFilter(
        ItemData item,
        LootKind kind,
        LootVisibility visibility,
        AutoLootAction action
    )
    {
        if (!ModSettings.Debug.Enabled.Value)
        {
            return;
        }

        try
        {
            Main.logger_instance?.Msg(
                AutoLootLog.Filter(
                    item.itemType,
                    kind,
                    ModItemDrop.CurrentOrigin,
                    visibility,
                    action
                )
            );
        }
        catch (Exception ex)
        {
            ErrorLog.Report(ex, "AutoLoot log");
        }
    }

    private static void LogStored(ItemData item, bool stored)
    {
        if (!ModSettings.Debug.Enabled.Value)
        {
            return;
        }

        try
        {
            AutoLootAction action = stored ? AutoLootAction.Stored : AutoLootAction.Kept;
            Main.logger_instance?.Msg(
                AutoLootLog.Direct(
                    item.itemType,
                    LootKindReader.Read(item),
                    ModItemDrop.CurrentOrigin,
                    action
                )
            );
        }
        catch (Exception ex)
        {
            ErrorLog.Report(ex, "AutoLoot log");
        }
    }

    private static bool TryPickUp(Actor actor, ItemData item)
    {
        ItemContainersManager manager = ItemContainersManager.Instance;
        if (manager.IsNullOrDestroyed())
        {
            return false;
        }

        return manager.attemptToPickupItem(item, actor.position());
    }

    private static bool TrySell(Actor actor, ItemDataUnpacked item)
    {
        if (actor.goldTracker.IsNullOrDestroyed())
        {
            return false;
        }

        actor.goldTracker.modifyGold(item.VendorSaleValue);
        return true;
    }
}
