using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarmonyLib;
using Il2Cpp;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace LastEpoch_Hud;

public class Main : MelonLoader.MelonMod
{
    public static MelonLoader.MelonLogger.Instance logger_instance = null;
    public const string company_name = "Eleventh Hour Games";
    public const string game_name = "Last Epoch";
    public const string mod_name = "LastEpoch_Hud";
    public const string mod_version = "5.1.1";
    public static bool debug = false;

    public override void OnInitializeMelon()
    {
        logger_instance = LoggerInstance;
        LoggerInstance.Msg(
            Scripts.Core.Diagnostics.BuildStamp.Format(
                BuildInfo.Commit,
                BuildInfo.Dirty,
                BuildInfo.Time
            )
        );
        LoggerInstance.Msg("[Offline] Click Play Offline to continue. Online actions are blocked.");
    }

    public override void OnSceneWasLoaded(int buildIndex, string sceneName)
    {
        Scenes.SceneName = SceneManager.GetActiveScene().name;
        Scripts.Mods.Cosmetics.OfflineCosmeticCatalog.OnSceneLoaded();
    }

    public override void OnSceneWasUnloaded(int buildIndex, string sceneName)
    {
        Scenes.SceneName = SceneManager.GetActiveScene().name;
    }

    public override void OnLateUpdate()
    {
        if ((!Base.Initializing) && (!Base.Initialized))
        {
            Base.Init();
        }
        Scripts.Mods.Login.Login_OfflineOnly.Tick();
        Scripts.Mods.Login.Login_OfflineCharacterSelect.Tick();
    }

    public override void OnApplicationQuit()
    {
        Caching.ClearCache();
    }
}

public class Locales
{
    public enum Selected
    {
        Unknow,
        English,
        French,
        Korean,
        German,
        Russian,
        Polish,
        Portuguese,
        Chinese,
        Spanish,
    }

    public static Selected current = Selected.Unknow;
    private static readonly string dictionary_path =
        Application.dataPath + "/../Mods/" + Main.mod_name + "/Locales";
    public static string dictionnary_filename = "";
    public static Dictionary<string, string> current_dictionary = null;
    public static bool update = false;

    //public static bool debug_text = false; //used to generate default json from prefab
    //public static List<string>? debug_json;
    public static char[] igrone_str =
    {
        '+',
        '%',
        '0',
        '1',
        '2',
        '3',
        '4',
        '5',
        '6',
        '7',
        '8',
        '9',
    };

    public static string CanonicalKey(string key) =>
        LastEpoch_Hud.Scripts.Core.ModUI.HudLocaleKey.Canonicalize(key);

    public static bool TryGetTranslation(string key, out string translated)
    {
        translated = null;
        if (current_dictionary == null)
        {
            return false;
        }
        string canonical = CanonicalKey(key);
        return current_dictionary.TryGetValue(canonical, out translated)
            && !string.IsNullOrEmpty(translated);
    }

    [HarmonyPatch(typeof(Localization), "get_Locale")]
    public class Localization_get_Locale
    {
        [HarmonyPostfix]
        static void Postfix(string __result)
        {
            Selected backup = current;
            current = Selected.Unknow;
            switch (__result)
            {
                case "English (en)":
                {
                    current = Selected.English;
                    dictionnary_filename = "en";
                    break;
                }
                case "French (fr)":
                {
                    current = Selected.French;
                    dictionnary_filename = "fr";
                    break;
                }
                case "Korean (ko)":
                {
                    current = Selected.Korean;
                    dictionnary_filename = "ko";
                    break;
                }
                case "German (Germany) (de-DE)":
                {
                    current = Selected.German;
                    dictionnary_filename = "de";
                    break;
                }
                case "Russian (ru)":
                {
                    current = Selected.Russian;
                    dictionnary_filename = "ru";
                    break;
                }
                case "Polish (pl)":
                {
                    current = Selected.Polish;
                    dictionnary_filename = "pl";
                    break;
                }
                case "Portuguese (pt)":
                {
                    current = Selected.Portuguese;
                    dictionnary_filename = "pt";
                    break;
                }
                case "Chinese (Simplified) (zh)":
                {
                    current = Selected.Chinese;
                    dictionnary_filename = "zh";
                    break;
                }
                case "Spanish (Spain) (es-ES)":
                {
                    current = Selected.Spanish;
                    dictionnary_filename = "es";
                    break;
                }
                default:
                {
                    current = Selected.English;
                    dictionnary_filename = "en";
                    break;
                }
            }
            if (current != backup)
            {
                if (backup == Selected.Unknow)
                {
                    Main.logger_instance?.Msg("Locale initialized to " + current.ToString());
                }
                else
                {
                    Main.logger_instance?.Msg("Locale change to " + current.ToString());
                }
                update = LoadDictionary();
            }
        }
    }

