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
using R = LastEpoch_Hud.Scripts.Mods.Items.Crafting.Items_RecombinatorWindow;

namespace LastEpoch_Hud.Scripts.Mods.Items.Crafting;

internal static partial class Items_MemoryReflectorWindow
{
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
        if (buttonTemplate == null || labelTemplate == null)
        {
            return false;
        }
        headFont = labelTemplate.GetComponent<TextMeshProUGUI>()?.font;
        bodyFont = main.Find("SealedAffixInfoHolder/AffixName")?.GetComponent<TextMeshProUGUI>()?.font ?? headFont;

        root = new GameObject(RootName);
        root.AddComponent<RectTransform>();
        root.AddComponent<UIMouseListener>(); // the pointer is over UI, so clicks do not move the character
        root.transform.SetParent(parent, false);
        CraftingWidgets.CopyRect(forge.GetComponent<RectTransform>(), root.GetComponent<RectTransform>());

        var content = new GameObject("Content");
        content.AddComponent<RectTransform>();
        content.transform.SetParent(root.transform, false);
        CraftingWidgets.CopyRect(main.GetComponent<RectTransform>(), content.GetComponent<RectTransform>());
        contentRect = content.GetComponent<RectTransform>();
        contentBase = contentRect.anchoredPosition;

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
        if (title != null && title.GetComponent<TextMeshProUGUI>() != null)
        {
            title.GetComponent<TextMeshProUGUI>().text = "Memory Reflector";
        }
        Transform close = content.transform.Find("Header/Close_Button");
        if (close != null)
        {
            CraftingWidgets.OnClick(close.gameObject, Close);
        }
        AffixCard.Prepare(main);
        root.SetActive(false);
        return true;
    }

    // ---------------------------------------------------------------- small drawing helpers

    private static void ClearDynamic()
    {
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
        TMP_FontAsset font = headFont;
        if (font != null)
        {
            t.font = font;
        }
        t.fontStyle = FontStyles.UpperCase; // all text of the windows is in capitals, in the game's own font
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.enableWordWrapping = true;
        go.SetActive(true);
        dynamic.Add(go);
        return go;
    }

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
        if (tint.HasValue)
        {
            Image img = go.GetComponent<Image>();
            if (img != null)
            {
                img.color = tint.Value;
            }
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

    // ---------------------------------------------------------------- the window's content

    private static Items_MemoryReflector.Candidate Chosen() =>
        offer == null ? null : offer.Candidates.FirstOrDefault(c => c.CanGo && c.AffixId == selectedId);

    private static void Refresh()
    {
        dirty = false;
        ClearDynamic();
        CloseTooltip();
        Array.Clear(slotRects, 0, slotRects.Length);
        Array.Clear(slotItems, 0, slotItems.Length);
        Array.Clear(slotGroups, 0, slotGroups.Length);
        rows.Clear();
        wovenLights.Clear();
        wovenNeedsPose = true;
        silkSettled = eyesSettled = mirrorSettled = false;
        webStrands.Clear();
        wovenEyes.Clear();
        mirrorLiquid = mirrorCurrent = mirrorFrost = null;
        BuildWovenEcho();
        R.EnsureCurrencyIcons();
        FindAmberIcon();

        // while item B is still being cracked it is drawn as it was
        ItemData shownB = itemB ?? (animating && !shattered ? animB : null);
        SlotBox(itemA, 0, CenterX - SlotOffset - IconW / 2, AY, () => ClickSlot(0));
        SlotBox(shownB, 1, CenterX + SlotOffset - IconW / 2, BY, () => ClickSlot(1));

        offer = null;
        string why = Items_MemoryReflector.WhyNot(itemA, itemB);
        if (why.Length == 0)
        {
            offer = Items_MemoryReflector.Describe(itemA, itemB);
        }

        int candidateRows = 0;
        // item B's affixes: prefixes on its left, suffixes on its right. A legendary item moves a random one instead.
        if (offer != null && !offer.Legendary && !animating)
        {
            var left = offer.Candidates.Where(c => c.Placement != 2).ToList();
            var right = offer.Candidates.Where(c => c.Placement == 2).ToList();
            candidateRows = Math.Max(left.Count, right.Count);
            Column(left, Left, BY + IconH + 50);
            Column(right, Left + WinW - ColumnWidth, BY + IconH + 50);
        }

        float y = BY + IconH + 64 + candidateRows * (AffixCard.Height + AffixCard.Gap);
        if (offer == null)
        {
            if (!animating && itemA != null && itemB != null)
            {
                Label(why, Left, y + 30, WinW, 40, 18, Bad, TextAlignmentOptions.Top);
            }
        }
        else
        {
            Items_MemoryReflector.Candidate chosen = Chosen();
            long gold = Items_MemoryReflector.Gold(offer, chosen);
            int amber = Items_MemoryReflector.Amber(offer, chosen);
            int bones = Items_MemoryReflector.Bones(offer, chosen);
            bool ready = offer.Legendary || chosen != null;
            if (ready)
            {
                double chance = Items_MemoryReflector.Chance(offer, chosen);
                Label((chance * 100).ToString("0.#") + "% chance", Left, y, WinW, 28, 22, Gold, TextAlignmentOptions.Top);
            }
            long haveGold = R.HaveGold();
            long haveAmber = HaveAmber();
            long haveBones = R.HaveBones();
            ChipRow(CenterX, y + 30, 20,
                (R.GoldIcon, 0, gold.ToString("N0"), haveGold >= gold ? Good : Bad),
                (amberIcon, 1, amber.ToString("N0"), haveAmber >= amber ? Good : Bad),
                (R.BonesIcon, 2, bones.ToString("N0"), haveBones >= bones ? Good : Bad));
            bool canPay = haveGold >= gold && haveAmber >= amber && haveBones >= bones;
            float by = y + 70;
            long cg = gold;
            int ca = amber;
            int cb = bones;
            Btn("Reflect", CenterX - 110, by, 220, 44, () => DoReflect(cg, ca, cb), ready && canPay ? (Color?)null : new Color(0.4f, 0.4f, 0.4f, 0.8f));
            float rx = CenterX + 110 + 18;
            Chip(R.GoldIcon, 0, haveGold.ToString("N0"), rx, by - 6, 15, Dim);
            Chip(amberIcon, 1, haveAmber.ToString("N0"), rx, by + 14, 15, Dim);
            Chip(R.BonesIcon, 2, haveBones.ToString("N0"), rx, by + 34, 15, Dim);
        }
    }

    private const float ColumnWidth = 226;

    private static void Column(List<Items_MemoryReflector.Candidate> list, float x, float y)
    {
        float cursor = y;
        foreach (var c in list)
        {
            AffixPlate(c, x, cursor);
            cursor += AffixCard.Height + AffixCard.Gap;
        }
    }
}
