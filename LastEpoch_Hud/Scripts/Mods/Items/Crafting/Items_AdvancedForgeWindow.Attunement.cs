using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using R = LastEpoch_Hud.Scripts.Mods.Items.Crafting.Items_RecombinatorWindow;
using Object = UnityEngine.Object;

namespace LastEpoch_Hud.Scripts.Mods.Items.Crafting;

internal static partial class Items_AdvancedForgeWindow
{
    private sealed class AttunementFlipbook
    {
        public Image Current;
        public Image Next;
        public RectTransform Rect;
        public Sprite[] Frames;
        public float Start;
        public float Duration;
    }

    private sealed class EssenceThread
    {
        public Image Image;
        public RectTransform Rect;
        public float Start;
        public float Life;
        public float Phase;
        public float Radius;
        public bool Gold;
    }

    private static readonly Dictionary<string, Sprite[]> attunementFrames = new();
    private static readonly List<EssenceThread> essenceThreads = new();
    private static readonly Color EssenceGold = new(1f, 0.76f, 0.26f);
    private static readonly Color EssencePurple = new(0.69f, 0.35f, 1f);
    private static AttunementFlipbook attunementBurst;
    private static AttunementFlipbook attunementOutcome;
    private static AttunementItemWarp attunementWarp;
    private static Image attunementLight;
    private static RectTransform attunementLightRect;
    private static float attunementOutcomeAt;
    private static bool attunementPositive;

    private static Sprite[] AttunementFrames(string name)
    {
        if (attunementFrames.TryGetValue(name, out var cached)) return cached;
        Sprite sheet = OrbArt.Named(name);
        if (sheet == null) return System.Array.Empty<Sprite>();
        Texture2D original = sheet.texture;
        var pixels = original.GetPixels32().ToArray();
        // A cell-local feather keeps transparent fluid edges smooth through atlas filtering.
        for (int y = 0; y < original.height; y++)
        {
            float v = ((y + 0.5f) * 4f / original.height) % 1f;
            for (int x = 0; x < original.width; x++)
            {
                float u = ((x + 0.5f) * 4f / original.width) % 1f;
                double dx = (u - 0.5f) * 2f, dy = (v - 0.5f) * 2f;
                double edge = System.Math.Clamp((System.Math.Sqrt(dx * dx + dy * dy) - 0.72) / 0.27, 0, 1);
                double feather = 1 - edge * edge * (3 - 2 * edge);
                int index = y * original.width + x;
                Color32 color = pixels[index];
                color.a = (byte)(color.a * feather);
                pixels[index] = color;
            }
        }
        var texture = new Texture2D(original.width, original.height, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.HideAndDontSave,
        };
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        var frames = new Sprite[16];
        float width = texture.width / 4f, height = texture.height / 4f;
        for (int frame = 0; frame < frames.Length; frame++)
        {
            frames[frame] = Sprite.Create(texture,
                new Rect(frame % 4 * width, texture.height - (frame / 4 + 1) * height, width, height),
                new Vector2(0.5f, 0.5f), 100f);
            frames[frame].hideFlags = HideFlags.HideAndDontSave;
        }
        attunementFrames[name] = frames;
        return frames;
    }

    private static void WarmAttunementVfx()
    {
        foreach (string sheet in new[] { "attunement_burst_sheet", "attunement_negative_sheet", "attunement_positive_sheet" })
            AttunementFrames(sheet);
    }

    private static AttunementFlipbook AttunementLayer(string name, string sheet, Vector2 at, Vector2 size, float now, float duration)
    {
        Sprite[] frames = AttunementFrames(sheet);
        if (frames.Length == 0) return null;
        Image first = CentredImage(name, frames[0], size.x, size.y, out RectTransform rect);
        rect.anchoredPosition = at;
        first.preserveAspect = false;
        Image next = R.MakeImage(first.transform, "NextFrame", Color.clear);
        next.raycastTarget = false;
        next.preserveAspect = false;
        Stretch(next.rectTransform, 0f);
        return new AttunementFlipbook { Current = first, Next = next, Rect = rect, Frames = frames, Start = now, Duration = duration };
    }

    private static void StartAttunementBurst(float now)
    {
        attunementBurst = AttunementLayer("AttunementRupture", "attunement_burst_sheet",
            OrbPoint, new Vector2(240f, 240f), now, 0.80f);
        for (int i = 0; i < 26; i++)
        {
            float angle = i * 2.39996f;
            Vector2 direction = new(Mathf.Cos(angle), Mathf.Sin(angle));
            AddSpark(OrbPoint, i % 2 == 0 ? EssenceGold : EssencePurple, UnityEngine.Random.Range(9f, 17f),
                direction * UnityEngine.Random.Range(100f, 220f), UnityEngine.Random.Range(0.30f, 0.65f), ember: true);
        }
    }