    private static Dictionary<string, string> ReadDictionary(string filename)
    {
        string fullPath = dictionary_path + "/" + filename + Extensions.json;
        if (!File.Exists(fullPath))
        {
            return null;
        }
        try
        {
            return JsonConvert.DeserializeObject<Dictionary<string, string>>(
                File.ReadAllText(fullPath)
            );
        }
        catch (System.Exception ex)
        {
            Main.logger_instance?.Warning("Could not load locale " + filename + ": " + ex.Message);
            return null;
        }
    }

    private static bool LoadDictionary()
    {
        // Start fresh so a failed load cannot retain the previous language.
        // English also supplies corrected labels for missing translation keys.
        Dictionary<string, string> english = ReadDictionary("en");
        var dictionary = english ?? new Dictionary<string, string>();
        if (english == null)
        {
            Main.logger_instance?.Warning(
                "English locale missing or invalid; using built-in labels."
            );
        }
        if (dictionnary_filename != "en")
        {
            Dictionary<string, string> selected = ReadDictionary(dictionnary_filename);
            if (selected == null)
            {
                Main.logger_instance?.Warning(
                    "Locale " + dictionnary_filename + " missing or invalid; using English."
                );
            }
            else
            {
                foreach (var entry in selected)
                {
                    if (!string.IsNullOrEmpty(entry.Value))
                    {
                        dictionary[entry.Key] = entry.Value;
                    }
                }
            }
        }
        current_dictionary = dictionary;
        return true;
    }
}

public class Base
{
    public static readonly string base_object_name = "BaseHud";
    public static bool Initialized = false;
    public static bool Initializing = false;

    public static void Init()
    {
        Initializing = true;
        GameObject base_object = Object.Instantiate(
            new GameObject(name: base_object_name),
            Vector3.zero,
            Quaternion.identity
        );
        Object.DontDestroyOnLoad(base_object);
        base_object.AddComponent<Scripts.Refs_Manager>();
        base_object.AddComponent<Scripts.Save_Manager>();
        base_object.AddComponent<Scripts.ModUI.Settings.SaveManager>();
        base_object.AddComponent<Scripts.Hud_Manager>();
        base_object.AddComponent<Scripts.Mods_Manager>();
        Initialized = true;
        Initializing = false;
    }
}

public class Scenes
{
    public static string SceneName = "";
    private static readonly string[] SceneMenuNames =
    {
        "ClientSplash",
        "PersistentUI",
        "Login",
        "CharacterSelectScene",
    };

    public static bool IsGameScene()
    {
        if (!string.IsNullOrWhiteSpace(SceneName) && (!SceneMenuNames.Contains(SceneName)))
        {
            return true;
        }
        else
        {
            return false;
        }
    }

    public static bool IsCharacterSelection()
    {
        if (!string.IsNullOrWhiteSpace(SceneName) && (SceneName.Contains(SceneMenuNames[3])))
        {
            return true;
        }
        else
        {
            return false;
        }
    }
}

public class Extensions
{
    public static readonly string jpg = ".jpg";
    public static readonly string png = ".png";
    public static readonly string json = ".json";
    public static readonly string prefab = ".prefab";
}

public static class Functions
{
    public static bool IsNullOrDestroyed(this object obj)
    {
        try
        {
            if (obj == null)
            {
                return true;
            }
            else if (obj is Object unityObj && !unityObj)
            {
                return true;
            }
            return false;
        }
        catch
        {
            return true;
        }
    }

    public static GameObject GetChild(GameObject obj, string name)
    {
        return GetChild(obj, name, true);
    }

    public static GameObject GetChild(GameObject obj, string name, bool log)
    {
        GameObject result = null;
        if (!obj.IsNullOrDestroyed())
        {
            bool found = false;
            for (int i = 0; i < obj.transform.childCount; i++)
            {
                string obj_name = obj.transform.GetChild(i).gameObject.name;
                if (obj_name == name)
                {
                    result = obj.transform.GetChild(i).gameObject;
                    found = true;
                    break;
                }
            }
            string[] no_bug = { "skin", "Modifier Button", "legendary_icon", "quad_stash_row" };
            if (log && (!found) && (!no_bug.Contains(name)))
            {
                Main.logger_instance?.Error("Functions.GetChild, Child : " + name + " not Found");
            }
        }
        else if (log)
        {
            Main.logger_instance.Error("GetChild(" + name + ") : Obj is null");
        }

        return result;
    }

    public static GameObject FindDescendant(GameObject obj, string name)
    {
        if (obj.IsNullOrDestroyed())
        {
            return null;
        }
        for (int i = 0; i < obj.transform.childCount; i++)
        {
            GameObject child = obj.transform.GetChild(i).gameObject;
            if (child.name == name)
            {
                return child;
            }
            GameObject nested = FindDescendant(child, name);
            if (!nested.IsNullOrDestroyed())
            {
                return nested;
            }
        }
        return null;
    }

