using LastEpoch_Hud.Scripts.ModUI.Settings;
using UnityEngine;

namespace LastEpoch_Hud.Scripts.ModUI.Keybind;

// Public API consumed by feature code (e.g. Skills_AutoCast.IsModifierHeld).
// Reads the tagged-string binding produced by KeybindSetting and tests whether
// the corresponding input is currently held this frame
public static class KeybindMatcher
{
    internal static bool Conflicts(string first, string second)
    {
        if (string.IsNullOrEmpty(first) || string.IsNullOrEmpty(second))
            return false;
        if (first.StartsWith("kb:") && second.StartsWith("kb:"))
        {
            var a = new System.Collections.Generic.HashSet<string>(first.Substring(3).Split('+'));
            var b = new System.Collections.Generic.HashSet<string>(second.Substring(3).Split('+'));
            // A modifier-only AutoCast binding also conflicts with a teleport chord using it.
            return a.IsSubsetOf(b) || b.IsSubsetOf(a);
        }
        return first == second;
    }

    internal static bool Rejects(KeybindSetting setting, string candidate)
    {
        if (object.ReferenceEquals(setting, ModSettings.SafeTeleport.Key))
            return Conflicts(candidate, ModSettings.SkillsAutoCast.ModifierKey.Value);
        if (object.ReferenceEquals(setting, ModSettings.SkillsAutoCast.ModifierKey))
            return Conflicts(candidate, ModSettings.SafeTeleport.Key.Value);
        return false;
    }

    public static bool IsHeld(string binding)
    {
        if (string.IsNullOrEmpty(binding))
            return false;
        if (binding.StartsWith("kb:"))
        {
            foreach (var part in binding.Substring(3).Split('+'))
                if (!System.Enum.TryParse(part, out KeyCode key) || !Input.GetKey(key))
                    return false;
            return true;
        }
        if (binding.StartsWith("gp:"))
            return KeybindRewired.ButtonHeldByName(binding.Substring(3));
        return false;
    }
}
