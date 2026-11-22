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

