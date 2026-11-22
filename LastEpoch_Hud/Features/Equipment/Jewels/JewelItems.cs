using System;
using System.Linq;
using Il2Cpp;
using LastEpoch_Hud.Scripts.Mods.Items.AutoLoot;

namespace LastEpoch_Hud.Scripts.Mods.Items.Jewels;

internal static class JewelItems
{
    private static readonly Random Random = new();
    internal static string LastProbe { get; private set; } = "Use Items > Test Drops > Give Jewel set to test the catalog.";

    internal static void Tick()
    {
        JewelBaseType.Ensure();
        if (JewelBaseType.Registered) JewelAffixes.EnsureRegistered();
    }

    internal static ItemDataUnpacked Create(int subtype, bool exalted = false, int legendaryPotential = 0)
    {
        Tick();
        if (!JewelBaseType.Registered || !JewelAffixes.Registered)
            throw new InvalidOperationException(JewelBaseType.Status);
        if (subtype < 0 || subtype >= JewelBaseType.Names.Length) throw new ArgumentOutOfRangeException(nameof(subtype));
        var item = new ItemDataUnpacked
        {
            itemType = JewelBaseType.Id, subType = (ushort)subtype,
            rarity = 4, sockets = 4, forgingPotential = 50,
            classReq = ItemList.ClassRequirement.Any,
            affixes = JewelAffixes.CreateAffixes(Random, 4, exalted ? 7 : 5),
        };
        if (exalted && item.affixes.Count > 0) item.affixes[0].affixTier = 6;
        item.RollIndividualID();
        item.randomiseImplicitRolls();
        item.RefreshIDAndValues();
        JewelCrafting.SetLegendaryPotential(item, legendaryPotential);
        var restored = new ItemDataUnpacked(item.GetID());
        string error = Validate(item, restored);
        if (error.Length != 0) throw new InvalidOperationException("Jewel serialization failed: " + error);
        return item;
    }

    private static string Validate(ItemDataUnpacked expected, ItemDataUnpacked actual)
    {
        if (actual.itemType != JewelBaseType.Id || actual.subType != expected.subType) return "base type/subtype changed";
        if (actual.individualID != expected.individualID) return "individual item identity changed";
        var implicitDefinitions = ItemList.get().GetEquipmentSubItem(JewelBaseType.Id, expected.subType).implicits;
        if (actual.Implicits == null || actual.Implicits.Count != implicitDefinitions.Count) return "implicit definitions were not resolved";
        for (int i = 0; i < implicitDefinitions.Count; i++)
            if (expected.implicitRolls[i] != actual.implicitRolls[i]) return "implicit roll changed";
        if (actual.forgingPotential != expected.forgingPotential) return "forging potential changed";
        if (JewelCrafting.LegendaryPotentialOf(actual) != JewelCrafting.LegendaryPotentialOf(expected)) return "legendary potential changed";
        if (actual.affixes.Count != expected.affixes.Count) return "affix count changed";
        for (int i = 0; i < actual.affixes.Count; i++)
        {
            var a = actual.affixes[i]; var e = expected.affixes[i];
            if (a.affixId != e.affixId || a.affixTier != e.affixTier || a.affixRoll != e.affixRoll) return "affix identity/tier/value changed";
        }
        return "";
    }

    internal static void GiveSet(bool exalted)
    {
        try
        {
            var player = PlayerFinder.getPlayerData();
            var manager = ItemContainersManager.Instance;
            if (player.IsNullOrDestroyed() || !player.IsOffline || manager.IsNullOrDestroyed() || manager.inventory.IsNullOrDestroyed())
                throw new InvalidOperationException("Load an offline character first.");
            for (int i = 0; i < JewelBaseType.Names.Length; i++)
            {
                var item = Create(i, exalted, exalted ? 2 : 0);
                if (!manager.inventory.TryAddItem(item, 1, Context.DEFAULT))
                {
                    if (Refs_Manager.player_actor.IsNullOrDestroyed() || Refs_Manager.ground_item_manager.IsNullOrDestroyed())
                        throw new InvalidOperationException("Make four inventory cells available.");
                    ModItemDrop.Drop(Refs_Manager.ground_item_manager, Refs_Manager.player_actor, item, Refs_Manager.player_actor.position());
                }
            }
            LastProbe = "Created all four " + (exalted ? "Exalted" : "Rare") + " Jewel subtypes. Serialization, FP, LP, affix IDs, tiers and rolls passed.";
            Main.logger_instance?.Msg(LastProbe);
        }
        catch (Exception ex) { LastProbe = ex.ToString(); ErrorLog.Report(ex, "Create Jewel set"); }
    }

    internal static string Inspect()
    {
        if (!JewelBaseType.Registered) return JewelBaseType.Status;
        return JewelBaseType.Status + "\nAffix definitions: " + JewelAffixes.Definitions.Count
            + "\nEquipment layout: one Charm and three Jewels. Native Forge: corruption only."
            + "\nIdol tier multipliers: 2.5 / 3.125 / 3.75 / 4.375 / 5 / 6.5 / 8."
            + "\n" + LastProbe;
    }
}
