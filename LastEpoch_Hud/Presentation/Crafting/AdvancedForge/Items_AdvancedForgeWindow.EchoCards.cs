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
    private static Items_EchoOrb.Echoes shownEchoes;
    private static uint echoItemId;
    private static int selectedEcho = -1;
    private static float echoRevealedAt;
    private static float echoRevealEndsAt;
    private static float echoSelectedAt;
    private static readonly List<ItemData> echoPreviews = new();
    private static readonly List<EchoCardView> echoCards = new();

    private static bool EchoOpen => selectedKey == AdvancedMaterials.EchoOrb && item != null
        && echoItemId == item.individualID && Items_EchoOrb.IsCurrent(item, shownEchoes);

    private static void ResetEchoView()
    {
        shownEchoes = null;
        echoItemId = 0;
        selectedEcho = -1;
        echoPreviews.Clear();
    }

    private static void EchoClicked(bool atImpact = false)
    {
        if (!EchoOpen)
        {
            if (!atImpact)
            {
                StartForgeFx(AdvancedMaterials.EchoOrb);
                return;
            }
            shownEchoes = Items_EchoOrb.Reveal(item, out status);
            if (shownEchoes != null)
            {
                echoItemId = item.individualID;
                selectedEcho = -1;
                echoRevealedAt = Time.unscaledTime;
                echoPreviews.Clear();
                foreach (var rolls in shownEchoes.Options)
                {
                    try
                    {
                        echoPreviews.Add(Items_EchoOrb.Preview(item, rolls));
                    }
                    catch (Exception ex)
                    {
                        Main.logger_instance?.Warning("Echo preview: " + ex.Message);
                        echoPreviews.Add(null);
                    }
                }
            }
            else
            {
                statusColour = Bad;
            }
        }
        else if (selectedEcho >= 0)
        {
            Vector2 origin = echoCards[selectedEcho].Rect.anchoredPosition;
            bool kept = Items_EchoOrb.Keep(item, shownEchoes, selectedEcho, out status);
            statusColour = kept ? Good : Bad;
            if (kept) status = "";
            if (kept)
            {
                PlayEchoCommit(origin);
                ResetEchoView();
                if (AdvancedMaterials.Count(AdvancedMaterials.EchoOrb) == 0)
                {
                    selectedKey = null;
                }
            }
        }
        dirty = true;
    }

    private const TooltipItemManager.SlotType ForgeTooltipSlot = TooltipItemManager.SlotType.CRAFTING;
    private const float EchoCardWidth = 190f, EchoCardHeight = 226f, EchoCardStride = 201f;
    private const float EchoArrivalStagger = 0.13f, EchoCardFadeTime = 0.36f;
    private static float EchoCardsTop => SlotY + FrameH + ForgeH + 39f;

    private sealed class EchoCardView
    {
        public RectTransform Rect;
        public Image Art;
        public CanvasGroup Fade;
        public Image[] Frames;
        public float Hover;
    }

    private static Vector2 EchoCardCenter(int index)
        => new(CenterX + (index - 1) * EchoCardStride, -(EchoCardsTop + EchoCardHeight * 0.5f));

    private static float EchoCardAlpha(int index, float now)
        => Ease(Mathf.Clamp01((now - echoRevealedAt - index * EchoArrivalStagger) / EchoCardFadeTime));

    private static string EchoPercentages(byte[] rolls) =>
        string.Join("  ·  ", rolls.Select(roll => (roll / 255f).ToString("0%")));

    private static string EchoRollSummary(Items_EchoOrb.Rolls rolls)
    {
        var lines = new List<string>();
        void Add(string name, byte[] values)
        {
            if (values.Length == 0) return;
            lines.Add("<color=#90cfcf>" + name + "</color>\n" + EchoPercentages(values));
        }
        Add("Implicit", rolls.Implicits);
        Add("Affix", Items_EchoOrb.AffixPreview(item, rolls));
        return string.Join("\n\n", lines);
    }

    private static void BuildEchoCards()
    {
        Sprite plate = OrbArt.Named("echo_choice_plate");
        for (int i = 0; i < shownEchoes.Options.Count; i++)
        {
            int index = i;
            Image hit = CraftingWidgets.MakeImage(Content, "EchoChoice", Color.clear);
            RectTransform rect = hit.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = EchoCardCenter(i);
            rect.sizeDelta = new Vector2(EchoCardWidth, EchoCardHeight);
            dynamic.Add(hit.gameObject);
            var view = new EchoCardView
            {
                Rect = rect,
                Fade = hit.gameObject.AddComponent<CanvasGroup>(),
                Frames = new Image[3],
            };
            view.Fade.alpha = EchoCardAlpha(i, Time.unscaledTime);
            view.Fade.blocksRaycasts = false;
            for (int j = view.Frames.Length - 1; j >= 0; j--)
            {
                view.Frames[j] = EchoCardLayer(hit.transform, "FrameEcho", plate, Color.clear);
            }
            view.Art = EchoCardLayer(hit.transform, "EchoPlate", plate, plate != null ? Color.white : new Color(0.06f, 0.12f, 0.14f));
            Label("Echo " + (i == 0 ? "I" : i == 1 ? "II" : "III"), 14, 25, EchoCardWidth - 28f, 24, 18,
                new Color(0.70f, 0.96f, 0.94f), TextAlignmentOptions.Center, hit.transform);
            GameObject summary = Label(EchoRollSummary(shownEchoes.Options[i]), 16, 57, EchoCardWidth - 32f,
                EchoCardHeight - 83f, 14f, new Color(0.96f, 0.92f, 0.78f), TextAlignmentOptions.Center, hit.transform, body: true);
            TextMeshProUGUI text = summary.GetComponent<TextMeshProUGUI>();
            text.fontStyle = FontStyles.Normal;
            text.enableWordWrapping = true;
            text.enableAutoSizing = true;
            text.fontSizeMin = 10f;
            text.fontSizeMax = 14f;
            text.raycastTarget = false;

            Button button = hit.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener((UnityEngine.Events.UnityAction)(() =>
            {
                if (animating) return;
                bool changed = selectedEcho != index;
                selectedEcho = index;
                echoSelectedAt = Time.unscaledTime;
                status = "";
                if (changed) dirty = true;
            }));
            echoCards.Add(view);
        }
    }

    private static Image EchoCardLayer(Transform parent, string name, Sprite sprite, Color color)
    {
        Image layer = CraftingWidgets.MakeImage(parent, name, color);
        layer.sprite = sprite;
        layer.raycastTarget = false;
        CraftingWidgets.Stretch(layer.rectTransform, 0f);
        return layer;
    }

    private static int TickEchoCards(Vector2 mouse, UnityEngine.Camera cam)
    {
        int hovered = -1;
        float now = Time.unscaledTime;
        for (int i = 0; i < echoCards.Count; i++)
        {
            EchoCardView view = echoCards[i];
            view.Fade.alpha = EchoCardAlpha(i, now);
            bool visible = view.Fade.alpha > 0.35f;
            view.Fade.blocksRaycasts = visible && !animating;
            bool hover = view.Fade.blocksRaycasts && RectTransformUtility.RectangleContainsScreenPoint(view.Rect, mouse, cam);
            if (hover) hovered = i;
            float blend = view.Hover = Mathf.MoveTowards(view.Hover, hover ? 1f : 0f, Time.unscaledDeltaTime * 8f);
            bool chosen = selectedEcho == i;
            if (view.Art.sprite != null)
            {
                Color idle = chosen ? new Color(0.91f, 1f, 1f) : new Color(0.80f, 0.87f, 0.89f);
                view.Art.color = Color.Lerp(idle, Color.white, blend);
            }
            for (int j = 0; j < view.Frames.Length; j++)
            {
                float p = (now - echoSelectedAt - (j - 1) * 0.125f) / 0.55f;
                bool ripple = chosen && j > 0 && p >= 0f && p < 1f;
                float expansion = j == 0 ? Mathf.Lerp(2f, 3.5f, blend) : Mathf.Lerp(2f, 12f, Ease(Mathf.Clamp01(p)));
                float alpha = j == 0 ? Mathf.Max(blend * 0.56f, chosen ? 0.72f : 0f)
                    : ripple ? 0.90f * Mathf.Clamp01(p * 12f) * (1f - Ease(p)) : 0f;
                Image frame = view.Frames[j];
                frame.enabled = alpha > 0f && frame.sprite != null;
                if (frame.enabled)
                {
                    CraftingWidgets.Stretch(frame.rectTransform, -expansion);
                    frame.color = new Color(0.38f, 1f, 0.91f, alpha);
                }
            }
        }
        return hovered;
    }
}
