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
    // ---------------------------------------------------------------- the forging effect

    private static readonly List<GameObject> effects = new();
    private static bool animating;
    private static float animStart;
    private static string pendingKey;
    private static bool applied;
    private static Vector2 shakeBase;
    private const float OrbFlyEnd = 0.45f, VfxStart = 0.20f, ShatterAt = 0.80f;
    private const float ImpactAt = 0.98f;

    private static float EndAt => pendingKey == AdvancedMaterials.EchoOrb
        ? Mathf.Max(2.85f, echoRevealEndsAt - animStart) : pendingKey == AdvancedMaterials.AttunementOrb ? 3.65f : 2.48f;

    private static GameObject orbFx;
    private static Image orbFxImage, backGlow, bloom, ringFx;
    private static RectTransform orbFxRect, backGlowRect, bloomRect, ringRect;
    private static Vector2 orbStart;
    private static bool shatterDone;

    private sealed class Spark
    {
        public GameObject Go;
        public RectTransform Rect;
        public Image Img;
        public Vector2 Pos, Vel;
        public float Born, Life;
        public bool Ember;
    }

    private static readonly List<Spark> sparks = new();
    private static Vector2 SlotCentre => new(CenterX, -(SlotY + FrameH * ItemCentreFromTop));
    private static Vector2 OrbPoint => SlotCentre + Vector2.up * 74f;
    private static Material additive;
    private static bool additiveTried;

    /// <summary>An additive material for glows, if the game has a shader for it.</summary>
    internal static Material Additive()
    {
        if (!additiveTried)
        {
            additiveTried = true;
            foreach (string name in new[] { "Legacy Shaders/Particles/Additive", "Particles/Additive", "Mobile/Particles/Additive", "UI/Additive" })
            {
                try
                {
                    Shader shader = Shader.Find(name);
                    if (shader != null && shader.isSupported)
                    {
                        additive = new Material(shader);
                        Main.logger_instance?.Msg("Advanced Forge: additive glow with " + name);
                        break;
                    }
                }
                catch (Exception)
                {
                }
            }
            if (additive == null)
            {
                Main.logger_instance?.Msg("Advanced Forge: no additive shader found, glows use normal blending");
            }
        }
        return additive;
    }

    private static GameObject Effect(string name)
    {
        var go = new GameObject(name);
        var r = go.AddComponent<RectTransform>();
        r.anchorMin = r.anchorMax = new Vector2(0, 1);
        r.pivot = new Vector2(0, 1);
        r.anchoredPosition = Vector2.zero;
        r.sizeDelta = Vector2.zero;
        go.transform.SetParent(Content, false);
        effects.Add(go);
        return go;
    }

    private static Image CentredImage(string name, Sprite sprite, float w, float h, out RectTransform rect, bool glow = false)
    {
        GameObject go = Effect(name);
        rect = go.GetComponent<RectTransform>();
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(w, h);
        Image img = CraftingWidgets.MakeImage(go.transform, "Image", Color.white);
        img.sprite = sprite;
        img.preserveAspect = true;
        img.raycastTarget = false;
        CraftingWidgets.Stretch(img.rectTransform, 0);
        if (glow && Additive() != null)
        {
            img.material = additive;
        }
        return img;
    }

    private static void StartForgeFx(string key)
    {
        animating = true;
        applied = false;
        shatterDone = false;
        pendingKey = key;
        animStart = Time.unscaledTime;
        echoRevealEndsAt = animStart;
        status = "";
        CloseTooltips();

        // where the orb is lifted from: its own slot
        orbStart = OrbPoint + new Vector2(0, 150);
        foreach (OrbView v in orbViews)
        {
            if (v.Material.Key == key && v.Rect != null)
            {
                orbStart = v.Rect.anchoredPosition + new Vector2(OrbSize / 2, -OrbSize / 2);
            }
        }
        AdvancedMaterial m = AdvancedMaterials.All.Find(x => x.Key == key);

        shakeBase = new Vector2(CenterX - FrameW / 2, -SlotY);

        // a dark violet light behind the orb while it charges
        backGlow = CentredImage("BackGlow", OrbArt.Named("glow_violet"), 300f, 300f, out backGlowRect, glow: true);
        backGlow.color = new Color(0.6f, 0.3f, 1f, 0f);
        backGlowRect.anchoredPosition = OrbPoint;

        orbFxImage = CentredImage("FloatingOrb", OrbArt.Get(key, m.Tint, m.Glow), 104f, 104f, out orbFxRect);
        orbFxRect.anchoredPosition = orbStart;
        orbFx = orbFxRect.gameObject;

    }

    private static float Ease(float u) => u * u * (3f - 2f * u);

    private static void TickAnimation()
    {
        float now = Time.unscaledTime;
        if (obscuredSand != null)
        {
            obscuredSand.color = ObscuredSandColor(now);
        }
        TickMemoryMist(now);
        TickOrbSeals(now);
        TickOrbFragments(now);
        TickReverieScatter(now);
        TickEchoPulses(now);
        TickEchoStrands(now);
        TickSparks(now);
        TickAttunement(now);
        if (!animating)
        {
            return;
        }
        float t = now - animStart;

        // the orb rises out of its slot to its place over the item, and bobs there until it breaks
        if (orbFx != null)
        {
            float u = Mathf.Clamp01(t / OrbFlyEnd);
            Vector2 pos = Vector2.Lerp(orbStart, OrbPoint, Ease(u));
            if (t >= OrbFlyEnd)
            {
                pos.y += Mathf.Sin((t - OrbFlyEnd) * 5f) * 2.5f;
            }
            orbFxRect.anchoredPosition = pos;
            float charge = Mathf.Clamp01((t - VfxStart) / (ShatterAt - VfxStart));
            orbFxRect.localScale = Vector3.one * (0.7f + 0.3f * Ease(u) + 0.08f * charge);
            float fade = t < ShatterAt ? 1f : Mathf.Clamp01(1f - (t - ShatterAt) / 0.08f);
            orbFxImage.color = new Color(1f, 1f, 1f, fade);
            orbFx.SetActive(fade > 0f);

            // the dark light behind the orb pulses as it charges, and goes at the break
            float pulse = 0.85f + 0.30f * (0.5f + 0.5f * Mathf.Sin(t * 11f));
            float glowAlpha = t < ShatterAt ? 0.55f * Mathf.Clamp01((t - 0.05f) / 0.3f) : 0.55f * Mathf.Clamp01(1f - (t - ShatterAt) / 0.2f);
            Color chargeTone = OrbTone(pendingKey);
            chargeTone.a = glowAlpha;
            backGlow.color = chargeTone;
            backGlowRect.localScale = Vector3.one * pulse;
            backGlowRect.anchoredPosition = pos;
        }

        if (!shatterDone && t >= ShatterAt)
        {
            shatterDone = true;
            ShatterBurst(now);
        }
        if (!applied && t >= ImpactAt)
        {
            applied = true;
            Impact(now);
        }

        // the break: a bloom and a ring spread from the orb
        if (bloom != null)
        {
            float k = Mathf.Clamp01((t - ShatterAt) / 0.16f);
            Color burst = OrbTone(pendingKey);
            burst.a = 0.84f * (1f - k);
            bloom.color = burst;
            bloomRect.localScale = Vector3.one * (0.5f + 1.1f * k);
        }
        if (ringFx != null)
        {
            float k = Mathf.Clamp01((t - ShatterAt) / 0.40f);
            ringFx.color = new Color(1f, 1f, 1f, 0.85f * (1f - k) * (t >= ShatterAt ? 1f : 0f));
            ringRect.localScale = Vector3.one * (0.3f + 1.2f * Ease(k));
        }

        // the slot shudders a little at the break, and again as it takes the essence in
        if (slotRect != null)
        {
            Vector2 shake = Vector2.zero;
            float atBreak = t - ShatterAt, atMerge = t - ImpactAt;
            if (atBreak >= 0 && atBreak < 0.12f)
            {
                shake = new Vector2(UnityEngine.Random.Range(-1f, 1f), UnityEngine.Random.Range(-1f, 1f)) * 2.5f;
            }
            else if (atMerge >= 0 && atMerge < 0.35f)
            {
                shake = new Vector2(UnityEngine.Random.Range(-1f, 1f), UnityEngine.Random.Range(-1f, 1f)) * 3.5f * (1f - atMerge / 0.35f);
            }
            slotRect.anchoredPosition = shakeBase + shake;
        }

        if (t >= EndAt)
        {
            if (revealRerolledMemories) memoryRevealAt = now;
            EndAnimationNow();
            dirty = true;
        }
    }

    private static void AddSpark(Vector2 at, Color colour, float size, Vector2 vel, float life, bool ember = false)
    {
        GameObject go = Effect("Spark");
        RectTransform r = go.GetComponent<RectTransform>();
        r.pivot = new Vector2(0.5f, 0.5f);
        r.sizeDelta = ember ? new Vector2(size, UnityEngine.Random.Range(2.5f, 4.5f))
            : new Vector2(size, size * UnityEngine.Random.Range(1f, 2.4f));
        r.anchoredPosition = at;
        r.localRotation = Quaternion.Euler(0, 0, ember ? Mathf.Atan2(vel.y, vel.x) * Mathf.Rad2Deg : UnityEngine.Random.Range(0f, 90f));
        Image img = CraftingWidgets.MakeImage(go.transform, "S", colour);
        if (ember) img.sprite = ChainEmber();
        img.raycastTarget = false;
        CraftingWidgets.Stretch(img.rectTransform, 0);
        sparks.Add(new Spark { Go = go, Rect = r, Img = img, Pos = at, Vel = vel, Born = Time.unscaledTime, Life = life, Ember = ember });
    }

    private static void TickSparks(float now)
    {
        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
        for (int i = sparks.Count - 1; i >= 0; i--)
        {
            Spark s = sparks[i];
            float age = now - s.Born;
            if (s.Go == null || age >= s.Life)
            {
                if (s.Go != null)
                {
                    effects.Remove(s.Go);
                    Object.Destroy(s.Go);
                }
                sparks.RemoveAt(i);
                continue;
            }
            s.Vel.y -= 520f * dt;
            s.Pos += s.Vel * dt;
            s.Rect.anchoredPosition = s.Pos;
            float remaining = 1f - age / s.Life;
            if (s.Ember)
            {
                s.Rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(s.Vel.y, s.Vel.x) * Mathf.Rad2Deg);
                s.Rect.localScale = new Vector3(Mathf.Lerp(0.3f, 1f, remaining), 1f, 1f);
            }
            Color c = s.Img.color;
            c.a = remaining;
            s.Img.color = c;
        }
    }

    private static void EndAnimationNow()
    {
        ResetAttunementVfx();
        animating = false;
        revealRerolledMemories = false;
        foreach (GameObject go in effects)
        {
            if (go != null)
            {
                Object.Destroy(go);
            }
        }
        effects.Clear();
        memoryMist.Clear();
        orbSeals.Clear();
        orbFragments.Clear();
        reverieShards.Clear();
        echoPulses.Clear();
        echoStrands.Clear();
        sparks.Clear();
        orbFx = null;
        orbFxImage = null;
        backGlow = null;
        bloom = null;
        ringFx = null;
        if (slotRect != null)
        {
            slotRect.anchoredPosition = shakeBase;
        }
    }
}
