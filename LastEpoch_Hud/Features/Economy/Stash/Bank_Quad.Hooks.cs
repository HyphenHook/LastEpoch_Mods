using System.IO;
using HarmonyLib;
using Il2Cpp;
using Il2CppLE.Data;
using Il2CppTMPro;
using LastEpoch_Hud.Scripts.Mods.Items.AutoLoot;
using MelonLoader;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.UI;

namespace LastEpoch_Hud.Scripts.Mods.Bank;

public partial class Bank_Quad : MonoBehaviour
{

    public class Hooks
    {
        [HarmonyPatch(typeof(ItemContainer), "Clear", new System.Type[] { })]
        public class ItemContainer_Clear
        {
            [HarmonyPostfix]
            static void Postfix(ItemContainer __instance)
            {
                occupied_slots.Remove(__instance.Pointer);
            }
        }

        [HarmonyPatch(typeof(StashItemContainerUI), "Awake")]
        public class StashItemContainerUI_Awake
        {
            [HarmonyPostfix]
            static void Postfix(ref StashItemContainerUI __instance)
            {
                stash_item_container_ui = __instance;
            }
        }

        [HarmonyPatch(typeof(ItemContainersManager), "Awake")]
        public class ItemContainersManager_Awake
        {
            [HarmonyPrefix]
            static void Prefix(ref ItemContainersManager __instance)
            {
                item_contenairs_manager = __instance;
                occupied_slots.Clear();
                stash_item_container = null;
                backup_active_tab = -1;
            }
        }

        [HarmonyPatch(
            typeof(TabbedItemContainer),
            "AddNewTab",
            new System.Type[] { typeof(ItemContainer) }
        )]
        public class TabbedItemContainer_AddNewTab
        {
            [HarmonyPrefix]
            static void Prefix(TabbedItemContainer __instance, ItemContainer __0)
            {
                var stash = __instance.TryCast<StashItemContainer>();
                if (
                    stash.IsNullOrDestroyed()
                    || __0.IsNullOrDestroyed()
                    || __0.id != ContainerID.STASH
                )
                {
                    return;
                }
                stash_item_container = stash;
                int index = stash.containers.Count;
                // Do not overwrite native dimensions for ordinary/special-purpose tabs.
                if (Get.IsQuadStash(index))
                {
                    __0.size = quad_size;
                }
                occupied_slots.Remove(__0.Pointer);
            }
        }

        [HarmonyPatch(typeof(ItemContainerUI), "CalculateSlotSize")]
        public class ItemContainerUI_CalculateSlotSize
        {
            // The game keeps measuring a 12x17 grid. A quad tab has to use its own cell size,
            // taken from the grid image so icons stay inside it.
            [HarmonyPostfix]
            static void Postfix(ItemContainerUI __instance, ref Vector2 __result)
            {
                StashItemContainerUI stashUi = __instance.TryCast<StashItemContainerUI>();
                if (stashUi.IsNullOrDestroyed())
                {
                    return;
                }
                if (!ActiveTabIsQuad())
                {
                    if ((__result.x > 1f) && (__result.y > 1f))
                    {
                        native_slot_size = __result;
                        have_native_slot = true;
                    }
                    return;
                }
                if (!TryGridPixels(out float width, out float height))
                {
                    return;
                }
                __result = QuadSlotSize(width, height);
            }
        }

        [HarmonyPatch(typeof(ItemContainerUIWithSearch), "Tabbed_OnActiveTabChanged")]
        public class ItemContainerUIWithSearch_Tabbed_OnActiveTabChanged
        {
            // Let the game select the new container before calculating its slot size.
            [HarmonyPostfix]
            static void Postfix(ItemContainerUIWithSearch __instance, TabbedItemContainer __0)
            {
                var stash = __0.TryCast<StashItemContainer>();
                var ui = __instance.TryCast<StashItemContainerUI>();
                if (stash.IsNullOrDestroyed() || ui.IsNullOrDestroyed())
                {
                    return;
                }
                stash_item_container = stash;
                stash_item_container_ui = ui;
                backup_active_tab = -1;
                gridRefreshFrames = 5;
            }
        }