    public static List<GameObject> GetAllChild(GameObject obj)
    {
        List<GameObject> result = new List<GameObject>();
        for (int i = 0; i < obj.transform.childCount; i++)
        {
            //string obj_name = obj.transform.GetChild(i).gameObject.name;
            result.Add(obj.transform.GetChild(i).gameObject);
        }

        return result;
    }

    public static GameObject GetViewportContent(
        GameObject obj,
        string panel_name,
        string panel_content_name
    )
    {
        GameObject panel = GetChild(obj, panel_name, false);
        if (panel.IsNullOrDestroyed())
        {
            panel = FindDescendant(obj, panel_name);
        }
        if (panel.IsNullOrDestroyed())
        {
            Main.logger_instance?.Error("Functions.GetChild, Child : " + panel_name + " not Found");
            return null;
        }

        // The live menu wraps these lists in an extra Content object, and Center has
        // more than one child named Content. Use the one that actually owns a Viewport.
        GameObject content = FindChildWithViewport(panel, panel_content_name);
        if (content.IsNullOrDestroyed())
        {
            Main.logger_instance?.Error(
                "Functions.GetChild, Child : " + panel_content_name + " not Found"
            );
            return null;
        }

        GameObject viewport = GetChild(content, "Viewport", false);
        if (viewport.IsNullOrDestroyed())
        {
            Main.logger_instance?.Error("Functions.GetChild, Child : Viewport not Found");
            return null;
        }

        return GetChild(viewport, "Content", false);
    }

    static GameObject FindChildWithViewport(GameObject obj, string name)
    {
        if (obj.IsNullOrDestroyed())
        {
            return null;
        }
        for (int i = 0; i < obj.transform.childCount; i++)
        {
            GameObject child = obj.transform.GetChild(i).gameObject;
            if (child.name == name && !GetChild(child, "Viewport", false).IsNullOrDestroyed())
            {
                return child;
            }

            GameObject nested = FindChildWithViewport(child, name);
            if (!nested.IsNullOrDestroyed())
            {
                return nested;
            }
        }

        return null;
    }

    public static Toggle Get_ToggleInPanel(GameObject obj, string panel_name, string obj_name)
    {
        Toggle result = null; // new Toggle();
        GameObject panel = GetChild(obj, panel_name);
        if (!panel.IsNullOrDestroyed())
        {
            GameObject child = GetChild(panel, obj_name, false);
            if (!child.IsNullOrDestroyed())
            {
                result = child.GetComponent<Toggle>();
            }
        }

        return result;
    }

    public static Slider Get_SliderInPanel(GameObject obj, string panel_name, string obj_name)
    {
        Slider result = null; // new Slider();
        GameObject panel = GetChild(obj, panel_name);
        if (!panel.IsNullOrDestroyed())
        {
            GameObject child = GetChild(panel, obj_name, false);
            if (!child.IsNullOrDestroyed())
            {
                result = child.GetComponent<Slider>();
            }
        }

        return result;
    }

    public static Text Get_TextInButton(GameObject obj, string button_name, string text_name)
    {
        Text result = null; // new Text();
        GameObject button = GetChild(obj, button_name);
        if (!button.IsNullOrDestroyed())
        {
            GameObject child = GetChild(button, text_name, false);
            if (!child.IsNullOrDestroyed())
            {
                result = child.GetComponent<Text>();
            }
        }

        return result;
    }

    public static Dropdown Get_DopboxInPanel(
        GameObject obj,
        string panel_name,
        string dropdown_name,
        UnityAction<int> action
    )
    {
        Dropdown result = null; // new Dropdown();
        GameObject panel = GetChild(obj, panel_name);
        if (!panel.IsNullOrDestroyed())
        {
            GameObject dropdown = GetChild(panel, dropdown_name);
            if (!dropdown.IsNullOrDestroyed())
            {
                result = dropdown.GetComponent<Dropdown>();
                result.onValueChanged = new Dropdown.DropdownEvent();
                result.onValueChanged.AddListener(action);
            }
            else
            {
                Main.logger_instance?.Error(
                    "Dropdown : " + dropdown_name + " not found in " + panel_name
                );
            }
        }
        else
        {
            Main.logger_instance?.Error("Panel : " + panel_name + " not found");
        }

        return result;
    }

    public static bool Check_Texture(string name)
    {
        if (
            (
                name.Substring(name.Length - Extensions.jpg.Length, Extensions.jpg.Length).ToLower()
                == Extensions.jpg
            )
            || (
                name.Substring(name.Length - Extensions.png.Length, Extensions.png.Length).ToLower()
                == Extensions.png
            )
        )
        {
            return true;
        }
        else
        {
            return false;
        }
    }

    public static bool Check_Prefab(string name)
    {
        if (
            name.Substring(name.Length - Extensions.prefab.Length, Extensions.prefab.Length)
                .ToLower() == Extensions.prefab
        )
        {
            return true;
        }
        else
        {
            return false;
        }
    }
}
