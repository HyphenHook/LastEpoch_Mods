using System;
using System.Collections.Generic;
using System.Linq;
using Il2Cpp;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;

namespace LastEpoch_Hud.Scripts.Mods.Items.Jewels;

// Operates only on scratch containers and temporary items. Never seals the live cache.
internal static class HeorotCoreCacheProbe
{
    internal static string Inspect()
    {
        var actor = Refs_Manager.player_actor;
        var manager = actor.IsNullOrDestroyed() ? null : actor.itemContainersManager;
        var info = manager.IsNullOrDestroyed() ? null : manager.tempCharacterInfoProvider?.TryCast<CharacterInfoProvider>();
        var types = new Il2CppStructArray<int>(Enumerable.Range(0, JewelBaseType.Id).ToArray());
        var size = new Vector2Int(1, 1);
        var main = new EternityCacheBeforeMainContainer(manager, size, default, default, info, types);
        var other = new EternityCacheBeforeOtherContainer(manager, size, default, default, info, types);
        var after = new EternityCacheAfterContainer(manager, size, default, default, info, types);
        main.initialiseReferences(other, after);
        other.initialiseReferences(main, after);
        var lines = new List<string>();
        Check(!after.CanAddItemType(JewelBaseType.Id), "Unregistered native output excludes jewels", lines);
        HeorotCore.EnsureCacheTypes(main, other, after);
        Check(main.CanAddItemType(JewelBaseType.Id) && other.CanAddItemType(JewelBaseType.Id)
            && after.CanAddItemType(JewelBaseType.Id), "Native cache type lists register jewels in all three slots", lines);
        var donor = CreateDonor();
        Check(donor.is4ModExaltedItem(), "Native donor is a four-mod exalted jewel", lines);
        Check(other.CanReceiveItem(donor, 1), "Native donor slot accepts four-mod exalted jewel", lines);
        donor.corrupted = true;
        Check(!other.CanReceiveItem(donor, 1), "Native donor slot rejects corrupted jewel", lines);
        donor.corrupted = false;
        donor.affixes.RemoveAt(3);
        Check(!other.CanReceiveItem(donor, 1), "Native donor slot rejects three-affix jewel", lines);
        donor = CreateDonor();
        foreach (var affix in donor.affixes) affix.affixTier = 4;
        donor.rarity = 4;
        Check(!other.CanReceiveItem(donor, 1), "Native donor slot rejects non-exalted jewel", lines);

        int fused = 0;
        for (int mask = 1; mask <= 7; mask++)
        for (int lp = 1; lp <= 4; lp++)
        {
            var core = HeorotCore.Create(mask, lp);
            // Native slot validation retains the current dungeon's tier restriction.
            bool accepted = main.CanReceiveItem(core, 1);
            if (mask == 1 && lp == 1) lines.Add("Core slot accepted at current dungeon tier: " + accepted);
            core.legendaryPotential = 0;
            Check(!main.CanReceiveItem(core, 1), "Native unique slot rejects LP0 (mask " + mask + ")", null);
            core.legendaryPotential = (byte)lp;
            donor = CreateDonor();
            core.absorb4ModExaltedItemToBecomeLegendary(donor, new Il2CppSystem.Nullable<ushort>());
            core.RefreshIDAndValues();
            var restored = new ItemDataUnpacked(core.GetID());
            Check(HeorotCore.IsCore(restored) && restored.rarity == 9
                && restored.uniqueID == Core.Heorot.HeorotCoreRules.UniqueIdFor(mask)
                && restored.affixes.Count == lp && restored.Implicits.Count == 0,
                "Native fusion/serialization failed for mask " + mask + " LP" + lp, null);
            Check(after.CanAddItemType(JewelBaseType.Id), "Output category rejects jewels", null);
            Check(after.TryAddItem(restored, 1, Context.LOADING), "Native output insertion rejected the legendary Core", null);
            Check(after.content != null && after.content.data.individualID == restored.individualID,
                "Native output did not retain the exact Core", null);
            after.Clear();
            // A completed legendary is deliberately invalid as a NEW unique input.
            // Reproduce the old half-completed cache directly in this scratch container.
            main.content = new ItemContainerEntry(restored, Vector2Int.zero, size, 1);
            Check(HeorotCore.MoveCompletedCacheResult(main, after), "Completed-result recovery failed", null);
            Check(main.content == null && after.content?.data?.individualID == restored.individualID,
                "Recovery did not move exactly one original result", null);
            Check(!HeorotCore.MoveCompletedCacheResult(main, after), "Recovery duplicated an already moved result", null);
            after.Clear();
            fused++;
        }
        lines.Add("Verified " + fused + " native in-memory fusions, output-slot insertions, and idempotent stuck-result recoveries: correct Core identity, LP-sized affix transfer, legendary rarity, zero implicits, packed round trip.");
        return string.Join("\n", lines);
    }

    private static ItemDataUnpacked CreateDonor()
    {
        var item = new ItemDataUnpacked
        {
            itemType = JewelBaseType.Id, subType = 0, rarity = 6,
            classReq = ItemList.ClassRequirement.Any,
            affixes = JewelAffixes.CreateAffixes(new System.Random(), 4, 5),
        };
        item.affixes[0].affixTier = 6;
        item.RollIndividualID();
        item.randomiseImplicitRolls();
        item.RefreshIDAndValues();
        return item;
    }

    private static void Check(bool passed, string message, List<string> lines)
    {
        if (!passed) throw new InvalidOperationException(message);
        lines?.Add("PASS: " + message);
    }
}
