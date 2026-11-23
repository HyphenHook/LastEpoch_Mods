using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarmonyLib;
using Il2Cpp;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using LastEpoch_Hud.Scripts.Infrastructure.Persistence;
using LastEpoch_Hud.Scripts.Mods.Items.AutoLoot;
using LastEpoch_Hud.Scripts.Mods.Items.Charms;
using LastEpoch_Hud.Scripts.Mods.Items.Jewels;
using Newtonsoft.Json;
using UnityEngine.EventSystems;

namespace LastEpoch_Hud.Scripts.Mods.Items.Accessories;

/// <summary>
/// The equipped accessories (slot 0 the Charm, slots 1 to 3 the Jewels) as the character saves them: which item is in which
/// slot, the save file, items waiting to go back to the inventory, and moving items in and out of slots. What an accessory
/// does to the character belongs to <c>CharmEquipment</c> and <c>JewelEquipment</c>.
/// </summary>
internal static class AccessoryStore
{
    internal const int SlotCount = 4;
    internal const int CharmSlot = 0;

    private static readonly string savePath = Path.Combine(Directory.GetCurrentDirectory(), "Mods", Main.mod_name, "EquippedCharms.json");
    private static readonly ItemDataUnpacked[] equipped = new ItemDataUnpacked[SlotCount];
    private static readonly HashSet<string> deliveredReturns = new();
    private static Dictionary<string, byte[]> saved;
    private static string characterKey;
    private static string readiness = "Waiting for Charm registration.";
    private static Il2CppCysharp.Threading.Tasks.UniTask returnSave;
    private static string[] returningKeys;
    private static float migrateAt;
    private static float statsCheckAt;
    private static BaseStats statsSeen;

    internal static string Status { get; set; } = "";
    internal static bool HasCharacter => characterKey != null;
    internal static string Readiness => readiness;
    internal static ItemDataUnpacked At(int index) => index >= 0 && index < SlotCount ? equipped[index] : null;

    internal static bool Accepts(int index, ItemData item) => index >= 0 && index < SlotCount
        && (index == CharmSlot ? CharmEquipment.IsCharm(item) : JewelEquipment.IsJewel(item));

    private static bool IsAccessory(ItemData item) => !item.IsNullOrDestroyed()
        && (item.itemType == CharmBaseType.Id || item.itemType == JewelEquipment.Type);

    private static int FirstEmpty(ItemData item)
    {
        for (int i = 0; i < SlotCount; i++)
            if (equipped[i] == null && Accepts(i, item)) return i;
        return -1;
    }

    private static string SlotKey(string key, int index) => index == 0 ? key : key + "|JewelSlot" + index;

    internal static string Inspect()
    {
        var actor = ItemContainersManager.Instance?.actor;
        if (characterKey == null || actor.IsNullOrDestroyed() || actor.stats.IsNullOrDestroyed())
            return readiness;
        return $"Charm / Jewel slots: {string.Join(", ", equipped.Select((x, i) => x == null ? "empty" : i == 0 ? "Charm" : "Jewel"))}\n"
            + $"Inventory slot UI: {AccessoryPanel.Inspect()}\n"
            + CharmEquipment.Inspect(actor) + "\n"
            + $"Pending returns: {saved?.Keys.Count(x => x.StartsWith(characterKey + "|PendingReturn|", StringComparison.Ordinal)) ?? 0}\n"
            + $"Character Intelligence: {actor.stats.GetAttributeValue(CoreAttribute.Attribute.Intelligence)}"
            + "\n" + JewelEquipment.Inspect(actor);
    }

