using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using HarmonyLib;
using Il2Cpp;
using Il2CppLE.AssetBundles;
using Il2CppLE.AssetManagement;
using Il2CppLE.Factions;
using Il2CppTMPro;
using LastEpoch_Hud.Scripts.Core.Crafting;
using LastEpoch_Hud.Scripts.ModUI.Shell.Host;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using R = LastEpoch_Hud.Scripts.Mods.Items.Crafting.Items_RecombinatorWindow;

namespace LastEpoch_Hud.Scripts.Mods.Items.Crafting;

/// <summary>
/// The Memory Reflector window, opened from a button on the forge. Item A sits at the top and item B at the bottom
/// with a mirror tunnel between them. One affix of item B is reflected onto item A; B is destroyed either way. B
/// shatters, and its shards either stream up the tunnel into A or are swallowed by a black hole.
/// </summary>
internal static class Items_MemoryReflectorWindow
{
    private const string ForgePath = "GUI/Panel System/Panel Stacks/Left Panel Stack/ForgePanel (Clone)";
    private const string ButtonName = "MemoryReflectorButton";
    private const string RootName = "MemoryReflectorWindow";

    private const float Left = 48, WinW = 670, IconW = 140, IconH = 196;
    private const float AY = 104, BY = 470; // top of item A and of item B
    private static float CenterX => Left + WinW / 2;

    // ---------------------------------------------------------------- state

    private static GameObject root;
    private static GameObject buttonTemplate, labelTemplate;
    private static TMP_FontAsset headFont, bodyFont;
    private static RectTransform contentRect;
    private static Vector2 contentBase;

