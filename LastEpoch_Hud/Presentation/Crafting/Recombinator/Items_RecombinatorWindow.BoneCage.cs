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
    /// <summary>
    /// Primordial recombination: a dark, worn frame of old iron and bone round the result box. Nothing is clean: the
    /// bars are built of uneven pieces, the spikes bend and are chipped, and everything carries grime and cracks.
    /// All of it hangs from one object so it can squeeze shut.
    /// </summary>
    private static void BoneCage(float x, float y)
    {
        var rng = new System.Random(5521); // the same roughness every time, so it does not shimmer when redrawn
        float R(float lo, float hi) => lo + (float)rng.NextDouble() * (hi - lo);
        Color Iron() { float v = R(0.035f, 0.07f); return new Color(v, v * 0.93f, v * 0.86f, 1f); }
        Color Bronze() { float v = R(0.20f, 0.30f); return new Color(v, v * 0.74f, v * 0.45f, 1f); }
        Color Bone() { float v = R(0.34f, 0.46f); return new Color(v, v * 0.93f, v * 0.78f, 1f); }
        Color boneDark = new(0.15f, 0.135f, 0.11f, 1f);
        Color crack = new(0.04f, 0.035f, 0.03f, 1f);
        Color ember = new(0.85f, 0.36f, 0.08f, 1f);
        Vector2 half = new(0.5f, 0.5f);
        float hw = IconW / 2f, hh = IconH / 2f;

        var rootGo = new GameObject("BoneCage");
        cageRoot = rootGo.AddComponent<RectTransform>();
        rootGo.transform.SetParent(Content, false);
        cageRoot.anchorMin = cageRoot.anchorMax = new Vector2(0, 1);
        cageRoot.pivot = half;
        cageRoot.anchoredPosition = new Vector2(x + hw, -(y + hh));
        cageRoot.sizeDelta = new Vector2(IconW, IconH);
        dynamic.Add(rootGo);

        void Seg(float cx, float cy, float w, float h, float rot, Color c)
        {
            CraftingWidgets.Part(cageRoot, "Piece", c, half, half, half, new Vector2(cx, cy), new Vector2(w, h), rot);
        }

        // a bent, uneven, chipped spike from a point
        void Spike(Vector2 from, float angle, float length, float baseWidth)
        {
            const int parts = 7;
            float bend = R(-14f, 14f);
            Vector2 pos = from;
            float seg = length / parts;
            for (int i = 0; i < parts; i++)
            {
                if (i >= parts - 2 && rng.NextDouble() < 0.35)
                {
                    break; // a broken tip
                }
                float a = angle + bend * i / parts;
                float rad = a * Mathf.Deg2Rad;
                Vector2 dir = new(Mathf.Cos(rad), Mathf.Sin(rad));
                float k = 1f - (float)i / parts;
                float width = Mathf.Max(0.9f, baseWidth * Mathf.Pow(k, 1.2f) * R(0.72f, 1.15f));
                Vector2 c = pos + dir * (seg * 0.5f);
                Seg(c.x, c.y, seg + R(0.2f, 1.4f), width + 1.8f, a, boneDark);
                Seg(c.x, c.y, seg + R(0f, 1.2f), width, a + R(-3f, 3f), Bone());
                pos += dir * seg;
            }
        }

        // darkness behind, with a faint dull red heat
        Seg(0, 0, IconW + 84, IconH + 84, 0, new Color(0f, 0f, 0f, 0.45f));
        Seg(0, 0, IconW + 38, IconH + 38, R(-1f, 1f), new Color(0.45f, 0.08f, 0.03f, 0.11f));

        const float gap = 12;
        float l = -(hw + gap), r = hw + gap, t = hh + gap, bt = -(hh + gap);

        // bars: rows of uneven iron blocks, a broken bronze line, rust and soot
        void Bar(float x0, float y0, float x1, float y1)
        {
            bool horizontal = Mathf.Abs(y1 - y0) < 0.01f;
            float length = horizontal ? x1 - x0 : y1 - y0;
            for (float p = 0; p <= length; p += 7f)
            {
                float jx = R(-0.9f, 0.9f), jy = R(-0.9f, 0.9f);
                float cx = horizontal ? x0 + p : x0 + jx;
                float cy = horizontal ? y0 + jy : y0 + p;
                float thick = 6.5f + R(-1.2f, 1.6f);
                float along = 8f + R(0f, 2.5f);
                Seg(cx, cy, horizontal ? along : thick, horizontal ? thick : along, R(-2.5f, 2.5f), Iron());
                if (rng.NextDouble() < 0.6)
                {
                    float inner = (horizontal ? (y0 > 0 ? -1.8f : 1.8f) : (x0 > 0 ? -1.8f : 1.8f));
                    Seg(horizontal ? cx : cx + inner, horizontal ? cy + inner : cy, horizontal ? along : 1.3f, horizontal ? 1.3f : along, 0, Bronze());
                }
                if (rng.NextDouble() < 0.16)
                {
                    Seg(cx + R(-3f, 3f), cy + R(-3f, 3f), R(3f, 8f), R(3f, 7f), R(0f, 90f), new Color(0.2f, 0.09f, 0.04f, R(0.5f, 0.85f)));
                }
                if (rng.NextDouble() < 0.14)
                {
                    Seg(cx + R(-3f, 3f), cy + R(-3f, 3f), R(2f, 5f), R(2f, 5f), R(0f, 90f), crack);
                }
            }
            for (float p = 18; p < length - 10; p += R(19f, 26f))
            {
                Seg(horizontal ? x0 + p : x0, horizontal ? y0 : y0 + p, 3, 3, R(20f, 70f), Bronze());
            }
        }
        Bar(l, t, r, t);
        Bar(l, bt, r, bt);
        Bar(l, bt, l, t);
        Bar(r, bt, r, t);

        // corner plates, cracked and worn, each clawed by two bent bone spikes
        Vector2[] corner = { new(l, t), new(r, t), new(l, bt), new(r, bt) };
        float[] inward = { -45f, -135f, 45f, 135f };
        for (int k = 0; k < 4; k++)
        {
            Vector2 c = corner[k];
            Seg(c.x, c.y, 31f + R(-1f, 1f), 31f + R(-1f, 1f), 45f + R(-6f, 6f), Bronze());
            Seg(c.x, c.y, 26f + R(-1f, 1f), 26f + R(-1f, 1f), 45f + R(-6f, 6f), Iron());
            Seg(c.x, c.y, 13f, 13f, 45f + R(-8f, 8f), Bronze());
            Seg(c.x, c.y, 8f, 8f, 45f, ember);
            Seg(c.x + R(-3f, 3f), c.y + R(-3f, 3f), R(10f, 18f), 1.4f, R(0f, 180f), crack); // a crack across the plate
            Spike(c, inward[k] + 20f + R(-5f, 5f), 40f + R(-4f, 5f), 7f);
            Spike(c, inward[k] - 20f + R(-5f, 5f), 40f + R(-4f, 5f), 7f);
            Spike(c, inward[k] + 180f, 15f + R(-3f, 4f), 5f);
        }

        // studs on the lower and side bars
        foreach (Vector2 stud in new[] { new Vector2(0, bt), new Vector2(l, 0), new Vector2(r, 0) })
        {
            Seg(stud.x, stud.y, 17f, 17f, 45f + R(-6f, 6f), Bronze());
            Seg(stud.x, stud.y, 13f, 13f, 45f + R(-6f, 6f), Iron());
            Seg(stud.x, stud.y, 5f, 5f, 45f, ember);
        }

        // the crest above: a dim gem between two worn bone wings
        Seg(0, t, 24f, 24f, 45f + R(-5f, 5f), Bronze());
        Seg(0, t, 20f, 20f, 45f + R(-5f, 5f), Iron());
        Seg(0, t, 11f, 11f, 45f, ember);
        Seg(0, t, 4.5f, 4.5f, 45f, new Color(0.95f, 0.78f, 0.45f, 1f));
        Spike(new Vector2(13, t), 6f + R(-4f, 4f), 46f, 6f);
        Spike(new Vector2(-13, t), 174f + R(-4f, 4f), 46f, 6f);
        Spike(new Vector2(9, t + 6), 52f + R(-5f, 5f), 24f, 5f);
        Spike(new Vector2(-9, t + 6), 128f + R(-5f, 5f), 24f, 5f);
    }

    private static void TickCage(float now)
    {
        if (cageRoot == null)
        {
            return;
        }
        float scale;
        if (pouring && pourBones)
        {
            // it tightens while the embers gather, and snaps shut on every crushed bone
            scale = Mathf.Lerp(1f, 0.93f, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((now - pourStart) / 1.8f)));
            float since = now - crushAt;
            if (since >= 0f && since < 0.3f)
            {
                scale -= 0.1f * (1f - since / 0.3f);
            }
        }
        else
        {
            scale = Mathf.Lerp(0.93f, 1f, Mathf.Clamp01((now - cageReleaseAt) / 0.5f));
        }
        cageRoot.localScale = new Vector3(scale, scale, 1f);
    }
}
