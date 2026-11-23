using System;
using System.Linq;
using Il2Cpp;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using LastEpoch_Hud.Scripts.Core.CustomItems;
using LastEpoch_Hud.Scripts.Core.CustomItems.Affixes;
using LastEpoch_Hud.Scripts.Mods.Items.Accessories;
using LastEpoch_Hud.Scripts.Mods.Items.AutoLoot;
using LastEpoch_Hud.Scripts.Mods.Items.CustomItems;

namespace LastEpoch_Hud.Scripts.Mods.Items.Charms;

/// <summary>
/// The Charm: its test item, how an item is recognised as a Charm, and the Intelligence it gives while equipped. The slot
/// itself (saving, moving, dropping) is kept by <c>AccessoryStore</c>.
/// </summary>
internal static class CharmEquipment
{
    internal const string TestName = "Intelligence Test Charm";
    internal const int TestIntelligence = 10;
    private const ushort TestUniqueId = 506;
    private const byte CarrierType = CharmBaseType.Id;
    private static readonly CustomUniqueRegistrar registrar = new(new CustomUniqueDefinition
    {
        Spec = new CustomUniqueSpec
        {
            Name = TestName,
            UniqueId = TestUniqueId,
            BaseType = CarrierType,
            BaseId = CustomUniqueSpec.AllocateBaseId,
            AddsBase = true,
            OverrideLevelRequirement = true,
        },
        Flags = () => new CustomUniqueFlags(false, false, true),
        Mods = new[] { new CustomUniqueMod(CustomItemStat.Intelligence, CustomItemTag.None,
            CustomItemModType.Added, TestIntelligence, TestIntelligence, false) },
        TooltipEntries = () => new byte[] { 0 },
    });

    private static BaseStats appliedTo;
    private static int appliedCount;
    private static string appliedSignature;

    internal static bool IsRegistered => registrar.IsRegistered;

    internal static ItemDataUnpacked Equipped => AccessoryStore.At(AccessoryStore.CharmSlot);

    internal static bool IsCharm(ItemData item) => CharmBaseType.Registered && !item.IsNullOrDestroyed()
        && item.itemType == CarrierType;

    internal static string Inspect(Actor actor) =>
        $"Charm base type: {CarrierType}; subtype: {registrar.BaseId}\n"
        + $"Charm modifier: +{(appliedTo.IsNullOrDestroyed() ? 0 : appliedCount * TestIntelligence)} Intelligence";

    internal static void Tick()
    {
        if (CharmBaseType.Registered) registrar.Update();
    }

    /// <summary>A Charm saved before it had its own base type was a relic or ring; turn it into a Charm.</summary>
    internal static void Normalize(ItemDataUnpacked item)
    {
        if ((item.itemType == (byte)EquipmentType.RELIC || item.itemType == (byte)EquipmentType.RING) && item.uniqueID == TestUniqueId)
        {
            item.itemType = CarrierType;
            item.subType = (ushort)registrar.BaseId;
            item.RefreshIDAndValues();
        }
    }

    /// <summary>Gives the character the Charm's Intelligence, or takes it back.</summary>
    internal static void Refresh(BaseStats target)
    {
        var charm = Equipped;
        string signature = target.IsNullOrDestroyed() ? null
            : charm == null ? "" : Convert.ToBase64String(charm.GetID().ToArray());
        if (appliedTo == target && appliedSignature == signature) return;
        appliedSignature = null;
        if (!appliedTo.IsNullOrDestroyed())
        {
            if (appliedCount > 0)
                appliedTo.RemoveStatModifier(SP.Intelligence, appliedCount * TestIntelligence, BaseStats.ModType.ADDED, (AT)0, 0, 0);
            appliedTo.UpdateStatsNextFrame();
        }
        appliedTo = null;
        appliedCount = 0;
        if (target.IsNullOrDestroyed()) return;
        appliedTo = target;
        if (charm != null && IsCharm(charm))
        {
            target.AddStatModifier(SP.Intelligence, TestIntelligence, BaseStats.ModType.ADDED, (AT)0, 0, 0);
            appliedCount = 1;
        }
        appliedSignature = signature;
        target.UpdateStatsNextFrame();
    }

    internal static void MigrateInventory(ItemContainersManager manager)
    {
        if (manager.inventory.IsNullOrDestroyed()) return;
        foreach (var entry in manager.inventory.GetContent().ToArray())
        {
            var data = entry.data;
            if (data.uniqueID != TestUniqueId || data.rarity != 7
                || (data.itemType != (byte)EquipmentType.RING && data.itemType != (byte)EquipmentType.RELIC)) continue;
            var updated = new ItemDataUnpacked(data.GetID());
            CharmBaseType.Migrate(updated);
            if (!IsCharm(updated)) continue;
            var original = data;
            if (!manager.inventory.TryRemoveItem(entry, 1, Context.DEFAULT)) continue;
            if (!manager.inventory.TryAddItem(updated, 1, Context.DEFAULT))
            {
                if (!manager.inventory.TryAddItem(original, 1, Context.DEFAULT))
                    throw new InvalidOperationException("Could not restore migrated Charm to inventory.");
            }
        }
    }

    internal static void GiveTestCharm()
    {
        if (!AccessoryStore.HasCharacter)
        {
            Main.logger_instance?.Warning("Could not give the test Charm. " + AccessoryStore.Readiness);
            return;
        }
        var item = MakeTestCharm();
        if (item == null) return;
        if (!ItemContainersManager.Instance.inventory.TryAddItem(item, 1, Context.DEFAULT))
            AccessoryStore.Status = "Make room in your inventory for the test charm.";
    }

    internal static void DropTestCharm()
    {
        if (!AccessoryStore.HasCharacter || Refs_Manager.player_actor.IsNullOrDestroyed()
            || Refs_Manager.ground_item_manager.IsNullOrDestroyed()) return;
        var item = MakeTestCharm();
        if (item == null) return;
        try
        {
            ModItemDrop.Drop(Refs_Manager.ground_item_manager, Refs_Manager.player_actor,
                item, Refs_Manager.player_actor.position());
        }
        catch (Exception ex)
        {
            ErrorLog.Report(ex, "Drop test Charm");
        }
    }

    private static ItemDataUnpacked MakeTestCharm()
    {
        var item = new ItemDataUnpacked
        {
            itemType = CarrierType,
            subType = (ushort)registrar.BaseId,
            rarity = 7,
            uniqueID = TestUniqueId,
        };
        item.RollIndividualID();
        item.RefreshIDAndValues();
        if (!IsCharm(new ItemDataUnpacked(item.GetID())))
        {
            Main.logger_instance?.Warning("The test Charm did not survive item serialization.");
            return null;
        }
        return item;
    }
}
