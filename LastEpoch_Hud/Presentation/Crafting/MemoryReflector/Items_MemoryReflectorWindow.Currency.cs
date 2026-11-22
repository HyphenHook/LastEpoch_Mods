using System;
using System.Collections.Generic;
using System.Linq;
using Il2Cpp;
using Il2CppLE.Factions;
using Il2CppTMPro;
using LastEpoch_Hud.Scripts.Core.Crafting;
using LastEpoch_Hud.Scripts.ModUI.Shell.Host;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace LastEpoch_Hud.Scripts.Mods.Items.Crafting;

internal static partial class Items_MemoryReflectorWindow
{
    // ---------------------------------------------------------------- currency

    private static Sprite amberIcon;
    private static float nextAmberSearch;

    private static void FindAmberIcon()
    {
        if (amberIcon != null || Time.unscaledTime < nextAmberSearch)
        {
            return;
        }
        nextAmberSearch = Time.unscaledTime + 15f;
        try
        {
            // the Weaver faction's own Memory Amber sprite
            foreach (string wanted in new[] { "Faction_TW_MemoryAmber", "EchoReward_MemoryAmber" })
            {
                foreach (Sprite sp in Resources.FindObjectsOfTypeAll<Sprite>())
                {
                    if (sp != null && sp.name == wanted)
                    {
                        amberIcon = sp;
                        break;
                    }
                }
                if (amberIcon != null)
                {
                    break;
                }
            }
            Main.logger_instance?.Msg("Memory Reflector amber icon: " + (amberIcon != null ? amberIcon.name : "not found"));
        }
        catch (Exception ex)
        {
            Main.logger_instance?.Warning("Memory Reflector amber icon search failed: " + ex.Message);
        }
    }

    /// <summary>Memory Amber is the Weaver's favor.</summary>
    private static Faction Weaver()
    {
        try
        {
            if (Refs_Manager.player_actor.IsNullOrDestroyed())
            {
                return null;
            }
            FactionTracker tracker = Refs_Manager.player_actor.gameObject.GetComponent<FactionTracker>();
            if (!tracker.IsNullOrDestroyed() && tracker.factions != null
                && tracker.factions.TryGetValue(FactionID.TheWeaver, out Faction faction) && !faction.IsNullOrDestroyed())
            {
                return faction;
            }
        }
        catch (Exception)
        {
        }
        return null;
    }

    private static long HaveAmber()
    {
        Faction f = Weaver();
        return f != null && f.IsMember ? f.Favor : 0;
    }

    private const float ChipIcon = 24;
    private static float ChipWidth(string text, float size) => ChipIcon + 6 + text.Length * size * 0.58f;

    private static float Chip(Sprite icon, int kind, string text, float x, float y, float size, Color color)
    {
        float w = ChipWidth(text, size);
        float h = Mathf.Max(ChipIcon, size + 6);
        if (icon != null)
        {
            Image img = CraftingWidgets.MakeImage(Content, "CurrencyIcon", Color.white);
            img.sprite = icon;
            img.preserveAspect = true;
            img.raycastTarget = false;
            RectTransform r = img.rectTransform;
            r.anchorMin = r.anchorMax = new Vector2(0, 1);
            r.pivot = new Vector2(0, 1);
            r.anchoredPosition = new Vector2(x, -(y + (h - ChipIcon) / 2));
            r.sizeDelta = new Vector2(ChipIcon, ChipIcon);
            dynamic.Add(img.gameObject);
        }
        else
        {
            // no sprite found: a small hand-drawn gem (amber) or a word
            Image glow = CraftingWidgets.MakeImage(Content, "Glyph", kind == 1 ? new Color(0.95f, 0.62f, 0.15f, 0.95f) : new Color(0.8f, 0.8f, 0.8f, 0.9f));
            glow.raycastTarget = false;
            RectTransform gr = glow.rectTransform;
            gr.anchorMin = gr.anchorMax = new Vector2(0, 1);
            gr.pivot = new Vector2(0.5f, 0.5f);
            gr.anchoredPosition = new Vector2(x + ChipIcon / 2, -(y + h / 2));
            gr.sizeDelta = new Vector2(15, 15);
            gr.localRotation = Quaternion.Euler(0, 0, 45);
            dynamic.Add(glow.gameObject);
        }
        Label(text, x + ChipIcon + 6, y, w, h, size, color, TextAlignmentOptions.MidlineLeft);
        return w;
    }

    private static void ChipRow(float centre, float y, float size, params (Sprite Icon, int Kind, string Text, Color Color)[] items)
    {
        const float gap = 30;
        float total = -gap;
        foreach (var it in items)
        {
            total += ChipWidth(it.Text, size) + gap;
        }
        float x = centre - total / 2;
        foreach (var it in items)
        {
            x += Chip(it.Icon, it.Kind, it.Text, x, y, size, it.Color) + gap;
        }
    }
}