    internal static void Tick()
    {
        try
        {
            CharmEquipment.Tick();
            CompleteReturns();
            var manager = ItemContainersManager.Instance;
            var character = Scenes.IsGameScene() ? PlayerFinder.getPlayerData() : null;
            bool offline = !character.IsNullOrDestroyed() && character.IsOffline;
            string key = null;
            if (!CharmEquipment.IsRegistered) readiness = "The test Charm has not registered. Check the mod log.";
            else if (!JewelEquipment.IsRegistered) readiness = "Waiting for Jewel registration.";
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
                RefreshStats(null);
                // Receipts describe a live inventory instance, not an item saved
                // forever. A failed disk save followed by reload must redeliver.
                deliveredReturns.Clear();
                var loaded = new ItemDataUnpacked[SlotCount];
                if (key != null) LoadCharacter(key, loaded);
                Array.Copy(loaded, equipped, SlotCount);
                characterKey = key;
                Status = "";
            }
            var actor = ready ? manager.actor : null;
            if (ready && UnityEngine.Time.unscaledTime >= migrateAt)
            {
                migrateAt = UnityEngine.Time.unscaledTime + 1f;
                CharmEquipment.MigrateInventory(manager);
                RetryReturns(manager);
            }
            var stats = actor.IsNullOrDestroyed() ? null : actor.stats;
            // Slot actions refresh immediately. Poll for external roll changes only
            // four times a second rather than serializing four IDs every frame.
            if (stats != statsSeen || UnityEngine.Time.unscaledTime >= statsCheckAt)
            {
                statsCheckAt = UnityEngine.Time.unscaledTime + 0.25f;
                statsSeen = stats;
                RefreshStats(stats);
                JewelEquipment.MaintainAttributeBonuses();
            }
            AccessoryPanel.Tick(ready);
            CharmSlotUI.Tick();
            JewelSlotUI.Tick();
            if (!AccessoryPanel.Visible) AccessorySlotView.ResetHover();
        }
        catch (Exception ex)
        {
            AccessoryPanel.Tick(false);
            ErrorLog.Report(ex, "Charm equipment");
        }
    }

    /// <summary>Makes the character's stats match what is equipped.</summary>
    internal static void RefreshStats(BaseStats stats)
    {
        CharmEquipment.Refresh(stats);
        JewelEquipment.Refresh(stats);
    }

    private static void LoadCharacter(string key, ItemDataUnpacked[] loaded)
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
            CharmEquipment.Normalize(item);
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
                // The item goes exactly where it was dropped. A slot of the wrong type refuses it.
                if (!Accepts(index, held[0].data)) { Status = index == 0 ? "Only Charms fit here." : "Only Jewels fit here."; return; }
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
            RefreshStats(ItemContainersManager.Instance?.actor?.stats);
            Status = "";
        }
        catch (Exception ex)
        {
            Status = "Could not update the accessory slot.";
            ErrorLog.Report(ex, "Move accessory");
        }
    }

    internal static bool ShiftHeld => UnityEngine.Input.GetKey(UnityEngine.KeyCode.LeftShift)
        || UnityEngine.Input.GetKey(UnityEngine.KeyCode.RightShift);

    /// <summary>Shift + right click: the equipped item drops on the ground, like regular equipment.</summary>
    internal static void DropEquipped(int index)
    {
        if (characterKey == null || index < 0 || index >= SlotCount || equipped[index] == null) return;
        var ground = Refs_Manager.ground_item_manager;
        var player = Refs_Manager.player_actor;
        if (ground.IsNullOrDestroyed() || player.IsNullOrDestroyed()) return;
        var item = equipped[index];
        try { Save(index, null); }
        catch (Exception ex)
        {
            ErrorLog.Report(ex, "Drop accessory");
            return;
        }
        equipped[index] = null;
        try
        {
            ModItemDrop.Drop(ground, player, item, player.position());
        }
        catch (Exception ex)
        {
            ErrorLog.Report(ex, "Drop accessory");
            try { Save(index, item); equipped[index] = item; }
            catch (Exception restore) { ErrorLog.Report(restore, "Restore dropped accessory"); }
        }
        RefreshStats(ItemContainersManager.Instance?.actor?.stats);
        Status = "";
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
            ErrorLog.Report(ex, "Unequip accessory");
            return;
        }
        equipped[index] = null;
        RefreshStats(manager.actor?.stats);
        Status = "";
    }

    [HarmonyPatch(typeof(ItemContainerUI), nameof(ItemContainerUI.onPointerClick))]
    private static class RightClickEquip
    {
        [HarmonyPrefix]
        private static bool Prefix(ItemContainerUI __instance, PointerEventData __0)
        {
            if (characterKey == null || __instance.identifier != ContainerID.INVENTORY
                || __0.button != PointerEventData.InputButton.Right || ShiftHeld) return true;
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
                ErrorLog.Report(ex, "Quick equip accessory");
                return false;
            }
            equipped[index] = item;
            RefreshStats(manager.actor?.stats);
            Status = "";
            return false;
        }
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
