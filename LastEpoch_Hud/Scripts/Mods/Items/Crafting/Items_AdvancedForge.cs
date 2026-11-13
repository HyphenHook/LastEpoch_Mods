using System;
using System.Collections.Generic;
using HarmonyLib;
using Il2Cpp;
using LastEpoch_Hud.Scripts.Core.Items;
using LastEpoch_Hud.Scripts.ModUI;
using LastEpoch_Hud.Scripts.ModUI.Settings;
using UnityEngine;
using ModSaveManager = LastEpoch_Hud.Scripts.ModUI.Settings.SaveManager;
using Random = UnityEngine.Random;

namespace LastEpoch_Hud.Scripts.Mods.Items.Crafting;

// Restores the old Craft_MaxTier behavior in a narrower form:
// normal equipment may be upgraded through displayed T7 (internal tier 6).
// T8 remains reserved for primordial mechanics.
internal static class Items_AdvancedForge
{
    static CraftingSlotManager craftingSlotManager;
    static ItemData item;

    [ThreadStatic]
    static ForgeRejectionObservation activeCheck;
    static readonly HashSet<string> traces = new();
    static long tracedItem;

    static void Trace(
        CraftingManager manager,
        bool accepted,
        string title,
        bool flag1,
        bool flag2,
        string detail
    )
    {
        try
        {
            if (!T7Enabled())
                return;
            long pointer = item.IsNullOrDestroyed() ? 0 : item.Pointer.ToInt64();
            if (pointer != tracedItem)
            {
                tracedItem = pointer;
                traces.Clear();
            }
            int tier = GetTier(item, manager.appliedAffixID);
            string keys = activeCheck?.ObservedKeys ?? "none";
            string key =
                accepted
                + ":"
                + title
                + ":"
                + manager.appliedAffixID
                + ":"
                + tier
                + ":"
                + PreserveFp()
                + ":"
                + keys;
            if (traces.Count >= 12 || !traces.Add(key))
                return;
            Main.logger_instance?.Msg(
                "[ForgeTrace] nativeAccepted="
                    + accepted
                    + "; title="
                    + title
                    + "; flags="
                    + flag1
                    + ","
                    + flag2
                    + "; detail="
                    + detail
                    + "; cachedItem="
                    + pointer
                    + "; affix="
                    + manager.appliedAffixID
                    + "; internalTier="
                    + tier
                    + "; FP="
                    + (item.IsNullOrDestroyed() ? "n/a" : item.forgingPotential.ToString())
                    + "; infiniteFP="
                    + PreserveFp()
                    + "; localizationKeys="
                    + keys
            );
        }
        catch (Exception)
        { /* An observer must never interrupt native crafting. */
        }
    }

    static bool Ready() =>
        ModSaveManager.instance != null
        && ModSaveManager.instance.initialized
        && Scenes.IsGameScene();

    static bool T7Enabled() => Ready() && ModSettings.AdvancedForge.AllowT7Crafting.Value;

    static int GetTier(ItemData data, int affixId)
    {
        if (data.IsNullOrDestroyed())
            return -1;
        foreach (ItemAffix affix in data.affixes)
            if (affix.affixId == affixId && !affix.IsSealed)
                return affix.affixTier;
        return -1;
    }

    static ItemAffix GetAffix(ItemData data, int affixId)
    {
        if (data.IsNullOrDestroyed())
            return null;
        foreach (ItemAffix affix in data.affixes)
            if (affix.affixId == affixId && !affix.IsSealed)
                return affix;
        return null;
    }

    static bool IsIdol(ItemData data) =>
        !data.IsNullOrDestroyed() && data.itemType > 24 && data.itemType < 34;

    static int MaxForgeCost(int internalTier)
    {
        // Historical RCInet values for upgrades into T6/T7.
        if (internalTier >= 5)
            return 36;
        return 30;
    }

    static int SupportGlyphSubtype(CraftingSlotManager manager)
    {
        if (manager.IsNullOrDestroyed())
            return -1;
        OneItemContainer support = manager.GetSupport();
        if (support.IsNullOrDestroyed())
            return -1;
        ItemData supportItem = support.getItem();
        if (supportItem.IsNullOrDestroyed() || supportItem.itemType != 103)
            return -1;
        return supportItem.subType;
    }

