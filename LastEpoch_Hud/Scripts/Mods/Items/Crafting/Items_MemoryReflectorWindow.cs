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
/// The donor mirror shatters into the Woven Echo. The shards either transfer a memory to the left item or dissolve.
/// </summary>
internal static class Items_MemoryReflectorWindow
{
    private const string ForgePath = "GUI/Panel System/Panel Stacks/Left Panel Stack/ForgePanel (Clone)";
    private const string ButtonName = "MemoryReflectorButton";
    private const string RootName = "MemoryReflectorWindow";

    private const float Left = 48, WinW = 670, IconW = 119f, IconH = 174f;
    private const float AY = 142, BY = AY, SlotOffset = 149f;
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
            // directly under the Guide button, which hangs from the top right corner
            rect.anchorMin = help.anchorMin;
            rect.anchorMax = help.anchorMax;
            rect.pivot = help.pivot;
            rect.anchoredPosition = new Vector2(help.anchoredPosition.x, help.anchoredPosition.y - help.sizeDelta.y - 6);
        }
        else
        {
            rect.anchorMin = source.anchorMin;
            rect.anchorMax = source.anchorMax;
            rect.pivot = source.pivot;
            rect.anchoredPosition = new Vector2(source.anchoredPosition.x, source.anchoredPosition.y - 2 * (source.sizeDelta.y + 6));
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
            Items_AdvancedForgeWindow.Close();
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
        R.StripGameLogic(go);
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
                    Stretch(content.rectTransform, 6);
                }
            }
            slotRects[id] = br;
            slotItems[id] = item;
        }
        DressWovenSlot(box, id, item);
        Button button = box.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.onClick.AddListener(click);
    }

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
        Image image = R.MakeImage(parent, name, Color.clear);
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
                Stretch(art.GetComponent<RectTransform>(), -16f);
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

    // ---------------------------------------------------------------- affix plates (item B's affixes)

    private static readonly List<AffixCardView> rows = new();

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

    private static void AffixPlate(Items_MemoryReflector.Candidate c, float x, float y)
    {
        string text = Wording(c);
        if (!c.CanGo && c.Why.Length > 0)
        {
            text += "\n<size=75%>(" + c.Why + ")</size>";
        }
        ItemData owner = itemB ?? animB;
        var data = new AffixCardData
        {
            Text = text,
            Tier = c.Tier,
            Group = c.Placement == 1 ? AffixCard.Prefix : c.Placement == 2 ? AffixCard.Suffix : -1,
            Encapsulated = owner != null && c.Source != null && Items_Encapsulate.IsEncapsulated(owner, c.Source),
            Reminded = c.Reminded,
            Static = !c.CanGo,
            On = c.CanGo && c.AffixId == selectedId,
        };
        AffixCardView view = AffixCard.Build(Content, labelTemplate, bodyFont, data, x, y, dynamic);
        if (c.CanGo)
        {
            Button button = view.Bg.gameObject.AddComponent<Button>();
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
        rows.Add(view);
    }

    private static UnityEngine.Camera PointerCamera()
    {
        Canvas canvas = root.GetComponentInParent<Canvas>();
        return canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
    }

    private static void TickRows() => AffixCard.Tick(rows, animating, PointerCamera());

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
        public float Angle, Radius; // orbit around the Woven Echo
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

    private static readonly List<Shard> shards = new();
    private static readonly List<Spark> sparks = new();

    private static Vector2 BCenter => new(CenterX + SlotOffset, -(BY + IconH / 2));
    private static Vector2 ACenter => new(CenterX - SlotOffset, -(AY + IconH / 2));

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
        animSuccess = success;
        animStart = Time.unscaledTime;
        animB = itemB;
        animIconB = icons[1]; // the shards are cut from item B's own icon
        itemB = null; // item B is spent
        selectedId = -1;
        offer = null;
        dirty = true;
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
    private static Sprite mirrorSplinter;

    private static Sprite MirrorSplinter()
    {
        if (mirrorSplinter != null) return mirrorSplinter;
        const int size = 48;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            float v = y / (size - 1f);
            for (int x = 0; x < size; x++)
            {
                float u = x / (size - 1f);
                bool inside = u > Mathf.Abs(v - 0.37f) * 0.56f && u < 0.96f - v * 0.79f;
                pixels[y * size + x] = new Color32(255, 255, 255, inside ? (byte)255 : (byte)0);
            }
        }
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        texture.hideFlags = HideFlags.HideAndDontSave;
        mirrorSplinter = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        mirrorSplinter.hideFlags = HideFlags.HideAndDontSave;
        return mirrorSplinter;
    }

    private static void Shatter()
    {
        shattered = true;
        const int cols = 4, rows = 6;
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
                clip.sprite = MirrorSplinter();
                clip.raycastTarget = false;
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
                    Stretch(content.rectTransform, 6);
                }
                Image glint = WovenImage(art.transform, "ShardRedGlint", OrbArt.Named("sparkle_star"),
                    new Vector2(IconW * 0.5f, -IconH * 0.5f) + offset, new Vector2(tw, 8f));
                glint.color = new Color(1f, 0.22f, 0.28f, 0.78f);
                glint.rectTransform.localRotation = Quaternion.Euler(0f, 0f, (cx + cy) % 2 == 0 ? 34f : -38f);
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
        SparkBurst(centre, 38, 120f, 560f, 380f, new Color(0.7f, 0.9f, 1f), new Color(1f, 1f, 1f), 0.5f, 1.2f);
        crushShake = Time.unscaledTime;
        dirty = true; // item B's slot now draws empty
    }

    private static float crushShake = -10f;

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
        TickSparks(now, dt);
        TickWoven(now);
        float t = now - animStart;
        if (animating)
        {
            // The charged mirror rattles just before the glass breaks.
            if (slotRects[1] != null && !shattered)
            {
                float amp = Mathf.Clamp01(t / CrackTime) * 5f;
                slotRects[1].anchoredPosition = new Vector2(CenterX + SlotOffset - IconW / 2, -BY) + new Vector2(UnityEngine.Random.Range(-1f, 1f), UnityEngine.Random.Range(-1f, 1f)) * amp;
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
        // The glass travels through the Woven Echo into the left slot.
        Vector2[] path =
        {
            BCenter,
            EchoCenter + new Vector2(45f, 28f),
            EchoCenter + new Vector2(-38f, -22f),
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
                    if (u < 0.88f && UnityEngine.Random.value < dt * 4f)
                    {
                        SparkBurst(s.Pos, 1, 22f, 75f, 35f,
                            new Color(0.18f, 0.36f, 0.95f), new Color(1f, 0.73f, 0.20f), 0.23f, 0.46f);
                    }
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
        foreach (Shard s in shards)
        {
            if (s.Go == null)
            {
                continue;
            }
            if (t < BurstEnd)
            {
                s.Vel *= Mathf.Max(0f, 1f - 2.4f * dt);
                s.Vel.y -= 90f * dt;
                s.Pos += s.Vel * dt;
                s.Rot += s.Spin * dt;
                s.Group.alpha = 1f;
                Vector2 rel = s.Pos - EchoCenter;
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
                    s.Pos = EchoCenter + new Vector2(Mathf.Cos(s.Angle), Mathf.Sin(s.Angle)) * s.Radius;
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
                GameObject beam = Effect("WovenDisintegration");
                Image flare = WovenImage(beam.transform, "CrimsonBeam", OrbArt.Named("glow_violet"), Vector2.zero, new Vector2(86f, 285f));
                flare.rectTransform.anchorMin = flare.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                flare.rectTransform.anchoredPosition = Vector2.zero;
                flare.color = new Color(1f, 0.12f, 0.17f, 0.68f);
                RectTransform beamRect = beam.GetComponent<RectTransform>();
                beamRect.anchorMin = beamRect.anchorMax = new Vector2(0f, 1f);
                beamRect.anchoredPosition = EchoCenter;
                SparkBurst(EchoCenter, 28, 100f, 360f, 70f, new Color(0.56f, 0.02f, 0.05f), new Color(1f, 0.27f, 0.19f), 0.4f, 0.9f);
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
        reflectedItem = itemA;
        Vector2 centre = ACenter;
        Image seal = WovenImage(Content, "WovenSeal", OrbArt.Named("woven_echo_spiral"), centre, new Vector2(174f, 174f));
        seal.material = Items_AdvancedForgeWindow.Additive();
        seal.color = new Color(0.71f, 0.83f, 1f, 0.52f);
        effects.Add(seal.gameObject);
        finalFlash.Add((seal, now, 0.52f, 0.7f));
        SparkBurst(centre, 44, 140f, 700f, 480f, new Color(0.19f, 0.36f, 0.91f), new Color(1f, 0.79f, 0.22f), 0.7f, 1.4f);
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

    private static void EndAnimationNow()
    {
        animating = false;
        shattered = false;
        finalDone = false;
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
        finalFlash.Clear();
        foreach (GameObject go in effects)
        {
            if (go != null) Object.Destroy(go);
        }
        effects.Clear();
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
