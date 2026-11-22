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
    private static void MemoryCards(ObscuredMemory memory, float top)
    {
        ItemDataUnpacked data = item.TryCast<ItemDataUnpacked>();
        int group = memory.AffixType == (int)AffixList.AffixType.PREFIX ? AffixCard.Prefix : AffixCard.Suffix;
        string side = group == AffixCard.Prefix ? "Prefix memories" : "Suffix memories";
        Label(side, CenterX - 220f, top, 440f, 21f, 13f, AffixCard.GroupColour(group), TextAlignmentOptions.Center);
        top += 27f;
        var choices = memory.Choices.Where(choice => Items_Reminiscence.MatchesMemorySide(memory, choice)).ToList();
        affixRevealFrames ??= OrbArt.Sheet("affix_mist_reveal_sheet", 4, 4);
        affixVeilFrames ??= OrbArt.Sheet("affix_mist_veil_sheet", 4, 4);
        for (int i = 0; i < choices.Count; i++)
        {
            MemoryChoice choice = choices[i];
            ItemAffix preview = Items_Reminiscence.Preview(item, memory, choice);
            string text = string.IsNullOrEmpty(preview.affixName) ? "Affix " + choice.AffixId : preview.affixName;
            if (data != null)
            {
                try
                {
                    string formatted = TooltipItemManager.FormatAffix(data, preview, TooltipMode.DefaultInfo,
                        TooltipItemManager.SlotType.CRAFTING, false, -1, -1, false, false, ValueRangeFormat.None, true, false);
                    if (!string.IsNullOrWhiteSpace(formatted))
                    {
                        text = LineBreaks.Replace(ColourTags.Replace(formatted, ""), "\n");
                    }
                }
                catch (Exception ex)
                {
                    Main.logger_instance?.Warning("Memory preview: " + ex.Message);
                }
            }

            float x = i % 2 == 0 ? CenterX - AffixCard.Width - 6f : CenterX + 6f;
            float y = top + i / 2 * (AffixCard.Height + 8f);
            var card = new AffixCardData { Text = text, Tier = memory.Tier + 1, Group = group, On = choice == chosenMemory };
            AffixCardView view = AffixCard.Build(Content, labelTemplate, bodyFont, card, x, y, dynamic);
            memoryViews.Add(view);
            CanvasGroup face = view.Bg.gameObject.AddComponent<CanvasGroup>();
            Vector2 center = new(x + AffixCard.Width * 0.5f, -y - AffixCard.Height * 0.5f);
            memoryCardVfx.Add((face, MemoryMistLayer(center), MemoryMistLayer(center)));
            Button button = view.Bg.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener((UnityEngine.Events.UnityAction)(() =>
            {
                if (!face.interactable || animating || MemoryClosing) return;
                chosenMemory = choice;
                status = "";
                dirty = true;
            }));
        }
        TickMemoryCards(Time.unscaledTime);
    }

    private static Image MemoryMistLayer(Vector2 center)
    {
        Image mist = CraftingWidgets.MakeImage(Content, "MemoryCardMist", Color.clear);
        mist.raycastTarget = false;
        mist.enabled = false;
        RectTransform rect = mist.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = center;
        rect.sizeDelta = new Vector2(AffixCard.Width * 1.12f, AffixCard.Height * 1.66f);
        dynamic.Add(mist.gameObject);
        return mist;
    }

    private static float MemoryCardAlpha(float age) => Ease(Mathf.InverseLerp(0.23f, 0.50f, age));

    private static void DrawMemoryMist(Image layer, Sprite frame, float opacity)
    {
        layer.enabled = frame != null && opacity > 0.002f;
        if (!layer.enabled) return;
        layer.sprite = frame;
        layer.color = new Color(1f, 1f, 1f, opacity);
    }

    private static void TickMemoryCards(float now)
    {
        float coverDuration = MemoryRefuseDuration + Mathf.Max(0, memoryCardVfx.Count - 1) * MemoryRefuseStagger;
        if (MemoryClosing && now - memoryRefuseAt >= coverDuration)
        {
            ResetMemoryView();
            if (selectedKey == AdvancedMaterials.ReverieOrb) selectedKey = null;
            foreach (var card in memoryCardVfx)
            {
                card.Face.alpha = 0f;
                card.Face.blocksRaycasts = false;
                card.Mist.enabled = false;
                card.NextMist.enabled = false;
            }
            dirty = true;
            return;
        }
        float coverAt = revealRerolledMemories && animating ? animStart + EndAt - coverDuration : memoryRefuseAt;
        for (int i = 0; i < memoryCardVfx.Count; i++)
        {
            var card = memoryCardVfx[i];
            float revealAge = now - memoryRevealAt - i * MemoryRevealStagger;
            float faceAlpha = MemoryCardAlpha(revealAge);
            float mistAlpha = Ease(Mathf.InverseLerp(0f, 0.12f, revealAge))
                * (1f - Ease(Mathf.InverseLerp(0.43f, MemoryRevealDuration, revealAge)));
            float mistProgress = Mathf.Clamp01(revealAge / MemoryRevealDuration);
            Sprite[] frames = affixRevealFrames;
            if (now >= coverAt)
            {
                float coverAge = now - coverAt - i * MemoryRefuseStagger;
                float faceAtCover = MemoryCardAlpha(coverAt - memoryRevealAt - i * MemoryRevealStagger);
                faceAlpha = faceAtCover * (1f - Ease(Mathf.InverseLerp(0.16f, 0.40f, coverAge)));
                if (coverAge >= 0f)
                {
                    frames = affixVeilFrames;
                    mistProgress = Mathf.Clamp01(coverAge / MemoryRefuseDuration);
                    mistAlpha = Ease(Mathf.InverseLerp(0f, 0.12f, coverAge))
                        * (1f - Ease(Mathf.InverseLerp(0.48f, MemoryRefuseDuration, coverAge)));
                }
                else
                {
                    mistAlpha *= 1f - Ease(Mathf.InverseLerp(0f, 0.12f, now - coverAt));
                }
            }
            card.Face.alpha = faceAlpha;
            card.Face.interactable = card.Face.blocksRaycasts = !animating && !MemoryClosing && faceAlpha >= 0.99f;
            float sample = mistProgress * (frames.Length - 1);
            int frame = (int)sample;
            int next = Mathf.Min(frame + 1, frames.Length - 1);
            float blend = sample - frame;
            DrawMemoryMist(card.Mist, frames[frame], mistAlpha * (1f - blend));
            DrawMemoryMist(card.NextMist, frames[next], mistAlpha * blend);
        }
    }
    private static uint unveiledItemId;
    private static MemoryChoice chosenMemory;
    private static readonly List<AffixCardView> memoryViews = new();
    private static readonly List<(CanvasGroup Face, Image Mist, Image NextMist)> memoryCardVfx = new();
    private static Sprite[] affixRevealFrames, affixVeilFrames;
    private const float MemoryRevealStagger = 0.28f, MemoryRefuseStagger = 0.08f;
    private const float MemoryRevealDuration = 0.70f, MemoryRefuseDuration = 0.72f;
    private static float memoryRevealAt;
    private static float memoryRefuseAt = float.PositiveInfinity;
    private static bool revealRerolledMemories;
    private static bool MemoryClosing => !float.IsPositiveInfinity(memoryRefuseAt);

    private static void ResetMemoryView()
    {
        unveiledItemId = 0;
        chosenMemory = null;
        memoryRefuseAt = float.PositiveInfinity;
        revealRerolledMemories = false;
    }
}
