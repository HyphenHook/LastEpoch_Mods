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
    private static Vector2 EchoCenter => (ACenter + BCenter) * 0.5f;
    private static readonly List<Image> wovenLights = new();
    private const int EchoMotesPerArm = 8;
    private static float nextWovenTick;
    private static bool wovenNeedsPose, silkSettled, eyesSettled, mirrorSettled;
    private static Image echoBody, echoLight, mirrorLiquid, mirrorCurrent, mirrorFrost;
    private static readonly List<(Image Image, Vector2 From, Vector2 To, Vector2 Anchor)> webStrands = new();
    private static readonly List<Image> wovenEyes = new();
    private static ItemData reflectedItem;
    private static readonly ItemData[] wovenItems = new ItemData[2];
    private static readonly float[] wovenEntered = new float[2];

    private static Image WovenImage(Transform parent, string name, Sprite sprite, Vector2 position, Vector2 size)
    {
        Image image = CraftingWidgets.MakeImage(parent, name, Color.clear);
        image.sprite = sprite;
        image.raycastTarget = false;
        RectTransform rect = image.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return image;
    }

    private static void DressWovenSlot(Image box, int id, ItemData item)
    {
        if (wovenItems[id] != item)
        {
            wovenItems[id] = item;
            if (id == 0 && reflectedItem != item)
            {
                reflectedItem = null;
            }
            wovenEntered[id] = Time.unscaledTime;
        }
        Sprite frame = OrbArt.Named(id == 0 ? "woven_item_frame" : "woven_mirror_frame");
        if (frame == null)
        {
            return;
        }

        Vector2 center = new(IconW * 0.5f, -IconH * 0.5f);
        // The left opening is taller and narrower than the oval opening in the mirror texture.
        Vector2 frameSize = id == 0 ? new Vector2(181f, 297f) : new Vector2(160f, 284f);
        var itemArt = new List<Transform>();
        for (int i = 0; i < box.transform.childCount; i++)
        {
            itemArt.Add(box.transform.GetChild(i));
        }

        Color baseColor = box.color;
        box.color = Color.clear;
        Image aperture = WovenImage(box.transform, "SlotAperture", FrameOpening(frame, id), center, frameSize);
        aperture.color = Color.white;
        aperture.gameObject.AddComponent<Mask>().showMaskGraphic = false;
        aperture.transform.SetAsFirstSibling();
        Vector2 surfaceCenter = new(frameSize.x * 0.5f, -frameSize.y * 0.5f);
        Image backing = WovenImage(aperture.transform, "SlotWell", null, surfaceCenter, frameSize);
        backing.color = id == 0 ? new Color(baseColor.r, baseColor.g, baseColor.b) : new Color(0.075f, 0.14f, 0.21f);

        if (id == 1)
        {
            Sprite liquid = OrbArt.Named("woven_mirror_liquid");
            mirrorLiquid = WovenImage(aperture.transform, "LiquidSilver", liquid, surfaceCenter, new Vector2(190f, 276f));
            mirrorCurrent = WovenImage(aperture.transform, "SilverCurrent", liquid, surfaceCenter, new Vector2(184f, 263f));
        }

        var iconLayer = new GameObject("SlotItemArt");
        RectTransform iconRect = iconLayer.AddComponent<RectTransform>();
        iconLayer.transform.SetParent(aperture.transform, false);
        iconRect.anchorMin = iconRect.anchorMax = new Vector2(0.5f, 0.5f);
        iconRect.sizeDelta = new Vector2(IconW, IconH);
        iconRect.anchoredPosition = Vector2.zero;
        foreach (Transform art in itemArt)
        {
            art.SetParent(iconLayer.transform, false);
            if (art.name == "ItemBack")
            {
                // Push the native sprite's inset edge behind the rim. The stencil limits the visible fill.
                CraftingWidgets.Stretch(art.GetComponent<RectTransform>(), -16f);
            }
        }

        Image border = WovenImage(box.transform, "WovenFrame", frame, center, frameSize);
        border.color = Color.white;
        if (id == 0)
        {
            Sprite glint = OrbArt.Named("sparkle_star");
            BuildSilkWeb(box.transform, center);

            Vector2[] eyeSockets = { new(0, 103), new(-72, 98), new(71, 98), new(-83, 2),
                new(83, 2), new(-67, -107), new(67, -107), new(0, -110) };
            foreach (Vector2 socket in eyeSockets)
            {
                Vector2 scaledSocket = new(socket.x * frameSize.x / 222f, socket.y * frameSize.y / 306f);
                Image eye = WovenImage(box.transform, "CrimsonEye", glint, center + scaledSocket, Vector2.zero);
                eye.material = Items_AdvancedForgeWindow.Additive();
                wovenEyes.Add(eye);
            }
        }
        else
        {
            mirrorFrost = WovenImage(aperture.transform, "FrozenWeave", OrbArt.Named("woven_mirror_frost"),
                surfaceCenter + Vector2.up * 4f, new Vector2(162f, 260f));
            border.transform.SetAsLastSibling();
            mirrorFrost.type = Image.Type.Filled;
            mirrorFrost.fillMethod = Image.FillMethod.Vertical;
            mirrorFrost.fillOrigin = (int)Image.OriginVertical.Bottom;
        }
    }

    private static readonly Sprite[] frameOpenings = new Sprite[2];

    private static Sprite FrameOpening(Sprite frame, int id)
    {
        if (frameOpenings[id] != null) return frameOpenings[id];

        // Trace the connected transparent opening from the middle of the frame.
        // The stencil cuts away the rest of the surface even when the liquid overscans the frame.
        const int width = 128, height = 170;
        Texture2D source = frame.texture;
        var sourcePixels = source.GetPixels32();
        var clear = new bool[width * height];
        var mask = new Color32[width * height];
        for (int y = 0; y < height; y++)
        {
            int sy = y * source.height / height;
            for (int x = 0; x < width; x++)
            {
                int sx = x * source.width / width;
                clear[y * width + x] = sourcePixels[sy * source.width + sx].a < 112;
            }
        }

        var frontier = new Queue<int>();
        void Visit(int x, int y)
        {
            int at = y * width + x;
            if (!clear[at]) return;
            clear[at] = false;
            frontier.Enqueue(at);
        }

        Visit(width / 2, height / 2);
        bool touchesExterior = frontier.Count == 0;
        while (frontier.Count != 0)
        {
            int at = frontier.Dequeue();
            int x = at % width, y = at / width;
            mask[at] = new Color32(255, 255, 255, 255);
            if (x == 0 || y == 0 || x == width - 1 || y == height - 1)
            {
                touchesExterior = true;
                break;
            }
            Visit(x - 1, y);
            Visit(x + 1, y);
            Visit(x, y - 1);
            Visit(x, y + 1);
        }

        if (touchesExterior)
        {
            for (int y = 0; y < height; y++)
            {
                float v = (y / (height - 1f) - 0.5f) / 0.34f;
                for (int x = 0; x < width; x++)
                {
                    float u = (x / (width - 1f) - 0.5f) / 0.39f;
                    bool inside = id == 0 ? Mathf.Abs(u) < 0.84f && Mathf.Abs(v) < 0.83f : u * u + v * v <= 1f;
                    mask[y * width + x] = new Color32(255, 255, 255, inside ? (byte)255 : (byte)0);
                }
            }
        }

        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.SetPixels32(mask);
        texture.Apply(false, true);
        texture.hideFlags = HideFlags.HideAndDontSave;
        frameOpenings[id] = Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f);
        frameOpenings[id].hideFlags = HideFlags.HideAndDontSave;
        return frameOpenings[id];
    }

    private static void BuildSilkWeb(Transform parent, Vector2 center)
    {
        const int spokes = 10;
        var rim = new Vector2[spokes];
        Vector2 hub = center + new Vector2(-13f, 9f);
        float halfW = IconW * 0.5f;
        float halfH = IconH * 0.5f;
        for (int i = 0; i < spokes; i++)
        {
            float angle = (i + 0.16f) * Mathf.PI * 2f / spokes;
            Vector2 direction = new(Mathf.Cos(angle), Mathf.Sin(angle));
            float radius = Mathf.Min(halfW / Mathf.Max(0.01f, Mathf.Abs(direction.x)),
                halfH / Mathf.Max(0.01f, Mathf.Abs(direction.y)));
            rim[i] = center + direction * radius;
            AddSilk(parent, hub, rim[i], rim[i]);
        }

        // Sagging threads connect the spokes into offset rings. Each strand retracts toward a rim anchor.
        for (int ring = 1; ring <= 3; ring++)
        {
            for (int i = 0; i < spokes; i++)
            {
                int next = (i + 1) % spokes;
                float ratio = ring * 0.25f;
                Vector2 a = Vector2.Lerp(hub, rim[i], ratio + 0.025f * Mathf.Sin(i * 2.3f));
                Vector2 b = Vector2.Lerp(hub, rim[next], ratio + 0.025f * Mathf.Sin(next * 2.3f));
                Vector2 joint = Vector2.Lerp((a + b) * 0.5f, hub, 0.12f);
                AddSilk(parent, a, joint, rim[i]);
                AddSilk(parent, joint, b, rim[next]);
            }
        }
    }

    private static void AddSilk(Transform parent, Vector2 from, Vector2 to, Vector2 anchor)
    {
        Image strand = WovenImage(parent, "SilkStrand", null, (from + to) * 0.5f, Vector2.zero);
        webStrands.Add((strand, from, to, anchor));
    }

    private static void BuildWovenEcho()
    {
        Sprite spiral = OrbArt.Named("woven_echo_spiral");
        echoBody = WovenImage(Content, "WovenEchoSilk", spiral, EchoCenter, new Vector2(141f, 141f));
        echoLight = WovenImage(Content, "WovenEchoEmission", spiral, EchoCenter, new Vector2(115f, 115f));
        echoLight.material = Items_AdvancedForgeWindow.Additive();
        dynamic.Add(echoBody.gameObject);
        dynamic.Add(echoLight.gameObject);
        Sprite light = OrbArt.Named("sparkle_star");
        for (int i = 0; i < EchoMotesPerArm * 2; i++)
        {
            Image mote = WovenImage(Content, "WovenEcho", light, EchoCenter, new Vector2(9f, 4f));
            dynamic.Add(mote.gameObject);
            wovenLights.Add(mote);
        }
    }

    private static void TickWoven(float now)
    {
        if (now < nextWovenTick && !wovenNeedsPose)
        {
            return;
        }

        bool initialPose = wovenNeedsPose;
        wovenNeedsPose = false;
        nextWovenTick = now + 1f / 30f;
        bool failed = animating && !animSuccess && shattered;
        float energy = animating && shattered ? 1f : 0f;
        if (echoBody != null && echoLight != null)
        {
            float pulse = 1f + 0.055f * Mathf.Sin(now * 4f);
            echoBody.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -now * 42f);
            echoLight.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -now * 68f + 63f);
            echoBody.rectTransform.localScale = Vector3.one * pulse;
            echoBody.color = failed ? new Color(1f, 0.25f, 0.30f, 0.92f) : new Color(1f, 1f, 1f, 0.69f + 0.22f * energy);
            echoLight.color = failed ? new Color(1f, 0.11f, 0.17f, 0.48f) : new Color(0.64f, 0.77f, 1f, 0.28f + 0.17f * energy);
        }
        for (int i = 0; i < wovenLights.Count; i++)
        {
            Image light = wovenLights[i];
            if (light == null) continue;
            float run = Mathf.Repeat(now * (0.43f + 0.35f * energy) + (i % EchoMotesPerArm) / (float)EchoMotesPerArm, 1f);
            float angle = now * -0.9f + run * Mathf.PI * 3.4f + (i / EchoMotesPerArm) * Mathf.PI;
            float radius = Mathf.Lerp(64f, 6f, run);
            light.rectTransform.anchoredPosition = EchoCenter + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            light.rectTransform.localRotation = Quaternion.Euler(0f, 0f, angle * Mathf.Rad2Deg);
            float alpha = 0.66f * Mathf.Sin(run * Mathf.PI);
            light.color = failed ? new Color(1f, 0.17f, 0.13f, alpha)
                : i < EchoMotesPerArm ? new Color(0.32f, 0.56f, 1f, alpha) : new Color(1f, 0.76f, 0.33f, alpha);
        }

        float webAge = now - wovenEntered[0];
        bool itemLoaded = wovenItems[0] != null;
        Vector2 center = new(IconW * 0.5f, -IconH * 0.5f);
        for (int i = 0; (initialPose || !silkSettled) && i < webStrands.Count; i++)
        {
            var strand = webStrands[i];
            float snap = itemLoaded ? Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.025f * (i % 5), 0.39f, webAge)) : 0f;
            Vector2 from = Vector2.Lerp(strand.From, strand.Anchor, snap);
            Vector2 to = Vector2.Lerp(strand.To, strand.Anchor, snap * snap);
            Vector2 length = to - from;
            RectTransform silk = strand.Image.rectTransform;
            silk.anchoredPosition = (from + to) * 0.5f;
            silk.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(length.y, length.x) * Mathf.Rad2Deg);
            silk.sizeDelta = new Vector2(length.magnitude + 0.5f, i < 10 ? 1.2f : 0.8f);
            strand.Image.color = new Color(0.78f, 0.78f, 0.84f, (i < 10 ? 0.65f : 0.47f) * (1f - snap));
            strand.Image.enabled = snap < 1f;
        }
        silkSettled = !itemLoaded || webAge >= 0.39f;
        bool reflected = itemLoaded && reflectedItem == wovenItems[0];
        for (int i = 0; (initialPose || reflected || !eyesSettled) && i < wovenEyes.Count; i++)
        {
            float open = itemLoaded ? Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.12f + i * 0.025f, 0.62f, webAge)) : 0f;
            Image eye = wovenEyes[i];
            eye.rectTransform.sizeDelta = new Vector2(17f, Mathf.Lerp(0.4f, 9f, open));
            eye.enabled = open > 0f;
            if (reflected)
            {
                float flicker = 0.55f + 0.33f * Mathf.Sin(now * 6f + i * 1.8f);
                eye.color = i % 2 == 0 ? new Color(0.22f, 0.43f, 1f, flicker) : new Color(1f, 0.75f, 0.22f, flicker);
            }
            else
            {
                eye.color = new Color(1f, 0.075f, 0.12f, open * 0.86f);
            }
        }

        eyesSettled = !itemLoaded || webAge >= 0.62f;
        if (mirrorLiquid == null || mirrorCurrent == null || mirrorFrost == null) return;
        bool donorLoaded = wovenItems[1] != null;
        float age = Mathf.Max(0f, now - wovenEntered[1]);
        if (mirrorSettled && !animating && !initialPose) return;
        float freeze = donorLoaded ? Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.07f, 0.68f, age)) : 0f;
        float clock = donorLoaded ? wovenEntered[1] + Mathf.Min(0.7f, age) * (1f - freeze * 0.5f) : now;
        float ripple = Mathf.Sin(clock * 3.5f);
        var aperture = mirrorCurrent.rectTransform.parent.GetComponent<RectTransform>();
        Vector2 surfaceCenter = new(aperture.sizeDelta.x * 0.5f, -aperture.sizeDelta.y * 0.5f);
        mirrorCurrent.rectTransform.anchoredPosition = surfaceCenter + new Vector2(2.5f * ripple, Mathf.Cos(clock * 2.9f) * 6f);
        mirrorCurrent.rectTransform.localScale = new Vector3(1f + 0.023f * ripple, 1f - 0.035f * ripple, 1f);
        mirrorCurrent.color = new Color(0.63f, 0.82f, 1f, (1f - freeze) * (0.25f + 0.07f * ripple));
        mirrorLiquid.color = Color.Lerp(new Color(0.91f, 0.95f, 1f, 0.97f), new Color(0.61f, 0.80f, 1f, 0.68f), freeze);
        mirrorCurrent.enabled = freeze < 1f;
        mirrorFrost.fillAmount = freeze;
        mirrorSettled = freeze >= 1f;
        float charge = animating && !shattered ? Mathf.Clamp01((now - animStart) / CrackTime) : 0f;
        Color ice = Color.Lerp(new Color(0.83f, 0.94f, 1f), new Color(1f, 0.36f, 0.49f), charge);
        ice.a = 0.35f + 0.42f * Mathf.Sin(freeze * Mathf.PI) + 0.46f * charge;
        mirrorFrost.color = ice;
    }
}