    private static void StartAttunementOutcome(float now, bool positive)
    {
        attunementPositive = positive;
        attunementOutcomeAt = now;
        attunementOutcome = AttunementLayer("AttunementEssence",
            positive ? "attunement_positive_sheet" : "attunement_negative_sheet",
            SlotCentre, new Vector2(ItemW * 2.2f, ItemH * 1.6f), now, 2.30f);
        attunementLight = CentredImage("AttunementCoreLight", OrbArt.Named("glow_violet"),
            110f, 160f, out attunementLightRect, glow: true);
        attunementLightRect.anchoredPosition = SlotCentre;
        attunementLight.color = Color.clear;
        Transform iconTransform = itemRect?.Find("ItemIcon");
        if (!positive && iconTransform != null)
        {
            try { attunementWarp = new AttunementItemWarp(iconTransform.GetComponent<Image>()); }
            catch (System.Exception ex) { ErrorLog.Report(ex, "Attunement icon distortion"); }
        }
        for (int i = 0; i < 76; i++)
        {
            bool gold = positive ? i % 2 == 0 : i % 7 == 0;
            Image image = CentredImage("AttunementThread", ChainEmber(), 22f, 3f, out RectTransform rect, glow: true);
            image.color = Color.clear;
            essenceThreads.Add(new EssenceThread
            {
                Image = image,
                Rect = rect,
                Start = now + i * 0.017f,
                Life = 0.65f + i % 5 * 0.08f,
                Phase = i * 2.39996f,
                Radius = 76f + i % 8 * 7f,
                Gold = gold,
            });
        }
    }

    private static void TickAttunementLayer(AttunementFlipbook layer, float now, float alpha)
    {
        if (layer == null || layer.Current == null) return;
        float progress = Mathf.Clamp01((now - layer.Start) / layer.Duration);
        float frame = progress * (layer.Frames.Length - 1);
        int index = Mathf.FloorToInt(frame);
        int next = Mathf.Min(index + 1, layer.Frames.Length - 1);
        float blend = frame - index;
        layer.Current.sprite = layer.Frames[index];
        layer.Current.color = new Color(1f, 1f, 1f, alpha * (1f - blend));
        layer.Next.sprite = layer.Frames[next];
        layer.Next.color = new Color(1f, 1f, 1f, alpha * blend);
    }

    private static Vector2 EssencePosition(EssenceThread thread, float progress)
    {
        float angle = thread.Phase + progress * (attunementPositive ? 1.5f : 7f);
        float radius = thread.Radius * (attunementPositive ? 1f - Ease(progress) : Mathf.Lerp(1f, 0.36f, Ease(progress)));
        Vector2 orbit = new(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius * 1.22f);
        if (!attunementPositive && thread.Gold)
            orbit *= 1f + progress * 0.65f;
        float drift = attunementPositive ? 0f : Mathf.Sin(progress * Mathf.PI) * 13f;
        return SlotCentre + orbit + new Vector2(drift, (1f - progress) * 22f);
    }

    private static void TickAttunement(float now)
    {
        if (attunementBurst != null)
        {
            float age = now - attunementBurst.Start;
            float alpha = 0.90f * Mathf.Clamp01(age / 0.07f) * (1f - Mathf.InverseLerp(0.56f, 0.80f, age));
            attunementBurst.Rect.anchoredPosition = Vector2.Lerp(OrbPoint, SlotCentre + Vector2.up * 22f, Ease(Mathf.Clamp01(age / 0.8f)));
            TickAttunementLayer(attunementBurst, now, alpha);
        }
        if (attunementOutcome == null) return;
        float elapsed = now - attunementOutcomeAt;
        float envelope = Mathf.Clamp01(elapsed / 0.20f) * (1f - Mathf.InverseLerp(1.78f, 2.3f, elapsed));
        TickAttunementLayer(attunementOutcome, now, 0.86f * envelope);
        if (attunementWarp != null)
            attunementWarp.SetDistortion(Mathf.Sin(Mathf.Clamp01(elapsed / 2.2f) * Mathf.PI) * 0.92f, elapsed * 5.3f);
        if (attunementLight != null)
        {
            Color tone = attunementPositive ? EssenceGold : EssencePurple;
            tone.a = envelope * (attunementPositive ? 0.32f : 0.22f) * (0.8f + 0.2f * Mathf.Sin(elapsed * 8f));
            attunementLight.color = tone;
        }
        foreach (var thread in essenceThreads)
        {
            float age = now - thread.Start;
            if (thread.Image == null) continue;
            bool visible = age >= 0f && age < thread.Life;
            thread.Image.enabled = visible;
            if (!visible) continue;
            float progress = age / thread.Life;
            Vector2 position = EssencePosition(thread, progress);
            Vector2 tangent = EssencePosition(thread, Mathf.Min(1f, progress + 0.025f)) - position;
            thread.Rect.anchoredPosition = position;
            thread.Rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(tangent.y, tangent.x) * Mathf.Rad2Deg);
            Color color = thread.Gold ? EssenceGold : EssencePurple;
            float fade = Mathf.Clamp01(age / 0.10f) * (1f - Mathf.InverseLerp(0.70f, 1f, progress));
            color.a = fade * envelope * (!attunementPositive && thread.Gold ? 0.28f : 0.88f);
            thread.Image.color = color;
            thread.Rect.sizeDelta = new Vector2(16f + 15f * Mathf.Sin(progress * Mathf.PI), thread.Gold ? 2.5f : 3.3f);
        }
    }

    private static void ResetAttunementVfx()
    {
        if (attunementWarp != null)
        {
            attunementWarp.Dispose();
        }
        attunementWarp = null;
        attunementBurst = null;
        attunementOutcome = null;
        attunementLight = null;
        attunementLightRect = null;
        essenceThreads.Clear();
    }
}
