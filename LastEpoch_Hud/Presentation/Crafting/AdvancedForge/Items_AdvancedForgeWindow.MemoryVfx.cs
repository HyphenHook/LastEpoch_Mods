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
    private static readonly List<(Image Image, RectTransform Rect, float Born, int Layer, bool Pale)> memoryMist = new();

    private static void StartMemoryMist(bool pale = false, float delay = 0f)
    {
        Sprite veil = OrbArt.Named("memory_unveil_mist");
        if (veil == null)
        {
            return;
        }
        float now = Time.unscaledTime;
        for (int i = 0; i < 3; i++)
        {
            Image mist = CentredImage("UnveiledMemoryMist", veil, ItemW, ItemH, out RectTransform rect, glow: i == 1);
            mist.color = Color.clear;
            rect.anchoredPosition = SlotCentre;
            memoryMist.Add((mist, rect, now + delay + i * 0.11f, i, pale));
        }
    }

    private static void TickMemoryMist(float now)
    {
        for (int i = memoryMist.Count - 1; i >= 0; i--)
        {
            var mist = memoryMist[i];
            float age = now - mist.Born;
            if (mist.Image == null || age >= 1.25f)
            {
                if (mist.Rect != null)
                {
                    effects.Remove(mist.Rect.gameObject);
                    Object.Destroy(mist.Rect.gameObject);
                }
                memoryMist.RemoveAt(i);
                continue;
            }

            float progress = Mathf.Clamp01(age / 1.25f);
            float opacity = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(age / 0.19f)) * Mathf.Pow(1f - progress, 1.45f);
            float direction = mist.Layer == 1 ? -1f : 1f;
            mist.Rect.anchoredPosition = SlotCentre + new Vector2(direction * progress * 48f, -23f + progress * 70f);
            mist.Rect.sizeDelta = new Vector2(ItemW + progress * 112f, ItemH + progress * 56f);
            mist.Rect.localRotation = Quaternion.Euler(0f, 0f, direction * (15f + progress * 31f));
            mist.Image.color = mist.Pale ? new Color(1f, 1f, 1f, opacity * 0.84f)
                : mist.Layer == 1 ? new Color(0.95f, 0.81f, 1f, opacity * 0.83f)
                : new Color(0.72f, 0.60f, 1f, opacity * 0.68f);
        }
    }

    private static readonly List<(Image Image, RectTransform Rect, Vector2 Source, Vector2 Scatter, float Born, float Spin)> reverieShards = new();

    private static void StartReverieScatter()
    {
        Vector2 source = SlotCentre + Vector2.up * (ItemH * 0.65f);
        foreach (OrbView orb in orbViews)
        {
            if (orb.Material.Key == AdvancedMaterials.ReverieOrb)
            {
                source = orb.Rect.anchoredPosition + new Vector2(OrbSize * 0.5f, -OrbSize * 0.5f);
                break;
            }
        }

        if (animating && pendingKey == AdvancedMaterials.ReverieOrb) source = OrbPoint;
        StartMemoryMist(pale: true, delay: 0.24f);
        StartOrbSeal(AdvancedMaterials.ReverieOrb, 0.63f);
        Sprite sprite = OrbArt.Diamond();
        if (sprite == null) return;

        float now = Time.unscaledTime;
        for (int i = 0; i < 13; i++)
        {
            float angle = i * 2.39996f;
            Vector2 scatter = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle) * 0.58f) * (40f + i * 4f);
            Image shard = CentredImage("ReverieCrystal", sprite, 11f + i % 3 * 3f, 27f + i % 4 * 6f,
                out RectTransform rect, glow: true);
            rect.anchoredPosition = source;
            shard.color = Color.clear;
            reverieShards.Add((shard, rect, source, scatter, now + i % 5 * 0.044f, (i % 2 == 0 ? 1f : -1f) * (290f + i * 17f)));
        }
    }

    private static void TickReverieScatter(float now)
    {
        for (int i = reverieShards.Count - 1; i >= 0; i--)
        {
            var shard = reverieShards[i];
            float age = now - shard.Born;
            if (shard.Image == null || age >= 0.89f)
            {
                if (shard.Rect != null)
                {
                    effects.Remove(shard.Rect.gameObject);
                    Object.Destroy(shard.Rect.gameObject);
                }
                reverieShards.RemoveAt(i);
                continue;
            }

            float burst = Ease(Mathf.Clamp01(age / 0.24f));
            float pull = Ease(Mathf.InverseLerp(0.24f, 0.82f, age));
            Vector2 arc = shard.Scatter.normalized * (24f * Mathf.Sin(pull * Mathf.PI));
            shard.Rect.anchoredPosition = Vector2.Lerp(shard.Source + shard.Scatter * burst, SlotCentre, pull) + arc;
            shard.Rect.localRotation = Quaternion.Euler(0f, 0f, shard.Spin * age);
            shard.Rect.localScale = Vector3.one * Mathf.Lerp(1f, 0.26f, pull);
            float alpha = Mathf.Clamp01(age / 0.12f) * (1f - Mathf.InverseLerp(0.77f, 0.89f, age));
            shard.Image.color = new Color(1f, 0.94f, 1f, alpha);
        }
    }
}
