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
    // ---------------------------------------------------------------- animation

    private static bool animating;
    private static bool shattered;
    private static bool animSuccess;
    private static float animStart;
    private static ItemData animB;
    private static ItemIconSource animIconB;
    private static bool finalDone;
    private static readonly List<GameObject> effects = new(); // everything the animation has made, to keep above the window
    private const float CrackTime = 0.8f;
    private const float BurstEnd = 1.55f;
    private const float PullStart = 1.55f;

    private sealed class Shard
    {
        public GameObject Go;
        public RectTransform Rect;
        public CanvasGroup Group;
        public Vector2 Pos, Vel;
        public float Spin, Rot, Delay, Scale = 1f;
        public float Angle, Radius; // orbit around the Woven Echo
        public bool Captured;
        public Vector2 PullFrom;
        public bool Pulling;
    }

    private sealed class Spark
    {
        public GameObject Go;
        public RectTransform Rect;
        public CanvasGroup Group;
        public Vector2 Pos, Vel;
        public float Gravity, Born, Life, Spin;
    }

    private static readonly List<Shard> shards = new();
    private static readonly List<Spark> sparks = new();
    private static Vector2 BCenter => new(CenterX + SlotOffset, -(BY + IconH / 2));
    private static Vector2 ACenter => new(CenterX - SlotOffset, -(AY + IconH / 2));

    private static GameObject Effect(string name)
    {
        var go = new GameObject(name);
        go.AddComponent<RectTransform>();
        go.transform.SetParent(Content, false);
        effects.Add(go);
        return go;
    }

    private static void StartAnimation(bool success)
    {
        animating = true;
        shattered = false;
        finalDone = false;
        animSuccess = success;
        animStart = Time.unscaledTime;
        animB = itemB;
        animIconB = icons[1]; // the shards are cut from item B's own icon
        itemB = null; // item B is spent
        selectedId = -1;
        offer = null;
        dirty = true;
    }

    private static Spark NewSpark(Vector2 at, Color colour, float size, Vector2 vel, float gravity, float life, float delay = 0)
    {
        GameObject go = Effect("Spark");
        Image img = CraftingWidgets.MakeImage(go.transform, "S", colour);
        img.raycastTarget = false;
        RectTransform r = go.GetComponent<RectTransform>();
        r.anchorMin = r.anchorMax = new Vector2(0, 1);
        r.pivot = new Vector2(0.5f, 0.5f);
        r.sizeDelta = new Vector2(size, size * UnityEngine.Random.Range(1f, 2.2f));
        r.anchoredPosition = at;
        r.localRotation = Quaternion.Euler(0, 0, UnityEngine.Random.Range(0f, 90f));
        CraftingWidgets.Stretch(img.rectTransform, 0);
        CanvasGroup g = go.AddComponent<CanvasGroup>();
        g.blocksRaycasts = false;
        g.alpha = 0;
        var s = new Spark { Go = go, Rect = r, Group = g, Pos = at, Vel = vel, Gravity = gravity, Born = Time.unscaledTime + delay, Life = life, Spin = UnityEngine.Random.Range(-540f, 540f) };
        sparks.Add(s);
        return s;
    }

    private static void SparkBurst(Vector2 at, int count, float minSpeed, float maxSpeed, float gravity, Color a, Color b, float minLife, float maxLife)
    {
        for (int i = 0; i < count; i++)
        {
            float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2);
            float speed = UnityEngine.Random.Range(minSpeed, maxSpeed);
            NewSpark(at, Color.Lerp(a, b, UnityEngine.Random.value), UnityEngine.Random.Range(3f, 7f),
                new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed, gravity, UnityEngine.Random.Range(minLife, maxLife));
        }
    }

    /// <summary>Item B breaks into shards: each is a cut-out of its own icon, so it really looks shattered.</summary>
    private static Sprite mirrorSplinter;

    private static Sprite MirrorSplinter()
    {
        if (mirrorSplinter != null) return mirrorSplinter;
        const int size = 48;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            float v = y / (size - 1f);
            for (int x = 0; x < size; x++)
            {
                float u = x / (size - 1f);
                bool inside = u > Mathf.Abs(v - 0.37f) * 0.56f && u < 0.96f - v * 0.79f;
                pixels[y * size + x] = new Color32(255, 255, 255, inside ? (byte)255 : (byte)0);
            }
        }
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        texture.hideFlags = HideFlags.HideAndDontSave;
        mirrorSplinter = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        mirrorSplinter.hideFlags = HideFlags.HideAndDontSave;
        return mirrorSplinter;
    }

    private static void Shatter()
    {
        shattered = true;
        const int cols = 4, rows = 6;
        float tw = IconW / cols, th = IconH / rows;
        Vector2 centre = BCenter;
        ItemIconSource icon = animIconB;
        Color rarity = animB != null ? ItemColour(animB) : Color.white;
        var rng = new System.Random();
        for (int cy = 0; cy < rows; cy++)
        {
            for (int cx = 0; cx < cols; cx++)
            {
                // the tile's centre, measured from the middle of the box (y up)
                Vector2 offset = new(cx * tw + tw / 2 - IconW / 2, IconH / 2 - (cy * th + th / 2));
                GameObject go = Effect("Shard");
                RectTransform r = go.GetComponent<RectTransform>();
                r.anchorMin = r.anchorMax = new Vector2(0, 1);
                r.pivot = new Vector2(0.5f, 0.5f);
                r.sizeDelta = new Vector2(tw + 1, th + 1);
                Image clip = go.AddComponent<Image>();
                clip.color = Color.white;
                clip.sprite = MirrorSplinter();
                clip.raycastTarget = false;
                Mask mask = go.AddComponent<Mask>();
                mask.showMaskGraphic = false;
                CanvasGroup group = go.AddComponent<CanvasGroup>();
                group.blocksRaycasts = false;
                // the art, laid so this tile shows its own part of the icon
                var art = new GameObject("Art");
                RectTransform ar = art.AddComponent<RectTransform>();
                art.transform.SetParent(go.transform, false);
                ar.anchorMin = ar.anchorMax = new Vector2(0.5f, 0.5f);
                ar.pivot = new Vector2(0.5f, 0.5f);
                ar.sizeDelta = new Vector2(IconW, IconH);
                ar.anchoredPosition = -offset;
                if (icon != null && icon.Back != null)
                {
                    Image back = CraftingWidgets.MakeImage(art.transform, "Back", icon.BackColor);
                    back.sprite = icon.Back;
                    back.raycastTarget = false;
                    CraftingWidgets.Stretch(back.rectTransform, 4);
                }
                else
                {
                    Image tint = CraftingWidgets.MakeImage(art.transform, "Tint", Color.Lerp(new Color(0.05f, 0.04f, 0.035f), rarity, 0.25f));
                    tint.raycastTarget = false;
                    CraftingWidgets.Stretch(tint.rectTransform, 4);
                }
                if (icon != null && icon.Content != null)
                {
                    Image content = CraftingWidgets.MakeImage(art.transform, "Icon", Color.white);
                    content.sprite = icon.Content;
                    content.preserveAspect = true;
                    content.raycastTarget = false;
                    CraftingWidgets.Stretch(content.rectTransform, 6);
                }
                Image glint = WovenImage(art.transform, "ShardRedGlint", OrbArt.Named("sparkle_star"),
                    new Vector2(IconW * 0.5f, -IconH * 0.5f) + offset, new Vector2(tw, 8f));
                glint.color = new Color(1f, 0.22f, 0.28f, 0.78f);
                glint.rectTransform.localRotation = Quaternion.Euler(0f, 0f, (cx + cy) % 2 == 0 ? 34f : -38f);
                Vector2 away = offset.normalized;
                float speed = 120f + (float)rng.NextDouble() * 330f;
                var shard = new Shard
                {
                    Go = go,
                    Rect = r,
                    Group = group,
                    Pos = centre + offset,
                    Vel = away * speed + new Vector2(((float)rng.NextDouble() - 0.5f) * 120f, ((float)rng.NextDouble() - 0.4f) * 140f),
                    Spin = ((float)rng.NextDouble() - 0.5f) * 720f,
                    Delay = (float)rng.NextDouble() * 0.7f,
                };
                r.anchoredPosition = shard.Pos;
                shards.Add(shard);
            }
        }
        SparkBurst(centre, 38, 120f, 560f, 380f, new Color(0.7f, 0.9f, 1f), new Color(1f, 1f, 1f), 0.5f, 1.2f);
        crushShake = Time.unscaledTime;
        dirty = true; // item B's slot now draws empty
    }

    private static float crushShake = -10f;

    private static Vector2 Polyline(Vector2[] pts, float u)
    {
        float total = 0;
        for (int i = 0; i + 1 < pts.Length; i++)
        {
            total += Vector2.Distance(pts[i], pts[i + 1]);
        }
        float d = Mathf.Clamp01(u) * total;
        for (int i = 0; i + 1 < pts.Length; i++)
        {
            float len = Vector2.Distance(pts[i], pts[i + 1]);
            if (d <= len || i + 2 == pts.Length)
            {
                return Vector2.Lerp(pts[i], pts[i + 1], len <= 0 ? 0 : Mathf.Clamp01(d / len));
            }
            d -= len;
        }
        return pts[pts.Length - 1];
    }

    private static void TickAnimation()
    {
        float now = Time.unscaledTime;
        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
        TickSparks(now, dt);
        TickWoven(now);
        float t = now - animStart;
        if (animating)
        {
            // The charged mirror rattles just before the glass breaks.
            if (slotRects[1] != null && !shattered)
            {
                float amp = Mathf.Clamp01(t / CrackTime) * 5f;
                slotRects[1].anchoredPosition = new Vector2(CenterX + SlotOffset - IconW / 2, -BY) + new Vector2(UnityEngine.Random.Range(-1f, 1f), UnityEngine.Random.Range(-1f, 1f)) * amp;
            }
            if (!shattered && t >= CrackTime)
            {
                Shatter();
            }
            if (shattered)
            {
                if (animSuccess)
                {
                    TickShardsSuccess(t, now, dt);
                }
                else
                {
                    TickShardsFailure(t, now, dt);
                }
            }
        }
        // the window trembles
        if (contentRect != null)
        {
            float amp = 0f;
            float sinceShatter = now - crushShake;
            if (sinceShatter >= 0 && sinceShatter < 0.6f)
            {
                amp = 9f * (1f - sinceShatter / 0.6f);
            }
            float sinceFinal = now - finalAt;
            if (sinceFinal >= 0 && sinceFinal < 0.8f)
            {
                amp = Mathf.Max(amp, 14f * (1f - sinceFinal / 0.8f));
            }
            if (animating && !shattered)
            {
                amp = Mathf.Max(amp, Mathf.Clamp01(t / CrackTime) * 2.5f);
            }
            contentRect.anchoredPosition = amp > 0.05f
                ? contentBase + new Vector2(UnityEngine.Random.Range(-1f, 1f), UnityEngine.Random.Range(-1f, 1f)) * amp
                : contentBase;
        }
    }

    private static float finalAt = -10f;

    private static void TickShardsSuccess(float t, float now, float dt)
    {
        // The glass travels through the Woven Echo into the left slot.
        Vector2[] path =
        {
            BCenter,
            EchoCenter + new Vector2(45f, 28f),
            EchoCenter + new Vector2(-38f, -22f),
            ACenter,
        };
        int alive = 0;
        foreach (Shard s in shards)
        {
            if (s.Go == null)
            {
                continue;
            }
            alive++;
            if (t < BurstEnd)
            {
                s.Vel *= Mathf.Max(0f, 1f - 2.4f * dt);
                s.Vel.y -= 90f * dt;
                s.Pos += s.Vel * dt;
                s.Rot += s.Spin * dt;
                s.PullFrom = s.Pos;
                s.Group.alpha = 1f;
            }
            else
            {
                float u = (t - BurstEnd - s.Delay * 1.3f) / 1.45f;
                if (u < 0)
                {
                    s.Rot += s.Spin * dt * 0.3f;
                    s.Pos += new Vector2(0, Mathf.Sin(now * 5f + s.Delay * 9f) * 14f * dt);
                    s.PullFrom = s.Pos;
                }
                else
                {
                    float e = u * u * (3f - 2f * u);
                    Vector2 onPath = Polyline(path, e);
                    float swirl = Mathf.Sin(u * Mathf.PI * 3f + s.Delay * 6f) * 26f * (1f - e);
                    s.Pos = Vector2.Lerp(s.PullFrom, onPath, Mathf.Clamp01(u * 3f)) + new Vector2(swirl, 0);
                    s.Rot += s.Spin * dt * 0.4f;
                    if (u < 0.88f && UnityEngine.Random.value < dt * 4f)
                    {
                        SparkBurst(s.Pos, 1, 22f, 75f, 35f,
                            new Color(0.18f, 0.36f, 0.95f), new Color(1f, 0.73f, 0.20f), 0.23f, 0.46f);
                    }
                    s.Scale = Mathf.Lerp(1f, 0.35f, e);
                    s.Group.alpha = u > 0.88f ? Mathf.Clamp01((1f - u) / 0.12f) : 1f;
                    if (u >= 1f)
                    {
                        Object.Destroy(s.Go);
                        s.Go = null;
                        continue;
                    }
                }
            }
            s.Rect.anchoredPosition = s.Pos;
            s.Rect.localRotation = Quaternion.Euler(0, 0, s.Rot);
            s.Rect.localScale = new Vector3(s.Scale, s.Scale, 1);
        }
        if (alive == 0 && !finalDone)
        {
            FinishSuccess(now);
        }
        if (finalDone && now - finalAt > 1.0f)
        {
            EndAnimationNow();
        }
    }

    private static void TickShardsFailure(float t, float now, float dt)
    {
        foreach (Shard s in shards)
        {
            if (s.Go == null)
            {
                continue;
            }
            if (t < BurstEnd)
            {
                s.Vel *= Mathf.Max(0f, 1f - 2.4f * dt);
                s.Vel.y -= 90f * dt;
                s.Pos += s.Vel * dt;
                s.Rot += s.Spin * dt;
                s.Group.alpha = 1f;
                Vector2 rel = s.Pos - EchoCenter;
                s.Angle = Mathf.Atan2(rel.y, rel.x);
                s.Radius = rel.magnitude;
            }
            else
            {
                float u = (t - BurstEnd - s.Delay * 0.8f) / 1.1f;
                if (u > 0)
                {
                    // spiral in: the angle runs ahead faster as the radius falls
                    s.Radius = Mathf.Max(0f, s.Radius - (60f + s.Radius * 2.1f) * dt);
                    s.Angle += (1.6f + 140f / Mathf.Max(18f, s.Radius)) * dt;
                    s.Pos = EchoCenter + new Vector2(Mathf.Cos(s.Angle), Mathf.Sin(s.Angle)) * s.Radius;
                    s.Rot += s.Spin * dt * 1.5f;
                    s.Scale = Mathf.Clamp01(s.Radius / 70f);
                    s.Group.alpha = Mathf.Clamp01(s.Radius / 20f);
                    if (s.Radius < 4f)
                    {
                        Object.Destroy(s.Go);
                        s.Go = null;
                        continue;
                    }
                }
            }
            s.Rect.anchoredPosition = s.Pos;
            s.Rect.localRotation = Quaternion.Euler(0, 0, s.Rot);
            s.Rect.localScale = new Vector3(s.Scale, s.Scale, 1);
        }
        if (t >= BurstEnd + 1.95f)
        {
            if (!finalDone)
            {
                finalDone = true;
                finalAt = now;
                GameObject beam = Effect("WovenDisintegration");
                Image flare = WovenImage(beam.transform, "CrimsonBeam", OrbArt.Named("glow_violet"), Vector2.zero, new Vector2(86f, 285f));
                flare.rectTransform.anchorMin = flare.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                flare.rectTransform.anchoredPosition = Vector2.zero;
                flare.color = new Color(1f, 0.12f, 0.17f, 0.68f);
                RectTransform beamRect = beam.GetComponent<RectTransform>();
                beamRect.anchorMin = beamRect.anchorMax = new Vector2(0f, 1f);
                beamRect.anchoredPosition = EchoCenter;
                SparkBurst(EchoCenter, 28, 100f, 360f, 70f, new Color(0.56f, 0.02f, 0.05f), new Color(1f, 0.27f, 0.19f), 0.4f, 0.9f);
            }
            if (now - finalAt > 0.7f)
            {
                EndAnimationNow();
            }
        }
    }

    /// <summary>The shards have all reached item A: its final flash.</summary>
    private static void FinishSuccess(float now)
    {
        finalDone = true;
        finalAt = now;
        reflectedItem = itemA;
        Vector2 centre = ACenter;
        Image seal = WovenImage(Content, "WovenSeal", OrbArt.Named("woven_echo_spiral"), centre, new Vector2(174f, 174f));
        seal.material = Items_AdvancedForgeWindow.Additive();
        seal.color = new Color(0.71f, 0.83f, 1f, 0.52f);
        effects.Add(seal.gameObject);
        finalFlash.Add((seal, now, 0.52f, 0.7f));
        SparkBurst(centre, 44, 140f, 700f, 480f, new Color(0.19f, 0.36f, 0.91f), new Color(1f, 0.79f, 0.22f), 0.7f, 1.4f);
        dirty = true;
    }

    private static readonly List<(Image Img, float Start, float Peak, float Duration)> finalFlash = new();

    private static void TickSparks(float now, float dt)
    {
        for (int i = sparks.Count - 1; i >= 0; i--)
        {
            Spark s = sparks[i];
            if (s.Go == null)
            {
                sparks.RemoveAt(i);
                continue;
            }
            if (now < s.Born)
            {
                continue;
            }
            float age = now - s.Born;
            float p = age / s.Life;
            if (p >= 1f)
            {
                Object.Destroy(s.Go);
                sparks.RemoveAt(i);
                continue;
            }
            s.Pos += s.Vel * dt;
            s.Vel.y -= s.Gravity * dt;
            s.Rect.anchoredPosition = s.Pos;
            s.Rect.Rotate(0, 0, s.Spin * dt);
            s.Group.alpha = Mathf.Clamp01(1f - p) * UnityEngine.Random.Range(0.7f, 1f);
        }
        for (int i = finalFlash.Count - 1; i >= 0; i--)
        {
            var f = finalFlash[i];
            if (f.Img == null)
            {
                finalFlash.RemoveAt(i);
                continue;
            }
            float p = (now - f.Start) / f.Duration;
            if (p >= 1f)
            {
                Color gone = f.Img.color;
                gone.a = 0f;
                f.Img.color = gone;
                finalFlash.RemoveAt(i);
                continue;
            }
            Color c = f.Img.color;
            c.a = f.Peak * (1f - p) * (1f - p);
            f.Img.color = c;
            if (f.Img.gameObject.name == "G")
            {
                f.Img.transform.parent.localScale = Vector3.one * (1f + 1.2f * p);
            }
        }
    }

    private static void EndAnimationNow()
    {
        animating = false;
        shattered = false;
        finalDone = false;
        foreach (Shard s in shards)
        {
            if (s.Go != null) Object.Destroy(s.Go);
        }
        shards.Clear();
        foreach (Spark s in sparks)
        {
            if (s.Go != null) Object.Destroy(s.Go);
        }
        sparks.Clear();
        finalFlash.Clear();
        foreach (GameObject go in effects)
        {
            if (go != null) Object.Destroy(go);
        }
        effects.Clear();
        animIconB?.Dispose();
        if (icons[1] == animIconB)
        {
            icons[1] = null;
        }
        animIconB = null;
        animB = null;
        if (contentRect != null)
        {
            contentRect.anchoredPosition = contentBase;
        }
        dirty = true;
    }
}
