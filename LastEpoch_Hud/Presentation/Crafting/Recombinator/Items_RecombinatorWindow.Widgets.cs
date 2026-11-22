using System;
using System.Collections.Generic;
using System.Linq;
using Il2Cpp;
using Il2CppTMPro;
using LastEpoch_Hud.Scripts.Core.Crafting;
using LastEpoch_Hud.Scripts.Mods.Character;
using LastEpoch_Hud.Scripts.ModUI.ForceDrop;
using LastEpoch_Hud.Scripts.ModUI.Shell.Host;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace LastEpoch_Hud.Scripts.Mods.Items.Crafting;

internal static partial class Items_RecombinatorWindow
{
    internal static void CopyRect(RectTransform from, RectTransform to)
        => CraftingWidgets.CopyRect(from, to);

    internal static void StripGameLogic(GameObject go)
        => CraftingWidgets.StripGameLogic(go);

    internal static void OnClick(GameObject go, Action action)
        => CraftingWidgets.OnClick(go, action);

    internal static void SetText(GameObject go, string text)
        => CraftingWidgets.SetText(go, text);

    private static void SetTextOn(GameObject go, string text)
    {
        TextMeshProUGUI t = go.GetComponent<TextMeshProUGUI>();
        if (t != null)
        {
            t.text = text;
        }
    }

    internal static Image MakeImage(Transform parent, string name, Color color)
        => CraftingWidgets.MakeImage(parent, name, color);

    internal static Image Part(Transform parent, string name, Color color, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 pos, Vector2 size, float rotation = 0f)
        => CraftingWidgets.Part(parent, name, color, anchorMin, anchorMax, pivot, pos, size, rotation);
}
