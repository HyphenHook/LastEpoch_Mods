using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using HarmonyLib;
using Il2Cpp;
using Il2CppLE.AssetBundles;
using Il2CppLE.AssetManagement;
using Il2CppTMPro;
using LastEpoch_Hud.Scripts.ModUI.Shell.Host;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using R = LastEpoch_Hud.Scripts.Mods.Items.Crafting.Items_RecombinatorWindow;

namespace LastEpoch_Hud.Scripts.Mods.Items.Crafting;

/// <summary>
/// The Advanced Forge, opened from a button on the forge. One slot takes an item; a row of orbs above it lists the
/// advanced crafting materials. Clicking an orb brings a hammer down on the item and applies the orb.
/// </summary>
internal static class Items_AdvancedForgeWindow
{
    private const string ForgePath = "GUI/Panel System/Panel Stacks/Left Panel Stack/ForgePanel (Clone)";
    private const string ButtonName = "AdvancedForgeButton";
    private const string RootName = "AdvancedForgeWindow";

    private const float Left = 48, WinW = 670, OrbSize = 92;
    private const float FrameW = 190, FrameH = 256; // the whole ornate slot frame
    private const float ItemW = 102, ItemH = 175; // the dark panel inside it, which is the item's box
    private const float ItemCentreFromTop = 0.5f; // the panel's middle, as a share of the frame's height
    private const float ForgeW = 210, ForgeH = 70; // the Forge button
    private const float OrbY = 100, SlotY = 206;
    private static float CenterX => Left + WinW / 2;

    // ---------------------------------------------------------------- state

    private static GameObject root;
    private static GameObject buttonTemplate, labelTemplate;
    private static TMP_FontAsset headFont, bodyFont;
    private static ItemData item;
    private static bool dirty = true;
    private static float nextCheck;
    private static int escapeFrames;
    private static string status = "";
    private static Color statusColour = Color.white;
    private static readonly List<GameObject> dynamic = new();

    private static readonly Color Gold = new(0.93f, 0.78f, 0.38f);
    private static readonly Color Dim = new(0.65f, 0.62f, 0.58f);
    private static readonly Color Good = new(0.55f, 0.85f, 0.5f);
    private static readonly Color Bad = new(0.9f, 0.4f, 0.35f);

    public static bool IsOpen => root != null && root.activeSelf;

    private static Transform Content => root.transform.Find("Content");

    // ---------------------------------------------------------------- tick, button, open and close

