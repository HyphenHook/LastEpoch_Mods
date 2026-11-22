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
    internal static void EnsureCurrencyIcons() => FindCurrencyIcons();
    private static float nextIconSearch;
    private static int emberSearches;

    /// <summary>Finds the game's own currency icons (the inventory footer's gold and bones, the ember counter).</summary>
    private static void FindCurrencyIcons()
    {
        if ((goldIcon != null && bonesIcon != null && emberIcon != null) || Time.unscaledTime < nextIconSearch)
        {
            return;
        }
        nextIconSearch = Time.unscaledTime + 15f;
        emberSearches++;
        try
        {
            foreach (Image img in Object.FindObjectsOfType<Image>(true))
            {
                if (img == null || img.sprite == null)
                {
                    continue;
                }
                string n = img.gameObject.name;
                if (goldIcon == null && n == "Gold Coins Icon")
                {
                    goldIcon = img.sprite;
                }
                else if (bonesIcon == null && n == "Ancient Bones Icon")
                {
                    bonesIcon = img.sprite;
                }
            }
            if (emberIcon == null)
            {
                // the Soul Ember icon by its exact name (the game also has rune icons with "ember" in the name)
                Sprite buff = null;
                foreach (Sprite sp in Resources.FindObjectsOfTypeAll<Sprite>())
                {
                    if (sp == null)
                    {
                        continue;
                    }
                    if (sp.name.Equals("SoulEmber_Icon", StringComparison.OrdinalIgnoreCase))
                    {
                        emberIcon = sp;
                        break;
                    }
                    if (buff == null && sp.name.Equals("SoulEmber_Icon_Buff", StringComparison.OrdinalIgnoreCase))
                    {
                        buff = sp;
                    }
                }
                emberIcon ??= buff;
            }
            if (emberIcon == null)
            {
                // the Soul Ember status icon: the picture on the game's own ember counter
                var seen = new List<string>();
                foreach (SoulEmberCounterUI counter in Object.FindObjectsOfType<SoulEmberCounterUI>(true))
                {
                    if (counter == null)
                    {
                        continue;
                    }
                    foreach (Image img in counter.GetComponentsInChildren<Image>(true))
                    {
                        if (img == null || img.sprite == null)
                        {
                            continue;
                        }
                        seen.Add(img.gameObject.name + "/" + img.sprite.name);
                        string name = (img.gameObject.name + img.sprite.name).ToLowerInvariant();
                        if (emberIcon == null || name.Contains("icon") || name.Contains("ember"))
                        {
                            emberIcon = img.sprite;
                        }
                    }
                }
                Main.logger_instance?.Msg("Recombinator ember counter images: " + string.Join(", ", seen.Take(12)));
            }
            if (emberIcon == null)
            {
                // otherwise: any sprite named for soul embers, the icon one first
                Sprite best = null;
                var names = new List<string>();
                foreach (Sprite sp in Resources.FindObjectsOfTypeAll<Sprite>())
                {
                    if (sp == null)
                    {
                        continue;
                    }
                    string n = sp.name.ToLowerInvariant();
                    if (n.Contains("ember") && n.Contains("soul"))
                    {
                        names.Add(sp.name);
                        if (best == null || (n.Contains("icon") && !best.name.ToLowerInvariant().Contains("icon")))
                        {
                            best = sp;
                        }
                    }
                }
                emberIcon = best;
                Main.logger_instance?.Msg("Recombinator ember sprites: " + string.Join(", ", names.Take(12)));
            }
            Main.logger_instance?.Msg("Recombinator icons: gold " + (goldIcon != null) + ", bones " + (bonesIcon != null) + ", ember " + (emberIcon != null));
        }
        catch (Exception ex)
        {
            Main.logger_instance?.Warning("Recombinator icon search failed: " + ex.Message);
        }
    }

    private const float ChipIcon = 24;
    private static float ChipWidth(string text, float size) => ChipIcon + 6 + text.Length * size * 0.58f;

    /// <summary>A currency amount with the game's icon in front; returns its width.</summary>
    private static float Chip(Sprite icon, string fallback, string text, float x, float y, float size, Color color)
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
            Label(text, x + ChipIcon + 6, y, w, h, size, color, TextAlignmentOptions.MidlineLeft);
        }
        else if (fallback == "Soul Embers")
        {
            // no sprite found: a small glowing ember drawn by hand
            Image glow = CraftingWidgets.MakeImage(Content, "EmberGlyph", new Color(1f, 0.5f, 0.12f, 0.9f));
            glow.raycastTarget = false;
            RectTransform gr = glow.rectTransform;
            gr.anchorMin = gr.anchorMax = new Vector2(0, 1);
            gr.pivot = new Vector2(0.5f, 0.5f);
            gr.anchoredPosition = new Vector2(x + ChipIcon / 2, -(y + h / 2));
            gr.sizeDelta = new Vector2(15, 15);
            gr.localRotation = Quaternion.Euler(0, 0, 45);
            dynamic.Add(glow.gameObject);
            Image core = CraftingWidgets.MakeImage(glow.transform, "Core", new Color(1f, 0.92f, 0.6f, 1f));
            core.raycastTarget = false;
            RectTransform cr = core.rectTransform;
            cr.anchorMin = cr.anchorMax = new Vector2(0.5f, 0.5f);
            cr.sizeDelta = new Vector2(7, 7);
            Label(text, x + ChipIcon + 6, y, w, h, size, color, TextAlignmentOptions.MidlineLeft);
        }
        else
        {
            Label(text + " " + fallback, x, y, w + 60, h, size, color, TextAlignmentOptions.MidlineLeft);
            w += 60;
        }
        return w;
    }

    /// <summary>Several currency amounts side by side, centred on x.</summary>
    private static void ChipRow(float centre, float y, float size, params (Sprite Icon, string Fallback, string Text, Color Color)[] items)
    {
        const float gap = 30;
        float total = -gap;
        foreach (var it in items)
        {
            total += ChipWidth(it.Text, size) + gap + (it.Icon == null ? 60 : 0);
        }
        float x = centre - total / 2;
        foreach (var it in items)
        {
            x += Chip(it.Icon, it.Fallback, it.Text, x, y, size, it.Color) + gap;
        }
    }

    internal static void Notify(string text)
    {
        try
        {
            Notifications.ShowBazaarError(text); // the same red notice the Bazaar shows
        }
        catch (Exception)
        {
            message = text;
            dirty = true;
        }
    }

    private static RectTransform cageRoot;
    private static float cageReleaseAt = -10f;

    internal static AncientBonesTracker BonesTracker()
    {
        AncientBonesTracker tracker = PlayerFinder.getAncientBonesTracker();
        if (tracker.IsNullOrDestroyed() && !Refs_Manager.player_actor.IsNullOrDestroyed())
        {
            tracker = Refs_Manager.player_actor.ancientBonesTracker;
        }
        return tracker.IsNullOrDestroyed() ? null : tracker;
    }

    internal static long HaveBones()
    {
        try
        {
            AncientBonesTracker tracker = BonesTracker();
            if (tracker == null)
            {
                return 0;
            }
            long lo = 0, hi = 2_000_000_000;
            while (lo < hi)
            {
                long mid = (lo + hi + 1) / 2;
                if (tracker.CanAfford((int)mid))
                {
                    lo = mid;
                }
                else
                {
                    hi = mid - 1;
                }
            }
            return lo;
        }
        catch (Exception)
        {
            return 0;
        }
    }

    internal static long HaveGold()
    {
        try
        {
            ItemContainersManager manager = ItemContainersManager.Instance;
            GoldTracker tracker = Refs_Manager.player_gold_tracker;
            if (tracker.IsNullOrDestroyed())
            {
                return 0;
            }
            // CanAfford probes without spending: find the largest amount it accepts.
            long lo = 0, hi = 2_000_000_000;
            while (lo < hi)
            {
                long mid = (lo + hi + 1) / 2;
                if (tracker.CanAfford((int)mid))
                {
                    lo = mid;
                }
                else
                {
                    hi = mid - 1;
                }
            }
            return lo;
        }
        catch (Exception)
        {
            return 0;
        }
    }

    private static int HaveEmbers()
    {
        try
        {
            DungeonRunManager runs = Character_SoulEmbers.GetManager();
            return runs.IsNullOrDestroyed() ? 0 : runs.GetSoulEmbers();
        }
        catch (Exception)
        {
            return 0;
        }
    }
}
