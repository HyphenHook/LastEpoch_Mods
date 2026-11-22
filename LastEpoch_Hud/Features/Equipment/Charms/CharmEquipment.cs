using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarmonyLib;
using Il2Cpp;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using LastEpoch_Hud.Scripts.Core.CustomItems;
using LastEpoch_Hud.Scripts.Core.CustomItems.Affixes;
using LastEpoch_Hud.Scripts.Mods.Items.CustomItems;
using LastEpoch_Hud.Scripts.Mods.Items.AutoLoot;
using LastEpoch_Hud.Scripts.Mods.Items.Jewels;
using Newtonsoft.Json;
using LastEpoch_Hud.Scripts.Infrastructure.Persistence;
using UnityEngine.EventSystems;

namespace LastEpoch_Hud.Scripts.Mods.Items.Charms;

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

    private static readonly string savePath = Path.Combine(Directory.GetCurrentDirectory(), "Mods", Main.mod_name, "EquippedCharms.json");
    private static Dictionary<string, byte[]> saved;
    private static string characterKey;
    private static string readiness = "Waiting for Charm registration.";
    private static BaseStats appliedTo;
    private static int appliedCount;
    private static string appliedSignature;
    private static readonly List<Stats.Stat> appliedJewelStats = new();
    private const byte JewelType = 44;
    private static readonly HashSet<string> deliveredReturns = new();
    private static Il2CppCysharp.Threading.Tasks.UniTask returnSave;
    private static string[] returningKeys;
    private static float migrateAt;
    private static float statsCheckAt;
    internal const int SlotCount = 4;
    private static readonly ItemDataUnpacked[] equipped = new ItemDataUnpacked[SlotCount];
    internal static ItemDataUnpacked EquippedAt(int index) => index >= 0 && index < SlotCount ? equipped[index] : null;
    internal static string Status { get; private set; } = "";

    internal static bool IsCharm(ItemData item) => CharmBaseType.Registered && !item.IsNullOrDestroyed()
        && item.itemType == CarrierType;

    internal static bool IsJewel(ItemData item) => JewelBaseType.Registered && !item.IsNullOrDestroyed()
        && item.itemType == JewelType && item.subType < 4
        && item.rarity <= 6;

    internal static bool Accepts(int index, ItemData item) => index >= 0 && index < SlotCount
        && (index == 0 ? IsCharm(item) : IsJewel(item));

    private static bool IsAccessory(ItemData item) => !item.IsNullOrDestroyed()
        && (item.itemType == CarrierType || item.itemType == JewelType);

    private static int FirstEmpty(ItemData item)
    {
        for (int i = 0; i < SlotCount; i++)
            if (equipped[i] == null && Accepts(i, item)) return i;
        return -1;
    }

    internal static string Inspect()
    {
        var actor = ItemContainersManager.Instance?.actor;
        if (characterKey == null || actor.IsNullOrDestroyed() || actor.stats.IsNullOrDestroyed())
            return readiness;
        return $"Charm / Jewel slots: {string.Join(", ", equipped.Select((x, i) => x == null ? "empty" : i == 0 ? "Charm" : "Jewel"))}\n"
            + $"Inventory slot UI: {CharmSlotUI.Inspect()}\n"
            + $"Charm base type: {CarrierType}; subtype: {registrar.BaseId}\n"
            + $"Charm modifier: +{(appliedTo.IsNullOrDestroyed() ? 0 : appliedCount * TestIntelligence)} Intelligence\n"
            + $"Native Jewel modifiers: {appliedJewelStats.Count}; pending returns: {saved?.Keys.Count(x => x.StartsWith(characterKey + "|PendingReturn|", StringComparison.Ordinal)) ?? 0}\n"
            + $"Character Intelligence: {actor.stats.GetAttributeValue(CoreAttribute.Attribute.Intelligence)}";
    }

    internal static void Tick()
    {
        try
        {
            if (CharmBaseType.Registered) registrar.Update();
            CompleteReturns();
            var manager = ItemContainersManager.Instance;
            var character = Scenes.IsGameScene() ? PlayerFinder.getPlayerData() : null;
            bool offline = !character.IsNullOrDestroyed() && character.IsOffline;
            string key = null;
            if (!registrar.IsRegistered) readiness = "The test Charm has not registered. Check the mod log.";
            else if (!JewelBaseType.Registered || !JewelAffixes.Registered) readiness = "Waiting for Jewel registration.";
            else if (!offline) readiness = "Load an offline character to inspect the Charm slot.";
            else if (manager.IsNullOrDestroyed() || !manager.IsFullyInitializedAndLoaded)
                readiness = "Waiting for the character's item containers.";
            else
            {
                string id = character.Id;
                if (!string.IsNullOrWhiteSpace(id))
                    key = JsonConvert.SerializeObject(new[] { character.PartitionKey, id });
                else if (!string.IsNullOrWhiteSpace(character.CharacterName))
                    key = JsonConvert.SerializeObject(new object[]
                        { "offline", character.CharacterName, character.CharacterClass, character.Hardcore });
                readiness = key == null ? "The offline character has no available save identity." : "Charm slot ready.";
            }
            bool ready = key != null;
            if (key != characterKey)
            {
                SetStatsTarget(null);
                // Receipts describe a live inventory instance, not an item saved
                // forever. A failed disk save followed by reload must redeliver.
                deliveredReturns.Clear();
                var loaded = new ItemDataUnpacked[SlotCount];
                if (key != null)
                {
                    if (saved == null)
                    {
                        var store = new AtomicJsonRepository<Dictionary<string, byte[]>>(savePath);
                        saved = store.TryLoad(out var loadedSave) ? loadedSave : new();
                    }
                    var next = new Dictionary<string, byte[]>(saved);
                    // Old slots remain recoverable until their items have reached a native save.
                    for (int i = 1; i < SlotCount; i++)
                    {
                        string legacy = key + "|CharmSlot" + i;
                        if (!next.TryGetValue(legacy, out var old)) continue;
                        next[key + "|PendingReturn|" + Guid.NewGuid().ToString("N")] = old;
                        next.Remove(legacy);
                    }
                    for (int i = 0; i < SlotCount; i++)
                    {
                        if (!next.TryGetValue(SlotKey(key, i), out var bytes)) continue;
                        var item = new ItemDataUnpacked(new Il2CppStructArray<byte>(bytes));
                        if ((item.itemType == (byte)EquipmentType.RELIC || item.itemType == (byte)EquipmentType.RING) && item.uniqueID == TestUniqueId)
                        {
                            item.itemType = CarrierType;
                            item.subType = (ushort)registrar.BaseId;
                            item.RefreshIDAndValues();
                        }
                        if (!Accepts(i, item))
                        {
                            next[key + "|PendingReturn|" + Guid.NewGuid().ToString("N")] = item.GetID().ToArray();
                            next.Remove(SlotKey(key, i));
                            continue;
                        }
                        loaded[i] = item;
                        var updated = item.GetID().ToArray();
                        if (!bytes.SequenceEqual(updated))
                        {
                            string backup = savePath + ".before-base43";
                            if (File.Exists(savePath) && !File.Exists(backup)) File.Copy(savePath, backup);
                            next[SlotKey(key, i)] = updated;
                        }
                    }
                    if (next.Count != saved.Count || next.Any(x => !saved.TryGetValue(x.Key, out var previous)
                        || !x.Value.SequenceEqual(previous)))
                    {
                        string backup = savePath + ".before-jewel-slots";
                        if (File.Exists(savePath) && !File.Exists(backup)) File.Copy(savePath, backup);
                        Persist(next);
                        saved = next;
                    }
                }
                Array.Copy(loaded, equipped, SlotCount);
                characterKey = key;
                Status = "";
            }
            var actor = ready ? manager.actor : null;
            if (ready && UnityEngine.Time.unscaledTime >= migrateAt)
            {
                migrateAt = UnityEngine.Time.unscaledTime + 1f;
                MigrateInventory(manager);
                RetryReturns(manager);
            }
            var stats = actor.IsNullOrDestroyed() ? null : actor.stats;
            // Slot actions refresh immediately. Poll for external roll changes only
            // four times a second rather than serializing four IDs every frame.
            if (stats != appliedTo || UnityEngine.Time.unscaledTime >= statsCheckAt)
            {
                statsCheckAt = UnityEngine.Time.unscaledTime + 0.25f;
                SetStatsTarget(stats);
            }
            CharmSlotUI.Tick(ready);
        }
        catch (Exception ex)
        {
            CharmSlotUI.Tick(false);
            ErrorLog.Report(ex, "Charm equipment");
        }
    }

    private static void SetStatsTarget(BaseStats target)
    {
        string signature = target.IsNullOrDestroyed() ? null : string.Join("|",
            equipped.Select(x => x == null ? "" : Convert.ToBase64String(x.GetID().ToArray())));
        if (appliedTo == target && appliedSignature == signature) return;
        // Resolve all native rolls before touching the active modifiers. Keep independent
        // snapshots: addStat may collapse/mutate the Stat passed to it.
        var modifiers = new List<Stats.Stat>();
        if (!target.IsNullOrDestroyed())
            for (int i = 1; i < SlotCount; i++)
            {
                var item = equipped[i];
                if (item == null || !Accepts(i, item)) continue;
                var native = new Il2CppSystem.Collections.Generic.List<Stats.Stat>();
                item.AddImplicitValuesToStatList(false, ref native);
                if (item.affixes != null)
                    foreach (var affix in item.affixes)
                        item.AddAffixValueToStatList(affix, false, ref native);
                foreach (var stat in native) modifiers.Add(new Stats.Stat(stat));
            }
        appliedSignature = null;
        if (!appliedTo.IsNullOrDestroyed())
        {
            // Pop only after successful removal so an interrupted refresh can retry.
            while (appliedJewelStats.Count > 0)
            {
                int last = appliedJewelStats.Count - 1;
                appliedTo.removeStat(appliedJewelStats[last]);
                appliedJewelStats.RemoveAt(last);
            }
            if (appliedCount > 0)
            {
                appliedTo.RemoveStatModifier(SP.Intelligence, appliedCount * TestIntelligence, BaseStats.ModType.ADDED, (AT)0, 0, 0);
                appliedCount = 0;
            }
            appliedTo.UpdateStatsNextFrame();
        }
        appliedTo = null;
        appliedCount = 0;
        appliedJewelStats.Clear();
        appliedSignature = null;
        if (target.IsNullOrDestroyed())
        {
            JewelStatEffects.Clear();
            return;
        }
        appliedTo = target;
        int count = equipped[0] != null && Accepts(0, equipped[0]) ? 1 : 0;
        if (count > 0)
        {
            target.AddStatModifier(SP.Intelligence, count * TestIntelligence, BaseStats.ModType.ADDED, (AT)0, 0, 0);
            appliedCount = count;
        }
        foreach (var stat in modifiers)
        {
            target.addStat(new Stats.Stat(stat));
            appliedJewelStats.Add(stat);
        }
        JewelStatEffects.Refresh(ItemContainersManager.Instance.actor, equipped.Skip(1));
        appliedSignature = signature;
        target.UpdateStatsNextFrame();
    }

    private static string SlotKey(string key, int index) => index == 0 ? key : key + "|JewelSlot" + index;

    private static void CompleteReturns()
    {
        if (returnSave == null || returnSave.Status == Il2CppCysharp.Threading.Tasks.UniTaskStatus.Pending) return;
        var completed = returnSave;
        returnSave = null;
        completed.GetAwaiter().GetResult(); // Faults leave the durable queue and delivery receipts intact.
        var next = new Dictionary<string, byte[]>(saved);
        foreach (string key in returningKeys) next.Remove(key);
        Persist(next);
        saved = next;
        foreach (string key in returningKeys) deliveredReturns.Remove(key);
        returningKeys = null;
    }

    private static void RetryReturns(ItemContainersManager manager)
    {
        if (returnSave != null || manager.inventory.IsNullOrDestroyed() || characterKey == null) return;
        string prefix = characterKey + "|PendingReturn|";
        var delivered = new List<string>();
        foreach (var pair in saved.Where(x => x.Key.StartsWith(prefix, StringComparison.Ordinal)).ToArray())
        {
            var item = new ItemDataUnpacked(new Il2CppStructArray<byte>(pair.Value));
            CharmBaseType.Migrate(item);
            if (item.individualID == 0)
            {
                item.RollIndividualID();
                item.RefreshIDAndValues();
                var next = new Dictionary<string, byte[]>(saved) { [pair.Key] = item.GetID().ToArray() };
                Persist(next);
                saved = next;
            }
            bool present = deliveredReturns.Contains(pair.Key)
                || equipped.Any(x => x != null && x.individualID == item.individualID)
                || manager.inventory.GetContent().ToArray().Any(x => x.data.individualID == item.individualID)
                || (!manager.cursor.IsNullOrDestroyed()
                    && manager.cursor.GetContent().ToArray().Any(x => x.data.individualID == item.individualID));
            if (!present)
            {
                var containers = manager.GetKeyContainers().GetEnumerator();
                var iterator = containers.Cast<Il2CppSystem.Collections.IEnumerator>();
                while (iterator.MoveNext())
                {
                    var container = containers.Current;
                    foreach (var entry in container.GetContent())
                        if (entry.data.individualID == item.individualID) { present = true; break; }
                    if (present) break;
                }
            }
            if (!present && !manager.inventory.TryAddItem(item, 1, Context.DEFAULT))
            {
                Status = "Make room in your inventory: removed Charms are safely queued for return.";
                continue;
            }
            deliveredReturns.Add(pair.Key);
            delivered.Add(pair.Key);
        }
        if (delivered.Count == 0) return;
        // Never discard the queue merely because TryAddItem succeeded. Wait for the
        // character's native asynchronous disk save; identities prevent replay.
        var data = PlayerFinder.getPlayerData();
        data.SaveItems(manager);
        returningKeys = delivered.ToArray();
        returnSave = data.SaveData();
    }

    private static void MigrateInventory(ItemContainersManager manager)
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

    private static void Save(int index, ItemData item)
    {
        var next = new Dictionary<string, byte[]>(saved);
        string key = SlotKey(characterKey, index);
        if (item == null) next.Remove(key);
        else next[key] = item.GetID().ToArray();
        Persist(next);
        saved = next;
    }

    private static void Persist(Dictionary<string, byte[]> next)
    {
        new AtomicJsonRepository<Dictionary<string, byte[]>>(savePath,
            new JsonStorePolicy { KeepBackup = true }).Save(next);
    }

    internal static void Move(int index, bool takeOnly = false)
    {
        if (characterKey == null || index < 0 || index >= SlotCount) return;
        var cursor = ItemContainersManager.Instance?.cursor;
        if (cursor.IsNullOrDestroyed()) return;
        var held = cursor.GetContent();
        try
        {
            if (held.Count == 0)
            {
                if (equipped[index] == null || !cursor.TryAddItem(equipped[index], 1, Context.DEFAULT)) return;
                try { Save(index, null); }
                catch
                {
                    cursor.TryRemoveItem(cursor.GetContent()[0], 1, Context.DEFAULT);
                    throw;
                }
                equipped[index] = null;
            }
            else
            {
                if (takeOnly) return;
                if (equipped[index] == null || !Accepts(index, held[0].data))
                {
                    // A cursor click/drop may target either column; route to the
                    // first free slot of the item's own type, never to a swap.
                    int valid = FirstEmpty(held[0].data);
                    if (valid < 0) { Status = index == 0 ? "Only Charms fit here." : "Only Jewels fit here."; return; }
                    index = valid;
                }
                var item = new ItemDataUnpacked(held[0].data.GetID());
                if (!Accepts(index, item) || !cursor.TryRemoveItem(held[0], 1, Context.DEFAULT)) return;
                bool swapped = equipped[index] != null;
                if (swapped && !cursor.TryAddItem(equipped[index], 1, Context.DEFAULT))
                {
                    cursor.TryAddItem(item, 1, Context.DEFAULT);
                    return;
                }
                try { Save(index, item); }
                catch
                {
                    if (swapped) cursor.TryRemoveItem(cursor.GetContent()[0], 1, Context.DEFAULT);
                    cursor.TryAddItem(item, 1, Context.DEFAULT);
                    throw;
                }
                equipped[index] = item;
            }
            SetStatsTarget(ItemContainersManager.Instance?.actor?.stats);
            Status = "";
        }
        catch (Exception ex)
        {
            Status = "Could not update the accessory slot.";
            ErrorLog.Report(ex, "Move accessory");
        }
    }

    internal static void UnequipToInventory(int index)
    {
        if (characterKey == null || index < 0 || index >= SlotCount || equipped[index] == null) return;
        var manager = ItemContainersManager.Instance;
        if (manager.IsNullOrDestroyed() || manager.cursor.IsNullOrDestroyed()
            || manager.cursor.HasContent() || manager.inventory.IsNullOrDestroyed()) return;
        var item = equipped[index];
        if (!manager.inventory.TryAddItem(item, 1, Context.DEFAULT))
        {
            Status = "Make room in your inventory to remove the item.";
            return;
        }
        try { Save(index, null); }
        catch (Exception ex)
        {
            foreach (var entry in manager.inventory.GetContent())
            {
                if (entry.data.individualID != item.individualID) continue;
                manager.inventory.TryRemoveItem(entry, 1, Context.DEFAULT);
                break;
            }
            ErrorLog.Report(ex, "Unequip Charm");
            return;
        }
        equipped[index] = null;
        SetStatsTarget(manager.actor?.stats);
        Status = "";
    }

    [HarmonyPatch(typeof(ItemContainerUI), nameof(ItemContainerUI.onPointerClick))]
    private static class RightClickEquip
    {
        [HarmonyPrefix]
        private static bool Prefix(ItemContainerUI __instance, PointerEventData __0)
        {
            if (characterKey == null || __instance.identifier != ContainerID.INVENTORY
                || __0.button != PointerEventData.InputButton.Right) return true;
            var manager = ItemContainersManager.Instance;
            if (manager.IsNullOrDestroyed() || manager.inventory.IsNullOrDestroyed()
                || manager.cursor.IsNullOrDestroyed() || manager.cursor.HasContent()) return true;
            var entry = manager.inventory.GetEntryAt(__instance.GetCursorPosInContainer(__0));
            if (entry.IsNullOrDestroyed() || !IsAccessory(entry.data)) return true;
            int index = FirstEmpty(entry.data);
            if (index < 0) { Status = "No valid empty accessory slot."; return false; }
            var item = new ItemDataUnpacked(entry.data.GetID());
            if (!Accepts(index, item)) return false;
            if (!manager.inventory.TryRemoveItem(entry, 1, Context.DEFAULT)) return false;
            try { Save(index, item); }
            catch (Exception ex)
            {
                manager.inventory.TryAddItem(item, 1, Context.DEFAULT);
                ErrorLog.Report(ex, "Quick equip Charm");
                return false;
            }
            equipped[index] = item;
            SetStatsTarget(manager.actor?.stats);
            Status = "";
            return false;
        }
    }

    internal static void GiveTestCharm()
    {
        if (characterKey == null)
        {
            Main.logger_instance?.Warning("Could not give the test Charm. " + readiness);
            return;
        }
        var item = MakeTestCharm();
        if (item == null) return;
        if (!ItemContainersManager.Instance.inventory.TryAddItem(item, 1, Context.DEFAULT))
            Status = "Make room in your inventory for the test charm.";
    }

    internal static void DropTestCharm()
    {
        if (characterKey == null || Refs_Manager.player_actor.IsNullOrDestroyed()
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

    [HarmonyPatch(typeof(OneSlotItemContainerWithCharacterRequirements), nameof(OneSlotItemContainerWithCharacterRequirements.TryAddItem))]
    private static class NativeEquipmentGuard
    {
        [HarmonyPrefix]
        private static bool Prefix(OneSlotItemContainerWithCharacterRequirements __instance, ItemData __0, ref bool __result)
        {
            if (__instance.id < ContainerID.EQ_HELMET || __instance.id > ContainerID.EQ_RELIC || !IsAccessory(__0)) return true;
            __result = false;
            return false;
        }
    }

    [HarmonyPatch(typeof(OneSlotItemContainer), nameof(OneSlotItemContainer.CanAddItemRarity))]
    private static class NativeSlotValidation
    {
        [HarmonyPostfix]
        private static void Postfix(OneSlotItemContainer __instance, ItemData __0, ref bool __result)
        {
            if (__instance.id >= ContainerID.EQ_HELMET && __instance.id <= ContainerID.EQ_RELIC && IsAccessory(__0))
                __result = false;
        }
    }
}
