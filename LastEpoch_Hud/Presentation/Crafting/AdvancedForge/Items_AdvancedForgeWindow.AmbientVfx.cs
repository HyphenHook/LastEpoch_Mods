using System;
using System.Collections.Generic;
using System.Linq;
using LastEpoch_Hud.Scripts.Core.Crafting;
using Il2Cpp;
using Il2CppTMPro;
using LastEpoch_Hud.Scripts.ModUI.Shell.Host;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace LastEpoch_Hud.Scripts.Mods.Items.Crafting;

internal static partial class Items_AdvancedForgeWindow
{
    // ---------------------------------------------------------------- the slot while it holds an item

    private sealed class Glimmer
    {
        public RectTransform Rect;
        public Image Img;
        public float Seed, Speed, Size, X0, Sway, SwaySpeed;
    }

    private static readonly List<Glimmer> glimmers = new();
    private static Image slotGlow;

    /// <summary>A violet light behind the item, inside the frame. It is drawn first, so the item is in front of it.</summary>
    private static void AmbientBelow(Transform frame)
    {
        slotGlow = CraftingWidgets.MakeImage(frame, "SlotGlow", new Color(0.62f, 0.35f, 1f, 0.3f));
        slotGlow.sprite = OrbArt.Named("glow_violet");
        slotGlow.raycastTarget = false;
        RectTransform r = slotGlow.rectTransform;
        r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
        r.pivot = new Vector2(0.5f, 0.5f);
        r.anchoredPosition = Vector2.zero;
        r.sizeDelta = new Vector2(FrameW * 1.25f, FrameH * 1.15f);
        if (Additive() != null)
        {
            slotGlow.material = additive;
        }
    }

    /// <summary>Glimmers that drift up past the item, in front of it.</summary>
    private static void AmbientAbove(Transform frame)
    {
        glimmers.Clear();
        // the glimmers rise and twinkle round the whole frame; where each is at any moment follows the clock, so they carry on
        // unbroken when the window is redrawn
        var rng = new System.Random(7);
        for (int i = 0; i < 18; i++)
        {
            Image img = CraftingWidgets.MakeImage(frame, "Glimmer", new Color(1f, 1f, 1f, 0f));
            img.sprite = OrbArt.Named("sparkle_star");
            img.raycastTarget = false;
            if (Additive() != null)
            {
                img.material = additive;
            }
            RectTransform r = img.rectTransform;
            r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
            r.pivot = new Vector2(0.5f, 0.5f);
            float size = 9f + (float)rng.NextDouble() * 15f;
            r.sizeDelta = new Vector2(size, size);
            glimmers.Add(new Glimmer
            {
                Rect = r,
                Img = img,
                Seed = (float)rng.NextDouble(),
                Speed = 0.10f + (float)rng.NextDouble() * 0.12f,
                Size = size,
                X0 = ((float)rng.NextDouble() - 0.5f) * (FrameW + 14f),
                Sway = 3f + (float)rng.NextDouble() * 7f,
                SwaySpeed = 0.8f + (float)rng.NextDouble() * 1.4f,
            });
        }
    }

    private static void TickAmbient()
    {
        float t = Time.unscaledTime;
        bool showGlimmers = !animating || (pendingKey != AdvancedMaterials.EncapsulationOrb && pendingKey != AdvancedMaterials.AttunementOrb);
        if (slotGlow != null)
        {
            slotGlow.color = new Color(0.62f, 0.35f, 1f, 0.26f + 0.16f * Mathf.Sin(t * 2.2f));
        }
        foreach (Glimmer g in glimmers)
        {
            if (g.Rect == null)
            {
                continue;
            }
            float u = (t * g.Speed + g.Seed) % 1f;
            float y = Mathf.Lerp(-FrameH * 0.5f - 6f, FrameH * 0.5f + 6f, u);
            float x = g.X0 + Mathf.Sin(t * g.SwaySpeed + g.Seed * 20f) * g.Sway;
            g.Rect.anchoredPosition = new Vector2(x, y);
            float twinkle = 0.55f + 0.45f * Mathf.Sin(t * 5.5f + g.Seed * 40f);
            float fade = Mathf.Pow(Mathf.Sin(u * Mathf.PI), 0.8f);
            g.Img.color = new Color(1f, 0.92f, 1f, showGlimmers ? fade * twinkle : 0f);
            g.Rect.localScale = Vector3.one * (0.55f + 0.65f * twinkle);
            g.Rect.localRotation = Quaternion.Euler(0, 0, t * 20f + g.Seed * 90f);
        }
    }
}
