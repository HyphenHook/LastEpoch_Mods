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
    private static Image obscuredSand;
    private const float VeilFadeTime = 0.65f;
    private static float obscuredSandBorn;
    private static Sprite[] chaosEruption;
    private static readonly List<(Image Image, RectTransform Rect, string Key, float Born, int Index, int Emitted)> orbSeals = new();

    private static void ShowObscuredSand(float now, bool fadeIn)
    {
        Sprite mist = OrbArt.Named("orb_memory_sand");
        if (itemRect == null || obscuredSand != null || mist == null) return;
        obscuredSandBorn = fadeIn ? now : now - VeilFadeTime;
        obscuredSand = CraftingWidgets.MakeImage(itemRect, "ObscuredSand", ObscuredSandColor(now));
        obscuredSand.sprite = mist;
        obscuredSand.raycastTarget = false;
        CraftingWidgets.Stretch(obscuredSand.rectTransform, 0f);
    }

    private static Color ObscuredSandColor(float now)
    {
        float fade = Ease(Mathf.Clamp01((now - obscuredSandBorn) / VeilFadeTime));
        return new Color(1f, 1f, 1f, fade * (0.47f + 0.035f * Mathf.Sin(now * 1.8f)));
    }

    private static Color OrbTone(string key) => key switch
    {
        AdvancedMaterials.EncapsulationOrb => new Color(0.44f, 0.79f, 1f),
        AdvancedMaterials.ReminiscenceOrb => new Color(0.80f, 0.57f, 0.98f),
        AdvancedMaterials.EchoOrb => new Color(0.38f, 1f, 0.87f),
        AdvancedMaterials.ReverieOrb => new Color(1f, 0.93f, 1f),
        AdvancedMaterials.ChaosOrb => new Color(1f, 0.39f, 0.12f),
        AdvancedMaterials.AttunementOrb => new Color(0.73f, 0.42f, 1f),
        _ => Color.white,
    };

    private static void StartOrbSeal(string key, float delay = 0f)
    {
        string spriteName = key switch
        {
            AdvancedMaterials.EncapsulationOrb => "orb_chain_kunai",
            AdvancedMaterials.ReminiscenceOrb => "orb_memory_sand",
            AdvancedMaterials.ChaosOrb => "orb_chaos_eruption_sheet",
            _ => "ring_violet",
        };
        Sprite sprite = key == AdvancedMaterials.ChaosOrb
            ? (chaosEruption ??= OrbArt.Sheet(spriteName, 4, 4))[0]
            : OrbArt.Named(spriteName);
        if (sprite == null) return;

        int layers = key == AdvancedMaterials.ChaosOrb ? 1 : key == AdvancedMaterials.EncapsulationOrb ? 2 : 3;
        for (int i = 0; i < layers; i++)
        {
            Image image = CentredImage("OrbImpactSeal", sprite, 100f, 100f, out RectTransform rect, glow: key == AdvancedMaterials.ReverieOrb);
            image.color = Color.clear;
            if (key == AdvancedMaterials.ReminiscenceOrb) image.preserveAspect = false;
            rect.anchoredPosition = SlotCentre;
            orbSeals.Add((image, rect, key, Time.unscaledTime + delay + i * 0.044f, i, 0));
        }
    }

    private const float ChainTravelEnd = 0.51f;
    private static Sprite chainEmber;

    private static readonly Color[] ChainSparkColors =
    {
        new(1f, 0.91f, 0.36f),
        new(1f, 0.50f, 0.15f),
        new(0.70f, 0.94f, 0.29f),
    };

    private static Vector2 ChainTip(Vector2 top, Vector2 bottom, float travel)
        => Vector2.Lerp(top, bottom, Mathf.Lerp(0.15f, 1f, Ease(travel)));

    private static Sprite ChainEmber()
    {
        if (chainEmber != null) return chainEmber;
        const int width = 24, height = 8;
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var pixels = new Color32[width * height];
        for (int x = 0; x < width; x++)
        {
            float along = (x + 0.5f) / width;
            float intensity = along * (1f - Ease(Mathf.InverseLerp(0.80f, 1f, along)));
            float radius = Mathf.Lerp(0.20f, 0.92f, along);
            for (int y = 0; y < height; y++)
            {
                float across = Mathf.Abs((y + 0.5f) * 2f / height - 1f);
                float edge = 1f - Ease(Mathf.Clamp01(across / radius));
                pixels[y * width + x] = new Color32(255, 255, 255, (byte)(intensity * edge * 255f));
            }
        }
        texture.SetPixels32(pixels);
        texture.Apply();
        chainEmber = Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 100f);
        return chainEmber;
    }

    private static int EmitChainSparks(Vector2 top, Vector2 bottom, float travel, int emitted, int chainIndex)
    {
        const int sparksPerChain = 24;
        int due = Mathf.FloorToInt(travel * sparksPerChain);
        if (emitted >= due) return emitted;

        Vector2 forward = (bottom - top).normalized;
        Vector2 across = new(-forward.y, forward.x);
        // Emit at sampled tip positions to keep the trail continuous through slow frames.
        for (int i = emitted + 1; i <= due; i++)
        {
            Color color = ChainSparkColors[(i + chainIndex) % ChainSparkColors.Length];
            Vector2 tip = ChainTip(top, bottom, (float)i / sparksPerChain);
            Vector2 velocity = -forward * UnityEngine.Random.Range(75f, 190f)
                + across * UnityEngine.Random.Range(-125f, 125f);
            AddSpark(tip, color, UnityEngine.Random.Range(9f, 22f), velocity, UnityEngine.Random.Range(0.13f, 0.27f), ember: true);
        }
        return due;
    }

    private static void TickOrbSeals(float now)
    {
        for (int i = orbSeals.Count - 1; i >= 0; i--)
        {
            var seal = orbSeals[i];
            float elapsed = now - seal.Born;
            float duration = seal.Key switch
            {
                AdvancedMaterials.ReminiscenceOrb => 1.32f,
                AdvancedMaterials.ChaosOrb => 1.08f,
                _ => 0.94f,
            };
            if (seal.Image == null || elapsed >= duration)
            {
                if (seal.Rect != null)
                {
                    effects.Remove(seal.Rect.gameObject);
                    Object.Destroy(seal.Rect.gameObject);
                }
                orbSeals.RemoveAt(i);
                continue;
            }

            if (elapsed < 0f) continue;
            float phase = Mathf.Clamp01(elapsed / duration);
            float alpha = Mathf.Clamp01(elapsed / 0.12f) * (1f - Mathf.InverseLerp(0.66f, 1f, phase));
            if (seal.Key == AdvancedMaterials.EncapsulationOrb)
            {
                float side = seal.Index == 0 ? -1f : 1f;
                float travel = Mathf.InverseLerp(0f, ChainTravelEnd, phase);
                Vector2 top = SlotCentre + new Vector2(side * (ItemW * 0.5f + 6f), ItemH * 0.5f + 10f);
                Vector2 bottom = SlotCentre + new Vector2(-side * (ItemW * 0.5f + 6f), -ItemH * 0.5f - 10f);
                Vector2 tip = ChainTip(top, bottom, travel);
                Vector2 chain = tip - top;
                seal.Rect.anchoredPosition = (top + tip) * 0.5f;
                seal.Rect.sizeDelta = new Vector2(chain.magnitude, 82f);
                seal.Rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(chain.y, chain.x) * Mathf.Rad2Deg);
                seal.Emitted = EmitChainSparks(top, bottom, travel, seal.Emitted, seal.Index);
                orbSeals[i] = seal;
            }
            else if (seal.Key == AdvancedMaterials.ChaosOrb)
            {
                int frame = Mathf.Clamp((int)(phase * chaosEruption.Length), 0, chaosEruption.Length - 1);
                seal.Image.sprite = chaosEruption[frame];
                seal.Rect.anchoredPosition = SlotCentre + Vector2.up * 26f;
                seal.Rect.sizeDelta = new Vector2(239f, 265f);
            }
            else if (seal.Key == AdvancedMaterials.ReminiscenceOrb)
            {
                float bloom = Ease(Mathf.InverseLerp(0f, 0.34f, phase));
                float drift = Ease(Mathf.InverseLerp(0.29f, 1f, phase));
                float side = seal.Index == 0 ? 0f : seal.Index == 1 ? -1f : 1f;
                seal.Rect.anchoredPosition = SlotCentre + new Vector2(side * (8f * bloom + 59f * drift), -10f + 10f * bloom + 67f * drift);
                seal.Rect.sizeDelta = new Vector2(
                    ItemW * (Mathf.Lerp(0.56f, 1.69f, bloom) + 0.23f * drift),
                    ItemH * (Mathf.Lerp(0.58f, 1.42f, bloom) + 0.19f * drift));
                seal.Rect.localRotation = Quaternion.Euler(0f, 0f, side * (10f + 14f * drift));
                float dissipation = Ease(Mathf.InverseLerp(0.33f, 1f, phase));
                alpha = Ease(Mathf.InverseLerp(0f, 0.21f, phase)) * (1f - dissipation) * (seal.Index == 0 ? 1.15f : 0.51f);
            }
            else
            {
                seal.Rect.anchoredPosition = SlotCentre;
                float size = Mathf.Lerp(45f, 185f, Ease(phase));
                seal.Rect.sizeDelta = new Vector2(size, size * 1.12f);
            }

            Color tint = seal.Key == AdvancedMaterials.ReverieOrb ? OrbTone(seal.Key) : Color.white;
            tint.a = alpha * 0.79f;
            seal.Image.color = tint;
        }
    }
}
