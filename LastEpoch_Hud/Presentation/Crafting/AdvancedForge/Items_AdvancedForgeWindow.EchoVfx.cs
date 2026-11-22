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
    private static Sprite[] echoPulseFrames;
    private static Sprite echoStrandSprite;
    private const float EchoStrandTravel = 0.62f, EchoStrandTail = 0.22f;
    private static readonly List<(Image Image, RectTransform Rect, Vector2 From, Vector2 To, float Born, float Duration, float Opacity, bool ItemGhost)> echoPulses = new();
    private static readonly List<EchoStrand> echoStrands = new();

    private sealed class EchoStrand
    {
        public GameObject Root;
        public Image[] Segments;
        public Vector2 From, Bend, To;
        public Color Tint;
        public float Born;
    }

    private static void StartEchoPulse(Vector2 from, Vector2 to, float born, float duration, float size, float opacity)
    {
        echoPulseFrames ??= OrbArt.Sheet("echo_pulse_sheet", 4, 4);
        if (echoPulseFrames[0] == null) return;
        Image image = CentredImage("EchoPulse", echoPulseFrames[0], size, size, out RectTransform rect);
        rect.anchoredPosition = from;
        image.color = Color.clear;
        echoPulses.Add((image, rect, from, to, born, duration, opacity, false));
    }

    private static void PlayEchoReveal(float now)
    {
        Sprite itemSprite = icon?.Content;
        for (int i = 0; i < 3; i++)
        {
            float beat = now + 0.19f + i * 0.19f;
            StartEchoPulse(SlotCentre, SlotCentre, beat, 0.56f, 216f + 29f * i, 1f - 0.12f * i);
            if (itemSprite != null)
            {
                Image ghost = CentredImage("EchoItemAfterimage", itemSprite, ItemW - 20f, ItemH - 20f, out RectTransform ghostRect);
                ghostRect.anchoredPosition = SlotCentre;
                ghost.color = Color.clear;
                echoPulses.Add((ghost, ghostRect, SlotCentre, SlotCentre, beat, 0.41f, 0.66f - i * 0.10f, true));
            }
        }
        float launch = now + 1.00f;
        int count = shownEchoes.Options.Count;
        echoRevealedAt = launch + EchoStrandTravel;
        echoRevealEndsAt = echoRevealedAt + (count - 1) * EchoArrivalStagger + EchoCardFadeTime + 0.08f;
        for (int i = 0; i < count; i++)
        {
            float depart = launch + i * EchoArrivalStagger;
            Vector2 destination = EchoCardCenter(i);
            StartEchoStrand(destination, depart, i);
            StartEchoPulse(destination, destination, depart + EchoStrandTravel, 0.42f, 162f, 0.92f);
        }
    }

    private static Sprite EchoStrandSprite()
    {
        if (echoStrandSprite != null) return echoStrandSprite;
        const int width = 32, height = 16;
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.HideAndDontSave,
        };
        var pixels = new Color32[width * height];
        for (int y = 0; y < height; y++)
        {
            float distance = Mathf.Abs((y + 0.5f) * 2f / height - 1f);
            float glow = 0.64f * Mathf.Exp(-distance * distance * 17f)
                + 0.36f * Mathf.Exp(-distance * distance * 3.5f);
            for (int x = 0; x < width; x++)
            {
                float ends = Mathf.Min(1f, Mathf.Min(x, width - 1 - x) / 5f);
                pixels[y * width + x] = new Color32(255, 255, 255, (byte)(255f * glow * ends * (1f - distance)));
            }
        }
        texture.SetPixels32(pixels);
        texture.Apply();
        echoStrandSprite = Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 100f);
        echoStrandSprite.hideFlags = HideFlags.HideAndDontSave;
        return echoStrandSprite;
    }

    private static void StartEchoStrand(Vector2 destination, float born, int index)
    {
        const int segments = 18;
        GameObject root = Effect("EchoStrand");
        float side = index - 1f;
        var strand = new EchoStrand
        {
            Root = root,
            Segments = new Image[segments],
            From = SlotCentre,
            To = destination,
            Bend = SlotCentre + new Vector2(side == 0f ? 34f : side * 183f, -106f),
            Tint = new Color(0.45f + index * 0.13f, 0.91f + index * 0.04f, 1f - index * 0.055f),
            Born = born,
        };
        Sprite sprite = EchoStrandSprite();
        for (int i = 0; i < segments; i++)
        {
            Image segment = CraftingWidgets.MakeImage(root.transform, "Wave", Color.clear);
            segment.sprite = sprite;
            segment.raycastTarget = false;
            strand.Segments[i] = segment;
        }
        echoStrands.Add(strand);
    }

    private static Vector2 EchoStrandPoint(EchoStrand strand, float position, float age)
    {
        Vector2 left = Vector2.Lerp(strand.From, strand.Bend, position);
        Vector2 right = Vector2.Lerp(strand.Bend, strand.To, position);
        float wave = Mathf.Sin(position * 18f - age * 16f) * Mathf.Sin(position * Mathf.PI) * 9f;
        return Vector2.Lerp(left, right, position) + Vector2.right * wave;
    }

    private static void TickEchoStrands(float now)
    {
        for (int i = echoStrands.Count - 1; i >= 0; i--)
        {
            EchoStrand strand = echoStrands[i];
            float age = now - strand.Born;
            if (strand.Root == null || age >= EchoStrandTravel + EchoStrandTail)
            {
                if (strand.Root != null)
                {
                    effects.Remove(strand.Root);
                    Object.Destroy(strand.Root);
                }
                echoStrands.RemoveAt(i);
                continue;
            }
            if (age < 0f) continue;
            float head = Ease(Mathf.Clamp01(age / EchoStrandTravel));
            float tail = Ease(Mathf.Clamp01((age - EchoStrandTail) / EchoStrandTravel));
            float fade = Mathf.InverseLerp(0f, 0.10f, age) *
                (1f - Mathf.InverseLerp(EchoStrandTravel + 0.06f, EchoStrandTravel + EchoStrandTail, age));
            Vector2 previous = EchoStrandPoint(strand, tail, age);
            for (int j = 0; j < strand.Segments.Length; j++)
            {
                float along = (j + 1f) / strand.Segments.Length;
                float position = Mathf.Lerp(tail, head, along);
                Vector2 point = EchoStrandPoint(strand, position, age);
                Vector2 delta = point - previous;
                Image segment = strand.Segments[j];
                float beat = 0.5f + 0.5f * Mathf.Sin(position * 49f - age * 26f);
                segment.rectTransform.anchoredPosition = (point + previous) * 0.5f;
                segment.rectTransform.sizeDelta = new Vector2(delta.magnitude * 1.34f, (5f + 6f * along) * (0.8f + 0.45f * beat));
                segment.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
                Color color = Color.Lerp(strand.Tint, Color.white, beat * 0.66f);
                color.a = fade * along * (0.56f + 0.44f * beat);
                segment.color = color;
                previous = point;
            }
        }
    }

    private static void PlayEchoCommit(Vector2 origin)
    {
        float now = Time.unscaledTime;
        for (int i = 0; i < 3; i++)
        {
            StartEchoPulse(origin, SlotCentre, now + i * 0.13f, 0.65f, 139f + 20f * i, 0.95f);
            StartEchoPulse(SlotCentre, SlotCentre, now + 0.46f + i * 0.13f, 0.64f, 211f + 25f * i, 0.94f - 0.14f * i);
        }
    }

    private static void TickEchoPulses(float now)
    {
        for (int i = echoPulses.Count - 1; i >= 0; i--)
        {
            var pulse = echoPulses[i];
            float age = now - pulse.Born;
            if (pulse.Image == null || age >= pulse.Duration)
            {
                if (pulse.Rect != null)
                {
                    effects.Remove(pulse.Rect.gameObject);
                    Object.Destroy(pulse.Rect.gameObject);
                }
                echoPulses.RemoveAt(i);
                continue;
            }
            if (age < 0f) continue;
            float progress = age / pulse.Duration;
            pulse.Rect.anchoredPosition = Vector2.Lerp(pulse.From, pulse.To, Ease(progress));
            if (pulse.ItemGhost)
            {
                pulse.Rect.localScale = Vector3.one * Mathf.Lerp(1f, 1.32f, Ease(progress));
                pulse.Image.color = new Color(0.67f, 1f, 0.94f, Mathf.Sin(progress * Mathf.PI) * (1f - progress) * pulse.Opacity);
                continue;
            }
            int frame = Mathf.Min(echoPulseFrames.Length - 1, (int)(progress * echoPulseFrames.Length));
            pulse.Image.sprite = echoPulseFrames[frame];
            pulse.Image.color = new Color(0.81f, 1f, 0.98f, pulse.Opacity);
        }
    }
}