    public static void Tick()
    {
        try
        {
            if (Time.unscaledTime >= nextCheck)
            {
                nextCheck = Time.unscaledTime + 0.5f;
                EnsureForgeButton();
                if (IsOpen && Refs_Manager.player_actor.IsNullOrDestroyed())
                {
                    Close();
                }
            }
            if (IsOpen && Input.GetKeyDown(KeyCode.Escape))
            {
                Close();
                escapeFrames = 3; // the game may have opened its pause menu on the same key; put it away
            }
            if (escapeFrames > 0)
            {
                escapeFrames--;
                try
                {
                    GamePauseMenu.Resume();
                }
                catch (Exception)
                {
                }
            }
            if (IsOpen)
            {
                TickAnimation();
                TickHover();
                TickIcon();
            }
            if (IsOpen && dirty)
            {
                Refresh();
                foreach (GameObject go in effects)
                {
                    if (go != null)
                    {
                        go.transform.SetAsLastSibling();
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Main.logger_instance?.Warning("Advanced Forge window: " + ex.Message);
        }
    }

    private static void EnsureForgeButton()
    {
        GameObject forge = GameObject.Find(ForgePath);
        if (forge == null)
        {
            return;
        }
        Transform main = forge.transform.Find("MainContent");
        if (main == null || main.Find(ButtonName) != null)
        {
            return;
        }
        Transform template = main.Find("ViewCraftingMaterialsButton");
        if (template == null)
        {
            return;
        }
        GameObject button = Object.Instantiate(template.gameObject, main, false);
        button.name = ButtonName;
        R.StripGameLogic(button);
        R.SetText(button, "Advanced Forge");
        RectTransform rect = button.GetComponent<RectTransform>();
        RectTransform source = template.GetComponent<RectTransform>();
        rect.anchorMin = source.anchorMin;
        rect.anchorMax = source.anchorMax;
        rect.pivot = source.pivot;
        rect.sizeDelta = new Vector2(150, source.sizeDelta.y);
        // under the Recombinate button, which is under the Crafting Materials button
        rect.anchoredPosition = new Vector2(source.anchoredPosition.x, source.anchoredPosition.y - 2 * (source.sizeDelta.y + 6));
        R.OnClick(button, OpenFromForge);
        button.SetActive(true);
    }

    private static void OpenFromForge()
    {
        try
        {
            GameObject forge = GameObject.Find(ForgePath);
            if (forge == null)
            {
                return;
            }
            Items_RecombinatorWindow.Close();
            Items_MemoryReflectorWindow.Close();
            if (!Build(forge, forge.transform.parent))
            {
                return;
            }
            try
            {
                UIBase.instance.closeAllCraftingUIs();
            }
            catch (Exception)
            {
                forge.SetActive(false);
            }
            ReturnItem();
            status = "";
            dirty = true;
            root.SetActive(true);
            root.transform.SetAsLastSibling();
        }
        catch (Exception ex)
        {
            Main.logger_instance?.Warning("Advanced Forge open failed: " + ex.Message);
        }
    }

    public static void Close()
    {
        if (root == null)
        {
            return;
        }
        CloseTooltips();
        EndAnimationNow();
        icon?.Dispose();
        icon = null;
        ReturnItem();
        root.SetActive(false);
    }

    /// <summary>F is the forge's key: with this window open it closes the window instead of reopening the forge.</summary>
    [HarmonyPatch(typeof(UIBase), "CraftingKeyDown")]
    internal static class CraftingKey
    {
        static bool Prefix()
        {
            if (!IsOpen)
            {
                return true;
            }
            Close();
            return false;
        }
    }

    private static void ReturnItem()
    {
        GiveBack(item);
        item = null;
    }

    private static void GiveBack(ItemData given)
    {
        ItemContainersManager manager = ItemContainersManager.Instance;
        if (given.IsNullOrDestroyed() || manager.IsNullOrDestroyed())
        {
            return;
        }
        if (!manager.inventory.TryAddItem(given, 1, Context.DEFAULT))
        {
            manager.cursor.TryAddItem(given, 1, Context.DEFAULT);
        }
    }

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
        R.CopyRect(forge.GetComponent<RectTransform>(), root.GetComponent<RectTransform>());

        var content = new GameObject("Content");
        content.AddComponent<RectTransform>();
        content.transform.SetParent(root.transform, false);
        R.CopyRect(main.GetComponent<RectTransform>(), content.GetComponent<RectTransform>());

        foreach (string piece in new[] { "backgroundTile", "backgroundTile (1)", "Framing", "EterraFrameHeader", "Header" })
        {
            Transform source = main.Find(piece);
            if (source != null)
            {
                GameObject copy = Object.Instantiate(source.gameObject, content.transform, false);
                copy.name = piece;
                R.StripGameLogic(copy);
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
            R.OnClick(close.gameObject, Close);
        }
        CaptureForgeArt(main);
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
    }

    private static GameObject Label(string text, float x, float y, float w, float h, float size, Color color, TextAlignmentOptions align, Transform parent = null, List<GameObject> keep = null, bool body = false)
    {
        GameObject go = Object.Instantiate(labelTemplate, parent ?? Content, false);
        R.StripGameLogic(go);
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

    private static void Stretch(RectTransform r, float margin)
    {
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = new Vector2(margin, margin);
        r.offsetMax = new Vector2(-margin, -margin);
    }

    // ---------------------------------------------------------------- the item's icon

    private sealed class Icon
    {
        public Sprite Back, Content;
        public Color BackColor = Color.white;
        public LoadRef<Sprite> Load;
        public GameObject Holder;
        public InventoryItemUI Copy;

        public bool Poll()
        {
            if (Back == null && !Copy.IsNullOrDestroyed() && !Copy.backgroundImage.IsNullOrDestroyed() && Copy.backgroundImage.sprite != null)
            {
                Back = Copy.backgroundImage.sprite;
                BackColor = Copy.backgroundImage.color;
                if (BackColor.a < 0.2f)
                {
                    BackColor.a = 1f;
                }
            }
            if (Content == null && Load != null && Load.Status == AssetStatus.Loaded && !Load.AssetOrNull.IsNullOrDestroyed())
            {
                Content = Load.AssetOrNull;
            }
            return Content != null;
        }

        public void Dispose()
        {
            try
            {
                Load?.Dispose();
            }
            catch (Exception)
            {
            }
            if (Holder != null)
            {
                Object.Destroy(Holder);
            }
            Holder = null;
            Copy = null;
            Load = null;
            Content = null;
            Back = null;
        }
    }

    private static Icon icon;
    private static long iconFor;

    private static Icon IconFor(ItemData data)
    {
        long key = (long)data.Pointer;
        if (icon != null && iconFor == key)
        {
            return icon;
        }
        icon?.Dispose();
        icon = BuildIcon(data);
        iconFor = key;
        return icon;
    }

    private static Icon BuildIcon(ItemData data)
    {
        try
        {
            SoftRef<Sprite> soft = data.GetItemSpriteFromData(ItemUIContext.Default);
            LoadRef<Sprite> load = soft == null || !soft ? null : SoftRefExtensions.CreateLoadRef(soft, "LastEpoch_Hud", 0);
            var made = new Icon { Load = load };
            try
            {
                InventoryItemUI source = Object.FindObjectsOfType<InventoryItemUI>()
                    .FirstOrDefault(u => u != null && u.gameObject.activeInHierarchy && !u.backgroundImage.IsNullOrDestroyed());
                if (source != null)
                {
                    var holder = new GameObject("AdvancedForgeBackgroundHolder");
                    holder.AddComponent<RectTransform>();
                    holder.transform.SetParent(root.transform, false);
                    holder.SetActive(false);
                    InventoryItemUI copy = Object.Instantiate(source.gameObject, holder.transform, false).GetComponent<InventoryItemUI>();
                    copy.SetBackgroundSpriteAndColour(copy.backgroundImage, data);
                    made.Holder = holder;
                    made.Copy = copy;
                }
            }
            catch (Exception ex)
            {
                Main.logger_instance?.Warning("Advanced Forge background failed: " + ex.Message);
            }
            made.Poll();
            return made;
        }
        catch (Exception ex)
        {
            Main.logger_instance?.Warning("Advanced Forge icon failed: " + ex.Message);
            return null;
        }
    }

    private static void TickIcon()
    {
        if (icon != null && (icon.Content == null || icon.Back == null))
        {
            bool had = icon.Content != null;
            icon.Poll();
            if (!had && icon.Content != null)
            {
                dirty = true;
            }
        }
    }

    // ---------------------------------------------------------------- the item slot

    private static RectTransform slotRect; // the whole ornate frame
    private static RectTransform itemRect; // what the frame leaves for the item

    private static void SlotBox(ItemData data)
    {
        Image frame = R.MakeImage(Content, "SlotFrame", Color.white);
        frame.sprite = OrbArt.Named("forge_slot_frame");
        frame.preserveAspect = false;
        RectTransform br = frame.rectTransform;
        br.anchorMin = br.anchorMax = new Vector2(0, 1);
        br.pivot = new Vector2(0, 1);
        br.anchoredPosition = new Vector2(CenterX - FrameW / 2, -SlotY);
        br.sizeDelta = new Vector2(FrameW, FrameH);
        dynamic.Add(frame.gameObject);
        slotRect = br;

        var holder = new GameObject("Item");
        RectTransform hr = holder.AddComponent<RectTransform>();
        holder.transform.SetParent(frame.transform, false);
        hr.anchorMin = hr.anchorMax = new Vector2(0.5f, 0.5f);
        hr.pivot = new Vector2(0.5f, 0.5f);
        hr.anchoredPosition = new Vector2(0, FrameH * (0.5f - ItemCentreFromTop));
        hr.sizeDelta = new Vector2(ItemW, ItemH);
        itemRect = hr;
        if (data != null)
        {
            Icon shown = IconFor(data);
            if (shown != null)
            {
                if (shown.Back != null)
                {
                    Image back = R.MakeImage(holder.transform, "ItemBack", shown.BackColor);
                    back.sprite = shown.Back;
                    back.raycastTarget = false;
                    Stretch(back.rectTransform, 2);
                }
                if (shown.Content != null)
                {
                    Image content = R.MakeImage(holder.transform, "ItemIcon", Color.white);
                    content.sprite = shown.Content;
                    content.preserveAspect = true;
                    content.raycastTarget = false;
                    Stretch(content.rectTransform, 10);
                }
            }
        }
        Button button = frame.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.onClick.AddListener((UnityEngine.Events.UnityAction)ClickSlot);
    }

    private static void ClickSlot()
    {
        if (animating)
        {
            return;
        }
        try
        {
            ItemContainersManager manager = ItemContainersManager.Instance;
            if (manager.IsNullOrDestroyed())
            {
                return;
            }
            if (item != null)
            {
                GiveBack(item);
                item = null;
                icon?.Dispose();
                icon = null;
            }
            else
            {
                var content = manager.cursor.GetContent();
                if (content != null && content.Count > 0)
                {
                    ItemContainerEntry entry = content[0];
                    ItemData taken = entry.data;
                    if (!taken.IsNullOrDestroyed() && manager.cursor.TryRemoveItem(entry, 1, Context.DEFAULT))
                    {
                        item = taken;
                    }
                }
            }
            status = "";
            dirty = true;
        }
        catch (Exception ex)
        {
            Main.logger_instance?.Warning("Advanced Forge slot failed: " + ex.Message);
        }
    }

    // ---------------------------------------------------------------- the orbs and the Forge button

    private sealed class OrbView
    {
        public RectTransform Rect;
        public Image Frame, Art, Glow;
        public AdvancedMaterial Material;
        public bool Have;
    }

    private static readonly List<OrbView> orbViews = new();
    private static string selectedKey;
    private static RectTransform forgeRect;
    private static Image forgeImage;
    private static TextMeshProUGUI forgeLabel;
    private static Image forgeGlow;
    private static float forgeBlend; // 0 at rest, 1 under the pointer

    private static TMP_FontAsset countFontCache;

    /// <summary>The font the game draws the count of a stack of items in.</summary>
    private static TMP_FontAsset CountFont()
    {
        if (countFontCache == null)
        {
            try
            {
                InventoryItemUI any = Object.FindObjectsOfType<InventoryItemUI>(true).FirstOrDefault(u => u != null && !u.quantityText.IsNullOrDestroyed());
                countFontCache = any?.quantityText.font;
            }
            catch (Exception)
            {
            }
        }
        return countFontCache;
    }

    private static void OrbRow()
    {
        orbViews.Clear();
        int n = AdvancedMaterials.All.Count;
        float total = n * OrbSize + (n - 1) * 14;
        float x = CenterX - total / 2;
        Label("Orbs", CenterX - 150, OrbY - 26, 300, 20, 15, Gold, TextAlignmentOptions.Center);
        foreach (AdvancedMaterial m in AdvancedMaterials.All)
        {
            int have = AdvancedMaterials.Count(m.Key);
            Image frame = R.MakeImage(Content, "Orb_" + m.Key, Color.white);
            frame.sprite = OrbArt.Named("orb_slot_empty");
            RectTransform br = frame.rectTransform;
            br.anchorMin = br.anchorMax = new Vector2(0, 1);
            br.pivot = new Vector2(0, 1);
            br.anchoredPosition = new Vector2(x, -OrbY);
            br.sizeDelta = new Vector2(OrbSize, OrbSize);
            dynamic.Add(frame.gameObject);

            Image glow = R.MakeImage(frame.transform, "Glow", new Color(1f, 1f, 1f, 0.5f));
            glow.sprite = OrbArt.Named("glow_violet");
            glow.raycastTarget = false;
            RectTransform gr = glow.rectTransform;
            gr.anchorMin = gr.anchorMax = new Vector2(0.5f, 0.5f);
            gr.pivot = new Vector2(0.5f, 0.5f);
            gr.anchoredPosition = Vector2.zero;
            gr.sizeDelta = new Vector2(OrbSize * 0.92f, OrbSize * 0.92f);

            Image art = R.MakeImage(frame.transform, "Art", Color.white);
            art.preserveAspect = true;
            art.raycastTarget = false;
            RectTransform ar = art.rectTransform;
            ar.anchorMin = ar.anchorMax = new Vector2(0.5f, 0.5f);
            ar.pivot = new Vector2(0.5f, 0.5f);
            ar.anchoredPosition = Vector2.zero;
            ar.sizeDelta = new Vector2(OrbSize * 0.56f, OrbSize * 0.56f);

            // the count sits at the bottom right of the orb itself (the orb fills the middle 56% of the slot), a little way off it
            float orbRight = OrbSize / 2 + OrbSize * 0.28f, orbBottom = OrbSize / 2 + OrbSize * 0.28f;
            GameObject count = Label(have.ToString("N0"), orbRight + 2 - 30, orbBottom + 1 - 14, 30, 14, 10, have > 0 ? Color.white : Dim, TextAlignmentOptions.BottomRight, frame.transform);
            TMP_FontAsset countFont = CountFont();
            if (countFont != null)
            {
                count.GetComponent<TextMeshProUGUI>().font = countFont;
            }
            string key = m.Key;
            Button b = frame.gameObject.AddComponent<Button>();
            b.transition = Selectable.Transition.None;
            b.onClick.AddListener((UnityEngine.Events.UnityAction)(() => SelectOrb(key)));
            orbViews.Add(new OrbView { Rect = br, Frame = frame, Art = art, Glow = glow, Material = m, Have = have > 0 });
            x += OrbSize + 14;
        }
        if (selectedKey != null && AdvancedMaterials.Count(selectedKey) < 1)
        {
            selectedKey = null;
        }
        ApplyOrbStates();
    }

    /// <summary>
    /// An orb slot is empty, hovered (a violet ring) or selected (a violet sphere that pulses behind the orb). An orb
    /// that has run out is grey and cannot be selected.
    /// </summary>
    private static void ApplyOrbStates()
    {
        for (int i = 0; i < orbViews.Count; i++)
        {
            OrbView v = orbViews[i];
            if (v.Frame == null)
            {
                continue;
            }
            bool selected = v.Have && selectedKey == v.Material.Key;
            bool hover = v.Have && !selected && hoveredNow == i;
            v.Frame.sprite = OrbArt.Named(selected ? "orb_slot_filled" : hover ? "orb_slot_hover" : "orb_slot_empty");
            v.Art.sprite = v.Have ? OrbArt.Get(v.Material.Key, v.Material.Tint, v.Material.Glow) : OrbArt.OrbGrey();
            v.Glow.enabled = selected;
        }
    }

    private static void PulseOrbs()
    {
        float t = Time.unscaledTime;
        foreach (OrbView v in orbViews)
        {
            if (v.Glow != null && v.Glow.enabled)
            {
                float k = 0.5f + 0.5f * Mathf.Sin(t * 3.2f);
                v.Glow.color = new Color(1f, 1f, 1f, 0.28f + 0.42f * k);
                v.Glow.rectTransform.localScale = Vector3.one * (0.94f + 0.14f * k);
            }
        }
    }

    private static void SelectOrb(string key)
    {
        if (animating || AdvancedMaterials.Count(key) < 1)
        {
            return; // an orb that has run out cannot be selected
        }
        selectedKey = selectedKey == key ? null : key;
        status = "";
        dirty = true;
    }

    private static bool ForgeReady() => selectedKey != null && item != null && Items_Encapsulation.WhyNot(item).Length == 0;

    private static void ForgeButton()
    {
        float y = SlotY + FrameH + 4;
        Image img = R.MakeImage(Content, "ForgeButton", Color.white);
        img.sprite = OrbArt.Named("forge_button_frame");
        RectTransform r = img.rectTransform;
        r.anchorMin = r.anchorMax = new Vector2(0, 1);
        r.pivot = new Vector2(0, 1);
        r.anchoredPosition = new Vector2(CenterX - ForgeW / 2, -y);
        r.sizeDelta = new Vector2(ForgeW, ForgeH);
        dynamic.Add(img.gameObject);
        forgeRect = r;
        forgeImage = img;
        GameObject label = Label("Forge", 0, 0, ForgeW, ForgeH, 20, Gold, TextAlignmentOptions.Center, img.transform);
        forgeLabel = label.GetComponent<TextMeshProUGUI>();
        // a violet wash over the frame, which comes up when the pointer is on the button
        Image glow = R.MakeImage(img.transform, "HoverWash", new Color(0.78f, 0.55f, 1f, 0f));
        glow.sprite = OrbArt.Named("forge_button_frame");
        glow.raycastTarget = false;
        Stretch(glow.rectTransform, 0);
        glow.transform.SetSiblingIndex(0);
        forgeGlow = glow;
        forgeBlend = 0f;
        RectTransform lr = label.GetComponent<RectTransform>();
        lr.anchorMin = Vector2.zero;
        lr.anchorMax = Vector2.one;
        lr.offsetMin = lr.offsetMax = Vector2.zero;
        Button b = img.gameObject.AddComponent<Button>();
        b.transition = Selectable.Transition.None;
        b.onClick.AddListener((UnityEngine.Events.UnityAction)ForgeClicked);
        TintForge(false);
    }

    private static void TintForge(bool hover)
    {
        if (forgeImage == null)
        {
            return;
        }
        bool ready = ForgeReady() && !animating;
        float target = ready && hover ? 1f : 0f;
        forgeBlend = Mathf.MoveTowards(forgeBlend, target, Time.unscaledDeltaTime * 7f);
        float dim = ready ? 1f : 0.5f;
        forgeImage.color = new Color(dim, dim, dim, ready ? 1f : 0.9f);
        if (forgeGlow != null)
        {
            forgeGlow.color = new Color(0.78f, 0.55f, 1f, 0.34f * forgeBlend);
        }
        if (forgeLabel != null)
        {
            Color rest = ready ? new Color(1f, 0.9f, 0.55f) : new Color(0.6f, 0.58f, 0.55f);
            forgeLabel.color = Color.Lerp(rest, new Color(1f, 1f, 0.92f), forgeBlend);
        }
    }

    private static void ForgeClicked()
    {
        if (animating)
        {
            return;
        }
        string why = "";
        if (selectedKey == null)
        {
            why = "Select an orb first.";
        }
        else if (selectedKey == AdvancedMaterials.EncapsulationOrb)
        {
            why = Items_Encapsulation.WhyNot(item);
        }
        if (why.Length > 0)
        {
            status = why;
            statusColour = Bad;
            dirty = true;
            return;
        }
        StartForgeFx(selectedKey);
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
        itemRect = null;
        forgeImage = null;
        forgeRect = null;

        OrbRow();
        SlotBox(item);
        // the Forge button is there only when an orb is selected and it can be used on the item
        bool ready = ForgeReady();
        if (ready)
        {
            ForgeButton();
        }

        float statusY = SlotY + FrameH + ForgeH + 8;
        string note = status;
        Color noteColour = statusColour;
        if (note.Length == 0 && !ready && selectedKey != null && item != null)
        {
            // an orb is selected and an item is in, but it cannot be used: say why
            note = selectedKey == AdvancedMaterials.EncapsulationOrb ? Items_Encapsulation.WhyNot(item) : "";
            noteColour = Bad;
        }
        if (note.Length > 0)
        {
            Label(note, CenterX - 260, statusY, 520, 40, 14, noteColour, TextAlignmentOptions.Top);
        }
        if (item != null)
        {
            AffixPlates(statusY + 62);
        }
    }

    // ---------------------------------------------------------------- the item's affixes

    private sealed class Line
    {
        public string Text;
        public int Tier;
        public bool Fractured;
        public int Group; // 0 prefix, 1 suffix, 2 sealed, 3 primordial, 4 corrupted
        public int Rows;
    }

    private static readonly string[] GroupName = { "Prefixes", "Suffixes", "Sealed", "Primordial", "Corrupted" };

    private static readonly Color[] GroupColour =
    {
        new(0.93f, 0.76f, 0.38f), // prefix: gold
        new(0.42f, 0.82f, 0.74f), // suffix: teal
        new(0.68f, 0.52f, 0.92f), // sealed: violet
        new(0.95f, 0.5f, 0.3f), // primordial: ember
        new(0.82f, 0.28f, 0.5f), // corrupted: crimson
    };

    private static readonly Color FracturedColour = new(0.45f, 0.8f, 1f);

    private const float PlateW = 306, TextW = PlateW - 62 - 16, PlateFont = 12;

    /// <summary>How many rows of text a wording takes in the width of a plate.</summary>
    private static int RowsOf(string text)
    {
        int perRow = Math.Max(10, (int)(TextW / (PlateFont * 0.5f)));
        int rows = 0;
        foreach (string part in text.Split('\n'))
        {
            rows += Math.Max(1, (part.Length + perRow - 1) / perRow);
        }
        return Math.Min(rows, 5);
    }

    private static List<Line> ReadAffixes()
    {
        var list = new List<Line>();
        ItemDataUnpacked data = item.TryCast<ItemDataUnpacked>();
        foreach (ItemAffix a in item.affixes.ToArray())
        {
            if (a == null)
            {
                continue;
            }
            string text = "Affix " + a.affixId;
            try
            {
                if (data != null)
                {
                    text = TooltipItemManager.FormatAffix(data, a, TooltipMode.DefaultInfo, TooltipItemManager.SlotType.CRAFTING,
                        false, -1, -1, false, false, ValueRangeFormat.None, true, false);
                    text = LineBreaks.Replace(ColourTags.Replace(text ?? "", ""), "\n");
                    text = string.Join("\n", text.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0));
                }
            }
            catch (Exception)
            {
            }
            int group = a.IsSealedCorrupted ? 4 : a.IsSealedPrimordial ? 3 : a.IsSealed ? 2 : a.affixType == AffixList.AffixType.PREFIX ? 0 : 1;
            list.Add(new Line
            {
                Text = text,
                Tier = a.DisplayTier,
                Group = group,
                Fractured = Items_Fracture.IsFractured(item, a),
                Rows = RowsOf(text),
            });
        }
        return list;
    }

    /// <summary>
    /// The affixes as plates under the item: prefixes in a column on the left, suffixes on the right, and below them the
    /// sealed, primordial and corrupted ones. Each kind has its own colour; a fractured affix is blue with a diamond.
    /// </summary>
    private static void AffixPlates(float top)
    {
        const float gap = 12;
        List<Line> all = ReadAffixes();
        float leftX = CenterX - PlateW - gap / 2, rightX = CenterX + gap / 2;
        float y = top;

        float prefixEnd = Column(all.Where(l => l.Group == 0).ToList(), leftX, y, 0);
        float suffixEnd = Column(all.Where(l => l.Group == 1).ToList(), rightX, y, 1);
        y = Math.Max(prefixEnd, suffixEnd);

        List<Line> special = all.Where(l => l.Group >= 2).ToList();
        if (special.Count > 0)
        {
            y += 8;
            Label("Sealed, primordial and corrupted", CenterX - 200, y, 400, 20, 13, new Color(0.72f, 0.66f, 0.8f), TextAlignmentOptions.Center);
            y += 24;
            for (int i = 0; i < special.Count; i += 2)
            {
                float h = 0;
                for (int k = i; k < Math.Min(i + 2, special.Count); k++)
                {
                    h = Math.Max(h, PlateHeight(special[k]));
                }
                for (int k = i; k < Math.Min(i + 2, special.Count); k++)
                {
                    Plate(special[k], k - i == 0 ? leftX : rightX, y, h);
                }
                y += h + 8;
            }
        }
    }

    private static float PlateHeight(Line l) => 20 + l.Rows * 15;

    /// <summary>One column of plates under a coloured heading. Returns where it ends.</summary>
    private static float Column(List<Line> lines, float x, float y, int group)
    {
        Color colour = GroupColour[group];
        Label(GroupName[group], x, y - 24, PlateW, 20, 14, colour, TextAlignmentOptions.Center);
        R.Part(Content, "Rule", colour * 0.6f, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(x + 40, -(y - 4)), new Vector2(PlateW - 80, 1));
        foreach (Line l in lines)
        {
            float h = PlateHeight(l);
            Plate(l, x, y, h);
            y += h + 8;
        }
        return y;
    }

    private static void Plate(Line l, float x, float y, float h)
    {
        const float pad = 10;
        Color accent = l.Fractured ? FracturedColour : GroupColour[l.Group];
        Image plate = R.MakeImage(Content, "AffixPlate", l.Fractured ? new Color(0.035f, 0.07f, 0.095f, 0.93f) : new Color(0.04f, 0.034f, 0.03f, 0.92f));
        plate.raycastTarget = false;
        RectTransform pr = plate.rectTransform;
        pr.anchorMin = pr.anchorMax = new Vector2(0, 1);
        pr.pivot = new Vector2(0, 1);
        pr.anchoredPosition = new Vector2(x, -y);
        pr.sizeDelta = new Vector2(PlateW, h);
        dynamic.Add(plate.gameObject);
        Transform t = plate.transform;
        R.Part(t, "Bar", accent, new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f), Vector2.zero, new Vector2(4, 0));
        R.Part(t, "Top", accent * 0.45f, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(0, 1));
        R.Part(t, "Bottom", accent * 0.45f, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), Vector2.zero, new Vector2(0, 1));
        Label("T" + l.Tier, pad + 4, 8, 40, 16, 12, accent, TextAlignmentOptions.Left, t);
        GameObject text = Label(l.Text, pad + 48, 8, TextW + 14, h - 12, PlateFont,
            l.Fractured ? new Color(0.62f, 0.9f, 1f) : new Color(0.88f, 0.84f, 0.76f), TextAlignmentOptions.TopLeft, t, body: true);
        TextMeshProUGUI tmp = text.GetComponent<TextMeshProUGUI>();
        tmp.enableAutoSizing = true; // a long wording shrinks to fit the plate instead of running over it
        tmp.fontSizeMin = 8;
        tmp.fontSizeMax = PlateFont;
        if (l.Fractured)
        {
            Image diamond = R.MakeImage(t, "Diamond", Color.white);
            diamond.sprite = OrbArt.Diamond();
            diamond.preserveAspect = true;
            diamond.raycastTarget = false;
            RectTransform dr = diamond.rectTransform;
            dr.anchorMin = dr.anchorMax = new Vector2(1, 0.5f);
            dr.pivot = new Vector2(1, 0.5f);
            dr.anchoredPosition = new Vector2(-8, 0);
            dr.sizeDelta = new Vector2(14, 14);
        }
    }

    // ---------------------------------------------------------------- hover: the orbs' own tooltip and the item's

    private static int hoveredNow = -1; // an orb's index, -2 for the item, -1 for nothing
    private static bool tooltipOpen;

    private static UnityEngine.Camera PointerCamera()
    {
        Canvas canvas = root.GetComponentInParent<Canvas>();
        return canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
    }

    private static void CloseTooltips()
    {
        hoveredNow = -1;
        if (tooltipOpen)
        {
            tooltipOpen = false;
            try
            {
                TooltipItemManager.ForceClose();
            }
            catch (Exception)
            {
            }
        }
    }

    /// <summary>Opens the game's own item tooltip for the item, beside the rectangle.</summary>
    private static void OpenGameTooltip(ItemData data, RectTransform at)
    {
        TooltipItemManager manager = Object.FindObjectOfType<TooltipItemManager>();
        ItemDataUnpacked unpacked = data?.TryCast<ItemDataUnpacked>();
        if (manager != null && unpacked != null)
        {
            manager.OpenTooltip(unpacked, TooltipItemManager.SlotType.CRAFTING, Vector2.zero, at.position, at.gameObject, at.rect.size);
            tooltipOpen = true;
        }
    }

    private static void TickHover()
    {
        Vector2 mouse = Input.mousePosition;
        UnityEngine.Camera cam = PointerCamera();
        int target = -1;
        for (int i = 0; i < orbViews.Count; i++)
        {
            if (orbViews[i].Rect != null && RectTransformUtility.RectangleContainsScreenPoint(orbViews[i].Rect, mouse, cam))
            {
                target = i;
                break;
            }
        }
        if (target < 0 && !animating && item != null && itemRect != null && RectTransformUtility.RectangleContainsScreenPoint(itemRect, mouse, cam))
        {
            target = -2;
        }
        TintForge(forgeRect != null && RectTransformUtility.RectangleContainsScreenPoint(forgeRect, mouse, cam));
        if (target != hoveredNow)
        {
            if (tooltipOpen)
            {
                CloseTooltips();
            }
            hoveredNow = target;
            try
            {
                if (target >= 0)
                {
                    // an orb is shown with the game's own tooltip, through the item it stands in for on the ground
                    OpenGameTooltip(Drop.OrbDrop.TooltipCarrier(), orbViews[target].Rect);
                }
                else if (target == -2)
                {
                    OpenGameTooltip(item, itemRect);
                }
            }
            catch (Exception ex)
            {
                Main.logger_instance?.Warning("Advanced Forge tooltip failed: " + ex.Message);
            }
        }
        ApplyOrbStates();
        PulseOrbs();
    }

    // ---------------------------------------------------------------- the forging effect

    private static readonly List<GameObject> effects = new();
    private static bool animating;
    private static float animStart;
    private static string pendingKey;
    private static bool applied;
    private static Image flash;
    private static Vector2 shakeBase;

    // The supplied Essence Shatter VFX: 40 frames, each a whole slot (2:3) with the orb at 43.6% down and the point the
    // item takes the essence in at 65.8% down. It is shown as one overlay that never moves.
    private const float VfxW = 221f, VfxH = 332f; // the overlay, sized so its orb is as big as the orb art (104)
    private const float OrbAtY = 0.436f, ItemAtY = 0.658f;
    private const float OrbFlyEnd = 0.45f, VfxStart = 0.20f;
    private static readonly int[] PhaseFrames = { 8, 8, 12, 12 }; // charge, shatter, absorb, residual
    private static readonly float[] PhaseSeconds = { 0.075f, 0.060f, 0.070f, 0.080f };
    private static readonly float[] FrameStart = BuildFrameStarts();
    private static float ShatterAt => VfxStart + PhaseFrames[0] * PhaseSeconds[0];
    private static float AbsorbAt => ShatterAt + PhaseFrames[1] * PhaseSeconds[1];
    private static float ResidualAt => AbsorbAt + PhaseFrames[2] * PhaseSeconds[2];
    private static float VfxEnd => ResidualAt + PhaseFrames[3] * PhaseSeconds[3];
    private static float ImpactAt => AbsorbAt + 6 * PhaseSeconds[2]; // the merge peaks in frames 23 to 26 of the effect
    private static float EndAt => VfxEnd + 0.06f;

    private static float[] BuildFrameStarts()
    {
        var starts = new float[40];
        float at = 0;
        int i = 0;
        for (int phase = 0; phase < PhaseFrames.Length; phase++)
        {
            for (int k = 0; k < PhaseFrames[phase]; k++)
            {
                starts[i++] = at;
                at += PhaseSeconds[phase];
            }
        }
        return starts;
    }

    private static GameObject orbFx;
    private static Image orbFxImage, backGlow, bloom, ringFx, vfxImage, vfxExtra;
    private static RectTransform orbFxRect, backGlowRect, bloomRect, ringRect, vfxRect, vfxExtraRect;
    private static Vector2 orbStart;
    private static Sprite[] vfxSheet;
    private static bool shatterDone, residualDone, absorbDone;

    private sealed class Spark
    {
        public GameObject Go;
        public RectTransform Rect;
        public Image Img;
        public Vector2 Pos, Vel;
        public float Born, Life;
    }

    private static readonly List<Spark> sparks = new();

    /// <summary>A mote of essence that travels from the orb into the item.</summary>
    private sealed class Mote
    {
        public GameObject Go;
        public RectTransform Rect;
        public Image Img;
        public Vector2 From, To;
        public float Born, Life, Sway, Size;
    }

    private static readonly List<Mote> motes = new();

    private static Vector2 SlotCentre => new(CenterX, -(SlotY + FrameH * ItemCentreFromTop));
    private static Vector2 OverlayCentre => SlotCentre + new Vector2(0, (ItemAtY - 0.5f) * VfxH);
    private static Vector2 OrbPoint => OverlayCentre + new Vector2(0, (0.5f - OrbAtY) * VfxH);

    private static Material additive;
    private static bool additiveTried;

    /// <summary>An additive material for glows, if the game has a shader for it.</summary>
    private static Material Additive()
    {
        if (!additiveTried)
        {
            additiveTried = true;
            foreach (string name in new[] { "Legacy Shaders/Particles/Additive", "Particles/Additive", "Mobile/Particles/Additive", "UI/Additive" })
            {
                try
                {
                    Shader shader = Shader.Find(name);
                    if (shader != null)
                    {
                        additive = new Material(shader);
                        Main.logger_instance?.Msg("Advanced Forge: additive glow with " + name);
                        break;
                    }
                }
                catch (Exception)
                {
                }
            }
            if (additive == null)
            {
                Main.logger_instance?.Msg("Advanced Forge: no additive shader found, glows use normal blending");
            }
        }
        return additive;
    }

    private static GameObject Effect(string name)
    {
        var go = new GameObject(name);
        var r = go.AddComponent<RectTransform>();
        r.anchorMin = r.anchorMax = new Vector2(0, 1);
        r.pivot = new Vector2(0, 1);
        r.anchoredPosition = Vector2.zero;
        r.sizeDelta = Vector2.zero;
        go.transform.SetParent(Content, false);
        effects.Add(go);
        return go;
    }

    private static Image CentredImage(string name, Sprite sprite, float w, float h, out RectTransform rect, bool glow = false)
    {
        GameObject go = Effect(name);
        rect = go.GetComponent<RectTransform>();
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(w, h);
        Image img = R.MakeImage(go.transform, "Image", Color.white);
        img.sprite = sprite;
        img.preserveAspect = true;
        img.raycastTarget = false;
        Stretch(img.rectTransform, 0);
        if (glow && Additive() != null)
        {
            img.material = additive;
        }
        return img;
    }

    private static void StartForgeFx(string key)
    {
        animating = true;
        applied = false;
        shatterDone = residualDone = absorbDone = false;
        pendingKey = key;
        animStart = Time.unscaledTime;
        status = "";
        CloseTooltips();
        vfxSheet ??= OrbArt.Sheet("essence_vfx_atlas", 8, 5);

        // where the orb is lifted from: its own slot
        orbStart = OrbPoint + new Vector2(0, 150);
        foreach (OrbView v in orbViews)
        {
            if (v.Material.Key == key && v.Rect != null)
            {
                orbStart = v.Rect.anchoredPosition + new Vector2(OrbSize / 2, -OrbSize / 2);
            }
        }
        AdvancedMaterial m = AdvancedMaterials.All.Find(x => x.Key == key);

        // under everything: the glow of the item as it takes the essence in
        GameObject f = Effect("Flash");
        RectTransform fr = f.GetComponent<RectTransform>();
        fr.anchoredPosition = new Vector2(CenterX - ItemW / 2, SlotCentre.y + ItemH / 2);
        fr.sizeDelta = new Vector2(ItemW, ItemH);
        flash = R.MakeImage(f.transform, "F", new Color(0.8f, 0.6f, 1f, 0f));
        flash.raycastTarget = false;
        Stretch(flash.rectTransform, 0);
        shakeBase = new Vector2(CenterX - FrameW / 2, -SlotY);

        // a dark violet light behind the orb while it charges
        backGlow = CentredImage("BackGlow", OrbArt.Named("glow_violet"), 300f, 300f, out backGlowRect, glow: true);
        backGlow.color = new Color(0.6f, 0.3f, 1f, 0f);
        backGlowRect.anchoredPosition = OrbPoint;

        orbFxImage = CentredImage("FloatingOrb", OrbArt.Get(key, m.Tint, m.Glow), 104f, 104f, out orbFxRect);
        orbFxRect.anchoredPosition = orbStart;
        orbFx = orbFxRect.gameObject;

        vfxImage = CentredImage("EssenceVfx", null, VfxW, VfxH, out vfxRect);
        vfxImage.enabled = false;
        vfxRect.anchoredPosition = OverlayCentre;
        // the same frames again, added on top, for a stronger glow
        vfxExtra = CentredImage("EssenceVfxGlow", null, VfxW, VfxH, out vfxExtraRect, glow: true);
        vfxExtra.enabled = false;
        vfxExtra.color = new Color(0.85f, 0.6f, 1f, 0.35f);
        vfxExtraRect.anchoredPosition = OverlayCentre;
    }

    private static float Ease(float u) => u * u * (3f - 2f * u);

    private static void TickAnimation()
    {
        float now = Time.unscaledTime;
        TickSparks(now);
        TickMotes(now);
        if (!animating)
        {
            return;
        }
        float t = now - animStart;

        // the orb rises out of its slot to its place over the item, and bobs there until it breaks
        if (orbFx != null)
        {
            float u = Mathf.Clamp01(t / OrbFlyEnd);
            Vector2 pos = Vector2.Lerp(orbStart, OrbPoint, Ease(u));
            if (t >= OrbFlyEnd)
            {
                pos.y += Mathf.Sin((t - OrbFlyEnd) * 5f) * 2.5f;
            }
            orbFxRect.anchoredPosition = pos;
            float charge = Mathf.Clamp01((t - VfxStart) / (ShatterAt - VfxStart));
            orbFxRect.localScale = Vector3.one * (0.7f + 0.3f * Ease(u) + 0.08f * charge);
            float fade = t < ShatterAt ? 1f : Mathf.Clamp01(1f - (t - ShatterAt) / 0.08f);
            orbFxImage.color = new Color(1f, 1f, 1f, fade);
            orbFx.SetActive(fade > 0f);

            // the dark light behind the orb pulses as it charges, and goes at the break
            float pulse = 0.85f + 0.30f * (0.5f + 0.5f * Mathf.Sin(t * 11f));
            float glowAlpha = t < ShatterAt ? 0.55f * Mathf.Clamp01((t - 0.05f) / 0.3f) : 0.55f * Mathf.Clamp01(1f - (t - ShatterAt) / 0.2f);
            backGlow.color = new Color(0.6f, 0.3f, 1f, glowAlpha);
            backGlowRect.localScale = Vector3.one * pulse;
            backGlowRect.anchoredPosition = pos;
        }

        // the effect frames, one overlay on the slot that does not move
        if (vfxSheet != null && vfxSheet.Length >= 40)
        {
            float into = t - VfxStart;
            if (into >= 0f && into < FrameStart[39] + PhaseSeconds[3])
            {
                int frame = 0;
                for (int i = 39; i >= 0; i--)
                {
                    if (into >= FrameStart[i])
                    {
                        frame = i;
                        break;
                    }
                }
                vfxImage.enabled = true;
                vfxImage.sprite = vfxSheet[frame];
                vfxExtra.enabled = true;
                vfxExtra.sprite = vfxSheet[frame];
            }
            else
            {
                vfxImage.enabled = false;
                vfxExtra.enabled = false;
            }
        }

        if (!shatterDone && t >= ShatterAt)
        {
            shatterDone = true;
            ShatterBurst(now);
        }
        if (!absorbDone && t >= AbsorbAt)
        {
            absorbDone = true;
            SendMotes(now);
        }
        if (!residualDone && t >= ResidualAt)
        {
            residualDone = true;
            ResidualSparks(now);
        }
        if (!applied && t >= ImpactAt)
        {
            applied = true;
            Impact(now);
        }

        // the break: a bloom and a ring spread from the orb
        if (bloom != null)
        {
            float k = Mathf.Clamp01((t - ShatterAt) / 0.16f);
            bloom.color = new Color(0.9f, 0.75f, 1f, 0.9f * (1f - k) * (t >= ShatterAt ? 1f : 0f));
            bloomRect.localScale = Vector3.one * (0.5f + 1.1f * k);
        }
        if (ringFx != null)
        {
            float k = Mathf.Clamp01((t - ShatterAt) / 0.40f);
            ringFx.color = new Color(1f, 1f, 1f, 0.85f * (1f - k) * (t >= ShatterAt ? 1f : 0f));
            ringRect.localScale = Vector3.one * (0.3f + 1.2f * Ease(k));
        }

        // the slot shudders a little at the break, and again as it takes the essence in
        if (slotRect != null)
        {
            Vector2 shake = Vector2.zero;
            float atBreak = t - ShatterAt, atMerge = t - ImpactAt;
            if (atBreak >= 0 && atBreak < 0.12f)
            {
                shake = new Vector2(UnityEngine.Random.Range(-1f, 1f), UnityEngine.Random.Range(-1f, 1f)) * 2.5f;
            }
            else if (atMerge >= 0 && atMerge < 0.35f)
            {
                shake = new Vector2(UnityEngine.Random.Range(-1f, 1f), UnityEngine.Random.Range(-1f, 1f)) * 3.5f * (1f - atMerge / 0.35f);
            }
            slotRect.anchoredPosition = shakeBase + shake;
        }

        // the item lights up as the essence goes in, at its brightest around the merge
        if (flash != null)
        {
            float u = Mathf.InverseLerp(AbsorbAt + 0.1f, ImpactAt + 0.45f, t);
            float bump = u <= 0f || u >= 1f ? 0f : Mathf.Sin(u * Mathf.PI);
            flash.color = new Color(0.8f, 0.6f, 1f, 0.5f * bump);
        }
        if (t >= EndAt)
        {
            EndAnimationNow();
            dirty = true;
        }
    }

    private static void ShatterBurst(float now)
    {
        bloom = CentredImage("Bloom", OrbArt.Named("glow_violet"), 260f, 260f, out bloomRect, glow: true);
        bloomRect.anchoredPosition = OrbPoint;
        ringFx = CentredImage("Ring", OrbArt.Named("ring_violet"), 230f, 230f, out ringRect, glow: true);
        ringRect.anchoredPosition = OrbPoint;
        for (int i = 0; i < 14; i++)
        {
            float ang = UnityEngine.Random.Range(0f, Mathf.PI * 2);
            AddSpark(OrbPoint, new Color(0.8f, 0.6f, 1f), UnityEngine.Random.Range(2.5f, 5f),
                new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * UnityEngine.Random.Range(80f, 260f), UnityEngine.Random.Range(0.3f, 0.6f));
        }
    }

    /// <summary>Little motes of essence leave the broken orb and go down into the item as the trails do.</summary>
    private static void SendMotes(float now)
    {
        for (int i = 0; i < 12; i++)
        {
            GameObject go = Effect("Mote");
            RectTransform r = go.GetComponent<RectTransform>();
            r.pivot = new Vector2(0.5f, 0.5f);
            float size = UnityEngine.Random.Range(12f, 22f);
            r.sizeDelta = new Vector2(size, size);
            Image img = R.MakeImage(go.transform, "M", new Color(0.85f, 0.65f, 1f, 0f));
            img.sprite = OrbArt.Named("glow_violet");
            img.raycastTarget = false;
            if (Additive() != null)
            {
                img.material = additive;
            }
            Stretch(img.rectTransform, 0);
            Vector2 from = OrbPoint + new Vector2(UnityEngine.Random.Range(-34f, 34f), UnityEngine.Random.Range(-20f, 20f));
            Vector2 to = SlotCentre + new Vector2(UnityEngine.Random.Range(-10f, 10f), UnityEngine.Random.Range(-10f, 10f));
            motes.Add(new Mote
            {
                Go = go, Rect = r, Img = img, From = from, To = to, Size = size,
                Born = now + UnityEngine.Random.Range(0f, 0.30f), Life = UnityEngine.Random.Range(0.45f, 0.65f),
                Sway = UnityEngine.Random.Range(-18f, 18f),
            });
        }
    }

    private static void TickMotes(float now)
    {
        for (int i = motes.Count - 1; i >= 0; i--)
        {
            Mote m = motes[i];
            float age = now - m.Born;
            if (m.Go == null || age >= m.Life)
            {
                if (m.Go != null)
                {
                    Object.Destroy(m.Go);
                }
                motes.RemoveAt(i);
                continue;
            }
            if (age < 0)
            {
                continue;
            }
            float u = age / m.Life;
            Vector2 pos = Vector2.Lerp(m.From, m.To, Ease(u)) + new Vector2(Mathf.Sin(u * Mathf.PI) * m.Sway, 0);
            m.Rect.anchoredPosition = pos;
            m.Rect.localScale = Vector3.one * Mathf.Lerp(1f, 0.4f, u);
            // they brighten as they leave the orb and fade as they come to the item
            m.Img.color = new Color(0.85f, 0.65f, 1f, Mathf.Clamp01(u * 5f) * Mathf.Clamp01((1f - u) * 2.5f));
        }
    }

    /// <summary>The faint wisps that escape once the item has taken the essence.</summary>
    private static void ResidualSparks(float now)
    {
        for (int i = 0; i < 9; i++)
        {
            float ang = UnityEngine.Random.Range(0f, Mathf.PI * 2);
            AddSpark(SlotCentre, new Color(0.78f, 0.62f, 1f), UnityEngine.Random.Range(2f, 4f),
                new Vector2(Mathf.Cos(ang), Mathf.Sin(ang) * 0.8f + 0.3f) * UnityEngine.Random.Range(40f, 130f), UnityEngine.Random.Range(0.5f, 0.9f));
        }
    }

    private static void Impact(float now)
    {
        AdvancedMaterial m = AdvancedMaterials.All.Find(x => x.Key == pendingKey);
        bool ok = false;
        string message = "";
        if (pendingKey == AdvancedMaterials.EncapsulationOrb)
        {
            ok = Items_Encapsulation.UseOn(item, out message);
        }
        status = message;
        statusColour = ok ? Good : Bad;
        if (selectedKey != null && AdvancedMaterials.Count(selectedKey) < 1)
        {
            selectedKey = null; // that was the last one
        }
        Color a = new Color(0.72f, 0.48f, 1f);
        Color b = ok ? Color.white : new Color(1f, 0.55f, 0.4f);
        Vector2 at = SlotCentre;
        for (int i = 0; i < 12; i++)
        {
            float ang = UnityEngine.Random.Range(0f, Mathf.PI * 2);
            float speed = UnityEngine.Random.Range(140f, 520f);
            AddSpark(at, Color.Lerp(a, b, UnityEngine.Random.value), UnityEngine.Random.Range(3f, 7f),
                new Vector2(Mathf.Cos(ang), Mathf.Sin(ang) * 0.8f + 0.25f) * speed, UnityEngine.Random.Range(0.35f, 0.9f));
        }
        dirty = false; // the window is rebuilt when the effect is done, so the item is not redrawn in the middle of it
    }

    private static void AddSpark(Vector2 at, Color colour, float size, Vector2 vel, float life)
    {
        GameObject go = Effect("Spark");
        RectTransform r = go.GetComponent<RectTransform>();
        r.pivot = new Vector2(0.5f, 0.5f);
        r.sizeDelta = new Vector2(size, size * UnityEngine.Random.Range(1f, 2.4f));
        r.anchoredPosition = at;
        r.localRotation = Quaternion.Euler(0, 0, UnityEngine.Random.Range(0f, 90f));
        Image img = R.MakeImage(go.transform, "S", colour);
        img.raycastTarget = false;
        Stretch(img.rectTransform, 0);
        sparks.Add(new Spark { Go = go, Rect = r, Img = img, Pos = at, Vel = vel, Born = Time.unscaledTime, Life = life });
    }

    private static void TickSparks(float now)
    {
        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
        for (int i = sparks.Count - 1; i >= 0; i--)
        {
            Spark s = sparks[i];
            float age = now - s.Born;
            if (s.Go == null || age >= s.Life)
            {
                if (s.Go != null)
                {
                    Object.Destroy(s.Go);
                }
                sparks.RemoveAt(i);
                continue;
            }
            s.Vel.y -= 520f * dt;
            s.Pos += s.Vel * dt;
            s.Rect.anchoredPosition = s.Pos;
            Color c = s.Img.color;
            c.a = 1f - age / s.Life;
            s.Img.color = c;
        }
    }

    private static void EndAnimationNow()
    {
        animating = false;
        foreach (GameObject go in effects)
        {
            if (go != null)
            {
                Object.Destroy(go);
            }
        }
        effects.Clear();
        sparks.Clear();
        orbFx = null;
        orbFxImage = null;
        vfxImage = null;
        vfxExtra = null;
        backGlow = null;
        bloom = null;
        ringFx = null;
        flash = null;
        motes.Clear();
        if (slotRect != null)
        {
            slotRect.anchoredPosition = shakeBase;
        }
    }
}
