using System;
using HarmonyLib;
using Il2Cpp;
using Il2CppLE.Factions;
using LastEpoch_Hud.Scripts.ModUI;
using CraftType = Il2Cpp.ZoneRestrictedCraftingManager.CraftingType;
using ModSaveManager = LastEpoch_Hud.Scripts.ModUI.Settings.SaveManager;

namespace LastEpoch_Hud.Scripts.Mods.Items.Crafting;

internal static class Items_IdolReroll
{
    [ThreadStatic]
    static int freeCraftDepth;

    [ThreadStatic]
    static IntPtr freeWeaver;

    static bool Ready =>
        Scenes.IsGameScene()
        && !ModSaveManager.instance.IsNullOrDestroyed()
        && ModSaveManager.instance.initialized
        && !Refs_Manager.player_actor.IsNullOrDestroyed();

    static bool Local(ItemContainersManager manager) =>
        Ready
        && !manager.IsNullOrDestroyed()
        && !ItemContainersManager.Instance.IsNullOrDestroyed()
        && manager.Pointer == ItemContainersManager.Instance.Pointer;

    static bool LocalActor(Actor actor) =>
        Ready && (actor.IsNullOrDestroyed() || actor.Pointer == Refs_Manager.player_actor.Pointer);

    static bool Reroll(IdolEnchantmentCraftType type) =>
        type == IdolEnchantmentCraftType.RerollClassSpecificIdol
        || type == IdolEnchantmentCraftType.RerollWeaverIdol;

    static bool Free => Ready && ModSettings.IdolReroll.FreeMemoryAmber.Value;
    static bool Unlimited => Ready && ModSettings.IdolReroll.UnlimitedUses.Value;

    [HarmonyPatch(typeof(ItemContainersManager), "GetIdolEnchantmentCost")]
    static class Cost
    {
        [HarmonyPrefix]
        static bool Prefix(
            ItemContainersManager __instance,
            IdolEnchantmentCraftType __0,
            ref int __result
        )
        {
            if (!Free || !Local(__instance) || !Reroll(__0))
                return true;
            __result = 0;
            return false;
        }
    }

    [HarmonyPatch(typeof(ItemContainersManager), "CanAffordIdolEnchantment")]
    static class Afford
    {
        [HarmonyPrefix]
        static bool Prefix(
            ItemContainersManager __instance,
            IdolEnchantmentCraftType __0,
            ref bool __result
        )
        {
            if (!Free || !Local(__instance) || !Reroll(__0))
                return true;
            __result = true;
            return false;
        }
    }

    // The native cost getter can be inlined. Cover the actual debit as well,
    // only inside a local reroll, and only for that manager's Weaver faction.
    internal sealed class CraftState
    {
        internal IntPtr PreviousWeaver;
        internal bool Free;
        internal LimitState Limit;
    }

    static void BeginCraft(
        ItemContainersManager manager,
        IdolEnchantmentCraftType type,
        out CraftState state
    )
    {
        state = null;
        if (!Local(manager) || !Reroll(type))
            return;
        state = new CraftState { PreviousWeaver = freeWeaver, Limit = BeginLimit() };
        if (!Free)
            return;
        var weaver = manager.GetWeaverFaction();
        if (weaver.IsNullOrDestroyed())
            return;
        state.Free = true;
        freeWeaver = weaver.Pointer;
        freeCraftDepth++;
    }

    static void EndCraft(CraftState state)
    {
        if (state == null)
            return;
        try
        {
            EndLimit(state.Limit);
        }
        finally
        {
            if (state.Free)
            {
                freeCraftDepth--;
                freeWeaver = state.PreviousWeaver;
            }
        }
    }

    [HarmonyPatch(typeof(ItemContainersManager), "CraftIdolEnchantment")]
    static class Craft
    {
        [HarmonyPrefix]
        static void Prefix(
            ItemContainersManager __instance,
            IdolEnchantmentCraftType __0,
            out CraftState __state
        ) => BeginCraft(__instance, __0, out __state);

        [HarmonyFinalizer]
        static void Finalizer(CraftState __state) => EndCraft(__state);
    }

    [HarmonyPatch(typeof(ItemContainersManager), "CanCraftIdolEnchantment")]
    static class CanCraft
    {
        [HarmonyPrefix]
        static void Prefix(
            ItemContainersManager __instance,
            IdolEnchantmentCraftType __0,
            out CraftState __state
        ) => BeginCraft(__instance, __0, out __state);

        [HarmonyFinalizer]
        static void Finalizer(CraftState __state) => EndCraft(__state);
    }

    [HarmonyPatch(typeof(Faction), "TrySpendFavor")]
    static class Debit
    {
        [HarmonyPrefix]
        static void Prefix(Faction __instance, ref int __0)
        {
            if (
                freeCraftDepth > 0
                && !__instance.IsNullOrDestroyed()
                && __instance.Pointer == freeWeaver
            )
                __0 = 0;
        }
    }

