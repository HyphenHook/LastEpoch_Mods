using System;
using LastEpoch_Hud.Scripts.ModUI.Settings;
using UnityEngine;
using UnityEngine.UI;

namespace LastEpoch_Hud.Scripts.ModUI.Keybind;

// Capture state machine. Polled from SaveManager.Update each frame.
// First key/button after Begin() commits and exits capture mode.
internal static class KeybindCapture
{
    private static KeybindSetting current;
    private static Text displayText;
    private static bool[] gamepadSnapshot;
    private static KeyCode pendingModifier;

    public static bool Active => current != null;

    public static void Begin(KeybindSetting setting, Text display)
    {
        current = setting;
        displayText = display;
        gamepadSnapshot = null;
        pendingModifier = KeyCode.None;
    }

    public static void Cancel()
    {
        if (current != null && displayText != null)
            displayText.text = KeybindFormat.Friendly(current.Value);
        current = null;
        displayText = null;
        gamepadSnapshot = null;
        pendingModifier = KeyCode.None;
    }

    public static void Tick()
    {
        if (current == null)
            return;

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Cancel();
            return;
        }

        if (Input.anyKeyDown && TryCaptureKeyboard())
            return;
        // A modifier by itself remains usable (e.g. AutoCast): commit on release.
        if (pendingModifier != KeyCode.None && Input.GetKeyUp(pendingModifier))
        {
            Commit("kb:" + pendingModifier);
            return;
        }
        TryCaptureGamepad();
    }

    private static bool TryCaptureKeyboard()
    {
        foreach (KeyCode kc in Enum.GetValues(typeof(KeyCode)))
        {
            if (kc == KeyCode.None)
                continue;
            if (kc == KeyCode.Escape)
                continue;
            if (kc == KeyCode.Mouse0 || kc == KeyCode.Mouse1)
                continue;
            if (!Input.GetKeyDown(kc))
                continue;
            if (IsModifier(kc))
            {
                pendingModifier = kc;
                continue;
            }
            var keys = new System.Collections.Generic.List<string>();
            foreach (
                var modifier in new[]
                {
                    KeyCode.LeftControl,
                    KeyCode.RightControl,
                    KeyCode.LeftShift,
                    KeyCode.RightShift,
                    KeyCode.LeftAlt,
                    KeyCode.RightAlt,
                    KeyCode.LeftCommand,
                    KeyCode.RightCommand,
                }
            )
                if (Input.GetKey(modifier))
                    keys.Add(modifier.ToString());
            keys.Add(kc.ToString());
            Commit("kb:" + string.Join("+", keys));
            return true;
        }
        return false;
    }

    private static bool IsModifier(KeyCode key) =>
        key == KeyCode.LeftControl
        || key == KeyCode.RightControl
        || key == KeyCode.LeftShift
        || key == KeyCode.RightShift
        || key == KeyCode.LeftAlt
        || key == KeyCode.RightAlt
        || key == KeyCode.LeftCommand
        || key == KeyCode.RightCommand;

    private static void TryCaptureGamepad()
    {
        var template = KeybindRewired.ActiveTemplate();
        if (template == null)
            return;

        int n = KeybindRewired.ButtonCount;

        // First Tick after Begin: snapshot current state. Buttons the player was already
        // holding when capture began won't fire until they release and press again.
        if (gamepadSnapshot == null)
        {
            gamepadSnapshot = new bool[n];
            for (int i = 0; i < n; i++)
                gamepadSnapshot[i] = KeybindRewired.ButtonHeldAt(template, i);
            return;
        }

        for (int i = 0; i < n; i++)
        {
            bool now = KeybindRewired.ButtonHeldAt(template, i);
            bool was = gamepadSnapshot[i];
            gamepadSnapshot[i] = now;
            if (now && !was)
            {
                Commit("gp:" + KeybindRewired.ButtonNameAt(i));
                return;
            }
        }
    }

    private static void Commit(string binding)
    {
        if (KeybindMatcher.Rejects(current, binding))
        {
            Main.logger_instance?.Warning(
                "Binding rejected: AutoCast and Safe Teleport cannot share a key or modifier combination."
            );
            if (displayText != null)
                displayText.text = LocaleRegistry.Translate(
                    "Conflict with AutoCast / Safe Teleport. Press another key."
                );
            pendingModifier = KeyCode.None;
            return;
        }
        ModSettings.Trace("KeybindCapture committed: " + binding);
        var s = current;
        current = null;
        displayText = null;
        gamepadSnapshot = null;
        pendingModifier = KeyCode.None;
        s.Set(binding);
    }
}