        [HarmonyPatch(
            typeof(ItemContainer),
            "CheckSlotsOccupied",
            new System.Type[] { typeof(Vector2Int), typeof(Vector2Int), typeof(Context) }
        )]
        public class ItemContainer_CheckSlotsOccupied
        {
            [HarmonyPrefix]
            static bool Prefix(
                ItemContainer __instance,
                ref bool __result,
                Vector2Int __0,
                Vector2Int __1
            )
            {
                if (!IsQuadContainer(__instance))
                {
                    return true;
                }
                // Invalid coordinates must never be treated as free space.
                __result = true;
                if (!Fits(__0, __1, __instance.size))
                {
                    return false;
                }
                var slots = GetOccupiedSlots(__instance);
                foreach (int index in Get.SlotsPosition(__0, __1))
                {
                    if ((index < 0) || (index >= slots.Length) || slots[index])
                    {
                        return false;
                    }
                }
                __result = false;
                return false;
            }
        }

        [HarmonyPatch(
            typeof(ItemContainer),
            "CheckSlotsOccupiedExceptArea",
            new System.Type[]
            {
                typeof(Vector2Int),
                typeof(Vector2Int),
                typeof(Vector2Int),
                typeof(Vector2Int),
            }
        )]
        public class ItemContainer_CheckSlotsOccupiedExceptArea
        {
            // The game's occupancy array stays 12x17. Quad tabs have to answer this themselves.
            [HarmonyPrefix]
            static bool Prefix(
                ItemContainer __instance,
                ref bool __result,
                Vector2Int __0,
                Vector2Int __1,
                Vector2Int __2,
                Vector2Int __3
            )
            {
                if (!IsQuadContainer(__instance))
                {
                    return true;
                }
                __result = true;
                if (!Fits(__0, __1, __instance.size))
                {
                    return false;
                }
                var slots = GetOccupiedSlots(__instance);
                foreach (int index in Get.SlotsPosition(__0, __1))
                {
                    int x = index % quad_size.x;
                    int y = index / quad_size.x;
                    if ((x >= __2.x) && (y >= __2.y) && (x < __2.x + __3.x) && (y < __2.y + __3.y))
                    {
                        continue;
                    }
                    if ((index < 0) || (index >= slots.Length) || slots[index])
                    {
                        return false;
                    }
                }
                __result = false;
                return false;
            }
        }

        [HarmonyPatch(
            typeof(ItemContainer),
            "SetSlotsOccupied",
            new System.Type[] { typeof(Vector2Int), typeof(Vector2Int), typeof(bool) }
        )]
        public class ItemContainer_SetSlotsOccupied
        {
            [HarmonyPrefix]
            static bool Prefix(
                ItemContainer __instance,
                ref bool __result,
                Vector2Int __0,
                Vector2Int __1,
                bool __2
            )
            {
                if (!IsQuadContainer(__instance))
                {
                    return true;
                }
                __result = false;
                if (!Fits(__0, __1, __instance.size))
                {
                    return false;
                }
                var slots = GetOccupiedSlots(__instance);
                foreach (int index in Get.SlotsPosition(__0, __1))
                {
                    slots[index] = __2;
                }
                __result = true;
                return false;
            }
        }

        [HarmonyPatch(typeof(ItemContainer), "GetItemsInArea")]
        public class ItemContainer_GetItemsInArea
        {
            [HarmonyPrefix]
            static bool Prefix(
                ItemContainer __instance,
                ref Il2CppSystem.Collections.Generic.HashSet<ItemContainerEntry> __result,
                Vector2Int __0,
                Vector2Int __1
            )
            {
                bool r = true;
                if ((__instance.id == ContainerID.STASH) && (__instance.size == quad_size))
                {
                    if (!Fits(__0, __1, quad_size))
                    {
                        __result =
                            new Il2CppSystem.Collections.Generic.HashSet<ItemContainerEntry>();
                        return false;
                    }
                    Il2CppSystem.Collections.Generic.List<int> area_positions =
                        new Il2CppSystem.Collections.Generic.List<int>();
                    foreach (int slot_index in Get.SlotsPosition(__0, __1))
                    {
                        area_positions.Add(slot_index);
                    }
                    Il2CppSystem.Collections.Generic.HashSet<ItemContainerEntry> item_list =
                        new Il2CppSystem.Collections.Generic.HashSet<ItemContainerEntry>();
                    foreach (ItemContainerEntry item_container_entry in __instance.content)
                    {
                        System.Collections.Generic.List<int> item_positions = Get.SlotsPosition(
                            item_container_entry.Position,
                            item_container_entry.size
                        );
                        foreach (int slot_position in item_positions)
                        {
                            if (area_positions.Contains(slot_position))
                            {
                                item_list.Add(item_container_entry);
                            }
                        }
                    }
                    __result = item_list;
                    r = false;
                }

                return r;
            }
        }