    private static ItemData itemA, itemB;
    private static Items_MemoryReflector.Offer offer;
    private static int selectedId = -1;
    private static bool dirty = true;
    private static float nextCheck;
    private static int escapeFrames;
    private static readonly List<GameObject> dynamic = new();

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
                TickIcons();
                TickRows();
            }
            if (IsOpen && dirty)
            {
                Refresh();
                foreach (GameObject go in effects)
                {
                    if (go != null)
                    {
                        go.transform.SetAsLastSibling(); // effects stay above the rebuilt window
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Main.logger_instance?.Warning("Memory Reflector window: " + ex.Message);
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
        R.SetText(button, "Memory Reflector");
        RectTransform rect = button.GetComponent<RectTransform>();
        RectTransform source = template.GetComponent<RectTransform>();
        Transform guide = main.Find("HelpButton");
        RectTransform help = guide != null ? guide.GetComponent<RectTransform>() : null;
        rect.sizeDelta = new Vector2(190, source.sizeDelta.y);
        if (help != null)
        {
            // just left of the Guide button, which hangs from the top right corner
            rect.anchorMin = help.anchorMin;
            rect.anchorMax = help.anchorMax;
            rect.pivot = help.pivot;
            rect.anchoredPosition = new Vector2(help.anchoredPosition.x - help.sizeDelta.x - 12, help.anchoredPosition.y);
        }
        else
        {
            rect.anchorMin = source.anchorMin;
            rect.anchorMax = source.anchorMax;
            rect.pivot = source.pivot;
            rect.anchoredPosition = new Vector2(source.anchoredPosition.x + source.sizeDelta.x + 12 + 150 + 12, source.anchoredPosition.y);
        }
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
            ReturnItems();
            selectedId = -1;
            dirty = true;
            root.SetActive(true);
            root.transform.SetAsLastSibling();
        }
        catch (Exception ex)
        {
            Main.logger_instance?.Warning("Memory Reflector open failed: " + ex.Message);
        }
    }

    public static void Close()
    {
        if (root == null)
        {
            return;
        }
        CloseTooltip();
        EndAnimationNow();
        for (int i = 0; i < icons.Length; i++)
        {
            icons[i]?.Dispose();
            icons[i] = null;
        }
        ReturnItems();
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

    private static void ReturnItems()
    {
        foreach (ItemData item in new[] { itemA, itemB })
        {
            GiveBack(item);
        }
        itemA = itemB = null;
        offer = null;
    }

    private static void GiveBack(ItemData item)
    {
        ItemContainersManager manager = ItemContainersManager.Instance;
        if (item.IsNullOrDestroyed() || manager.IsNullOrDestroyed())
        {
            return;
        }
        if (!manager.inventory.TryAddItem(item, 1, Context.DEFAULT))
        {
            manager.cursor.TryAddItem(item, 1, Context.DEFAULT);
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
        contentRect = content.GetComponent<RectTransform>();
        contentBase = contentRect.anchoredPosition;

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
            title.GetComponent<TextMeshProUGUI>().text = "Memory Reflector";
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

    // the forge's own frame pieces, for the item slots
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

    private static GameObject Label(string text, float x, float y, float w, float h, float size, Color color, TextAlignmentOptions align)
    {
        GameObject go = Object.Instantiate(labelTemplate, Content, false);
        R.StripGameLogic(go);
        RectTransform r = go.GetComponent<RectTransform>();
        r.anchorMin = r.anchorMax = new Vector2(0, 1);
        r.pivot = new Vector2(0, 1);
        r.anchoredPosition = new Vector2(x, -y);
        r.sizeDelta = new Vector2(w, h);
        TextMeshProUGUI t = go.GetComponent<TextMeshProUGUI>();
        TMP_FontAsset font = size >= 18 ? headFont : bodyFont;
        if (font != null)
        {
            t.font = font;
        }
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
        R.StripGameLogic(go);
        RectTransform r = go.GetComponent<RectTransform>();
        r.anchorMin = r.anchorMax = new Vector2(0, 1);
        r.pivot = new Vector2(0, 1);
        r.anchoredPosition = new Vector2(x, -y);
        r.sizeDelta = new Vector2(w, h);
        R.SetText(go, text);
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
        R.OnClick(go, action);
        go.SetActive(true);
        dynamic.Add(go);
        return go;
    }

    private static readonly Color Gold = new(0.93f, 0.78f, 0.38f);
    private static readonly Color Dim = new(0.65f, 0.62f, 0.58f);
    private static readonly Color Good = new(0.55f, 0.85f, 0.5f);
    private static readonly Color Bad = new(0.9f, 0.4f, 0.35f);

    private static void Stretch(RectTransform r, float margin)
    {
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = new Vector2(margin, margin);
        r.offsetMax = new Vector2(-margin, -margin);
    }

    // ---------------------------------------------------------------- item icons

    private sealed class Icon
    {
        public Sprite Back, Content;
        public Color BackColor = Color.white;
        public LoadRef<Sprite> Load;
        public GameObject Holder;
        public InventoryItemUI Copy;
        public float Born;

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

    private static readonly Icon[] icons = new Icon[2]; // 0: item A, 1: item B
    private static readonly long[] iconFor = new long[2];

    private static Icon IconFor(ItemData item, int id)
    {
        long key = (long)item.Pointer;
        if (icons[id] != null && iconFor[id] == key)
        {
            return icons[id];
        }
        icons[id]?.Dispose();
        icons[id] = BuildIcon(item);
        iconFor[id] = key;
        return icons[id];
    }

    private static Icon BuildIcon(ItemData item)
    {
        try
        {
            SoftRef<Sprite> soft = item.GetItemSpriteFromData(ItemUIContext.Default);
            LoadRef<Sprite> load = soft == null || !soft ? null : SoftRefExtensions.CreateLoadRef(soft, "LastEpoch_Hud", 0);
            var icon = new Icon { Load = load, Born = Time.unscaledTime };
            // the game's own item background, from a hidden copy of an inventory item widget
            try
            {
                InventoryItemUI source = Object.FindObjectsOfType<InventoryItemUI>()
                    .FirstOrDefault(u => u != null && u.gameObject.activeInHierarchy && !u.backgroundImage.IsNullOrDestroyed());
                if (source != null)
                {
                    var holder = new GameObject("ReflectorBackgroundHolder");
                    holder.AddComponent<RectTransform>();
                    holder.transform.SetParent(root.transform, false);
                    holder.SetActive(false);
                    InventoryItemUI copy = Object.Instantiate(source.gameObject, holder.transform, false).GetComponent<InventoryItemUI>();
                    copy.SetBackgroundSpriteAndColour(copy.backgroundImage, item);
                    icon.Holder = holder;
                    icon.Copy = copy;
                }
            }
            catch (Exception ex)
            {
                Main.logger_instance?.Warning("Memory Reflector background failed: " + ex.Message);
            }
            icon.Poll();
            return icon;
        }
        catch (Exception ex)
        {
            Main.logger_instance?.Warning("Memory Reflector icon failed: " + ex.Message);
            return null;
        }
    }

    private static void TickIcons()
    {
        for (int i = 0; i < icons.Length; i++)
        {
            Icon icon = icons[i];
            if (icon != null && (icon.Content == null || icon.Back == null) && icon.Born > 0)
            {
                bool had = icon.Content != null;
                icon.Poll();
                if (!had && icon.Content != null)
                {
                    dirty = true;
                }
            }
        }
    }

    private static Color ItemColour(ItemData item) => R.ItemColor(item);

    // ---------------------------------------------------------------- item slots

    private static readonly RectTransform[] slotRects = new RectTransform[2];
    private static readonly ItemData[] slotItems = new ItemData[2];
    private static readonly CanvasGroup[] slotGroups = new CanvasGroup[2];
    private static int hovered = -1;

    private static void SlotBox(ItemData item, int id, float x, float y, Action click)
    {
        Image box = R.MakeImage(Content, "Slot", new Color(0.05f, 0.04f, 0.035f, 0.97f));
        RectTransform br = box.rectTransform;
        br.anchorMin = br.anchorMax = new Vector2(0, 1);
        br.pivot = new Vector2(0, 1);
        br.anchoredPosition = new Vector2(x, -y);
        br.sizeDelta = new Vector2(IconW, IconH);
        dynamic.Add(box.gameObject);
        slotGroups[id] = box.gameObject.AddComponent<CanvasGroup>();
        if (item != null)
        {
            Icon icon = IconFor(item, id);
            if (icon == null || icon.Back == null)
            {
                box.color = Color.Lerp(new Color(0.05f, 0.04f, 0.035f, 0.97f), ItemColour(item), 0.2f);
            }
            if (icon != null)
            {
                if (icon.Back != null)
                {
                    Image back = R.MakeImage(box.transform, "ItemBack", icon.BackColor);
                    back.sprite = icon.Back;
                    back.raycastTarget = false;
                    Stretch(back.rectTransform, 4);
                }
                if (icon.Content != null)
                {
                    Image content = R.MakeImage(box.transform, "ItemIcon", Color.white);
                    content.sprite = icon.Content;
                    content.preserveAspect = true;
                    content.raycastTarget = false;
                    Stretch(content.rectTransform, 14);
                }
            }
            slotRects[id] = br;
            slotItems[id] = item;
        }
        SlotFrame(box, item != null ? ItemColour(item) : (Color?)null);
        Button button = box.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.onClick.AddListener(click);
    }

    /// <summary>The forge's own frame pieces round a slot, with a thin line of the item's rarity colour just inside.</summary>
    private static void SlotFrame(Image box, Color? rarity)
    {
        float th = 8f;
        for (int side = 0; side < 4; side++)
        {
            EdgeSprite e = edges[side];
            if (e == null || e.Sprite == null)
            {
                continue;
            }
            th = Mathf.Min(e.Thickness, 12f);
            Image img = R.MakeImage(box.transform, "Border", e.Color);
            img.sprite = e.Sprite;
            img.type = e.Type;
            img.raycastTarget = false;
            RectTransform r = img.rectTransform;
            switch (side)
            {
                case 0: r.anchorMin = new Vector2(0, 1); r.anchorMax = new Vector2(1, 1); r.pivot = new Vector2(0.5f, 1); r.sizeDelta = new Vector2(0, th); break;
                case 1: r.anchorMin = new Vector2(0, 0); r.anchorMax = new Vector2(1, 0); r.pivot = new Vector2(0.5f, 0); r.sizeDelta = new Vector2(0, th); break;
                case 2: r.anchorMin = new Vector2(0, 0); r.anchorMax = new Vector2(0, 1); r.pivot = new Vector2(0, 0.5f); r.sizeDelta = new Vector2(th, 0); break;
                default: r.anchorMin = new Vector2(1, 0); r.anchorMax = new Vector2(1, 1); r.pivot = new Vector2(1, 0.5f); r.sizeDelta = new Vector2(th, 0); break;
            }
            r.anchoredPosition = Vector2.zero;
        }
        Color line = rarity ?? new Color(0.5f, 0.4f, 0.24f);
        line.a = 0.9f;
        Transform ft = box.transform;
        R.Part(ft, "RarityTop", line, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -th), new Vector2(-2 * th, 2));
        R.Part(ft, "RarityBottom", line, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, th), new Vector2(-2 * th, 2));
        R.Part(ft, "RarityLeft", line, new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(th, 0), new Vector2(2, -2 * th));
        R.Part(ft, "RarityRight", line, new Vector2(1, 0), new Vector2(1, 1), new Vector2(1, 0.5f), new Vector2(-th, 0), new Vector2(2, -2 * th));
    }

    private static void ClickSlot(int index)
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
            ItemData current = index == 0 ? itemA : itemB;
            if (current != null)
            {
                GiveBack(current);
                if (index == 0) itemA = null; else itemB = null;
                icons[index]?.Dispose();
                icons[index] = null;
            }
            else
            {
                var content = manager.cursor.GetContent();
                if (content != null && content.Count > 0)
                {
                    ItemContainerEntry entry = content[0];
                    ItemData item = entry.data;
                    if (!item.IsNullOrDestroyed() && manager.cursor.TryRemoveItem(entry, 1, Context.DEFAULT))
                    {
                        if (index == 0) itemA = item; else itemB = item;
                    }
                }
            }
            selectedId = -1;
            dirty = true;
        }
        catch (Exception ex)
        {
            Main.logger_instance?.Warning("Memory Reflector slot failed: " + ex.Message);
        }
    }

    // ---------------------------------------------------------------- the mirror tunnel

    private static GameObject tubeRoot;
    private static readonly List<Image> tubeGlow = new();
    private static readonly List<float> tubeAlong = new(); // 0 at item B, 1 at item A
    private static readonly List<Color> tubeBase = new();
    private static float TubeTop => AY + IconH + 6;
    private static float TubeBottom => BY - 6;

    /// <summary>
    /// A tall mirror tube between the items, built once: chrome rails and a dark glass core, both halves exactly
    /// mirrored about the centre line, with pale sheen stripes and a ladder of rungs that light up as shards pass.
    /// </summary>
    private static void BuildTube()
    {
        if (tubeRoot != null)
        {
            return;
        }
        tubeRoot = new GameObject("MirrorTube");
        RectTransform tr = tubeRoot.AddComponent<RectTransform>();
        tubeRoot.transform.SetParent(Content, false);
        tr.anchorMin = Vector2.zero;
        tr.anchorMax = Vector2.one;
        tr.offsetMin = tr.offsetMax = Vector2.zero;
        tubeGlow.Clear();
        tubeAlong.Clear();
        tubeBase.Clear();

        float top = TubeTop, bottom = TubeBottom, length = bottom - top;
        Image Rect(float cx, float y0, float y1, float w, Color c)
        {
            Image img = R.MakeImage(tubeRoot.transform, "Tube", c);
            img.raycastTarget = false;
            RectTransform r = img.rectTransform;
            r.anchorMin = r.anchorMax = new Vector2(0, 1);
            r.pivot = new Vector2(0.5f, 0);
            r.anchoredPosition = new Vector2(cx, -y1);
            r.sizeDelta = new Vector2(w, y1 - y0);
            return img;
        }
        Image Block(float cx, float cy, float w, float h, float rot, Color c)
        {
            Image img = R.MakeImage(tubeRoot.transform, "Tube", c);
            img.raycastTarget = false;
            RectTransform r = img.rectTransform;
            r.anchorMin = r.anchorMax = new Vector2(0, 1);
            r.pivot = new Vector2(0.5f, 0.5f);
            r.anchoredPosition = new Vector2(cx, -cy);
            r.sizeDelta = new Vector2(w, h);
            r.localRotation = Quaternion.Euler(0, 0, rot);
            return img;
        }

        // the glass core and its mirror axis
        Rect(CenterX, top, bottom, 38, new Color(0.02f, 0.035f, 0.07f, 0.97f));
        Rect(CenterX, top, bottom, 1.5f, new Color(0.75f, 0.88f, 1f, 0.28f));

        // pale sheen stripes, mirrored left and right of the axis
        for (float y = top + 10; y < bottom - 10; y += 26)
        {
            Block(CenterX - 9, y, 15, 3, 55, new Color(0.8f, 0.92f, 1f, 0.11f));
            Block(CenterX + 9, y, 15, 3, -55, new Color(0.8f, 0.92f, 1f, 0.11f));
        }

        // the ladder of rungs: these are what the shards light
        for (float y = top + 6; y < bottom - 4; y += 9)
        {
            float along = 1f - (y - top) / length;
            Color baseColour = new(0.22f, 0.30f, 0.44f, 0.5f);
            foreach (float side in new[] { -1f, 1f })
            {
                Image rung = Block(CenterX + side * 9, y, 16, 2, side * 18, baseColour);
                tubeGlow.Add(rung);
                tubeAlong.Add(along);
                tubeBase.Add(baseColour);
            }
        }

        // chrome rails, the two sides mirror images of each other
        var rng = new System.Random(311);
        for (float y = top; y < bottom; y += 8)
        {
            float v = 0.38f + (float)rng.NextDouble() * 0.3f;
            Color chrome = new(v * 0.9f, v, v * 1.15f, 1f);
            Color dark = new(0.07f, 0.08f, 0.1f, 1f);
            foreach (float side in new[] { -1f, 1f })
            {
                Block(CenterX + side * 22, y + 4, 8, 9, 0, dark);
                Block(CenterX + side * 21.5f, y + 4, 5, 7, 0, chrome);
                Block(CenterX + side * 19.5f, y + 4, 1.5f, 8, 0, new Color(0.9f, 0.95f, 1f, 0.5f));
            }
        }

        // flared ends, wider toward each item, and a diamond at the middle
        for (int k = 0; k < 4; k++)
        {
            float w = 44 + k * 14;
            Color c = new(0.35f + 0.05f * k, 0.38f + 0.05f * k, 0.46f + 0.05f * k, 1f);
            Rect(CenterX, top + k * 5, top + k * 5 + 4, w, c);
            Rect(CenterX, bottom - k * 5 - 4, bottom - k * 5, w, c);
        }
        float midY = (top + bottom) / 2;
        Block(CenterX, midY, 14, 14, 45, new Color(0.45f, 0.5f, 0.6f));
        Block(CenterX, midY, 8, 8, 45, new Color(0.05f, 0.07f, 0.12f));
        Block(CenterX, midY, 3.5f, 3.5f, 45, new Color(0.75f, 0.9f, 1f));
    }

    /// <summary>A faint upside-down copy of an item, as if the tunnel were a mirror: below item A, above item B.</summary>
    private static void Reflection(ItemData item, int id, bool belowItem, float edgeY)
    {
        if (item == null)
        {
            return;
        }
        Icon icon = icons[id];
        const float shown = 70f;
        var container = new GameObject("Reflection");
        RectTransform cr = container.AddComponent<RectTransform>();
        container.AddComponent<CanvasRenderer>();
        Image mask = container.AddComponent<Image>();
        mask.color = Color.white;
        Mask m = container.AddComponent<Mask>();
        m.showMaskGraphic = false;
        CanvasGroup group = container.AddComponent<CanvasGroup>();
        group.alpha = 0.22f;
        group.blocksRaycasts = false;
        container.transform.SetParent(Content, false);
        cr.anchorMin = cr.anchorMax = new Vector2(0, 1);
        cr.pivot = new Vector2(0.5f, belowItem ? 1 : 0);
        cr.anchoredPosition = new Vector2(CenterX, -edgeY);
        cr.sizeDelta = new Vector2(IconW, shown);
        dynamic.Add(container);
        if (icon == null)
        {
            return;
        }
        // the art, flipped, hung from the clipping box so only the nearest part of it shows
        var art = new GameObject("Art");
        RectTransform ar = art.AddComponent<RectTransform>();
        art.transform.SetParent(container.transform, false);
        ar.anchorMin = ar.anchorMax = new Vector2(0.5f, belowItem ? 1 : 0);
        ar.pivot = new Vector2(0.5f, belowItem ? 1 : 0);
        ar.anchoredPosition = Vector2.zero;
        ar.sizeDelta = new Vector2(IconW, IconH);
        ar.localScale = new Vector3(1, -1, 1);
        if (icon.Back != null)
        {
            Image back = R.MakeImage(art.transform, "Back", icon.BackColor);
            back.sprite = icon.Back;
            back.raycastTarget = false;
            Stretch(back.rectTransform, 4);
        }
        if (icon.Content != null)
        {
            Image content = R.MakeImage(art.transform, "Icon", Color.white);
            content.sprite = icon.Content;
            content.preserveAspect = true;
            content.raycastTarget = false;
            Stretch(content.rectTransform, 14);
        }
    }

    // ---------------------------------------------------------------- affix plates (item B's affixes)

    private sealed class Row
    {
        public RectTransform Rect;
        public Image Bg, Bar;
        public Image[] Edge = new Image[4];
        public TextMeshProUGUI Name, Tier;
        public bool On, Hover, Static;
    }

    private static readonly List<Row> rows = new();
    private static readonly Color RowBase = new(0.035f, 0.03f, 0.026f, 0.82f);
    private static readonly Color RowHover = new(0.085f, 0.07f, 0.05f, 0.9f);
    private static readonly Color RowOn = new(0.13f, 0.10f, 0.055f, 0.94f);
    private static readonly Color EdgeBase = new(0.55f, 0.50f, 0.44f, 1f);
    private static readonly Color EdgeHover = new(0.9f, 0.8f, 0.6f, 1f);
    private static readonly Color EdgeOn = new(1f, 0.82f, 0.4f, 1f);

    private static readonly Regex ColourTags = new("</?color[^>]*>|</?link[^>]*>", RegexOptions.Compiled);
    private static readonly Regex LineBreaks = new(@"<br\s*/?>|\r\n|\r| | |\v|\\n", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>The affix worded the way the item's own tooltip words it, with its roll.</summary>
    private static string Wording(Items_MemoryReflector.Candidate c)
    {
        try
        {
            ItemDataUnpacked data = itemBForText.TryCast<ItemDataUnpacked>();
            if (data != null && c.Source != null)
            {
                string text = TooltipItemManager.FormatAffix(data, c.Source, TooltipMode.DefaultInfo, TooltipItemManager.SlotType.CRAFTING,
                    false, -1, -1, false, false, ValueRangeFormat.None, true, false);
                text = LineBreaks.Replace(ColourTags.Replace(text ?? "", ""), "\n");
                string[] lines = text.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).ToArray();
                if (lines.Length > 0)
                {
                    return string.Join("\n", lines);
                }
            }
        }
        catch (Exception)
        {
        }
        AffixList.Affix def = AffixList.get()?.GetAffix(c.AffixId);
        return def == null ? "Affix " + c.AffixId : def.affixDisplayName;
    }

    private static ItemData itemBForText => itemB;

    private static TextMeshProUGUI RowText(Transform parent, TextAlignmentOptions align, float size, float left, float right)
    {
        GameObject go = Object.Instantiate(labelTemplate, parent, false);
        R.StripGameLogic(go);
        RectTransform r = go.GetComponent<RectTransform>();
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.pivot = new Vector2(0.5f, 0.5f);
        r.offsetMin = new Vector2(left, 0);
        r.offsetMax = new Vector2(-right, 0);
        TextMeshProUGUI t = go.GetComponent<TextMeshProUGUI>();
        if (bodyFont != null)
        {
            t.font = bodyFont;
        }
        t.alignment = align;
        t.fontSize = size;
        t.fontStyle = FontStyles.Normal;
        t.enableWordWrapping = false;
        t.overflowMode = TextOverflowModes.Ellipsis;
        t.raycastTarget = false;
        go.SetActive(true);
        return t;
    }

    private static void AffixPlate(Items_MemoryReflector.Candidate c, float x, float y, float w, float h, float fontSize)
    {
        bool on = c.CanGo && c.AffixId == selectedId;
        Image bg = R.MakeImage(Content, "AffixRow", RowBase);
        RectTransform r = bg.rectTransform;
        r.anchorMin = r.anchorMax = new Vector2(0, 1);
        r.pivot = new Vector2(0, 1);
        r.anchoredPosition = new Vector2(x, -y);
        r.sizeDelta = new Vector2(w, h);
        dynamic.Add(bg.gameObject);

        var row = new Row { Rect = r, Bg = bg, On = on, Static = !c.CanGo };
        for (int side = 0; side < 4; side++)
        {
            Image e = R.MakeImage(bg.transform, "Edge", EdgeBase);
            e.raycastTarget = false;
            RectTransform er = e.rectTransform;
            switch (side)
            {
                case 0: er.anchorMin = new Vector2(0, 1); er.anchorMax = new Vector2(1, 1); er.pivot = new Vector2(0.5f, 1); er.sizeDelta = new Vector2(0, 2); break;
                case 1: er.anchorMin = new Vector2(0, 0); er.anchorMax = new Vector2(1, 0); er.pivot = new Vector2(0.5f, 0); er.sizeDelta = new Vector2(0, 2); break;
                case 2: er.anchorMin = new Vector2(0, 0); er.anchorMax = new Vector2(0, 1); er.pivot = new Vector2(0, 0.5f); er.sizeDelta = new Vector2(2, 0); break;
                default: er.anchorMin = new Vector2(1, 0); er.anchorMax = new Vector2(1, 1); er.pivot = new Vector2(1, 0.5f); er.sizeDelta = new Vector2(2, 0); break;
            }
            er.anchoredPosition = Vector2.zero;
            row.Edge[side] = e;
        }
        row.Bar = R.Part(bg.transform, "Bar", EdgeOn, new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(4, 0), new Vector2(3, -10));
        row.Bar.color = new Color(EdgeOn.r, EdgeOn.g, EdgeOn.b, 0f);

        TextMeshProUGUI tier = RowText(bg.transform, TextAlignmentOptions.Center, 8, 8, 0);
        RectTransform tr = tier.GetComponent<RectTransform>();
        tr.anchorMin = new Vector2(0, 0);
        tr.anchorMax = new Vector2(0, 1);
        tr.pivot = new Vector2(0, 0.5f);
        tr.offsetMin = new Vector2(8, 0);
        tr.offsetMax = new Vector2(34, 0);
        tier.overflowMode = TextOverflowModes.Overflow;
        tier.fontStyle = FontStyles.UpperCase;
        tier.text = "T" + c.Tier;
        tier.color = new Color(0.52f, 0.50f, 0.47f);
        row.Tier = tier;
        R.Part(bg.transform, "Divider", new Color(0.55f, 0.46f, 0.32f, 0.55f), new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(38, 0), new Vector2(1, -12));

        string text = Wording(c);
        if (!c.CanGo && c.Why.Length > 0)
        {
            text += "\n<size=75%>(" + c.Why + ")</size>";
        }
        TextMeshProUGUI name = RowText(bg.transform, TextAlignmentOptions.MidlineLeft, fontSize, 46, 10);
        name.enableWordWrapping = true;
        name.overflowMode = TextOverflowModes.Overflow;
        name.enableAutoSizing = true;
        name.fontSizeMin = 8;
        name.fontSizeMax = fontSize;
        name.text = text;
        row.Name = name;

        if (c.CanGo)
        {
            Button button = bg.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            int id = c.AffixId;
            button.onClick.AddListener((Action)(() =>
            {
                if (animating)
                {
                    return;
                }
                selectedId = selectedId == id ? -1 : id;
                dirty = true;
            }));
        }
        else
        {
            bg.raycastTarget = false;
        }
        ApplyRow(row, 1f);
        rows.Add(row);
    }

    private static void ApplyRow(Row row, float blend)
    {
        Color bg = row.On ? RowOn : row.Hover ? RowHover : RowBase;
        Color edge = row.On ? EdgeOn : row.Hover ? EdgeHover : EdgeBase;
        Color text = row.Static ? new Color(0.5f, 0.48f, 0.45f)
            : row.On ? new Color(1f, 0.93f, 0.68f) : row.Hover ? new Color(0.98f, 0.95f, 0.88f) : new Color(0.82f, 0.79f, 0.72f);
        row.Bg.color = Color.Lerp(row.Bg.color, bg, blend);
        foreach (Image e in row.Edge)
        {
            if (e != null)
            {
                e.color = Color.Lerp(e.color, row.Static ? new Color(0.3f, 0.28f, 0.25f, 0.8f) : edge, blend);
            }
        }
        row.Name.color = Color.Lerp(row.Name.color, text, blend);
        Color bar = row.Bar.color;
        bar.a = Mathf.Lerp(bar.a, row.On ? 1f : row.Hover ? 0.45f : 0f, blend);
        row.Bar.color = bar;
    }

    private static UnityEngine.Camera PointerCamera()
    {
        Canvas canvas = root.GetComponentInParent<Canvas>();
        return canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
    }

    private static void TickRows()
    {
        if (rows.Count == 0)
        {
            return;
        }
        Vector2 mouse = Input.mousePosition;
        UnityEngine.Camera cam = PointerCamera();
        float blend = Mathf.Clamp01(Time.unscaledDeltaTime * 16f);
        foreach (Row row in rows)
        {
            if (row.Rect == null)
            {
                continue;
            }
            row.Hover = !row.Static && !animating && RectTransformUtility.RectangleContainsScreenPoint(row.Rect, mouse, cam);
            ApplyRow(row, blend);
        }
    }

    private static void TickHover()
    {
        int now = -1;
        Vector2 mouse = Input.mousePosition;
        UnityEngine.Camera cam = PointerCamera();
        for (int i = 0; i < slotRects.Length; i++)
        {
            RectTransform r = slotRects[i];
            if (r != null && slotItems[i] != null && !animating && RectTransformUtility.RectangleContainsScreenPoint(r, mouse, cam))
            {
                now = i;
                break;
            }
        }
        if (now == hovered)
        {
            return;
        }
        CloseTooltip();
        hovered = now;
        if (now >= 0)
        {
            try
            {
                TooltipItemManager manager = Object.FindObjectOfType<TooltipItemManager>();
                ItemDataUnpacked data = slotItems[now].TryCast<ItemDataUnpacked>();
                RectTransform r = slotRects[now];
                if (manager != null && data != null)
                {
                    manager.OpenTooltip(data, TooltipItemManager.SlotType.CRAFTING, Vector2.zero, r.position, r.gameObject, r.rect.size);
                }
            }
            catch (Exception ex)
            {
                Main.logger_instance?.Warning("Memory Reflector tooltip failed: " + ex.Message);
            }
        }
    }

    private static void CloseTooltip()
    {
        if (hovered < 0)
        {
            return;
        }
        hovered = -1;
        try
        {
            TooltipItemManager.ForceClose();
        }
        catch (Exception)
        {
        }
    }

    // ---------------------------------------------------------------- currency

    private static Sprite amberIcon;
    private static float nextAmberSearch;

    private static void FindAmberIcon()
    {
        if (amberIcon != null || Time.unscaledTime < nextAmberSearch)
        {
            return;
        }
        nextAmberSearch = Time.unscaledTime + 15f;
        try
        {
            // the Weaver faction's own Memory Amber sprite
            foreach (string wanted in new[] { "Faction_TW_MemoryAmber", "EchoReward_MemoryAmber" })
            {
                foreach (Sprite sp in Resources.FindObjectsOfTypeAll<Sprite>())
                {
                    if (sp != null && sp.name == wanted)
                    {
                        amberIcon = sp;
                        break;
                    }
                }
                if (amberIcon != null)
                {
                    break;
                }
            }
            Main.logger_instance?.Msg("Memory Reflector amber icon: " + (amberIcon != null ? amberIcon.name : "not found"));
        }
        catch (Exception ex)
        {
            Main.logger_instance?.Warning("Memory Reflector amber icon search failed: " + ex.Message);
        }
    }

    /// <summary>Memory Amber is the Weaver's favor.</summary>
    private static Faction Weaver()
    {
        try
        {
            if (Refs_Manager.player_actor.IsNullOrDestroyed())
            {
                return null;
            }
            FactionTracker tracker = Refs_Manager.player_actor.gameObject.GetComponent<FactionTracker>();
            if (!tracker.IsNullOrDestroyed() && tracker.factions != null
                && tracker.factions.TryGetValue(FactionID.TheWeaver, out Faction faction) && !faction.IsNullOrDestroyed())
            {
                return faction;
            }
        }
        catch (Exception)
        {
        }
        return null;
    }

    private static long HaveAmber()
    {
        Faction f = Weaver();
        return f != null && f.IsMember ? f.Favor : 0;
    }

    private const float ChipIcon = 24;

    private static float ChipWidth(string text, float size) => ChipIcon + 6 + text.Length * size * 0.58f;

    private static float Chip(Sprite icon, int kind, string text, float x, float y, float size, Color color)
    {
        float w = ChipWidth(text, size);
        float h = Mathf.Max(ChipIcon, size + 6);
        if (icon != null)
        {
            Image img = R.MakeImage(Content, "CurrencyIcon", Color.white);
            img.sprite = icon;
            img.preserveAspect = true;
            img.raycastTarget = false;
            RectTransform r = img.rectTransform;
            r.anchorMin = r.anchorMax = new Vector2(0, 1);
            r.pivot = new Vector2(0, 1);
            r.anchoredPosition = new Vector2(x, -(y + (h - ChipIcon) / 2));
            r.sizeDelta = new Vector2(ChipIcon, ChipIcon);
            dynamic.Add(img.gameObject);
        }
        else
        {
            // no sprite found: a small hand-drawn gem (amber) or a word
            Image glow = R.MakeImage(Content, "Glyph", kind == 1 ? new Color(0.95f, 0.62f, 0.15f, 0.95f) : new Color(0.8f, 0.8f, 0.8f, 0.9f));
            glow.raycastTarget = false;
            RectTransform gr = glow.rectTransform;
            gr.anchorMin = gr.anchorMax = new Vector2(0, 1);
            gr.pivot = new Vector2(0.5f, 0.5f);
            gr.anchoredPosition = new Vector2(x + ChipIcon / 2, -(y + h / 2));
            gr.sizeDelta = new Vector2(15, 15);
            gr.localRotation = Quaternion.Euler(0, 0, 45);
            dynamic.Add(glow.gameObject);
        }
        Label(text, x + ChipIcon + 6, y, w, h, size, color, TextAlignmentOptions.MidlineLeft);
        return w;
    }

    private static void ChipRow(float centre, float y, float size, params (Sprite Icon, int Kind, string Text, Color Color)[] items)
    {
        const float gap = 30;
        float total = -gap;
        foreach (var it in items)
        {
            total += ChipWidth(it.Text, size) + gap;
        }
        float x = centre - total / 2;
        foreach (var it in items)
        {
            x += Chip(it.Icon, it.Kind, it.Text, x, y, size, it.Color) + gap;
        }
    }

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
        BuildTube();
        R.EnsureCurrencyIcons();
        FindAmberIcon();

        // while item B is still being cracked it is drawn as it was
        ItemData shownB = itemB ?? (animating && !shattered ? animB : null);
        SlotBox(itemA, 0, CenterX - IconW / 2, AY, () => ClickSlot(0));
        SlotBox(shownB, 1, CenterX - IconW / 2, BY, () => ClickSlot(1));
        Reflection(itemA, 0, true, AY + IconH);
        Reflection(shownB, 1, false, BY);

        offer = null;
        string why = Items_MemoryReflector.WhyNot(itemA, itemB);
        if (why.Length == 0)
        {
            offer = Items_MemoryReflector.Describe(itemA, itemB);
        }

        // item B's affixes: prefixes on its left, suffixes on its right. A legendary item moves a random one instead.
        if (offer != null && !offer.Legendary && !animating)
        {
            var left = offer.Candidates.Where(c => c.Placement != 2).ToList();
            var right = offer.Candidates.Where(c => c.Placement == 2).ToList();
            Column(left, Left, BY);
            Column(right, Left + WinW - ColumnWidth, BY);
        }

        float y = BY + IconH + 30;
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
        if (list.Count == 0)
        {
            return;
        }
        const float gap = 4, space = IconH, chars = 25;
        float units = 0;
        var heights = new List<int>();
        foreach (var c in list)
        {
            string text = Wording(c) + (c.CanGo ? "" : "\n(" + c.Why + ")");
            int lines = 0;
            foreach (string l in text.Split('\n'))
            {
                lines += Mathf.Max(1, Mathf.CeilToInt(l.Length / chars));
            }
            heights.Add(Mathf.Max(1, lines));
            units += Mathf.Max(1, lines);
        }
        float perLine = Mathf.Min(30f, (space - (list.Count - 1) * gap - list.Count * 10f) / units);
        float fontSize = Mathf.Clamp(perLine - 5f, 9f, 14f);
        float cursor = y;
        for (int i = 0; i < list.Count; i++)
        {
            float h = heights[i] * perLine + 10f;
            AffixPlate(list[i], x, cursor, ColumnWidth, h, fontSize);
            cursor += h + gap;
        }
    }

    // ---------------------------------------------------------------- performing the reflection

    private static void DoReflect(long gold, int amber, int bones)
    {
        try
        {
            if (animating || offer == null)
            {
                return;
            }
            Items_MemoryReflector.Candidate chosen = Chosen();
            if (!offer.Legendary && chosen == null)
            {
                R.Notify("Pick an affix to reflect.");
                return;
            }
            if (R.HaveGold() < gold)
            {
                R.Notify("Not enough gold.");
                return;
            }
            Faction weaver = Weaver();
            if (weaver == null || HaveAmber() < amber)
            {
                R.Notify("Not enough Memory Amber.");
                return;
            }
            if (R.HaveBones() < bones)
            {
                R.Notify("Not enough Ancient Bones.");
                return;
            }
            // pay first: item B is spent whatever happens
            if (!weaver.TrySpendFavor(amber, false))
            {
                R.Notify("Not enough Memory Amber.");
                return;
            }
            weaver.SaveAndSync(false);
            Refs_Manager.player_gold_tracker?.modifyGold((int)-Math.Min(gold, int.MaxValue));
            R.BonesTracker()?.modifyAncientBones(-bones);

            bool success = Items_MemoryReflector.Reflect(offer, chosen, out _);
            StartAnimation(success);
        }
        catch (Exception ex)
        {
            Main.logger_instance?.Warning("Memory Reflector failed: " + ex.Message);
            R.Notify("Something went wrong.");
        }
    }

    // ---------------------------------------------------------------- animation

    private static bool animating;
    private static bool shattered;
    private static bool animSuccess;
    private static float animStart;
    private static ItemData animB;
    private static Icon animIconB;
    private static bool finalDone;
    private static bool holeBuilt;
    private static readonly List<GameObject> effects = new(); // everything the animation has made, to keep above the window

    private const float CrackTime = 0.8f;
    private const float BurstEnd = 1.55f;
    private const float PullStart = 1.55f;

    private sealed class Shard
    {
        public GameObject Go;
        public RectTransform Rect;
        public CanvasGroup Group;
        public Vector2 Pos, Vel;
        public float Spin, Rot, Delay, Scale = 1f;
        public float Angle, Radius; // for the black hole
        public bool Captured;
        public Vector2 PullFrom;
        public bool Pulling;
    }

    private sealed class Spark
    {
        public GameObject Go;
        public RectTransform Rect;
        public CanvasGroup Group;
        public Vector2 Pos, Vel;
        public float Gravity, Born, Life, Spin;
    }

    private sealed class Crack
    {
        public Image Glow, Dark;
        public float At;
    }

    private static readonly List<Shard> shards = new();
    private static readonly List<Spark> sparks = new();
    private static readonly List<Crack> cracks = new();
    private static readonly List<Image> holeLayers = new();
    private static readonly List<float> holeSpeed = new();
    private static readonly List<Image> holeDots = new();
    private static readonly List<float> holeDotAngle = new();
    private static readonly List<float> holeDotRadius = new();
    private static GameObject holeRoot;
    private static float holeScale;
    private static Image darkness;

    private static Vector2 BCenter => new(CenterX, -(BY + IconH / 2));
    private static Vector2 ACenter => new(CenterX, -(AY + IconH / 2));

    private static GameObject Effect(string name)
    {
        var go = new GameObject(name);
        go.AddComponent<RectTransform>();
        go.transform.SetParent(Content, false);
        effects.Add(go);
        return go;
    }

    private static void StartAnimation(bool success)
    {
        animating = true;
        shattered = false;
        finalDone = false;
        holeBuilt = false;
        animSuccess = success;
        animStart = Time.unscaledTime;
        animB = itemB;
        animIconB = icons[1]; // the shards are cut from item B's own icon
        itemB = null; // item B is spent
        selectedId = -1;
        offer = null;
        // the cracks start on item B at once
        Vector2 centre = BCenter;
        float halfW = IconW / 2f - 4, halfH = IconH / 2f - 4;
        var rng = new System.Random();
        for (int k = 0; k < 11; k++)
        {
            float angle = k * Mathf.PI * 2 / 11 + (float)(rng.NextDouble() - 0.5) * 0.4f;
            Vector2 pos = centre;
            for (int step = 0; step < 6; step++)
            {
                float len = 14f + (float)rng.NextDouble() * 18f;
                angle += ((float)rng.NextDouble() - 0.5f) * 0.6f;
                Vector2 next = pos + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * len;
                next.x = Mathf.Clamp(next.x, centre.x - halfW, centre.x + halfW);
                next.y = Mathf.Clamp(next.y, centre.y - halfH, centre.y + halfH);
                AddCrack(pos, next, animStart + k * 0.02f + step * 0.09f);
                pos = next;
            }
        }
        dirty = true;
    }

    private static void AddCrack(Vector2 from, Vector2 to, float at)
    {
        Vector2 mid = (from + to) / 2;
        Vector2 d = to - from;
        float angle = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
        GameObject holder = Effect("Crack");
        RectTransform hrect = holder.GetComponent<RectTransform>();
        hrect.anchorMin = hrect.anchorMax = new Vector2(0, 1);
        hrect.pivot = new Vector2(0, 1);
        hrect.anchoredPosition = Vector2.zero;
        hrect.sizeDelta = Vector2.zero;
        Image glow = R.MakeImage(holder.transform, "Glow", new Color(0.6f, 0.85f, 1f, 0f));
        Image dark = R.MakeImage(holder.transform, "Dark", new Color(0.02f, 0.02f, 0.03f, 0f));
        foreach ((Image img, float thick) in new[] { (glow, 7f), (dark, 2.4f) })
        {
            img.raycastTarget = false;
            RectTransform r = img.rectTransform;
            r.anchorMin = r.anchorMax = new Vector2(0, 1);
            r.pivot = new Vector2(0.5f, 0.5f);
            r.anchoredPosition = mid;
            r.sizeDelta = new Vector2(d.magnitude + 2, thick);
            r.localRotation = Quaternion.Euler(0, 0, angle);
        }
        cracks.Add(new Crack { Glow = glow, Dark = dark, At = at });
    }

    private static Spark NewSpark(Vector2 at, Color colour, float size, Vector2 vel, float gravity, float life, float delay = 0)
    {
        GameObject go = Effect("Spark");
        Image img = R.MakeImage(go.transform, "S", colour);
        img.raycastTarget = false;
        RectTransform r = go.GetComponent<RectTransform>();
        r.anchorMin = r.anchorMax = new Vector2(0, 1);
        r.pivot = new Vector2(0.5f, 0.5f);
        r.sizeDelta = new Vector2(size, size * UnityEngine.Random.Range(1f, 2.2f));
        r.anchoredPosition = at;
        r.localRotation = Quaternion.Euler(0, 0, UnityEngine.Random.Range(0f, 90f));
        Stretch(img.rectTransform, 0);
        CanvasGroup g = go.AddComponent<CanvasGroup>();
        g.blocksRaycasts = false;
        g.alpha = 0;
        var s = new Spark { Go = go, Rect = r, Group = g, Pos = at, Vel = vel, Gravity = gravity, Born = Time.unscaledTime + delay, Life = life, Spin = UnityEngine.Random.Range(-540f, 540f) };
        sparks.Add(s);
        return s;
    }

    private static void SparkBurst(Vector2 at, int count, float minSpeed, float maxSpeed, float gravity, Color a, Color b, float minLife, float maxLife)
    {
        for (int i = 0; i < count; i++)
        {
            float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2);
            float speed = UnityEngine.Random.Range(minSpeed, maxSpeed);
            NewSpark(at, Color.Lerp(a, b, UnityEngine.Random.value), UnityEngine.Random.Range(3f, 7f),
                new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed, gravity, UnityEngine.Random.Range(minLife, maxLife));
        }
    }

    /// <summary>Item B breaks into shards: each is a cut-out of its own icon, so it really looks shattered.</summary>
    private static void Shatter()
    {
        shattered = true;
        const int cols = 6, rows = 8;
        float tw = IconW / cols, th = IconH / rows;
        Vector2 centre = BCenter;
        Icon icon = animIconB;
        Color rarity = animB != null ? ItemColour(animB) : Color.white;
        var rng = new System.Random();
        for (int cy = 0; cy < rows; cy++)
        {
            for (int cx = 0; cx < cols; cx++)
            {
                // the tile's centre, measured from the middle of the box (y up)
                Vector2 offset = new(cx * tw + tw / 2 - IconW / 2, IconH / 2 - (cy * th + th / 2));
                GameObject go = Effect("Shard");
                RectTransform r = go.GetComponent<RectTransform>();
                r.anchorMin = r.anchorMax = new Vector2(0, 1);
                r.pivot = new Vector2(0.5f, 0.5f);
                r.sizeDelta = new Vector2(tw + 1, th + 1);
                Image clip = go.AddComponent<Image>();
                clip.color = Color.white;
                Mask mask = go.AddComponent<Mask>();
                mask.showMaskGraphic = false;
                CanvasGroup group = go.AddComponent<CanvasGroup>();
                group.blocksRaycasts = false;
                // the art, laid so this tile shows its own part of the icon
                var art = new GameObject("Art");
                RectTransform ar = art.AddComponent<RectTransform>();
                art.transform.SetParent(go.transform, false);
                ar.anchorMin = ar.anchorMax = new Vector2(0.5f, 0.5f);
                ar.pivot = new Vector2(0.5f, 0.5f);
                ar.sizeDelta = new Vector2(IconW, IconH);
                ar.anchoredPosition = -offset;
                if (icon != null && icon.Back != null)
                {
                    Image back = R.MakeImage(art.transform, "Back", icon.BackColor);
                    back.sprite = icon.Back;
                    back.raycastTarget = false;
                    Stretch(back.rectTransform, 4);
                }
                else
                {
                    Image tint = R.MakeImage(art.transform, "Tint", Color.Lerp(new Color(0.05f, 0.04f, 0.035f), rarity, 0.25f));
                    tint.raycastTarget = false;
                    Stretch(tint.rectTransform, 4);
                }
                if (icon != null && icon.Content != null)
                {
                    Image content = R.MakeImage(art.transform, "Icon", Color.white);
                    content.sprite = icon.Content;
                    content.preserveAspect = true;
                    content.raycastTarget = false;
                    Stretch(content.rectTransform, 14);
                }
                Vector2 away = offset.normalized;
                float speed = 120f + (float)rng.NextDouble() * 330f;
                var shard = new Shard
                {
                    Go = go,
                    Rect = r,
                    Group = group,
                    Pos = centre + offset,
                    Vel = away * speed + new Vector2(((float)rng.NextDouble() - 0.5f) * 120f, ((float)rng.NextDouble() - 0.4f) * 140f),
                    Spin = ((float)rng.NextDouble() - 0.5f) * 720f,
                    Delay = (float)rng.NextDouble() * 0.7f,
                };
                r.anchoredPosition = shard.Pos;
                shards.Add(shard);
            }
        }
        SparkBurst(centre, 70, 120f, 560f, 380f, new Color(0.7f, 0.9f, 1f), new Color(1f, 1f, 1f), 0.5f, 1.2f);
        crushShake = Time.unscaledTime;
        dirty = true; // item B's slot now draws empty
    }

    private static float crushShake = -10f;

    private static void BuildBlackHole()
    {
        holeBuilt = true;
        holeRoot = Effect("BlackHole");
        RectTransform hr = holeRoot.GetComponent<RectTransform>();
        hr.anchorMin = hr.anchorMax = new Vector2(0, 1);
        hr.pivot = new Vector2(0.5f, 0.5f);
        hr.anchoredPosition = BCenter;
        hr.sizeDelta = new Vector2(10, 10);
        holeLayers.Clear();
        holeSpeed.Clear();
        holeDots.Clear();
        holeDotAngle.Clear();
        holeDotRadius.Clear();
        for (int i = 0; i < 12; i++)
        {
            float size = 150 - i * 11;
            float t = i / 11f;
            Color c = Color.Lerp(new Color(0.3f, 0.06f, 0.5f, 0.35f), new Color(0f, 0f, 0f, 0.97f), t);
            Image layer = R.MakeImage(holeRoot.transform, "Layer", c);
            layer.raycastTarget = false;
            RectTransform r = layer.rectTransform;
            r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
            r.pivot = new Vector2(0.5f, 0.5f);
            r.sizeDelta = new Vector2(size, size);
            r.localRotation = Quaternion.Euler(0, 0, i * 8f);
            holeLayers.Add(layer);
            holeSpeed.Add((i % 2 == 0 ? 1 : -1) * (20f + i * 9f));
        }
        for (int i = 0; i < 40; i++)
        {
            Image dot = R.MakeImage(holeRoot.transform, "Dot", Color.Lerp(new Color(1f, 0.55f, 0.15f), new Color(1f, 0.95f, 0.8f), UnityEngine.Random.value));
            dot.raycastTarget = false;
            RectTransform r = dot.rectTransform;
            r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
            r.pivot = new Vector2(0.5f, 0.5f);
            float s = UnityEngine.Random.Range(2f, 5f);
            r.sizeDelta = new Vector2(s, s);
            holeDots.Add(dot);
            holeDotAngle.Add(UnityEngine.Random.Range(0f, 360f));
            holeDotRadius.Add(UnityEngine.Random.Range(48f, 82f));
        }
        darkness = R.MakeImage(Content, "Darkness", new Color(0f, 0f, 0.02f, 0f));
        darkness.raycastTarget = false;
        effects.Add(darkness.gameObject);
        Stretch(darkness.rectTransform, 0);
        holeScale = 0;
    }

    private static Vector2 Polyline(Vector2[] pts, float u)
    {
        float total = 0;
        for (int i = 0; i + 1 < pts.Length; i++)
        {
            total += Vector2.Distance(pts[i], pts[i + 1]);
        }
        float d = Mathf.Clamp01(u) * total;
        for (int i = 0; i + 1 < pts.Length; i++)
        {
            float len = Vector2.Distance(pts[i], pts[i + 1]);
            if (d <= len || i + 2 == pts.Length)
            {
                return Vector2.Lerp(pts[i], pts[i + 1], len <= 0 ? 0 : Mathf.Clamp01(d / len));
            }
            d -= len;
        }
        return pts[pts.Length - 1];
    }

    private static void TickAnimation()
    {
        float now = Time.unscaledTime;
        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
        TickCracks(now);
        TickSparks(now, dt);
        TickTubeGlow(now);
        float t = now - animStart;
        if (animating)
        {
            // item B shakes while it cracks, then breaks
            if (slotRects[1] != null && !shattered)
            {
                float amp = Mathf.Clamp01(t / CrackTime) * 5f;
                slotRects[1].anchoredPosition = new Vector2(CenterX - IconW / 2, -BY) + new Vector2(UnityEngine.Random.Range(-1f, 1f), UnityEngine.Random.Range(-1f, 1f)) * amp;
            }
            if (!shattered && t >= CrackTime)
            {
                Shatter();
            }
            if (shattered)
            {
                if (animSuccess)
                {
                    TickShardsSuccess(t, now, dt);
                }
                else
                {
                    TickShardsFailure(t, now, dt);
                }
            }
        }
        // the window trembles
        if (contentRect != null)
        {
            float amp = 0f;
            float sinceShatter = now - crushShake;
            if (sinceShatter >= 0 && sinceShatter < 0.6f)
            {
                amp = 9f * (1f - sinceShatter / 0.6f);
            }
            float sinceFinal = now - finalAt;
            if (sinceFinal >= 0 && sinceFinal < 0.8f)
            {
                amp = Mathf.Max(amp, 14f * (1f - sinceFinal / 0.8f));
            }
            if (animating && !shattered)
            {
                amp = Mathf.Max(amp, Mathf.Clamp01(t / CrackTime) * 2.5f);
            }
            contentRect.anchoredPosition = amp > 0.05f
                ? contentBase + new Vector2(UnityEngine.Random.Range(-1f, 1f), UnityEngine.Random.Range(-1f, 1f)) * amp
                : contentBase;
        }
    }

    private static float finalAt = -10f;

    private static void TickShardsSuccess(float t, float now, float dt)
    {
        // out through the tunnel and up into item A
        Vector2[] path =
        {
            BCenter,
            new Vector2(CenterX, -(BY - 10)),
            new Vector2(CenterX, -(AY + IconH + 12)),
            ACenter,
        };
        int alive = 0;
        foreach (Shard s in shards)
        {
            if (s.Go == null)
            {
                continue;
            }
            alive++;
            if (t < BurstEnd)
            {
                s.Vel *= Mathf.Max(0f, 1f - 2.4f * dt);
                s.Vel.y -= 90f * dt;
                s.Pos += s.Vel * dt;
                s.Rot += s.Spin * dt;
                s.PullFrom = s.Pos;
                s.Group.alpha = 1f;
            }
            else
            {
                float u = (t - BurstEnd - s.Delay * 1.3f) / 1.45f;
                if (u < 0)
                {
                    s.Rot += s.Spin * dt * 0.3f;
                    s.Pos += new Vector2(0, Mathf.Sin(now * 5f + s.Delay * 9f) * 14f * dt);
                    s.PullFrom = s.Pos;
                }
                else
                {
                    float e = u * u * (3f - 2f * u);
                    Vector2 onPath = Polyline(path, e);
                    float swirl = Mathf.Sin(u * Mathf.PI * 3f + s.Delay * 6f) * 26f * (1f - e);
                    s.Pos = Vector2.Lerp(s.PullFrom, onPath, Mathf.Clamp01(u * 3f)) + new Vector2(swirl, 0);
                    s.Rot += s.Spin * dt * 0.4f;
                    s.Scale = Mathf.Lerp(1f, 0.35f, e);
                    s.Group.alpha = u > 0.88f ? Mathf.Clamp01((1f - u) / 0.12f) : 1f;
                    if (u >= 1f)
                    {
                        Object.Destroy(s.Go);
                        s.Go = null;
                        continue;
                    }
                }
            }
            s.Rect.anchoredPosition = s.Pos;
            s.Rect.localRotation = Quaternion.Euler(0, 0, s.Rot);
            s.Rect.localScale = new Vector3(s.Scale, s.Scale, 1);
        }
        if (alive == 0 && !finalDone)
        {
            FinishSuccess(now);
        }
        if (finalDone && now - finalAt > 1.0f)
        {
            EndAnimationNow();
        }
    }

    private static void TickShardsFailure(float t, float now, float dt)
    {
        if (!holeBuilt && t >= BurstEnd - 0.5f)
        {
            BuildBlackHole();
        }
        float holeT = t - (BurstEnd - 0.5f);
        if (holeBuilt && holeRoot != null)
        {
            // grows, holds, then collapses
            float grow = Mathf.Clamp01(holeT / 0.5f);
            float collapse = Mathf.Clamp01((t - (BurstEnd + 1.5f)) / 0.45f);
            holeScale = grow * (1f - collapse * collapse);
            holeRoot.GetComponent<RectTransform>().localScale = new Vector3(holeScale, holeScale, 1);
            for (int i = 0; i < holeLayers.Count; i++)
            {
                holeLayers[i].rectTransform.Rotate(0, 0, holeSpeed[i] * dt);
            }
            for (int i = 0; i < holeDots.Count; i++)
            {
                holeDotAngle[i] += (260f + 7000f / Mathf.Max(20f, holeDotRadius[i])) * dt;
                float rad = holeDotAngle[i] * Mathf.Deg2Rad;
                holeDots[i].rectTransform.anchoredPosition = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * holeDotRadius[i];
            }
            if (darkness != null)
            {
                Color c = darkness.color;
                c.a = 0.35f * holeScale;
                darkness.color = c;
            }
        }
        int alive = 0;
        foreach (Shard s in shards)
        {
            if (s.Go == null)
            {
                continue;
            }
            alive++;
            if (t < BurstEnd)
            {
                s.Vel *= Mathf.Max(0f, 1f - 2.4f * dt);
                s.Vel.y -= 90f * dt;
                s.Pos += s.Vel * dt;
                s.Rot += s.Spin * dt;
                s.Group.alpha = 1f;
                Vector2 rel = s.Pos - BCenter;
                s.Angle = Mathf.Atan2(rel.y, rel.x);
                s.Radius = rel.magnitude;
            }
            else
            {
                float u = (t - BurstEnd - s.Delay * 0.8f) / 1.1f;
                if (u > 0)
                {
                    // spiral in: the angle runs ahead faster as the radius falls
                    s.Radius = Mathf.Max(0f, s.Radius - (60f + s.Radius * 2.1f) * dt);
                    s.Angle += (1.6f + 140f / Mathf.Max(18f, s.Radius)) * dt;
                    s.Pos = BCenter + new Vector2(Mathf.Cos(s.Angle), Mathf.Sin(s.Angle)) * s.Radius;
                    s.Rot += s.Spin * dt * 1.5f;
                    s.Scale = Mathf.Clamp01(s.Radius / 70f);
                    s.Group.alpha = Mathf.Clamp01(s.Radius / 20f);
                    if (s.Radius < 4f)
                    {
                        Object.Destroy(s.Go);
                        s.Go = null;
                        continue;
                    }
                }
            }
            s.Rect.anchoredPosition = s.Pos;
            s.Rect.localRotation = Quaternion.Euler(0, 0, s.Rot);
            s.Rect.localScale = new Vector3(s.Scale, s.Scale, 1);
        }
        if (t >= BurstEnd + 1.95f)
        {
            if (!finalDone)
            {
                finalDone = true;
                finalAt = now;
                SparkBurst(BCenter, 28, 60f, 200f, 120f, new Color(0.35f, 0.1f, 0.55f), new Color(0.9f, 0.8f, 1f), 0.4f, 0.9f);
            }
            if (now - finalAt > 0.7f)
            {
                EndAnimationNow();
            }
        }
    }

    /// <summary>The shards have all reached item A: its final flash.</summary>
    private static void FinishSuccess(float now)
    {
        finalDone = true;
        finalAt = now;
        Vector2 centre = ACenter;
        GameObject wash = Effect("FinalWash");
        Image washImg = R.MakeImage(wash.transform, "Wash", new Color(0.85f, 0.95f, 1f, 0.38f));
        washImg.raycastTarget = false;
        Stretch(washImg.rectTransform, 0);
        RectTransform wr = wash.GetComponent<RectTransform>();
        wr.anchorMin = Vector2.zero;
        wr.anchorMax = Vector2.one;
        wr.offsetMin = wr.offsetMax = Vector2.zero;
        finalFlash.Add((washImg, now, 0.38f, 0.4f));
        for (int k = 0; k < 14; k++)
        {
            GameObject rayGo = Effect("Ray");
            Image ray = R.MakeImage(rayGo.transform, "R", k % 3 == 1 ? new Color(1f, 0.9f, 0.55f, 0.7f) : new Color(0.7f, 0.9f, 1f, 0.7f));
            ray.raycastTarget = false;
            Stretch(ray.rectTransform, 0);
            RectTransform rr = rayGo.GetComponent<RectTransform>();
            rr.anchorMin = rr.anchorMax = new Vector2(0, 1);
            rr.pivot = new Vector2(0.5f, 0f);
            rr.sizeDelta = new Vector2(UnityEngine.Random.Range(3f, 9f), UnityEngine.Random.Range(220f, 400f));
            rr.anchoredPosition = centre;
            rr.localRotation = Quaternion.Euler(0, 0, k * (360f / 14f));
            finalFlash.Add((ray, now, 0.7f, 0.9f));
        }
        GameObject glow = Effect("Glow");
        Image glowImg = R.MakeImage(glow.transform, "G", new Color(0.7f, 0.9f, 1f, 0.8f));
        glowImg.raycastTarget = false;
        Stretch(glowImg.rectTransform, 0);
        RectTransform gr = glow.GetComponent<RectTransform>();
        gr.anchorMin = gr.anchorMax = new Vector2(0, 1);
        gr.pivot = new Vector2(0.5f, 0.5f);
        gr.sizeDelta = new Vector2(IconW + 90, IconH + 90);
        gr.anchoredPosition = centre;
        finalFlash.Add((glowImg, now, 0.8f, 0.8f));
        for (int k = 0; k < 36; k++)
        {
            float a = k * Mathf.PI * 2 / 36;
            NewSpark(centre, new Color(0.85f, 0.95f, 1f), 6f, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 600f, 0f, 0.6f);
        }
        SparkBurst(centre, 80, 140f, 700f, 480f, new Color(0.6f, 0.85f, 1f), new Color(1f, 0.95f, 0.7f), 0.7f, 1.4f);
        dirty = true;
    }

    private static readonly List<(Image Img, float Start, float Peak, float Duration)> finalFlash = new();

    private static void TickSparks(float now, float dt)
    {
        for (int i = sparks.Count - 1; i >= 0; i--)
        {
            Spark s = sparks[i];
            if (s.Go == null)
            {
                sparks.RemoveAt(i);
                continue;
            }
            if (now < s.Born)
            {
                continue;
            }
            float age = now - s.Born;
            float p = age / s.Life;
            if (p >= 1f)
            {
                Object.Destroy(s.Go);
                sparks.RemoveAt(i);
                continue;
            }
            s.Pos += s.Vel * dt;
            s.Vel.y -= s.Gravity * dt;
            s.Rect.anchoredPosition = s.Pos;
            s.Rect.Rotate(0, 0, s.Spin * dt);
            s.Group.alpha = Mathf.Clamp01(1f - p) * UnityEngine.Random.Range(0.7f, 1f);
        }
        for (int i = finalFlash.Count - 1; i >= 0; i--)
        {
            var f = finalFlash[i];
            if (f.Img == null)
            {
                finalFlash.RemoveAt(i);
                continue;
            }
            float p = (now - f.Start) / f.Duration;
            if (p >= 1f)
            {
                Color gone = f.Img.color;
                gone.a = 0f;
                f.Img.color = gone;
                finalFlash.RemoveAt(i);
                continue;
            }
            Color c = f.Img.color;
            c.a = f.Peak * (1f - p) * (1f - p);
            f.Img.color = c;
            if (f.Img.gameObject.name == "G")
            {
                f.Img.transform.parent.localScale = Vector3.one * (1f + 1.2f * p);
            }
        }
    }

    private static void TickCracks(float now)
    {
        if (cracks.Count == 0)
        {
            return;
        }
        float age = now - animStart;
        float fade = age < CrackTime + 0.4f ? 1f : Mathf.Clamp01(1f - (age - CrackTime - 0.4f) / 0.3f);
        foreach (Crack c in cracks)
        {
            if (c.Dark == null)
            {
                continue;
            }
            float on = now >= c.At ? 1f : 0f;
            Color d = c.Dark.color;
            d.a = 0.95f * on * fade;
            c.Dark.color = d;
            Color g = c.Glow.color;
            g.a = 0.6f * on * fade * (0.7f + 0.3f * Mathf.Sin(now * 14f));
            c.Glow.color = g;
        }
        if (age > CrackTime + 0.8f)
        {
            foreach (Crack c in cracks)
            {
                if (c.Glow != null) Object.Destroy(c.Glow.transform.parent.gameObject);
            }
            cracks.Clear();
        }
    }

    /// <summary>Light climbs the tunnel's rungs from item B toward item A while the shards fly.</summary>
    private static void TickTubeGlow(float now)
    {
        bool active = animating && animSuccess && shattered;
        float t = now - animStart - BurstEnd;
        float front = Mathf.Clamp01(t / 1.6f) * 1.25f;
        for (int k = 0; k < tubeGlow.Count; k++)
        {
            Image img = tubeGlow[k];
            if (img == null)
            {
                continue;
            }
            Color baseColour = tubeBase[k];
            if (!active || t < 0)
            {
                img.color = Color.Lerp(img.color, baseColour, 0.2f);
                continue;
            }
            float behind = front - tubeAlong[k];
            float glow = behind <= 0 ? 0f : Mathf.Exp(-behind * 2.6f);
            Color hot = Color.Lerp(new Color(0.5f, 0.75f, 1f, 0.9f), new Color(1f, 1f, 1f, 1f), glow * glow);
            img.color = Color.Lerp(baseColour, hot, Mathf.Clamp01(glow));
        }
    }

    private static void EndAnimationNow()
    {
        animating = false;
        shattered = false;
        finalDone = false;
        holeBuilt = false;
        foreach (Shard s in shards)
        {
            if (s.Go != null) Object.Destroy(s.Go);
        }
        shards.Clear();
        foreach (Spark s in sparks)
        {
            if (s.Go != null) Object.Destroy(s.Go);
        }
        sparks.Clear();
        foreach (Crack c in cracks)
        {
            if (c.Glow != null) Object.Destroy(c.Glow.transform.parent.gameObject);
        }
        cracks.Clear();
        finalFlash.Clear();
        foreach (GameObject go in effects)
        {
            if (go != null) Object.Destroy(go);
        }
        effects.Clear();
        holeRoot = null;
        darkness = null;
        holeLayers.Clear();
        holeDots.Clear();
        animIconB?.Dispose();
        if (icons[1] == animIconB)
        {
            icons[1] = null;
        }
        animIconB = null;
        animB = null;
        if (contentRect != null)
        {
            contentRect.anchoredPosition = contentBase;
        }
        dirty = true;
    }
}
