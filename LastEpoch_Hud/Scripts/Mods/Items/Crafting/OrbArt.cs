using System;
using System.Collections.Generic;
using UnityEngine;

namespace LastEpoch_Hud.Scripts.Mods.Items.Crafting;

/// <summary>
/// Draws the crafting orbs. Each orb is a glass sphere in the dark, worn style of Path of Exile's currency: a deep
/// coloured core, a bright rim of light on the lit side, a dark edge, swirls of mist inside and a hard white glint.
/// </summary>
internal static class OrbArt
{
    private const int Size = 160;
    private static readonly Dictionary<string, Sprite> Cache = new();

    public static Sprite Get(string key, Color core, Color glow)
    {
        if (Cache.TryGetValue(key, out Sprite cached) && cached != null)
        {
            return cached;
        }
        Sprite sprite = Draw(core, glow);
        Cache[key] = sprite;
        return sprite;
    }

    private static float Hash(float x, float y)
    {
        float v = Mathf.Sin(x * 127.1f + y * 311.7f) * 43758.5453f;
        return v - Mathf.Floor(v);
    }

    private static float Noise(float x, float y)
    {
        float xi = Mathf.Floor(x), yi = Mathf.Floor(y);
        float xf = x - xi, yf = y - yi;
        float u = xf * xf * (3 - 2 * xf), v = yf * yf * (3 - 2 * yf);
        float a = Hash(xi, yi), b = Hash(xi + 1, yi), c = Hash(xi, yi + 1), d = Hash(xi + 1, yi + 1);
        return Mathf.Lerp(Mathf.Lerp(a, b, u), Mathf.Lerp(c, d, u), v);
    }

    private static Sprite Draw(Color core, Color glow)
    {
        var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        float c = (Size - 1) / 2f;
        float radius = Size * 0.43f;
        var pixels = new Color[Size * Size];
        Vector3 light = new Vector3(-0.45f, 0.6f, 0.65f).normalized;
        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                float dx = (x - c) / radius, dy = (y - c) / radius;
                float d2 = dx * dx + dy * dy;
                float d = Mathf.Sqrt(d2);
                Color px = new Color(0, 0, 0, 0);
                // a soft halo outside the sphere
                if (d > 1f)
                {
                    float halo = Mathf.Clamp01(1f - (d - 1f) / 0.22f);
                    halo *= halo * 0.45f;
                    px = new Color(glow.r * 0.6f, glow.g * 0.6f, glow.b * 0.6f, halo);
                    pixels[y * Size + x] = px;
                    continue;
                }
                float z = Mathf.Sqrt(Mathf.Max(0f, 1f - d2));
                var normal = new Vector3(dx, -dy, z);
                float diffuse = Mathf.Clamp01(Vector3.Dot(normal, light));
                // mist swirling inside the glass
                float swirl = Noise(dx * 3.2f + 5f, dy * 3.2f + 9f) * 0.6f + Noise(dx * 7f + dy * 2f, dy * 7f - dx * 2f) * 0.4f;
                float mist = Mathf.Clamp01(swirl * 1.25f - 0.15f) * (0.5f + 0.5f * z);
                Color body = Color.Lerp(core * 0.28f, core, Mathf.Clamp01(diffuse * 0.7f + mist * 0.55f));
                body = Color.Lerp(body, glow, mist * 0.35f * (1f - d));
                // light caught under the lower rim, and a darkened edge
                float underGlow = Mathf.Pow(Mathf.Clamp01(dy * 0.9f + 0.2f), 3f) * (1f - z) * 0.5f;
                body += glow * underGlow;
                float edge = Mathf.Pow(d, 3.2f);
                body = Color.Lerp(body, new Color(0.01f, 0.01f, 0.015f), edge * 0.78f);
                // hard glint, and a long soft one
                Vector2 g1 = new Vector2(dx + 0.38f, dy - 0.42f);
                float glint = Mathf.Clamp01(1f - g1.magnitude / 0.17f);
                glint = glint * glint * glint;
                Vector2 g2 = new Vector2((dx - 0.12f) / 1.6f + 0.2f, (dy + 0.55f) / 0.6f);
                float soft = Mathf.Clamp01(1f - g2.magnitude / 0.45f) * 0.25f;
                body += Color.white * (glint * 0.95f + soft * 0.4f);
                // the very rim: a thin dark ring so it reads against any background
                float ring = Mathf.SmoothStep(0.94f, 1f, d);
                body = Color.Lerp(body, new Color(0.02f, 0.02f, 0.02f), ring);
                float alpha = 1f - Mathf.SmoothStep(0.985f, 1f, d);
                pixels[y * Size + x] = new Color(Mathf.Clamp01(body.r), Mathf.Clamp01(body.g), Mathf.Clamp01(body.b), alpha);
            }
        }
        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), 100f);
    }
}
