using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
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
            title.GetComponent<TextMeshProUGUI>().text = "Advanced Forge";
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

    private sealed class EdgeSprite
    {
        public Sprite Sprite;
        public Image.Type Type;
        public Color Color;
        public float Thickness;
    }

    private static readonly EdgeSprite[] edges = new EdgeSprite[4];

    private static void CaptureForgeArt(Transform main)
    {
        string[] names = { "Framing/border-piece-top", "Framing/border-piece-bottom", "Framing/border-piece-left", "Framing/border-piece-right" };
        for (int i = 0; i < 4; i++)
        {
            Image img = main.Find(names[i])?.GetComponent<Image>();
            if (img == null)
            {
                continue;
            }
            Vector2 size = img.rectTransform.sizeDelta;
            float thick = i < 2 ? Mathf.Abs(size.y) : Mathf.Abs(size.x);
            edges[i] = new EdgeSprite { Sprite = img.sprite, Type = img.type, Color = img.color, Thickness = Mathf.Clamp(thick > 0 ? thick : 10, 6, 12) };
        }
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
        memoryViews.Clear();
        memoryCardVfx.Clear();
        echoCards.Clear();
    }

    private static GameObject Label(string text, float x, float y, float w, float h, float size, Color color, TextAlignmentOptions align, Transform parent = null, List<GameObject> keep = null, bool body = false)
    {
        GameObject go = Object.Instantiate(labelTemplate, parent ?? Content, false);
        CraftingWidgets.StripGameLogic(go);
        RectTransform r = go.GetComponent<RectTransform>();
        r.anchorMin = r.anchorMax = new Vector2(0, 1);
        r.pivot = new Vector2(0, 1);
        r.anchoredPosition = new Vector2(x, -y);
        r.sizeDelta = new Vector2(w, h);
        TextMeshProUGUI t = go.GetComponent<TextMeshProUGUI>();
        TMP_FontAsset font = body ? bodyFont : headFont;
        if (font != null)
        {
            t.font = font;
        }
        // all text of the window is in capitals, in the game's own font
        t.enableAutoSizing = false; // the size given is the size shown
        t.fontStyle = FontStyles.UpperCase;
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.enableWordWrapping = true;
        t.raycastTarget = false;
        go.SetActive(true);
        (keep ?? dynamic).Add(go);
        return go;
    }

    // ---------------------------------------------------------------- the window

    private static readonly Regex ColourTags = new("</?color[^>]*>|</?link[^>]*>", RegexOptions.Compiled);
    private static readonly Regex LineBreaks = new(@"<br\s*/?>|\r\n|\r|\v|\\n", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static void Refresh()
    {
        dirty = false;
        ClearDynamic();
        CloseTooltips();
        slotRect = null;
        obscuredSand = null;
        itemRect = null;
        forgeImage = null;
        forgeRect = null;

        OrbRow();
        SlotBox(item);
        // the Forge button is there only when an orb is selected and it can be used on the item
        ObscuredMemory memory = Items_Reminiscence.ObscuredOf(item);
        bool unveiled = memory != null && unveiledItemId == item.individualID;
        if (memory != null)
        {
            ShowObscuredSand(Time.unscaledTime, fadeIn: false);
        }
        bool ready = memory == null && selectedKey != AdvancedMaterials.ReverieOrb && ForgeReady();
        if (memory != null && selectedKey == AdvancedMaterials.ReverieOrb)
        {
            ForgeButton("Reroll Memory", memoryAction: true);
        }
        else if (memory != null && !unveiled)
        {
            ForgeButton("Unveil Memory", memoryAction: true);
        }
        else if (unveiled && chosenMemory != null)
        {
            ForgeButton("Keep Memory", memoryAction: true);
        }
        else if (ready)
        {
            if (selectedKey == AdvancedMaterials.AttunementOrb) ForgeButton("Attune");
            else if (selectedKey == AdvancedMaterials.EncapsulationOrb) ForgeButton("Encapsulate");
            else if (selectedKey != AdvancedMaterials.EchoOrb) ForgeButton();
            else if (!EchoOpen) ForgeButton("Reveal Echoes", echoAction: true);
            else if (selectedEcho >= 0) ForgeButton("Keep Echo", echoAction: true);
        }

        float statusY = SlotY + FrameH + ForgeH + 8;
        if (selectedKey == AdvancedMaterials.AttunementOrb && item != null)
        {
            var attunement = Items_Attunement.StateOf(item);
            Label(attunement == null ? "Attunement available\nCorrupts on use" : "Attuned",
                CenterX + ForgeW * 0.5f + 12f, SlotY + FrameH + 20f, 154f, 36f, 11f, Gold, TextAlignmentOptions.MidlineLeft);
        }
        if (selectedKey == AdvancedMaterials.ChaosOrb && item != null)
        {
            Label("Forging Potential  " + item.forgingPotential, CenterX + ForgeW * 0.5f + 12f, SlotY + FrameH + 28f,
                154f, 22f, 13f, Gold, TextAlignmentOptions.MidlineLeft);
        }
        if (unveiled)
        {
            RefuseMemoryButton();
            Label(status.Length == 0 ? "Choose one memory to keep" : status,
                CenterX - 250, statusY, 500, 25, 15, status.Length == 0 ? Gold : statusColour, TextAlignmentOptions.Center);
            MemoryCards(memory, statusY + 44f);
            return;
        }

        if (EchoOpen)
        {
            Label(status.Length == 0 ? "Choose one roll to keep" : status, CenterX - 270f, statusY, 540f,
                24f, 14f, status.Length == 0 ? Gold : statusColour, TextAlignmentOptions.Center);
            BuildEchoCards();
            return;
        }

        string note = status;
        Color noteColour = statusColour;
        if (note.Length == 0 && (memory == null || selectedKey == AdvancedMaterials.EchoOrb) && !ready && selectedKey != null && item != null)
        {
            // an orb is selected and an item is in, but it cannot be used: say why
            note = OrbRestriction(selectedKey);
            noteColour = Bad;
        }
        if (note.Length > 0)
        {
            Label(note, CenterX - 260, statusY, 520, 40, 14, noteColour, TextAlignmentOptions.Top);
        }
        if (item != null)
        {
            AffixPlates(statusY + (note.Length > 0 ? 57f : 21f), memory);
        }
    }
}
