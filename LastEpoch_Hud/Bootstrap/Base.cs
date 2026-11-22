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

