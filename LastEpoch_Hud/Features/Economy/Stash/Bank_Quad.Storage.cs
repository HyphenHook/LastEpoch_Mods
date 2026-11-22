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

    public class Get
    {
        public static string ActiveTabName()
        {
            string r = "";
            try
            {
                if (!stash_item_container.IsNullOrDestroyed())
                {
                    int index = stash_item_container.CurrentlyActiveTab;
                    for (int i = 0; i < stash_item_container.LinkedStash.Tabs.Count; i++)
                    {
                        if (stash_item_container.LinkedStash.Tabs[i].TabId == index)
                        {
                            r = stash_item_container.LinkedStash.Tabs[i].DisplayName;
                            break;
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                ErrorLog.Report(ex, "Bank_Quad.ActiveTabName");
            }

            return r;
        }

        public static string ActiveTabName(int index)
        {
            string r = "";
            try
            {
                if (!stash_item_container.IsNullOrDestroyed())
                {
                    for (int i = 0; i < stash_item_container.LinkedStash.Tabs.Count; i++)
                    {
                        if (stash_item_container.LinkedStash.Tabs[i].TabId == index)
                        {
                            r = stash_item_container.LinkedStash.Tabs[i].DisplayName;
                            break;
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                ErrorLog.Report(ex, "Bank_Quad.ActiveTabName(index)");
            }

            return r;
        }

        public static bool TryGetTab(int index, out StashTab tab)
        {
            tab = null;
            try
            {
                if (
                    stash_item_container.IsNullOrDestroyed()
                    || stash_item_container.LinkedStash.IsNullOrDestroyed()
                )
                {
                    return false;
                }
                var tabs = stash_item_container.LinkedStash.Tabs;
                if (tabs == null)
                {
                    return false;
                }
                // containers[index] lines up with Tabs[index]. TabId is only a fallback.
                if ((index >= 0) && (index < tabs.Count))
                {
                    tab = tabs[index];
                    if (!tab.IsNullOrDestroyed())
                    {
                        return true;
                    }
                }
                for (int i = 0; i < tabs.Count; i++)
                {
                    StashTab candidate = tabs[i];
                    if ((!candidate.IsNullOrDestroyed()) && (candidate.TabId == index))
                    {
                        tab = candidate;
                        return true;
                    }
                }
            }
            catch (System.Exception ex)
            {
                ErrorLog.Report(ex, "Bank_Quad.TryGetTab");
            }
            return false;
        }

        public static int TabStableId(int index)
        {
            if (TryGetTab(index, out StashTab tab))
            {
                return tab.TabId;
            }
            return index;
        }

        public static string TabName(int index)
        {
            if (TryGetTab(index, out StashTab tab) && (tab.DisplayName != null))
            {
                return tab.DisplayName;
            }
            return ActiveTabName(index);
        }

        public static void MigrateIdsFromNames()
        {
            if (ids_migrated)
            {
                return;
            }
            if (Save.Data.UserTabs.names == null)
            {
                Save.Data.UserTabs.names = new System.Collections.Generic.List<string>();
            }
            if (Save.Data.UserTabs.ids == null)
            {
                Save.Data.UserTabs.ids = new System.Collections.Generic.List<int>();
            }
            if (
                stash_item_container.IsNullOrDestroyed()
                || stash_item_container.LinkedStash.IsNullOrDestroyed()
            )
            {
                return;
            }
            if ((Save.Data.UserTabs.ids.Count == 0) && (Save.Data.UserTabs.names.Count > 0))
            {
                int count = stash_item_container.containers.Count;
                for (int i = 0; i < count; i++)
                {
                    string name = TabName(i);
                    if ((name == "") || (!Save.Data.UserTabs.names.Contains(name)))
                    {
                        continue;
                    }
                    int id = TabStableId(i);
                    if (!Save.Data.UserTabs.ids.Contains(id))
                    {
                        Save.Data.UserTabs.ids.Add(id);
                    }
                }
                if (Save.Data.UserTabs.ids.Count > 0)
                {
                    Save.Data.Save();
                }
            }
            ids_migrated = true;
        }

        public static void RememberQuadTabs()
        {
            if (Save.Data.UserTabs.names == null)
            {
                Save.Data.UserTabs.names = new System.Collections.Generic.List<string>();
            }
            if (Save.Data.UserTabs.ids == null)
            {
                Save.Data.UserTabs.ids = new System.Collections.Generic.List<int>();
            }
            Save.Data.UserTabs.names.Clear();
            if (stash_item_container.IsNullOrDestroyed())
            {
                return;
            }
            int count = stash_item_container.containers.Count;
            for (int i = 0; i < count; i++)
            {
                if (!Save.Data.UserTabs.ids.Contains(TabStableId(i)))
                {
                    continue;
                }
                string name = TabName(i);
                if ((name != "") && (!Save.Data.UserTabs.names.Contains(name)))
                {
                    Save.Data.UserTabs.names.Add(name);
                }
            }
        }

        public static bool IsQuadStash()
        {
            if (stash_item_container.IsNullOrDestroyed())
            {
                return false;
            }
            return IsQuadStash(stash_item_container.CurrentlyActiveTab);
        }

        public static bool IsQuadStash(int index)
        {
            try
            {
                MigrateIdsFromNames();
                if (Save.Data.UserTabs.ids == null)
                {
                    Save.Data.UserTabs.ids = new System.Collections.Generic.List<int>();
                }
                if (Save.Data.UserTabs.names == null)
                {
                    Save.Data.UserTabs.names = new System.Collections.Generic.List<string>();
                }
                int id = TabStableId(index);
                if (Save.Data.UserTabs.ids.Count > 0)
                {
                    return Save.Data.UserTabs.ids.Contains(id);
                }
                string tab_name = TabName(index);
                return (tab_name != "") && Save.Data.UserTabs.names.Contains(tab_name);
            }
            catch (System.Exception ex)
            {
                ErrorLog.Report(ex, "Bank_Quad.IsQuadStash");
                return false;
            }
        }

        public static System.Collections.Generic.List<int> SlotsPosition(
            Vector2Int slot_position,
            Vector2Int item_size
        )
        {
            System.Collections.Generic.List<int> positions =
                new System.Collections.Generic.List<int>();
            int base_position = slot_position.x + (slot_position.y * quad_size.x);
            for (int y = 0; y < item_size.y; y++)
            {
                for (int x = 0; x < item_size.x; x++)
                {
                    positions.Add(base_position + x + (y * quad_size.x));
                }
            }

            return positions;
        }
    }

    public class Save
    {
        public class Data
        {
            public static readonly string base_path =
                Directory.GetCurrentDirectory() + @"\Mods\LastEpoch_Hud\QuadStashs\";
            public static string filename = "QuadStashs.json";
            public static string path = "";
            public static Structures.tabs UserTabs = new Structures.tabs
            {
                names = new System.Collections.Generic.List<string>(),
            };

            public class Structures
            {
                public struct tabs
                {
                    public System.Collections.Generic.List<string> names;
                    public System.Collections.Generic.List<int> ids;
                }
            }

            public static void DefaultConfig()
            {
                Main.logger_instance.Msg("QuadStashs : Make DefaultConfig");

                UserTabs = new Structures.tabs
                {
                    names = new System.Collections.Generic.List<string>(),
                    ids = new System.Collections.Generic.List<int>(),
                };
            }

            public static void Load()
            {
                if (string.IsNullOrEmpty(Data.path))
                {
                    return;
                }
                Main.logger_instance.Msg("QuadStashs : Try to Load : " + Data.path + Data.filename);

                if (!File.Exists(Data.path + filename))
                {
                    DefaultConfig();
                    Save();
                }
                if (File.Exists(Data.path + filename))
                {
                    try
                    {
                        Data.UserTabs = JsonConvert.DeserializeObject<Structures.tabs>(
                            File.ReadAllText(Data.path + filename)
                        );
                        if (Data.UserTabs.names == null)
                        {
                            DefaultConfig();
                        }
                        if (Data.UserTabs.ids == null)
                        {
                            Data.UserTabs.ids = new System.Collections.Generic.List<int>();
                        }
                        Main.logger_instance.Msg("QuadStashs : Loaded");
                    }
                    catch (System.Exception ex)
                    {
                        ErrorLog.Report(
                            ex,
                            "QuadStashs : Error loading file : " + Data.path + filename
                        );
                    }
                }
            }

            public static void Save()
            {
                if (string.IsNullOrEmpty(Data.path))
                {
                    return;
                }
                Main.logger_instance.Msg("QuadStashs : Save : " + Data.path + Data.filename);
                string jsonString = JsonConvert.SerializeObject(
                    Data.UserTabs,
                    Newtonsoft.Json.Formatting.Indented
                );
                if (!Directory.Exists(Data.path))
                {
                    Directory.CreateDirectory(Data.path);
                }
                File.WriteAllText(Data.path + Data.filename, jsonString);
            }
        }
    }
}
