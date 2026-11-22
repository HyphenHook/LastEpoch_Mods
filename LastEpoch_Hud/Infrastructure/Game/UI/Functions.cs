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
