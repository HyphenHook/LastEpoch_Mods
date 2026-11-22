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
    private sealed class Ghost
    {
        public GameObject Go;
        public RectTransform Rect;
        public CanvasGroup Group;
        public Vector2 From, To;
        public int Side;
        public float Start, Duration;
    }

    private static readonly List<Ghost> ghosts = new();

    // The pour: embers run from both items along the trenches into the result.
    private static bool pouring;
    private static float pourStart;
    private static float pourTotal = 2.7f; // longer when bones are crushed too
    private static bool pourBones;
    private static float crushAt = -10f;

    private sealed class Piece
    {
        public GameObject Go;
        public RectTransform Rect;
        public CanvasGroup Group;
        public bool IsDrop; // a falling bone (or inward spark); otherwise a flying shard
        public bool Inward;
        public Vector2 From, To, Velocity;
        public float Start, Life, Spin, Gravity;
    }

    private static readonly List<Piece> pieces = new();
    private static readonly ItemData[] pourItems = new ItemData[2];
    private static readonly List<Image> trenchInner = new();
    private static readonly List<Vector2>[] pourPath = new List<Vector2>[2];
    private static readonly float[] pourLength = new float[2];
    private static bool pendingFlash;
    private static bool lastFailed;
    private static float flashAt = -10f;
    private static bool chargeDone;
    private static RectTransform contentRect;
    private static Vector2 contentBase;

    private sealed class Flash
    {
        public Image Glow, Overlay, Wash;
        public List<Image> Rays = new();
        public float Start;
    }

    private static readonly List<Flash> flashes = new();
    private static readonly Color TrenchOuter = new(0.48f, 0.38f, 0.20f, 0.95f);
    private static readonly Color TrenchDark = new(0.025f, 0.02f, 0.015f, 1f);
    private static readonly Color TrenchGlow = new(1f, 0.45f, 0.10f, 1f);

    private static Vector2 Sample(List<Vector2> pts, float distance)
    {
        for (int i = 0; i + 1 < pts.Count; i++)
        {
            float len = Vector2.Distance(pts[i], pts[i + 1]);
            if (distance <= len || i + 2 == pts.Count)
            {
                return Vector2.Lerp(pts[i], pts[i + 1], len <= 0 ? 0 : Mathf.Clamp01(distance / len));
            }
            distance -= len;
        }
        return pts[pts.Count - 1];
    }

    private static float PathLength(List<Vector2> pts)
    {
        float total = 0;
        for (int i = 0; i + 1 < pts.Count; i++)
        {
            total += Vector2.Distance(pts[i], pts[i + 1]);
        }
        return total;
    }

    /// <summary>The kit's effect runs: the slots power up, the pipes flow, the essence pools and pours into the result.</summary>
    private static void StartPour(bool crushBones)
    {
        FindCurrencyIcons();
        pourBones = crushBones;
        pourTotal = RecombKit.RevealAt;
        chargeDone = false;
        pouring = true;
        pourStart = Time.unscaledTime;
        resultRevealed = false;
        revealAt = pourStart + pourTotal;
        RecombKit.Start(pourStart);
        SpawnConsumeEmbers();
        if (crushBones)
        {
            Vector2 center = new(CenterX, -(ResY + IconH * 0.5f));
            for (int i = 0; i < 18; i++)
            {
                float angle = i * Mathf.PI * 2f / 18f;
                Vector2 rim = center + new Vector2(Mathf.Cos(angle) * (IconW * 0.5f + 63f),
                    Mathf.Sin(angle) * (IconH * 0.5f + 35f));
                Color tint = rim.x < center.x ? new Color(1f, 0.52f, 0.16f) : new Color(0.55f, 1f, 0.31f);
                bool fossil = i % 3 == 0;
                float delay = (i % 4) * 0.035f;
                Piece ember = NewSpark(rim, center, pourStart + RecombKit.PourAt + delay,
                    RecombKit.RevealAt - RecombKit.PourAt - delay, tint, fossil ? 25f : 12f, inward: true);
                ember.Go.GetComponent<Image>().sprite = OrbArt.Named(fossil ? "primordial_rib" : "sparkle_star");
            }
        }
    }

    private static void SpawnConsumeEmbers()
    {
        Sprite spark = OrbArt.Named("sparkle_star");
        Material emission = Items_AdvancedForgeWindow.Additive();
        for (int side = 0; side < 2; side++)
        {
            float centerX = side == 0 ? RecombKit.InCx0 : RecombKit.InCx1;
            float ignite = side == 0 ? RecombKit.DevourLeftAt : RecombKit.DevourRightAt;
            float pipeY = -RecombKit.Frame(side).yMax;
            for (int i = 0; i < 18; i++)
            {
                float burn = i / 17f;
                Vector2 origin = new(centerX + UnityEngine.Random.Range(-46f, 46f),
                    -(InY + Mathf.Lerp(RecombKit.ItemH - 18f, 20f, burn)));
                Vector2 socket = new(centerX + UnityEngine.Random.Range(-10f, 10f), pipeY);
                float start = pourStart + ignite + 0.24f + burn * 0.36f;
                Piece ember = NewSpark(origin, socket, start, 0.26f + burn * 0.27f,
                    FireTint(side, 1f), i % 5 == 0 ? 12f : 6f, inward: true);
                Image image = ember.Go.GetComponent<Image>();
                image.sprite = spark;
                if (emission != null)
                {
                    image.material = emission;
                }
            }
        }
    }

    private static bool PrimordialSkin => pouring ? pourBones : deterministic && primordial;

    private static void TickAnimations()
    {
        float now = Time.unscaledTime;
        TickPieces(now);
        TickCage(now);
        TickCracks(now);
        RecombKit.Tick(now);
        TickInputFlames(now);
        TickResultFlames(now);
        TickFossilShell(now);
        if (pouring)
        {
            for (int i = 0; i < 2; i++)
            {
                if (slotGroups[i] != null)
                {
                    slotGroups[i].alpha = RecombKit.InputAlpha(i, now); // the flame eats the items
                }
            }
        }
        if (pouring && now >= pourStart + pourTotal)
        {
            pouring = false;
            cageReleaseAt = now;
            Array.Clear(pourItems, 0, 2);
            resultRevealed = resultItem != null;
            pendingFlash = resultRevealed;
            dirty = true;
        }
        for (int i = ghosts.Count - 1; i >= 0; i--)
        {
            Ghost g = ghosts[i];
            if (g.Go == null)
            {
                ghosts.RemoveAt(i);
                continue;
            }
            if (now < g.Start)
            {
                continue;
            }
            float p = Mathf.Clamp01((now - g.Start) / g.Duration);
            List<Vector2> path = pourPath[g.Side];
            g.Rect.anchoredPosition = Sample(path, p * pourLength[g.Side]);
            float size = 0.7f + 0.5f * Mathf.Sin(p * Mathf.PI);
            g.Rect.localScale = new Vector3(size, size, 1);
            g.Group.alpha = p < 0.1f ? p / 0.1f : p > 0.85f ? (1f - p) / 0.15f : 1f;
            if (p >= 1f)
            {
                Object.Destroy(g.Go);
                ghosts.RemoveAt(i);
            }
        }
        for (int i = flashes.Count - 1; i >= 0; i--)
        {
            Flash f = flashes[i];
            float p = (now - f.Start) / 0.95f;
            if (p >= 1f || f.Glow == null)
            {
                DestroyFlash(f);
                flashes.RemoveAt(i);
                continue;
            }
            float inv = 1f - p;
            Color o = f.Overlay.color;
            o.a = 0.95f * inv * inv;
            f.Overlay.color = o;
            Color g2 = f.Glow.color;
            g2.a = 0.8f * inv;
            f.Glow.color = g2;
            f.Glow.rectTransform.localScale = Vector3.one * (1f + 1.4f * p);
            if (f.Wash != null)
            {
                Color w = f.Wash.color;
                w.a = 0.3f * Mathf.Pow(Mathf.Clamp01(1f - p * 3.2f), 2f);
                f.Wash.color = w;
            }
            for (int k = 0; k < f.Rays.Count; k++)
            {
                Image ray = f.Rays[k];
                if (ray == null)
                {
                    continue;
                }
                Color rc = ray.color;
                rc.a = 0.6f * inv * inv;
                ray.color = rc;
                ray.rectTransform.localScale = new Vector3(1f - 0.6f * p, 0.35f + 1.5f * p, 1f);
                ray.rectTransform.localRotation = Quaternion.Euler(0, 0, k * (360f / f.Rays.Count) + 55f * p);
            }
        }

        // the whole window trembles as the embers gather, harder when the bones are crushed and the result appears
        if (contentRect != null)
        {
            float amp = 0f;
            if (pouring)
            {
                amp = Mathf.Max(amp, Mathf.Clamp01(((now - pourStart) / pourTotal - 0.2f) / 0.6f) * 3.5f);
            }
            float sinceCrush = now - crushAt;
            if (sinceCrush >= 0 && sinceCrush < 0.5f)
            {
                amp = Mathf.Max(amp, 9f * (1f - sinceCrush / 0.5f));
            }
            float sinceFlash = now - flashAt;
            if (sinceFlash >= 0 && sinceFlash < 0.8f)
            {
                amp = Mathf.Max(amp, 17f * (1f - sinceFlash / 0.8f));
            }
            contentRect.anchoredPosition = amp > 0.05f
                ? contentBase + new Vector2(UnityEngine.Random.Range(-1f, 1f), UnityEngine.Random.Range(-1f, 1f)) * amp
                : contentBase;
        }
    }

    private static void ClearGhosts()
    {
        foreach (Piece pc in pieces)
        {
            if (pc.Go != null)
            {
                Object.Destroy(pc.Go);
            }
        }
        pieces.Clear();
        crushAt = -10f;
        foreach (Ghost g in ghosts)
        {
            if (g.Go != null)
            {
                Object.Destroy(g.Go);
            }
        }
        ghosts.Clear();
        foreach (Flash f in flashes)
        {
            DestroyFlash(f);
        }
        flashes.Clear();
        ClearCracks();
        if (contentRect != null)
        {
            contentRect.anchoredPosition = contentBase;
        }
    }
}