    static bool BlockedGuaranteedDespair(CraftingSlotManager manager)
    {
        if (
            !Ready()
            || !ModSettings.AdvancedForge.GuaranteedGlyphOfDespair.Value
            || item.IsNullOrDestroyed()
            || SupportGlyphSubtype(manager) != 3
        )
            return false;
        foreach (ItemAffix affix in item.affixes)
            if (
                !affix.IsNullOrDestroyed()
                && (affix.IsSealed || affix.IsSealedPrimordial || affix.IsSealedCorrupted)
            )
                return true;
        return false;
    }

    static bool PreserveFp() => ModSettings.InfiniteForgingPotential.Enabled.Value;

    static void ApplyForcedRoll(ItemAffix affix)
    {
        if (affix.IsNullOrDestroyed() || !ModSettings.AdvancedForge.AffixRoll.Enabled)
            return;
        affix.affixRoll = (byte)
            Mathf.Clamp(Mathf.RoundToInt(ModSettings.AdvancedForge.AffixRoll.Value), 0, 255);
    }

    [HarmonyPatch(typeof(CraftingManager), "OnMainItemChange")]
    internal static class MainItemChanged
    {
        [HarmonyPostfix]
        static void Postfix(Il2CppSystem.Object __0)
        {
            item = null;
            if (__0.IsNullOrDestroyed())
                return;
            OneItemContainer container = __0.TryCast<OneItemContainer>();
            if (!container.IsNullOrDestroyed() && !container.content.IsNullOrDestroyed())
                item = container.content.data;
        }
    }

    [HarmonyPatch(typeof(CraftingManager), "OnMainItemRemoved")]
    internal static class MainItemRemoved
    {
        [HarmonyPostfix]
        static void Postfix() => item = null;
    }

    [HarmonyPatch(typeof(CraftingSlotManager), "Awake")]
    internal static class SlotManagerAwake
    {
        [HarmonyPostfix]
        static void Postfix(CraftingSlotManager __instance) => craftingSlotManager = __instance;
    }

    // Vanilla rejects upgrades once displayed T5 is reached. Re-open capability
    // only for the T5->T6 and T6->T7 transitions.
    [HarmonyPatch(typeof(CraftingManager), "CheckForgeCapability")]
    internal static class ForgeCapability
    {
        [HarmonyPrefix]
        static void Prefix(out ForgeRejectionObservation __state)
        {
            __state = activeCheck;
            activeCheck = T7Enabled()
                ? new ForgeRejectionObservation(Scripts.Mods.Craft.Craft_Locales.affix_is_maxed_key)
                : null;
        }

        [HarmonyPostfix, HarmonyPriority(Priority.Last)]
        static void Postfix(
            CraftingManager __instance,
            ref bool __result,
            ref string __0,
            ref bool __1,
            ref bool __2,
            ref string __3
        )
        {
            Trace(__instance, __result, __0, __1, __2, __3);
            if (BlockedGuaranteedDespair(craftingSlotManager))
            {
                __result = false;
                __0 = LocaleRegistry.Translate("Already has a sealed affix");
                __3 = LocaleRegistry.Translate("Guaranteed Despair cannot seal a second affix.");
                return;
            }
            if (!T7Enabled() || item.IsNullOrDestroyed() || IsIdol(item))
                return;
            // Match the exact native key's output during this call, rather than
            // an uninitialized English placeholder or a label from another locale.
            if (activeCheck == null || !activeCheck.IsMaxedRejection(__result, __0))
                return;

            int tier = GetTier(item, __instance.appliedAffixID);
            if (tier < 4 || tier >= 6)
                return;

            // Infinite FP already handles the real cost. Without it, retain the
            // old mod's conservative "must be able to afford worst case" gate.
            if (!PreserveFp() && item.forgingPotential <= MaxForgeCost(tier))
                return;

            if (!craftingSlotManager.IsNullOrDestroyed() && !PreserveFp())
                craftingSlotManager.maxForgingPotentialText.text =
                    "<size=13><color=#FF0000>-" + MaxForgeCost(tier);

            __0 = "Upgrade Affix";
            __1 = false;
            __2 = false;
            __3 = "";
            __result = true;
        }

        [HarmonyFinalizer]
        static void Finalizer(ForgeRejectionObservation __state) => activeCheck = __state;
    }

    [HarmonyPatch(typeof(Il2Cpp.Localization), "GetText")]
    internal static class NativeForgeLabel
    {
        [HarmonyPostfix, HarmonyPriority(Priority.Last)]
        static void Postfix(string __0, string __result) => activeCheck?.Observe(__0, __result);
    }