        [HarmonyPatch(typeof(ConfigureTabUI), "OnModalOpen")]
        public class ConfigureTabUI_OnModalOpen
        {
            [HarmonyPostfix]
            static void Postfix(ConfigureTabUI __instance)
            {
                configure_tab_ui = __instance;
                open_configure = false;
                configure_stash_toggle = null;
                if (__instance.nameInputTMP.IsNullOrDestroyed())
                {
                    return;
                }
                configure_stash_name_backup = __instance.nameInputTMP.text;
                if (
                    __instance.contents.IsNullOrDestroyed()
                    || __instance.stashPriorityUI.IsNullOrDestroyed()
                )
                {
                    return;
                }
                GameObject content = __instance.contents.gameObject;
                if (!content.IsNullOrDestroyed())
                {
                    QuadStash_obj = Functions.GetChild(content, "quad_stash_row");
                    if (QuadStash_obj.IsNullOrDestroyed())
                    {
                        GameObject priority_section = Functions.FindDescendant(
                            content,
                            "Priority Section"
                        );
                        GameObject priority = __instance.stashPriorityUI.gameObject;
                        if (
                            (!priority_section.IsNullOrDestroyed())
                            && (!priority.IsNullOrDestroyed())
                        )
                        {
                            RectTransform priority_section_rect_transf =
                                priority_section.GetComponent<RectTransform>();
                            if (priority_section_rect_transf.IsNullOrDestroyed())
                            {
                                return;
                            }
                            float row_H = priority_section_rect_transf.rect.height;
                            float margin = 20 * priority_section_rect_transf.lossyScale.x; //Fix Hight resolution scaling

                            GameObject row = Functions.FindDescendant(priority, "Explanation Row");
                            if (row.IsNullOrDestroyed())
                            {
                                return;
                            }
                            QuadStash_obj = UnityEngine.Object.Instantiate(
                                row,
                                new Vector3(
                                    row.transform.position.x,
                                    (row.transform.position.y - row_H - margin),
                                    row.transform.position.z
                                ),
                                Quaternion.identity
                            );
                            QuadStash_obj.name = "quad_stash_row";
                            QuadStash_obj.transform.SetParent(content.transform);

                            GameObject toogle_obj = Functions.GetChild(
                                QuadStash_obj,
                                "Priority Toggle"
                            );
                            if (!toogle_obj.IsNullOrDestroyed())
                            {
                                configure_stash_toggle = toogle_obj.GetComponent<Toggle>();
                                PrepareToggle(configure_stash_toggle);
                            }
                            GameObject text_obj = Functions.GetChild(QuadStash_obj, "OptionText");
                            if (!text_obj.IsNullOrDestroyed())
                            {
                                GameObject title_obj = Functions.GetChild(text_obj, "Title");
                                if (!title_obj.IsNullOrDestroyed())
                                {
                                    configure_stash_toggle_title =
                                        title_obj.GetComponent<TextMeshProUGUI>();
                                }
                                GameObject explanation_obj = Functions.GetChild(
                                    text_obj,
                                    "Explanation"
                                );
                                if (!explanation_obj.IsNullOrDestroyed())
                                {
                                    configure_stash_toggle_explanation =
                                        explanation_obj.GetComponent<TextMeshProUGUI>();
                                }
                            }
                        }
                    }
                    // Rebind on every modal open, including an existing cloned row.
                    if (!QuadStash_obj.IsNullOrDestroyed())
                    {
                        configure_stash_toggle = QuadStash_obj.GetComponentInChildren<Toggle>(true);
                        if (!configure_stash_toggle.IsNullOrDestroyed())
                        {
                            PrepareToggle(configure_stash_toggle);
                            configure_stash_toggle.SetIsOnWithoutNotify(
                                Get.IsQuadStash(configure_tab_ui.selectedTabID)
                            );
                        }
                        if (!configure_stash_toggle_title.IsNullOrDestroyed())
                        {
                            configure_stash_toggle_title.text = toggle_str;
                        }
                        if (!configure_stash_toggle_explanation.IsNullOrDestroyed())
                        {
                            configure_stash_toggle_explanation.text = toggle_explain_str;
                        }
                    }
                }
            }
        }

