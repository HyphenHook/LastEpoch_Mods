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
    private static readonly List<(Image Image, RectTransform Rect, Vector2 Velocity, float Start, float Spin)> orbFragments = new();

    private static void SpawnOrbFragments(float now)
    {
        string asset = pendingKey switch
        {
            AdvancedMaterials.ReminiscenceOrb => "orb_reminiscence",
            AdvancedMaterials.ReverieOrb => "orb_reverie",
            AdvancedMaterials.ChaosOrb => "orb_chaos",
            AdvancedMaterials.AttunementOrb => "orb_attunement",
            _ => "orb_encapsulation",
        };
        Sprite[] pieces = OrbArt.Sheet(asset, 3, 3);
        const float tile = 104f / 3f;
        for (int i = 0; i < pieces.Length; i++)
        {
            if (pieces[i] == null) continue;
            Vector2 offset = new((i % 3 - 1) * tile, (1 - i / 3) * tile);
            Vector2 velocity = (offset + new Vector2(i - 4f, 8f)).normalized * (115f + i * 14f);
            Image piece = CentredImage("ShatteredOrbPiece", pieces[i], tile, tile, out RectTransform rect);
            rect.anchoredPosition = OrbPoint + offset;
            orbFragments.Add((piece, rect, velocity, now, i % 2 == 0 ? 190f : -225f));
        }
    }

    private static void TickOrbFragments(float now)
    {
        for (int i = orbFragments.Count - 1; i >= 0; i--)
        {
            var fragment = orbFragments[i];
            float age = now - fragment.Start;
            if (fragment.Image == null || age >= 0.48f)
            {
                if (fragment.Rect != null)
                {
                    effects.Remove(fragment.Rect.gameObject);
                    Object.Destroy(fragment.Rect.gameObject);
                }
                orbFragments.RemoveAt(i);
                continue;
            }
            fragment.Rect.anchoredPosition += (fragment.Velocity + Vector2.down * age * 320f) * Time.unscaledDeltaTime;
            fragment.Rect.localRotation = Quaternion.Euler(0f, 0f, fragment.Spin * age);
            fragment.Image.color = new Color(1f, 1f, 1f, 1f - Mathf.InverseLerp(0.10f, 0.48f, age));
        }
    }

    private static void ShatterBurst(float now)
    {
        if (pendingKey == AdvancedMaterials.AttunementOrb)
        {
            SpawnOrbFragments(now);
            StartAttunementBurst(now);
            return;
        }
        if (pendingKey == AdvancedMaterials.EchoOrb)
        {
            StartEchoPulse(OrbPoint, OrbPoint, now, 0.66f, 242f, 1f);
            StartEchoPulse(OrbPoint, OrbPoint, now + 0.14f, 0.64f, 278f, 0.74f);
            return;
        }
        if (pendingKey == AdvancedMaterials.EncapsulationOrb)
        {
            bloom = CentredImage("ChainReleaseFlash", OrbArt.Named("glow_violet"), 126f, 126f, out bloomRect, glow: true);
            bloomRect.anchoredPosition = OrbPoint;
            return;
        }
        SpawnOrbFragments(now);
        bloom = CentredImage("Bloom", OrbArt.Named("glow_violet"), 260f, 260f, out bloomRect, glow: true);
        bloomRect.anchoredPosition = OrbPoint;
        ringFx = CentredImage("Ring", OrbArt.Named("ring_violet"), 230f, 230f, out ringRect, glow: true);
        ringRect.anchoredPosition = OrbPoint;
        for (int i = 0; i < 14; i++)
        {
            float ang = UnityEngine.Random.Range(0f, Mathf.PI * 2);
            AddSpark(OrbPoint, OrbTone(pendingKey), UnityEngine.Random.Range(2.5f, 5f),
                new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * UnityEngine.Random.Range(80f, 260f), UnityEngine.Random.Range(0.3f, 0.6f));
        }
    }
}
