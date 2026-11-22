using System;
using System.Collections.Generic;
using HarmonyLib;
using Il2Cpp;
using UnityEngine.UI;

namespace LastEpoch_Hud.Scripts.Mods.Login;

public class Login_OfflineCharacterSelect
{
    private static OnlineOfflineSwitch modeSwitch;
    private static List<Button> switchButtons;
    private static bool reportedHidden;
    private static bool reportedMissingButton;

    private static void Track(OnlineOfflineSwitch instance)
    {
        if (modeSwitch != instance)
        {
            modeSwitch = instance;
            switchButtons = null;
            reportedHidden = false;
            reportedMissingButton = false;
        }
    }

    public static void Tick()
    {
        if (modeSwitch.IsNullOrDestroyed() || !modeSwitch.isActiveAndEnabled)
            return;

        try
        {
            var selection = modeSwitch.characterSelect;
            if (selection.IsNullOrDestroyed() || selection.isOnlineTabShowing)
                return;

            // This component owns the mode-switch UI. Hide its buttons, retaining
            // the parent status label/icon; no translated text or scene-wide search.
            if (switchButtons == null)
            {
                switchButtons = new List<Button>();
                foreach (var button in modeSwitch.GetComponentsInChildren<Button>(true))
                    switchButtons.Add(button);
            }
            if (switchButtons.Count == 0)
            {
                if (!reportedMissingButton)
                {
                    reportedMissingButton = true;
                    Main.logger_instance?.Warning(
                        "[Offline] Mode-switch button was not found; its online action is still blocked."
                    );
                }
                return;
            }

            foreach (var button in switchButtons)
            {
                if (button.IsNullOrDestroyed())
                {
                    switchButtons = null;
                    return;
                }
                button.interactable = false;
                if (button.gameObject.activeSelf)
                    button.gameObject.SetActive(false);
            }
            if (!reportedHidden)
            {
                reportedHidden = true;
                Main.logger_instance?.Msg("[Offline] Character selection online switch hidden.");
            }
        }
        catch (Exception ex)
        {
            // Action patches remain installed if this version's UI hierarchy differs.
            modeSwitch = null;
            Main.logger_instance?.Warning(
                "[Offline] Could not hide the character selection switch: " + ex.Message
            );
        }
    }

    private static bool AllowSwitch(CharacterSelect selection)
    {
        // Preserve a return to offline if an online tab was already selected.
        if (!selection.IsNullOrDestroyed() && selection.isOnlineTabShowing)
            return true;

        Main.logger_instance?.Msg("[Offline] Blocked character selection switch to online.");
        return false;
    }

    [HarmonyPatch(typeof(OnlineOfflineSwitch), "Awake")]
    public class SwitchCreated
    {
        [HarmonyPostfix]
        static void Postfix(OnlineOfflineSwitch __instance) => Track(__instance);
    }

    [HarmonyPatch(typeof(OnlineOfflineSwitch), "OnStatusChange")]
    public class SwitchStatusChanged
    {
        [HarmonyPostfix]
        static void Postfix(OnlineOfflineSwitch __instance) => Track(__instance);
    }

    [HarmonyPatch(typeof(OnlineOfflineSwitch), "OnDestroy")]
    public class SwitchDestroyed
    {
        [HarmonyPrefix]
        static void Prefix(OnlineOfflineSwitch __instance)
        {
            if (modeSwitch == __instance)
                modeSwitch = null;
        }
    }

    [HarmonyPatch(typeof(OnlineOfflineSwitch), "OnButtonPressed")]
    public class SwitchPressed
    {
        [HarmonyPrefix]
        static bool Prefix(OnlineOfflineSwitch __instance) =>
            AllowSwitch(__instance.characterSelect);
    }

    [HarmonyPatch(typeof(CharacterSelect), "SwitchOnlineOffline")]
    public class CharacterModeSwitch
    {
        [HarmonyPrefix]
        static bool Prefix(CharacterSelect __instance) => AllowSwitch(__instance);
    }

    [HarmonyPatch(typeof(CharacterSelect), "SetIsOnlineTabShowing")]
    public class OnlineTabRequested
    {
        [HarmonyPrefix]
        static bool Prefix(bool playOnline)
        {
            if (!playOnline)
                return true;

            Main.logger_instance?.Msg("[Offline] Blocked online character tab request.");
            return false;
        }
    }
}