        [HarmonyPatch(
            typeof(StashTabbedUIControls),
            "HandleConfigureTabResult",
            new System.Type[] { typeof(Il2CppLE.UI.PanelSystem.StashConfigureTabModalResult) }
        )]
        public class StashTabbedUIControls_HandleConfigureTabResult
        {
            static bool choiceValid;
            static bool wantQuad;

            static bool TryGetStashTab(
                StashTabbedUIControls ui,
                Il2CppLE.UI.PanelSystem.StashConfigureTabModalResult result,
                out ItemContainer container
            )
            {
                container = null;
                if (
                    result.IsNullOrDestroyed()
                    || result.Action != Il2CppLE.UI.PanelSystem.StashConfigureResult.Save
                    || configure_tab_ui.IsNullOrDestroyed()
                    || ui != configure_tab_ui.parentUIController
                    || result.SelectedTabId != configure_tab_ui.selectedTabID
                    || string.IsNullOrEmpty(Save.Data.path)
                    || Save.Data.UserTabs.names == null
                    || stash_item_container.IsNullOrDestroyed()
                )
                {
                    return false;
                }
                int selected = result.SelectedTabId;
                if (selected < 0 || selected >= stash_item_container.containers.Count)
                {
                    return false;
                }
                container = stash_item_container.containers[selected];
                return !container.IsNullOrDestroyed() && container.id == ContainerID.STASH;
            }

            // Read the checkbox before the game closes the window and forces it back on.
            [HarmonyPrefix]
            static void Prefix(
                StashTabbedUIControls __instance,
                Il2CppLE.UI.PanelSystem.StashConfigureTabModalResult __0
            )
            {
                choiceValid = false;
                if (
                    !TryGetStashTab(__instance, __0, out ItemContainer container)
                    || configure_stash_toggle.IsNullOrDestroyed()
                )
                {
                    return;
                }
                wantQuad = configure_stash_toggle.isOn;
                choiceValid = true;
                if (wantQuad)
                {
                    return;
                }
                if (!EjectOverflow(container, default_size))
                {
                    wantQuad = true;
                    Main.logger_instance?.Warning(
                        "Quad Stash stayed on because an item could not be moved to inventory or the ground."
                    );
                }
            }

            [HarmonyPostfix]
            static void Postfix(
                StashTabbedUIControls __instance,
                Il2CppLE.UI.PanelSystem.StashConfigureTabModalResult __0
            )
            {
                if (!choiceValid || !TryGetStashTab(__instance, __0, out ItemContainer container))
                {
                    return;
                }
                choiceValid = false;
                Get.MigrateIdsFromNames();
                if (Save.Data.UserTabs.ids == null)
                {
                    Save.Data.UserTabs.ids = new System.Collections.Generic.List<int>();
                }
                int id = Get.TabStableId(__0.SelectedTabId);
                if (wantQuad)
                {
                    if (!Save.Data.UserTabs.ids.Contains(id))
                    {
                        Save.Data.UserTabs.ids.Add(id);
                    }
                }
                else
                {
                    Save.Data.UserTabs.ids.Remove(id);
                }
                Get.RememberQuadTabs();
                Save.Data.Save();
                SyncTabSize(__0.SelectedTabId);
                backup_active_tab = -1;
                gridRefreshFrames = 5;
                RefreshActiveGrid();
            }
        }

        [HarmonyPatch(typeof(ItemContainerUI), "OnUpdateTick")]
        public class ItemContainerUI_OnUpdateTick
        {
            // Bank_Quad's Unity LateUpdate is not invoked reliably by this Il2Cpp build.
            // Run deferred presentation from the stash UI's own update instead.
            [HarmonyPostfix]
            static void Postfix(ItemContainerUI __instance)
            {
                StashItemContainerUI ui = __instance.TryCast<StashItemContainerUI>();
                if (ui.IsNullOrDestroyed() || stash_item_container.IsNullOrDestroyed())
                {
                    return;
                }
                stash_item_container_ui = ui;
                if (!BindGridImage())
                {
                    return;
                }
                int active = stash_item_container.CurrentlyActiveTab;
                bool needsRefresh = (backup_active_tab != active) || (gridRefreshFrames > 0);
                if (needsRefresh && RefreshActiveGrid())
                {
                    backup_active_tab = active;
                    if (gridRefreshFrames > 0)
                    {
                        gridRefreshFrames--;
                    }
                }
                BeforeRender();
            }
        }
    }
}