    [HarmonyPatch(typeof(CraftingUpgradeButton), "UpdateButton")]
    internal static class UpgradeButton
    {
        [HarmonyPrefix]
        static void Prefix(int __0, ref bool __1)
        {
            if (BlockedGuaranteedDespair(craftingSlotManager))
            {
                __1 = false;
                return;
            }
            if (!T7Enabled() || item.IsNullOrDestroyed() || IsIdol(item) || __0 < 0)
                return;
            int tier = GetTier(item, __0);
            if (tier < 4 || tier >= 6)
                return;
            if (PreserveFp() || item.forgingPotential > MaxForgeCost(tier))
                __1 = true;
        }
    }

    // The historical implementation had to own the T5+ craft because vanilla
    // never reaches its normal upgrade path above T5.
    [HarmonyPatch(typeof(CraftingSlotManager), "Forge")]
    internal static class Forge
    {
        sealed class VanillaForgeState
        {
            public ItemAffix Affix;
            public byte Tier;
            public byte Roll;
            public byte Fp;
            public int Glyph;
        }

        [HarmonyPrefix, HarmonyPriority(Priority.First)]
        static bool Prefix(CraftingSlotManager __instance, out VanillaForgeState __state)
        {
            __state = null;
            // Recheck at execution: a stale enabled button must never allow
            // another seal or consume resources on this rejected craft.
            if (BlockedGuaranteedDespair(__instance))
                return false;
            if (!Ready() || item.IsNullOrDestroyed() || IsIdol(item))
                return true;

            int affixId = __instance.appliedAffixID;
            ItemAffix affix = GetAffix(item, affixId);
            if (affix.IsNullOrDestroyed())
                return true;

            // Below T5 the native forge handles the craft (and its resource use);
            // remember the state so Postfix can enforce the guaranteed glyph outcome.
            if (affix.affixTier < 4 || !T7Enabled() || affix.affixTier >= 6)
            {
                int g = SupportGlyphSubtype(__instance);
                if (
                    affix.affixTier < 4
                    && (
                        (g == 0 && ModSettings.AdvancedForge.GuaranteedGlyphOfHope.Value)
                        || (g == 3 && ModSettings.AdvancedForge.GuaranteedGlyphOfDespair.Value)
                    )
                )
                    __state = new VanillaForgeState
                    {
                        Affix = affix,
                        Tier = affix.affixTier,
                        Roll = affix.affixRoll,
                        Fp = item.forgingPotential,
                        Glyph = g,
                    };
                return true;
            }

            int oldTier = affix.affixTier;
            int glyph = SupportGlyphSubtype(__instance);

            // Glyph of Despair: when guarantee is enabled and the actual glyph is
            // slotted, force the seal outcome rather than silently inventing a glyph.
            if (glyph == 3 && ModSettings.AdvancedForge.GuaranteedGlyphOfDespair.Value)
            {
                item.SealAffix(affix);
                item.RefreshIDAndValues();
                return false;
            }

            // Glyph of Order (subtype 2) preserves the existing roll.
            if (glyph != 2)
                affix.affixRoll = (byte)Random.Range(0f, 255f);

            affix.affixTier++;
            ApplyForcedRoll(affix);

            bool noCost = PreserveFp();
            if (!noCost && glyph == 0)
            {
                // Native Hope is 25%; guarantee only changes that outcome when
                // the player actually slotted a Hope glyph.
                noCost =
                    ModSettings.AdvancedForge.GuaranteedGlyphOfHope.Value
                    || Random.RandomRangeInt(0, 4) == 0;
            }

            if (!noCost)
                item.forgingPotential -= (byte)Random.RandomRangeInt(1, MaxForgeCost(oldTier));

            item.RefreshIDAndValues();
            return false;
        }

        [HarmonyPostfix]
        static void Postfix(VanillaForgeState __state)
        {
            if (__state == null || item.IsNullOrDestroyed() || __state.Affix.IsNullOrDestroyed())
                return;
            try
            {
                ItemAffix affix = __state.Affix;
                if (__state.Glyph == 0)
                {
                    // Guaranteed Hope: the native roll may have charged forging potential.
                    item.forgingPotential = __state.Fp;
                }
                else if (__state.Glyph == 3 && !affix.IsSealed)
                {
                    // Guaranteed Despair: undo a native non-seal outcome and seal instead.
                    affix.affixTier = __state.Tier;
                    affix.affixRoll = __state.Roll;
                    item.forgingPotential = __state.Fp;
                    item.SealAffix(affix);
                }
                item.RefreshIDAndValues();
            }
            catch (Exception)
            { /* Never interrupt native crafting. */
            }
        }
    }
}
