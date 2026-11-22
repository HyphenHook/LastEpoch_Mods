using System;
using System.Collections.Generic;
using HarmonyLib;
using Il2Cpp;

namespace LastEpoch_Hud.Scripts.ModUI.ForceDrop;

// Recognition derives from saved affix IDs, so it survives mode changes/reload.
public static class IllegalItemAdapter
{
    static UniqueList.Entry SourcePiece(AffixList.Affix definition)
    {
        if (
            definition.IsNullOrDestroyed()
            || definition.specialAffixType != AffixList.SpecialAffixType.Set
            || UniqueList.instance.IsNullOrDestroyed()
        )
            return null;
        var piece = UniqueList.getUnique(definition.uniqueId);
        return !piece.IsNullOrDestroyed() && piece.isSetItem ? piece : null;
    }

    static bool ForeignSetPiece(ItemData item, out UniqueList.Entry source)
    {
        source = null;
        if (
            item.IsNullOrDestroyed()
            || item.affixes.IsNullOrDestroyed()
            || item.rarity < 7
            || UniqueList.instance.IsNullOrDestroyed()
        )
            return false;
        var own = UniqueList.getUnique(item.uniqueID);
        if (own.IsNullOrDestroyed() || own.isSetItem)
            return false;
        foreach (var affix in item.affixes)
        {
            if (affix.IsNullOrDestroyed())
                return false;
            if (affix.specialAffixType != AffixList.SpecialAffixType.Set)
                continue;
            var piece = SourcePiece(ForceDropCatalog.Find(affix.affixId));
            if (piece.IsNullOrDestroyed() || !source.IsNullOrDestroyed())
                return false;
            source = piece;
        }
        return !source.IsNullOrDestroyed();
    }

    public static void VerifyMembership(ItemDataUnpacked item)
    {
        if (item.rarity < 7 || UniqueList.instance.IsNullOrDestroyed())
            return;
        var own = UniqueList.getUnique(item.uniqueID);
        if (own.IsNullOrDestroyed() || own.isSetItem)
            return;
        int count = 0;
        foreach (var affix in item.affixes)
            if (affix.specialAffixType == AffixList.SpecialAffixType.Set)
                count++;
        if (count == 0)
            return;
        if (
            count != 1
            || !ForeignSetPiece(item, out var source)
            || !item.grantsSetBonus()
            || item.getSetItemUniqueId() != source.uniqueID
            || item.SetId != source.setID
        )
            throw new InvalidOperationException(
                "A unique can represent one Set-piece identity; Set membership did not survive packing."
            );
    }

    [HarmonyPatch(typeof(ItemDataUnpacked), nameof(ItemDataUnpacked.MakeFullName))]
    static class PreserveUniqueName
    {
        [HarmonyPostfix]
        static void Postfix(ItemDataUnpacked __instance)
        {
            if (!ForeignSetPiece(__instance, out _))
                return;
            var own = UniqueList.getUnique(__instance.uniqueID);
            string name = null;
            try
            {
                name = Il2Cpp.Localization.Items.GetUniqueName(own.uniqueID, true, false);
            }
            catch { }
            if (string.IsNullOrWhiteSpace(name))
                name = string.IsNullOrWhiteSpace(own.displayName) ? own.name : own.displayName;
            if (!string.IsNullOrWhiteSpace(name))
                __instance.FullName = name;
        }
    }

    static HashSet<ushort> EquippedSetPieces(
        ItemContainersManager manager,
        byte setId,
        out bool hasForeign
    )
    {
        var pieces = new HashSet<ushort>();
        hasForeign = false;
        if (
            manager.IsNullOrDestroyed()
            || manager.equipment.IsNullOrDestroyed()
            || manager.equipment.Containers.IsNullOrDestroyed()
            || UniqueList.instance.IsNullOrDestroyed()
        )
            return pieces;
        foreach (var container in manager.equipment.Containers)
        {
            if (
                container.IsNullOrDestroyed()
                || !container.TryGetContentItemData(out ItemData item)
                || item.IsNullOrDestroyed()
            )
                continue;
            if (ForeignSetPiece(item, out var source))
            {
                if (source.setID == setId)
                {
                    pieces.Add(source.uniqueID);
                    hasForeign = true;
                }
            }
            else if (item.grantsSetBonus())
            {
                var nativeSource = UniqueList.getUnique(item.getSetItemUniqueId());
                if (
                    !nativeSource.IsNullOrDestroyed()
                    && nativeSource.isSetItem
                    && nativeSource.setID == setId
                )
                    pieces.Add(nativeSource.uniqueID);
            }
        }
        return pieces;
    }

    [HarmonyPatch(
        typeof(ItemContainersManager),
        nameof(ItemContainersManager.getGearCountForSetID)
    )]
    static class EquippedSetCount
    {
        [HarmonyPostfix]
        static void Postfix(ItemContainersManager __instance, byte __0, ref int __result)
        {
            var pieces = EquippedSetPieces(__instance, __0, out bool hasForeign);
            if (!hasForeign)
                return;
            // Preserve the existing explicit "remove set requirements" feature.
            if (
                !Save_Manager.instance.IsNullOrDestroyed()
                && Save_Manager.instance.initialized
                && Save_Manager.instance.data.Items.Req.set
            )
                return;
            __result = pieces.Count;
        }
    }

    [HarmonyPatch(typeof(ItemContainersManager), nameof(ItemContainersManager.IsSetItemEquipped))]
    static class EquippedSetPiece
    {
        [HarmonyPostfix]
        static void Postfix(ItemContainersManager __instance, ushort __0, ref bool __result)
        {
            if (__result || UniqueList.instance.IsNullOrDestroyed())
                return;
            var source = UniqueList.getUnique(__0);
            if (source.IsNullOrDestroyed() || !source.isSetItem)
                return;
            var pieces = EquippedSetPieces(__instance, source.setID, out bool hasForeign);
            if (hasForeign && pieces.Contains(__0))
                __result = true;
        }
    }

    [HarmonyPatch(typeof(ItemDataUnpacked), "AfterIDChange")]
    static class PackedSetIdentity
    {
        [HarmonyPostfix]
        static void Postfix(ItemDataUnpacked __instance)
        {
            // Native equipment code can read the cached field directly.
            if (ForeignSetPiece(__instance, out var source))
                __instance.SetId = source.setID;
        }
    }

    // These recognition patches are independent of the creation toggle so an
    // already-created item's bonuses continue working after reload or mode changes.
    [HarmonyPatch(typeof(ItemData), nameof(ItemData.grantsSetBonus))]
    static class GrantsSetBonus
    {
        [HarmonyPostfix]
        static void Postfix(ItemData __instance, ref bool __result)
        {
            if (ForeignSetPiece(__instance, out _))
                __result = true;
        }
    }

    [HarmonyPatch(typeof(ItemData), nameof(ItemData.getSetItemUniqueId))]
    static class SetPieceIdentity
    {
        [HarmonyPostfix]
        static void Postfix(ItemData __instance, ref ushort __result)
        {
            if (ForeignSetPiece(__instance, out var source))
                __result = source.uniqueID;
        }
    }

    [HarmonyPatch(typeof(ItemDataUnpacked), "get_SetId")]
    static class SetIdentity
    {
        [HarmonyPostfix]
        static void Postfix(ItemDataUnpacked __instance, ref int __result)
        {
            if (ForeignSetPiece(__instance, out var source))
                __result = source.setID;
        }
    }
}
