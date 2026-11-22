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
    private static Image SlotEffect(string name, Sprite sprite, Vector2 position, Vector2 size, float rotation, Color color)
    {
        Image image = CraftingWidgets.MakeImage(Content, name, color);
        image.sprite = sprite;
        image.raycastTarget = false;
        RectTransform rect = image.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        rect.localRotation = Quaternion.Euler(0, 0, rotation);
        dynamic.Add(image.gameObject);
        return image;
    }

    private static readonly List<(Image Band, int Side, int Offset)> inputFlames = new();
    private static readonly List<Image> resultFlames = new();
    private static readonly List<(Image Band, Image Heat, Vector2 Center, int Side)> consumeFlames = new();
    private static Image fossilShell;
    private static Image collisionLight;

    private static Image FireBand(string name, Vector2 position, Vector2 size, float rotation)
    {
        Image band = SlotEffect(name, FireFrame(0), position, size, rotation, Color.clear);
        Material emission = Items_AdvancedForgeWindow.Additive();
        if (emission != null)
        {
            band.material = emission;
        }
        return band;
    }

    private static Color FireTint(int side, float alpha)
    {
        Color color = side == 0 ? new Color(1f, 0.56f, 0.19f) : new Color(0.48f, 1f, 0.30f);
        color.a = alpha;
        return color;
    }

    private static void AddInputFlames(int side, float x, float y)
    {
        float centerY = -(y + RecombKit.ItemH * 0.5f);
        for (int edge = 0; edge < 2; edge++)
        {
            float borderX = edge == 0 ? x - 29f : x + RecombKit.InW + 29f;
            Image band = FireBand("InputBorderFire", new Vector2(borderX, centerY),
                new Vector2(RecombKit.ItemH + 12f, 47f), edge == 0 ? 90f : -90f);
            inputFlames.Add((band, side, edge * 23 + side * 17));
        }
        if (pouring)
        {
            Vector2 center = new(x + RecombKit.InW * 0.5f, centerY);
            Image heat = FireBand("ItemConsumeHeat", center, new Vector2(RecombKit.InW, RecombKit.ItemH), 180f);
            Image consume = FireBand("ItemConsumeFire", center, new Vector2(RecombKit.InW, RecombKit.ItemH), 0f);
            consumeFlames.Add((consume, heat, center, side));
        }
    }

    private static void TickInputFlames(float now)
    {
        foreach (var flame in inputFlames)
        {
            float occupied = pouring ? RecombKit.InputAlpha(flame.Side, now) : 1f;
            flame.Band.sprite = FireFrame((int)(now * 48f) + flame.Offset);
            float consumed = 1f - occupied;
            float flare = Mathf.Sin(consumed * Mathf.PI);
            flame.Band.color = FireTint(flame.Side, Mathf.Clamp01(0.78f * occupied + 0.8f * flare));
            RectTransform rect = flame.Band.rectTransform;
            rect.sizeDelta = new Vector2(RecombKit.ItemH + 12f, 47f + 26f * flare);
        }
        foreach (var flame in consumeFlames)
        {
            float ignite = flame.Side == 0 ? RecombKit.DevourLeftAt : RecombKit.DevourRightAt;
            float elapsed = now - pourStart - ignite;
            float ignition = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.06f, 0.29f, elapsed));
            float burnout = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.78f, 1.22f, elapsed));
            float intensity = pouring ? ignition * (1f - burnout) : 0f;
            int frame = (int)(now * 60f) + flame.Side * 29;

            // Opposing flame layers cover the top and bottom of the item opening at a fixed size.
            flame.Band.sprite = FireFrame(frame);
            flame.Band.color = FireTint(flame.Side, intensity);
            flame.Heat.sprite = FireFrame(frame + 23);
            float heat = intensity * (1f - 0.55f * burnout);
            flame.Heat.color = flame.Side == 0
                ? new Color(1f, Mathf.Lerp(0.31f, 0.12f, burnout), 0.07f, heat)
                : new Color(Mathf.Lerp(0.37f, 0.10f, burnout), 1f, 0.12f, heat);
        }
    }

    private const int FireFrameCount = 96;
    private static Sprite[] fireFrames;

    // A looping fire field. Heat advects away from the root while different frequencies break up the plume.
    // The geometry stays anchored, so the motion comes from the fire texture itself.
    private static Sprite FireFrame(int index)
    {
        if (fireFrames == null)
        {
            const int width = 96, height = 64, columns = 12;
            var atlas = new Texture2D(width * columns, height * (FireFrameCount / columns), TextureFormat.RGBA32, false);
            atlas.wrapMode = TextureWrapMode.Clamp;
            atlas.filterMode = FilterMode.Bilinear;
            atlas.hideFlags = HideFlags.HideAndDontSave;
            var pixels = new Color32[atlas.width * atlas.height];
            fireFrames = new Sprite[FireFrameCount];
            for (int frame = 0; frame < FireFrameCount; frame++)
            {
                int ox = frame % columns * width;
                int oy = frame / columns * height;
                float t = frame * 2f * Mathf.PI / FireFrameCount;
                float driftX = Mathf.Cos(t) * 1.2f;
                float driftY = Mathf.Sin(t) * 1.2f;
                for (int y = 1; y < height - 1; y++)
                {
                    float v = y / (height - 1f);
                    for (int x = 1; x < width - 1; x++)
                    {
                        float u = x / (width - 1f);
                        // Sample a looping domain so the turbulence wraps without an animation seam.
                        float fold = Mathf.PerlinNoise(u * 7f + driftX + 8f, v * 4f + driftY + 8f);
                        float detail = Mathf.PerlinNoise(u * 25f + fold * 2f + driftX + 16f,
                            v * 10f + driftY + 16f);
                        float plume = Mathf.PerlinNoise(u * 12f + detail * 1.8f + driftX * 1.6f + 24f,
                            v * 7f + driftY * 1.6f + 24f);
                        float rise = (plume - 0.5f) * (0.13f + 0.29f * v);
                        float heat = Mathf.Clamp01(0.82f - v * 1.30f + (fold - 0.5f) * 0.70f
                            + (detail - 0.5f) * 0.19f + rise);
                        float edge = Mathf.SmoothStep(0f, 1f, Mathf.Min(u, 1f - u) * 11f)
                            * Mathf.SmoothStep(0f, 1f, v * 12f);
                        // Low heat leaves a translucent fringe. Fine folds break up the luminous interior.
                        float fringe = Mathf.SmoothStep(0f, 1f, heat * 1.18f);
                        float filament = Mathf.SmoothStep(0f, 1f, (detail - 0.34f) * 2.5f);
                        byte alpha = (byte)(255f * fringe * edge * Mathf.Lerp(0.77f, 1f, filament));
                        byte core = (byte)Mathf.Lerp(55f, 255f, Mathf.Clamp01(heat * (0.76f + filament * 0.54f)));
                        pixels[(oy + y) * atlas.width + ox + x] = new Color32(core, core, core, alpha);
                    }
                }
            }
            atlas.SetPixels32(pixels);
            atlas.Apply(false, true);
            for (int frame = 0; frame < FireFrameCount; frame++)
            {
                Rect bounds = new(frame % columns * width, frame / columns * height, width, height);
                fireFrames[frame] = Sprite.Create(atlas, bounds, new Vector2(0.5f, 0.5f), 100f);
                fireFrames[frame].hideFlags = HideFlags.HideAndDontSave;
            }
        }
        return fireFrames[index % FireFrameCount];
    }

    private static void AddResultFlames()
    {
        for (int i = 0; i < 4; i++)
        {
            resultFlames.Add(FireBand("ResultBorderFire", Vector2.zero, Vector2.zero, 0f));
        }
        if (pouring)
        {
            collisionLight = SlotEffect("SoulFireContact", OrbArt.Named("glow_violet"),
                new Vector2(CenterX, -(ResY + IconH * 0.5f)), Vector2.zero, 0f, Color.clear);
            Material emission = Items_AdvancedForgeWindow.Additive();
            if (emission != null)
            {
                collisionLight.material = emission;
            }
        }
        if (pouring && pourBones)
        {
            fossilShell = SlotEffect("FossilShell", OrbArt.Named("primordial_reliquary"),
                new Vector2(CenterX, -(ResY + IconH * 0.5f)), Vector2.zero, 0f, Color.clear);
        }
    }

    private static void TickResultFlames(float now)
    {
        float elapsed = now - pourStart;
        float ignition = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(RecombKit.CrossFlowAt, RecombKit.PourAt, elapsed));
        float convergence = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(RecombKit.PourAt - 0.22f, RecombKit.RevealAt - 0.05f, elapsed));
        Vector2 center = new(CenterX, -(ResY + IconH * 0.5f));
        float rim = IconW * 0.5f + 29f;
        for (int i = 0; i < resultFlames.Count; i++)
        {
            int side = i / 2;
            float sign = side == 0 ? -1f : 1f;
            bool jet = i % 2 == 1;
            Image band = resultFlames[i];
            RectTransform rect = band.rectTransform;
            float reach = Mathf.Lerp(42f, rim * 1.55f, convergence);
            rect.anchoredPosition = center + new Vector2(sign * (jet ? rim - reach * 0.5f : rim + 4f), 0f);
            rect.localRotation = Quaternion.Euler(0f, 0f, sign * (jet ? 90f : -90f));
            rect.sizeDelta = jet ? new Vector2(IconH * 0.70f, reach) : new Vector2(IconH + 6f, 55f);
            float alpha = jet ? pouring ? ignition * 0.95f : 0f
                : pouring ? ignition * 0.8f : lastFailed ? 0.48f : 0.88f;
            band.sprite = FireFrame((int)(now * (jet ? 60f : 48f)) + i * 19);
            band.color = FireTint(side, alpha);
        }
        if (collisionLight != null)
        {
            float contact = pouring ? Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.52f, 1f, convergence)) : 0f;
            collisionLight.rectTransform.sizeDelta = Vector2.Lerp(new Vector2(32f, 70f), new Vector2(155f, 138f), contact);
            collisionLight.color = new Color(1f, 0.89f, 0.59f, contact * 0.58f);
        }
    }

    private static void TickFossilShell(float now)
    {
        if (fossilShell == null || !pouring)
        {
            return;
        }

        float elapsed = now - pourStart;
        float petrify = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(RecombKit.PourAt - 0.3f, RecombKit.RevealAt - 0.21f, elapsed));
        float fracture = Mathf.InverseLerp(RecombKit.RevealAt - 0.18f, RecombKit.RevealAt, elapsed);
        float heat = Mathf.Pow(fracture, 2f);
        float size = Mathf.Lerp(42f, 110f, petrify) + Mathf.Sin(elapsed * 63f) * fracture * 4f;
        fossilShell.rectTransform.sizeDelta = new Vector2(size, size * 1.08f);
        fossilShell.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(elapsed * 72f) * fracture * 5f);
        Color stone = Color.Lerp(new Color(0.56f, 0.49f, 0.39f), new Color(1f, 0.91f, 0.69f), petrify);
        stone = Color.Lerp(stone, new Color(1f, 0.63f, 0.30f), heat);
        stone.a = petrify;
        fossilShell.color = stone;
    }

    private static void BurstResultEmbers()
    {
        Vector2 center = new(CenterX, -(ResY + IconH * 0.5f));
        float now = Time.unscaledTime;
        bool fossil = pourBones;
        int count = fossil ? 44 : 28;
        for (int i = 0; i < count; i++)
        {
            float angle = i * 2.39996f;
            float speed = UnityEngine.Random.Range(115f, fossil ? 450f : 325f);
            bool bone = fossil && i % 5 == 0;
            Color tint = bone ? new Color(0.94f, 0.84f, 0.65f) : i % 2 == 0
                ? new Color(1f, 0.56f, 0.14f) : new Color(0.56f, 1f, 0.29f);
            Piece ember = NewSpark(center, center, now, UnityEngine.Random.Range(0.45f, 0.95f), tint, bone ? 20f : 8f);
            ember.Go.GetComponent<Image>().sprite = OrbArt.Named(bone ? "primordial_rib" : "sparkle_star");
            ember.Velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed;
            ember.Gravity = bone ? 340f : 100f;
            ember.Spin = bone ? 280f : 90f;
        }
    }
}
