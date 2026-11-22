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
    private sealed class CrackSeg
    {
        public Image Glow, Dark, Core;
        public float At;
    }

    private static readonly List<CrackSeg> cracks = new();
    private static Image crackWash;
    private static float crackStart;
    private static Sprite crackBlade;

    private static Sprite CrackBlade()
    {
        if (crackBlade != null)
        {
            return crackBlade;
        }

        const int width = 64, height = 16;
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;
        texture.hideFlags = HideFlags.HideAndDontSave;
        var pixels = new Color32[width * height];
        for (int x = 0; x < width; x++)
        {
            float u = (x + 0.5f) / width;
            float taper = Mathf.Min(u * 6f, (1f - u) * 2.5f);
            for (int y = 0; y < height; y++)
            {
                float distance = Mathf.Abs(y + 0.5f - height * 0.5f);
                byte alpha = (byte)(255f * Mathf.Clamp01(taper * height * 0.5f - distance + 0.35f));
                pixels[y * width + x] = new Color32(255, 255, 255, alpha);
            }
        }
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        crackBlade = Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f);
        crackBlade.hideFlags = HideFlags.HideAndDontSave;
        return crackBlade;
    }

    private static void AddCrack(Vector2 from, Vector2 to, float at)
    {
        Vector2 mid = (from + to) / 2;
        Vector2 d = to - from;
        float angle = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
        Image glow = CraftingWidgets.MakeImage(Content, "CrackGlow", new Color(0.58f, 0.32f, 0.95f, 0f));
        Image dark = CraftingWidgets.MakeImage(Content, "Crack", new Color(0.03f, 0.02f, 0.02f, 0f));
        Image core = CraftingWidgets.MakeImage(Content, "CrackCore", new Color(0.92f, 0.77f, 1f, 0f));
        foreach ((Image img, float thick) in new[] { (glow, 10f), (dark, 5f), (core, 2.3f) })
        {
            img.sprite = CrackBlade();
            img.raycastTarget = false;
            RectTransform r = img.rectTransform;
            r.anchorMin = r.anchorMax = new Vector2(0, 1);
            r.pivot = new Vector2(0.5f, 0.5f);
            r.anchoredPosition = mid;
            r.sizeDelta = new Vector2(d.magnitude + 2, thick);
            r.localRotation = Quaternion.Euler(0, 0, angle);
        }
        cracks.Add(new CrackSeg { Glow = glow, Dark = dark, Core = core, At = at });
    }

    /// <summary>The recombination failed: the result is shown as it is, cracking across its box.</summary>
    private static void SpawnCrack()
    {
        Vector2 centre = new(CenterX, -(ResY + IconH / 2));
        float now = Time.unscaledTime;
        crackStart = now;
        crushAt = now; // the same short jolt as a crushed bone
        float halfW = IconW / 2f - 3, halfH = IconH / 2f - 3;
        Vector2[] seam =
        {
            new(-halfW, halfH * 0.72f), new(-halfW * 0.55f, halfH * 0.44f),
            new(-halfW * 0.63f, halfH * 0.20f), new(-halfW * 0.05f, halfH * 0.04f),
            new(halfW * 0.16f, -halfH * 0.29f), new(halfW * 0.57f, -halfH * 0.35f),
            new(halfW * 0.45f, -halfH * 0.63f), new(halfW, -halfH * 0.84f),
        };
        for (int i = 1; i < seam.Length; i++)
        {
            AddCrack(centre + seam[i - 1], centre + seam[i], now + (i - 1) * 0.025f);
        }

        AddCrack(centre + seam[2], centre + new Vector2(-halfW, -halfH * 0.13f), now + 0.08f);
        Vector2 branch = new(halfW * 0.21f, halfH * 0.47f);
        AddCrack(centre + seam[3], centre + branch, now + 0.10f);
        AddCrack(centre + branch, centre + new Vector2(halfW * 0.66f, halfH * 0.65f), now + 0.13f);
        AddCrack(centre + seam[5], centre + new Vector2(halfW, -halfH * 0.13f), now + 0.16f);
        AddCrack(centre + seam[1], centre + new Vector2(-halfW * 0.26f, halfH), now + 0.08f);
        AddCrack(centre + seam[6], centre + new Vector2(halfW * 0.12f, -halfH), now + 0.18f);
        crackWash = CraftingWidgets.MakeImage(Content, "CrackWash", new Color(0.34f, 0.20f, 0.54f, 0f));
        crackWash.sprite = OrbArt.Named("glow_violet");

        crackWash.raycastTarget = false;
        RectTransform wr = crackWash.rectTransform;
        wr.anchorMin = wr.anchorMax = new Vector2(0, 1);
        wr.pivot = new Vector2(0.5f, 0.5f);
        wr.sizeDelta = new Vector2(IconW + 92f, IconH + 70f);
        wr.anchoredPosition = centre;
        // Stone chips and violet sparks erupt through the seam.
        for (int k = 0; k < 32; k++)
        {
            float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2);
            float speed = UnityEngine.Random.Range(95f, 340f);
            Piece flake = NewSpark(centre, centre, now + 0.1f, UnityEngine.Random.Range(0.5f, 1f), new Color(0.55f, 0.5f, 0.45f), UnityEngine.Random.Range(3f, 6f));
            if (k % 3 == 0)
            {
                Image spark = flake.Go.GetComponent<Image>();
                spark.sprite = OrbArt.Named("sparkle_star");
                spark.color = new Color(0.83f, 0.58f, 1f);
            }
            flake.Velocity = new Vector2(Mathf.Cos(angle) * speed, Mathf.Sin(angle) * speed * 0.6f);
            flake.Gravity = 520f;
            flake.Spin = UnityEngine.Random.Range(-300f, 300f);
        }
    }

    private static void TickCracks(float now)
    {
        if (cracks.Count == 0 && crackWash == null)
        {
            return;
        }
        float age = now - crackStart;
        float fade = age < 2.2f ? 1f : Mathf.Clamp01(1f - (age - 2.2f) / 0.6f);
        foreach (CrackSeg c in cracks)
        {
            if (c.Dark == null)
            {
                continue;
            }
            float on = now >= c.At ? 1f : 0f;
            Color d = c.Dark.color;
            d.a = 0.95f * on * fade;
            c.Dark.color = d;
            Color g = c.Glow.color;
            float heat = Mathf.Exp(-2.6f * Mathf.Max(0f, now - c.At));
            g.a = on * fade * (0.24f + 0.65f * heat);
            c.Glow.color = g;
            Color core = c.Core.color;
            core.a = on * fade * (0.16f + 0.84f * heat);
            c.Core.color = core;
        }
        if (crackWash != null)
        {
            Color w = crackWash.color;
            w.a = 0.7f * Mathf.Pow(Mathf.Clamp01(1f - age / 0.6f), 2f);
            crackWash.color = w;
        }
        if (age > 2.9f)
        {
            ClearCracks();
        }
    }

    private static void ClearCracks()
    {
        foreach (CrackSeg c in cracks)
        {
            if (c.Glow != null) Object.Destroy(c.Glow.gameObject);
            if (c.Dark != null) Object.Destroy(c.Dark.gameObject);
            if (c.Core != null) Object.Destroy(c.Core.gameObject);
        }
        cracks.Clear();
        if (crackWash != null)
        {
            Object.Destroy(crackWash.gameObject);
            crackWash = null;
        }
    }
}
