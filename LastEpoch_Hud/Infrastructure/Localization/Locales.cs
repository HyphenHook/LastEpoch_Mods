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
        UnityEngine.Application.dataPath + "/../Mods/" + Main.mod_name + "/Locales";
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

