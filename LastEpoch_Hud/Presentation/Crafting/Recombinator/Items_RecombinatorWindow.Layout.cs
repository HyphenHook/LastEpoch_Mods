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
    private const string ForgePath = "GUI/Panel System/Panel Stacks/Left Panel Stack/ForgePanel (Clone)";
    private static GameObject buttonTemplate; // the forge's own button, used as the model for ours
    private static GameObject labelTemplate; // the forge title, used as the model for text
    private static TMP_FontAsset headFont, bodyFont; // the game's own fonts, read from the forge
    private const float IconW = RecombKit.ResW, IconH = RecombKit.ResH, InY = RecombKit.InY;
    private static float ResY => RecombKit.ResY;
    private const float Left = 48, ColW = 320, Gap = 30;
    private static readonly List<GameObject> dynamic = new(); // rebuilt on each refresh

    // ---------------------------------------------------------------- building the window

    private static bool Build(GameObject forge, Transform parent)
    {
        if (root != null)
        {
            return true;
        }
        Transform main = forge.transform.Find("MainContent");
        if (main == null)
        {
            return false;
        }
        buttonTemplate = main.Find("ViewCraftingMaterialsButton")?.gameObject;
        labelTemplate = main.Find("Header/Title")?.gameObject;
        headFont = labelTemplate?.GetComponent<TextMeshProUGUI>()?.font;
        bodyFont = main.Find("SealedAffixInfoHolder/AffixName")?.GetComponent<TextMeshProUGUI>()?.font ?? headFont;
        Main.logger_instance?.Msg("Recombinator fonts: heading " + (headFont != null ? headFont.name : "none") + ", body " + (bodyFont != null ? bodyFont.name : "none"));
        if (buttonTemplate == null || labelTemplate == null)
        {
            return false;
        }

        root = new GameObject(RootName);
        root.AddComponent<RectTransform>();
        root.AddComponent<UIMouseListener>(); // tells the game the pointer is over UI, so clicks do not move the character
        root.transform.SetParent(parent, false);
        CraftingWidgets.CopyRect(forge.GetComponent<RectTransform>(), root.GetComponent<RectTransform>());

        var content = new GameObject("Content");
        content.AddComponent<RectTransform>();
        content.transform.SetParent(root.transform, false);
        CraftingWidgets.CopyRect(main.GetComponent<RectTransform>(), content.GetComponent<RectTransform>());
        contentRect = content.GetComponent<RectTransform>();
        contentBase = contentRect.anchoredPosition;

        // The forge's own backdrop, frame and header, so the window matches the game.
        foreach (string piece in new[] { "backgroundTile", "backgroundTile (1)", "Framing", "EterraFrameHeader", "Header" })
        {
            Transform source = main.Find(piece);
            if (source != null)
            {
                GameObject copy = Object.Instantiate(source.gameObject, content.transform, false);
                copy.name = piece;
                CraftingWidgets.StripGameLogic(copy);
            }
        }
        Transform title = content.transform.Find("Header/Title");
        if (title != null)
        {
            SetTextOn(title.gameObject, "Recombinator");
        }
        Transform close = content.transform.Find("Header/Close_Button");
        if (close != null)
        {
            CraftingWidgets.OnClick(close.gameObject, Close);
        }
        CaptureForgeArt(main);
        AffixCard.Prepare(main);
        root.SetActive(false);
        return true;
    }

    // ---------------------------------------------------------------- the changing part of the window

    private static Transform Content => root.transform.Find("Content");

    private static void ClearDynamic()
    {
        buttonHoverRims.Clear();
        foreach (GameObject go in dynamic)
        {
            if (go != null)
            {
                Object.Destroy(go);
            }
        }
        dynamic.Clear();
    }

    private static GameObject Label(string text, float x, float y, float w, float h, float size, Color color, TextAlignmentOptions align)
    {
        GameObject go = Object.Instantiate(labelTemplate, Content, false);
        CraftingWidgets.StripGameLogic(go);
        RectTransform r = go.GetComponent<RectTransform>();
        r.anchorMin = r.anchorMax = new Vector2(0, 1);
        r.pivot = new Vector2(0, 1);
        r.anchoredPosition = new Vector2(x, -y);
        r.sizeDelta = new Vector2(w, h);
        TextMeshProUGUI t = go.GetComponent<TextMeshProUGUI>();
        t.text = text;
        TMP_FontAsset font = headFont;
        if (font != null)
        {
            t.font = font;
        }
        t.fontStyle = FontStyles.UpperCase; // all text of the windows is in capitals, in the game's own font
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.enableWordWrapping = true;
        go.SetActive(true);
        dynamic.Add(go);
        return go;
    }

    private static Sprite recombinatorButtonSprite;

    private static Sprite RecombinatorButtonSprite()
    {
        if (recombinatorButtonSprite == null)
        {
            Sprite source = OrbArt.Named("recombinator_button");
            if (source == null)
            {
                return null;
            }

            Texture2D texture = source.texture;
            // Trim the transparent space above and below the plate in the generated sprite.
            float verticalPadding = texture.height * 0.16f;
            Rect bounds = new(0, verticalPadding, texture.width, texture.height - 2f * verticalPadding);
            recombinatorButtonSprite = Sprite.Create(texture, bounds, new Vector2(0.5f, 0.5f), 100f);
            recombinatorButtonSprite.hideFlags = HideFlags.HideAndDontSave;
        }

        return recombinatorButtonSprite;
    }

    private static readonly List<(RectTransform Rect, Outline Rim)> buttonHoverRims = new();

    private static GameObject Btn(string text, float x, float y, float w, float h, Action action, Color? tint = null)
    {
        GameObject go = Object.Instantiate(buttonTemplate, Content, false);
        CraftingWidgets.StripGameLogic(go);
        RectTransform r = go.GetComponent<RectTransform>();
        r.anchorMin = r.anchorMax = new Vector2(0, 1);
        r.pivot = new Vector2(0, 1);
        r.anchoredPosition = new Vector2(x, -y);
        r.sizeDelta = new Vector2(w, h);
        CraftingWidgets.SetText(go, text);
        TextMeshProUGUI t = go.GetComponentInChildren<TextMeshProUGUI>(true);
        if (t != null)
        {
            RectTransform tr = t.GetComponent<RectTransform>();
            tr.anchorMin = Vector2.zero;
            tr.anchorMax = Vector2.one;
            tr.offsetMin = new Vector2(8, 2);
            tr.offsetMax = new Vector2(-8, -2);
            t.enableWordWrapping = false;
            t.overflowMode = TextOverflowModes.Ellipsis;
        }
        HorizontalLayoutGroup layout = go.GetComponent<HorizontalLayoutGroup>();
        if (layout != null)
        {
            Object.Destroy(layout);
        }
        Image face = go.GetComponent<Image>();
        Button button = go.GetComponent<Button>();
        if (face != null && button != null)
        {
            bool selected = tint.HasValue && tint.Value.r > 0.5f;
            foreach (Image decoration in go.GetComponentsInChildren<Image>(true))
            {
                if (decoration != face)
                {
                    decoration.enabled = false;
                }
            }

            face.sprite = RecombinatorButtonSprite();
            face.overrideSprite = null;
            face.type = Image.Type.Simple;
            face.color = selected ? new Color(1f, 0.87f, 0.64f) : Color.white;
            button.targetGraphic = face;
            button.transition = Selectable.Transition.ColorTint;
            ColorBlock colors = ColorBlock.defaultColorBlock;
            colors.normalColor = tint.HasValue && !selected ? new Color(0.50f, 0.50f, 0.50f) : Color.white;
            colors.highlightedColor = new Color(1f, 0.97f, 0.90f);
            colors.selectedColor = colors.highlightedColor;
            colors.pressedColor = new Color(0.62f, 0.46f, 0.32f);
            colors.colorMultiplier = 1.6f;
            colors.fadeDuration = 0.12f;
            button.colors = colors;
        }

        if (face != null)
        {
            Outline rim = go.AddComponent<Outline>();
            rim.effectColor = new Color(1f, 0.66f, 0.25f, 0.72f);
            rim.effectDistance = new Vector2(1.5f, -1.5f);
            rim.useGraphicAlpha = true;
            rim.enabled = false;
            buttonHoverRims.Add((r, rim));
        }

        CraftingWidgets.OnClick(go, action);
        go.SetActive(true);
        dynamic.Add(go);
        return go;
    }

    private static readonly Color Gold = new(0.93f, 0.78f, 0.38f);
    private static readonly Color Dim = new(0.65f, 0.62f, 0.58f);
    private static readonly Color Good = new(0.55f, 0.85f, 0.5f);
    private static readonly Color Bad = new(0.9f, 0.4f, 0.35f);
    private static readonly Color Picked = new(1f, 0.85f, 0.45f);
    private static float SlotX(int i) => (i == 0 ? RecombKit.InCx0 : RecombKit.InCx1) - RecombKit.InW / 2; // the left edge of an input box
    private const float WinW = 670;
    private static float RunY => RecombKit.PipeLevel; // where the two pipes run in toward the centre
    private static float CenterX => Left + WinW / 2;

    private static void Refresh()
    {
        dirty = false;
        ClearDynamic();
        CloseTooltip();
        Array.Clear(slotRects, 0, slotRects.Length);
        Array.Clear(slotItems, 0, slotItems.Length);
        Array.Clear(slotGroups, 0, slotGroups.Length);
        rows.Clear();
        inputFlames.Clear();
        consumeFlames.Clear();
        resultFlames.Clear();
        fossilShell = null;
        collisionLight = null;
        cageRoot = null;

        RecombKit.Ensure(Content);
        RecombKit.ApplyPipeSkin(PrimordialSkin);
        RecombKit.LowerPipes();

        // the two item boxes, evenly spread, with each item's name above
        for (int i = 0; i < 2; i++)
        {
            int index = i;
            ItemData shown = pouring ? pourItems[i] : slots[i];
            SlotBox(shown, i, SlotX(i), InY, () => ClickSlot(index));
        }

        offer = null;
        string why = Items_Recombinator.WhyNot(slots[0], slots[1]);
        if (why.Length == 0)
        {
            offer = Items_Recombinator.Describe(slots[0], slots[1]);
        }

        // the result
        SlotBox(resultRevealed ? resultItem : null, 2, CenterX - IconW / 2, ResY, TakeResult);
        if (pouring || resultRevealed && resultItem != null)
        {
            AddResultFlames();
        }
        // the affixes of both items, one column each side of the result; only pickable in deterministic mode
        if (offer != null && !pouring)
        {
            ListAffixes(offer.AffixesA, Left, RecombKit.AffixTop);
            ListAffixes(offer.AffixesB, Left + WinW - ColumnWidth, RecombKit.AffixTop);
        }
        float y = RecombKit.Bottom + 8;
        List<RecombAffix> picks = Picks();
        if (offer == null)
        {
            if (!pouring && resultItem == null && slots[0] != null && slots[1] != null)
            {
                Label(why, Left, y + 30, WinW, 40, 18, Bad, TextAlignmentOptions.Top);
            }
        }
        else
        {
            long gold;
            int embers;
            bool valid = true;
            if (deterministic)
            {
                gold = RecombinatorMath.Gold(offer.All, offer.AnyExalted);
                embers = RecombinatorMath.Embers(offer.All, offer.AnyExalted);
                double chance = RecombinatorMath.SuccessChance(picks, offer.CapacityA, offer.CapacityB, primordial, offer.AffixesA.Count, offer.AffixesB.Count, offer.MaxPerSide);
                string fail = RecombinatorMath.Validate(picks, Math.Max(offer.CapacityA, offer.CapacityB), offer.MaxPerSide);
                valid = picks.Count > 0 && fail.Length == 0;
                if (valid)
                {
                    Label((chance * 100).ToString("0.#") + "% success", Left, y, WinW, 28, 22, Gold, TextAlignmentOptions.Top);
                }
                else
                {
                    Label(picks.Count == 0 ? "Select affixes to keep" : fail, Left, y, WinW, 28, 18, Bad, TextAlignmentOptions.Top);
                }
            }
            else
            {
                gold = RecombinatorMath.FlatGold;
                embers = RecombinatorMath.FlatEmbers;
            }
            bool usePrimordial = deterministic && primordial;
            int bones = usePrimordial ? RecombinatorMath.Bones(offer.All, offer.AnyExalted) : 0;
            long haveGold = HaveGold();
            int haveEmbers = HaveEmbers();
            long haveBones = HaveBones();
            bool canPay = haveGold >= gold && haveEmbers >= embers && haveBones >= bones;
            FindCurrencyIcons();
            var cost = new List<(Sprite, string, string, Color)>
            {
                (goldIcon, "gold", gold.ToString("N0"), haveGold >= gold ? Good : Bad),
                (emberIcon, "Soul Embers", embers.ToString("N0"), haveEmbers >= embers ? Good : Bad),
            };
            if (usePrimordial)
            {
                cost.Add((bonesIcon, "Ancient Bones", bones.ToString("N0"), haveBones >= bones ? Good : Bad));
            }
            ChipRow(CenterX, y + 28, 20, cost.ToArray());

            bool ready = canPay && valid;
            long cg = gold;
            int ce = embers;
            int cb = bones;
            float by = y + 70;
            Btn(deterministic ? "Deterministic" : "Indeterministic", Left + 43, by, 170, 44, () => SetMode(!deterministic), deterministic ? Picked : (Color?)null);
            Btn("Recombinate", Left + CenterX - Left - 110 - 0, by, 220, 44, () => DoRecombine(cg, ce, cb), ready ? (Color?)null : new Color(0.4f, 0.4f, 0.4f, 0.8f));
            float rx = Left + WinW / 2 + 110 + 18;
            Chip(goldIcon, "gold", haveGold.ToString("N0"), rx, by - 6, 15, Dim);
            Chip(emberIcon, "Soul Embers", haveEmbers.ToString("N0"), rx, by + 14, 15, Dim);
            if (deterministic)
            {
                Chip(bonesIcon, "Ancient Bones", haveBones.ToString("N0"), rx, by + 34, 15, Dim);
            }
            if (deterministic)
            {
                Btn("Primordial Recombination  (x1.5)", CenterX - 160, by + 56, 320, 38, () => { primordial = !primordial; message = ""; dirty = true; }, primordial ? Picked : (Color?)null);
            }
        }
        if (message.Length > 0)
        {
            Label(message, Left, y + 176, WinW, 40, 17, Gold, TextAlignmentOptions.Top);
        }
        RecombKit.Raise();
    }

    // ---------------------------------------------------------------- item slots, tooltips, animation

    private static Sprite goldIcon, bonesIcon, emberIcon;
    internal static Sprite GoldIcon => goldIcon;
    internal static Sprite BonesIcon => bonesIcon;
}