    [HarmonyPatch(typeof(Faction), "CanAffordFavorCost")]
    static class DebitCheck
    {
        [HarmonyPrefix]
        static void Prefix(Faction __instance, ref int __0)
        {
            if (
                freeCraftDepth > 0
                && !__instance.IsNullOrDestroyed()
                && __instance.Pointer == freeWeaver
            )
                __0 = 0;
        }
    }

    internal sealed class LimitState
    {
        internal ZoneRestrictedCraftingManager Manager;
        internal bool Limited;
        internal int TimesUsed;
    }

    static LimitState BeginLimit()
    {
        if (!Unlimited)
            return null;
        var all = ZoneRestrictedCraftingManager.all;
        if (
            all.IsNullOrDestroyed()
            || !all.TryGetValue(CraftType.IdolEnchantment, out var manager)
            || manager.IsNullOrDestroyed()
        )
            return null;
        var state = new LimitState
        {
            Manager = manager,
            Limited = manager.limitedUses,
            TimesUsed = manager.localTimesUsed,
        };
        manager.limitedUses = false;
        return state;
    }

    static void EndLimit(LimitState state)
    {
        if (state != null && !state.Manager.IsNullOrDestroyed())
        {
            // Preserve the count even if native OnUse was inlined.
            state.Manager.localTimesUsed = state.TimesUsed;
            state.Manager.limitedUses = state.Limited;
        }
    }

    [HarmonyPatch(
        typeof(ZoneRestrictedCraftingManager),
        "CanCraft",
        new Type[] { typeof(CraftType), typeof(Actor) }
    )]
    static class Eligibility
    {
        [HarmonyPrefix]
        static void Prefix(CraftType __0, Actor __1, out LimitState __state)
        {
            __state =
                Unlimited && __0 == CraftType.IdolEnchantment && LocalActor(__1)
                    ? BeginLimit()
                    : null;
        }

        [HarmonyFinalizer]
        static void Finalizer(LimitState __state)
        {
            EndLimit(__state);
        }
    }

    [HarmonyPatch(typeof(ZoneRestrictedCraftingManager), "UsesRemaining")]
    static class Remaining
    {
        [HarmonyPrefix]
        static bool Prefix(ZoneRestrictedCraftingManager __instance, Actor __0, ref int __result)
        {
            if (
                !Unlimited
                || !LocalActor(__0)
                || __instance.craftingType != CraftType.IdolEnchantment
            )
                return true;
            __result = int.MaxValue;
            return false;
        }
    }

    [HarmonyPatch(
        typeof(ZoneRestrictedCraftingManager),
        "OnUse",
        new Type[] { typeof(Actor), typeof(CraftType) }
    )]
    static class SpendUseStatic
    {
        [HarmonyPrefix]
        static bool Prefix(Actor __0, CraftType __1) =>
            !(Unlimited && LocalActor(__0) && __1 == CraftType.IdolEnchantment);
    }

    [HarmonyPatch(typeof(ZoneRestrictedCraftingManager), "OnUse", new Type[] { typeof(Actor) })]
    static class SpendUseInstance
    {
        [HarmonyPrefix]
        static bool Prefix(ZoneRestrictedCraftingManager __instance, Actor __0) =>
            !(Unlimited && LocalActor(__0) && __instance.craftingType == CraftType.IdolEnchantment);
    }

    [HarmonyPatch(typeof(IdolEnchantmentCraftingUI), "UpdateMemoryAmberCost")]
    static class CostDisplay
    {
        [HarmonyPostfix]
        static void Postfix(IdolEnchantmentCraftingUI __instance)
        {
            if (
                Free
                && Reroll(__instance._craftType)
                && !__instance._memoryAmberCost.IsNullOrDestroyed()
            )
                __instance._memoryAmberCost.text = "0";
        }
    }

    [HarmonyPatch(typeof(CraftingUsesRemainingUI), "SetUsesRemaining")]
    static class UsesDisplay
    {
        [HarmonyPostfix]
        static void Postfix(CraftingUsesRemainingUI __instance)
        {
            if (
                !Unlimited
                || __instance._craftingType != CraftType.IdolEnchantment
                || __instance._text.IsNullOrDestroyed()
            )
                return;
            __instance._text.text = Locales.TryGetTranslation("Unlimited", out string value)
                ? value
                : "Unlimited";
            __instance._text.color = __instance._hasUsesRemainingColor;
        }
    }

    internal static void RefreshUI()
    {
        if (!Ready)
            return;
        foreach (var ui in UnityEngine.Object.FindObjectsOfType<IdolEnchantmentCraftingUI>())
        {
            if (ui.IsNullOrDestroyed() || !ui.isActiveAndEnabled)
                continue;
            ui.ItemToCraftUpdated();
            if (!ui._craftingUsesRemainingUI.IsNullOrDestroyed())
                ui._craftingUsesRemainingUI.UpdateUsesRemaining();
        }
    }
}
