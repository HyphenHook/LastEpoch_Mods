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
    private static void DestroyFlash(Flash f)
    {
        if (f.Glow != null) Object.Destroy(f.Glow.gameObject);
        if (f.Overlay != null) Object.Destroy(f.Overlay.gameObject);
        if (f.Wash != null) Object.Destroy(f.Wash.gameObject);
        foreach (Image ray in f.Rays)
        {
            if (ray != null) Object.Destroy(ray.gameObject);
        }
    }

    private static Color SoulLight() =>
        Color.Lerp(new Color(0.3f, 0.8f, 1f), new Color(0.95f, 1f, 1f), UnityEngine.Random.value);

    /// <summary>Soul ember fire: an orange and green mix, with some pale heat.</summary>
    private static Color RandomFire() =>
        Color.Lerp(Color.Lerp(new Color(1f, 0.45f, 0.1f), new Color(0.5f, 0.95f, 0.25f), UnityEngine.Random.value), new Color(1f, 0.97f, 0.75f), UnityEngine.Random.value * 0.35f);

    /// <summary>A bright spark: flies out and falls, or (inward) streams in to a point.</summary>
    private static Piece NewSpark(Vector2 from, Vector2 to, float start, float life, Color color, float size, bool inward = false)
    {
        color.a *= UnityEngine.Random.Range(0.6f, 1f);
        Image img = CraftingWidgets.MakeImage(Content, "Spark", color);
        img.raycastTarget = false;
        RectTransform r = img.rectTransform;
        r.anchorMin = r.anchorMax = new Vector2(0, 1);
        r.pivot = new Vector2(0.5f, 0.5f);
        r.sizeDelta = new Vector2(size, size * (inward ? 1f : UnityEngine.Random.Range(1f, 2.4f)));
        r.anchoredPosition = from;
        r.localRotation = Quaternion.Euler(0, 0, UnityEngine.Random.Range(0f, 90f));
        CanvasGroup group = img.gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.alpha = 0;
        var piece = new Piece { Go = img.gameObject, Rect = r, Group = group, IsDrop = inward, Inward = inward, From = from, To = to, Start = start, Life = life };
        pieces.Add(piece);
        return piece;
    }

    /// <summary>A burst of sparks from a point.</summary>
    private static void SparkBurst(Vector2 at, int count, float minSpeed, float maxSpeed, float gravity, float minLife, float maxLife)
    {
        for (int i = 0; i < count; i++)
        {
            float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2);
            float speed = UnityEngine.Random.Range(minSpeed, maxSpeed);
            Piece spark = NewSpark(at, at, Time.unscaledTime, UnityEngine.Random.Range(minLife, maxLife), RandomFire(), UnityEngine.Random.Range(3f, 7f));
            spark.Velocity = new Vector2(Mathf.Cos(angle) * speed, Mathf.Sin(angle) * speed);
            spark.Gravity = gravity;
            spark.Spin = UnityEngine.Random.Range(-720f, 720f);
        }
    }

    /// <summary>The final flash: a white wash, bright rays, a swelling glow, a ring of sparks and a shower.</summary>
    private static void SpawnFlash()
    {
        Vector2 centre = new(CenterX, -(ResY + IconH / 2));
        flashAt = Time.unscaledTime;

        Image wash = CraftingWidgets.MakeImage(Content, "FlashWash", new Color(1f, 0.88f, 0.62f, 0.3f));
        wash.raycastTarget = false;
        RectTransform wr = wash.rectTransform;
        wr.anchorMin = Vector2.zero;
        wr.anchorMax = Vector2.one;
        wr.offsetMin = wr.offsetMax = Vector2.zero;

        var flash = new Flash { Start = Time.unscaledTime, Wash = wash };
        for (int k = 0; k < 14; k++)
        {
            Image ray = CraftingWidgets.MakeImage(Content, "FlashRay", k % 3 == 1 ? new Color(0.62f, 1f, 0.42f, 0.85f) : new Color(1f, 0.82f, 0.45f, 0.85f));
            ray.raycastTarget = false;
            RectTransform rr = ray.rectTransform;
            rr.anchorMin = rr.anchorMax = new Vector2(0, 1);
            rr.pivot = new Vector2(0.5f, 0f);
            rr.sizeDelta = new Vector2(UnityEngine.Random.Range(2.5f, 9f), UnityEngine.Random.Range(230f, 440f));
            rr.anchoredPosition = centre;
            flash.Rays.Add(ray);
        }
        Image glow = CraftingWidgets.MakeImage(Content, "FlashGlow", new Color(1f, 0.82f, 0.4f, 0.8f));
        Image overlay = CraftingWidgets.MakeImage(Content, "FlashOverlay", new Color(1f, 0.88f, 0.6f, 0.8f));
        foreach ((Image img, Vector2 size) in new[] { (glow, new Vector2(IconW + 90, IconH + 90)), (overlay, new Vector2(IconW, IconH)) })
        {
            img.raycastTarget = false;
            RectTransform r = img.rectTransform;
            r.anchorMin = r.anchorMax = new Vector2(0, 1);
            r.pivot = new Vector2(0.5f, 0.5f);
            r.sizeDelta = size;
            r.anchoredPosition = centre;
        }
        flash.Glow = glow;
        flash.Overlay = overlay;
        flashes.Add(flash);

        // a ring of sparks racing outward, then a shower
        for (int k = 0; k < 36; k++)
        {
            float angle = k * Mathf.PI * 2 / 36;
            Piece ring = NewSpark(centre, centre, Time.unscaledTime, 0.6f, new Color(1f, 0.93f, 0.65f), 6f);
            ring.Velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 640f;
            ring.Spin = 360f;
        }
        SparkBurst(centre, 90, 160f, 760f, 520f, 0.7f, 1.5f);
    }

    private static Piece NewPiece(bool drop, Vector2 from, Vector2 to, float start, float life)
    {
        Color bone = new(0.93f, 0.90f, 0.79f, 1f);
        Image img = CraftingWidgets.MakeImage(Content, drop ? "Bone" : "Shard", bone);
        img.raycastTarget = false;
        RectTransform r = img.rectTransform;
        r.anchorMin = r.anchorMax = new Vector2(0, 1);
        r.pivot = new Vector2(0.5f, 0.5f);
        if (drop && bonesIcon != null)
        {
            img.sprite = bonesIcon;
            img.preserveAspect = true;
            img.color = Color.white;
            r.sizeDelta = new Vector2(46, 46);
        }
        else if (drop)
        {
            r.sizeDelta = new Vector2(12, 40); // a plain bone shaft when the icon is not found
        }
        else
        {
            r.sizeDelta = new Vector2(UnityEngine.Random.Range(5f, 9f), UnityEngine.Random.Range(10f, 20f));
        }
        r.anchoredPosition = from;
        CanvasGroup group = img.gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.alpha = 0;
        var piece = new Piece { Go = img.gameObject, Rect = r, Group = group, IsDrop = drop, From = from, To = to, Start = start, Life = life };
        pieces.Add(piece);
        return piece;
    }

    /// <summary>The bone is crushed: it bursts into shards that fly out, fall and fade.</summary>
    private static void Crush(Vector2 at)
    {
        crushAt = Time.unscaledTime;
        SparkBurst(at, 26, 140f, 520f, 700f, 0.4f, 0.9f);
        for (int i = 0; i < 24; i++)
        {
            float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2);
            float speed = UnityEngine.Random.Range(160f, 460f);
            Piece shard = NewPiece(false, at, at, Time.unscaledTime, UnityEngine.Random.Range(0.55f, 0.9f));
            shard.Velocity = new Vector2(Mathf.Cos(angle) * speed, Mathf.Sin(angle) * speed + 90f);
            shard.Gravity = 620f;
            shard.Spin = UnityEngine.Random.Range(-540f, 540f);
            shard.Rect.localRotation = Quaternion.Euler(0, 0, UnityEngine.Random.Range(0f, 180f));
        }
    }

    private static void TickPieces(float now)
    {
        for (int i = pieces.Count - 1; i >= 0; i--)
        {
            Piece pc = pieces[i];
            if (pc.Go == null)
            {
                pieces.RemoveAt(i);
                continue;
            }
            if (now < pc.Start)
            {
                continue;
            }
            float age = now - pc.Start;
            if (pc.IsDrop)
            {
                float p = Mathf.Clamp01(age / pc.Life);
                pc.Rect.anchoredPosition = Vector2.Lerp(pc.From, pc.To, p * p);
                pc.Rect.localRotation = Quaternion.Euler(0, 0, pc.Spin * p);
                float scale = pc.Inward ? Mathf.Lerp(1.3f, 0.3f, p) : Mathf.Lerp(1.9f, 0.9f, p);
                pc.Rect.localScale = new Vector3(scale, scale, 1);
                pc.Group.alpha = pc.Inward ? Mathf.Clamp01(p * 8f) * Mathf.Clamp01((1f - p) * 4f) : Mathf.Clamp01(p * 6f);
                if (p >= 1f)
                {
                    Vector2 at = pc.To;
                    Object.Destroy(pc.Go);
                    pieces.RemoveAt(i);
                    if (!pc.Inward)
                    {
                        Crush(at);
                    }
                }
            }
            else
            {
                float p = age / pc.Life;
                Vector2 pos = pc.From + pc.Velocity * age;
                pos.y -= 0.5f * pc.Gravity * age * age;
                pc.Rect.anchoredPosition = pos;
                pc.Rect.Rotate(0, 0, pc.Spin * Time.unscaledDeltaTime);
                pc.Group.alpha = Mathf.Clamp01(1f - p);
                if (p >= 1f)
                {
                    Object.Destroy(pc.Go);
                    pieces.RemoveAt(i);
                }
            }
        }

        // the result box shakes while the bones are crushed
        float since = now - crushAt;
        RectTransform box = slotBoxes[2];
        if (box != null)
        {
            if (since >= 0 && since < 0.55f)
            {
                float k = 1f - since / 0.55f;
                box.anchoredPosition = slotBoxBase[2] + new Vector2(UnityEngine.Random.Range(-7f, 7f), UnityEngine.Random.Range(-5f, 5f)) * k;
            }
            else if (since >= 0.55f && since < 0.7f)
            {
                box.anchoredPosition = slotBoxBase[2];
            }
        }
    }
}
